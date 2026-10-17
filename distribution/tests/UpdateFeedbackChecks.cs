using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using StarTournament.Distribution;

static class UpdateFeedbackChecks
{
    public static async Task Run()
    {
        int passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        Check(UpdateFeedback.Explain(new MaintenanceUpgradeRequiredException()).Contains("новый установщик"), "legacy maintenance upgrade feedback");
        Check(UpdateFeedback.Explain(new UnauthorizedAccessException()).Contains("доступ"), "permission feedback");
        Check(UpdateFeedback.Explain(new Win32Exception(5)).Contains("доступ"), "native Windows permission feedback");
        Check(UpdateFeedback.Explain(new Win32Exception(1223)).Contains("администратора"), "UAC denial feedback");
        Check(!UpdateFeedback.Explain(new Win32Exception(2)).Contains("отменена"), "missing helper is not mislabeled UAC denial");
        Check(UpdateFeedback.Explain(new CryptographicException()).Contains("подлинности"), "signature feedback");
        Check(UpdateFeedback.Explain(new TimeoutException()).Contains("не отвечает"), "timeout feedback");
        Check(UpdateFeedback.Explain(new FileNotFoundException()).Contains("антивируса"), "missing payload feedback");
        var root = Path.Combine(Path.GetTempPath(), "star-feedback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "attempt.json");
            var journal = new UpdateAttemptJournal(path, (_, _) => { });
            Check(!journal.AlreadyAttempted("1.1.0"), "first launch may apply");
            journal.Begin("1.1.0");
            Check(new UpdateAttemptJournal(path, (_, _) => { }).AlreadyAttempted("1.1.0"), "interrupted handoff blocks automatic retry across launches");
            journal.Fail("1.1.0", "Windows не получила разрешение администратора.");
            Check(journal.Read()!.Message.Contains("администратора"), "precise failure survives restart");
            Check(!journal.AlreadyAttempted("1.2.0"), "new version can apply");
            journal.ClearIfInstalled("1.0.0");
            Check(journal.AlreadyAttempted("1.1.0"), "old installed version does not erase failed attempt");
            journal.ClearIfInstalled("1.1.0");
            Check(!File.Exists(path), "successful installed version clears failure");
            journal.Begin("1.1.0"); journal.Begin("1.1.0");
            Check(journal.Read()!.Version == "1.1.0", "explicit retry records a fresh attempt");
            foreach (var damaged in new[] { "broken", "{}", "null", "{\"Version\":\"1.1.0\"}", "{\"Version\":\"bad\",\"Message\":\"failure\"}" })
            {
                await File.WriteAllTextAsync(path, damaged);
                Check(journal.AlreadyAttempted("1.1.0"), "corrupt journal cannot trigger automatic apply loop: " + damaged);
            }

            using var rsa = RSA.Create(3072);
            byte[] bytes = "signed feedback test package"u8.ToArray();
            const string file = "fixture-1.1.0-full.nupkg";
            const string url = "https://github.com/afonasev/star-tournament/releases/download/v1.1.0/fixture-1.1.0-full.nupkg";
            var payload = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, platform = "win", architecture = "x64", channel = "production", version = "1.1.0", publicationDate = "2026-10-11", downloadUrl = url,
                feed = new { Assets = new[] { new { PackageId = "tech.fixture", Version = "1.1.0", Type = "Full", FileName = file, SHA256 = Convert.ToHexString(SHA256.HashData(bytes)), Size = bytes.Length } } } });
            var envelope = JsonSerializer.Serialize(new { payload = Convert.ToBase64String(payload), signature = Convert.ToBase64String(rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) });
            var config = new LauncherConfig("tech.fixture", "win", "x64", "1.0.0", "2026-10-11", AuthenticatedSource.ProductionBase, "game.exe", "production");
            foreach (var mode in new[] { "headers", "body", "cancel", "success", "corrupt" })
            {
                using var transport = new PackageTransport(mode, bytes);
                using var source = new AuthenticatedSource(config, rsa.ExportSubjectPublicKeyInfoPem(), transport, TimeSpan.FromMilliseconds(80));
                source.Admit(envelope);
                var destination = Path.Combine(root, mode + ".nupkg");
                using var cancellation = new CancellationTokenSource();
                if (mode == "cancel") cancellation.CancelAfter(20);
                try
                {
                    await source.DownloadReleaseEntry(null!, source.Asset!, destination, _ => { }, cancellation.Token);
                    Check(mode == "success" && File.ReadAllBytes(destination).SequenceEqual(bytes), "valid signed bytes complete download");
                }
                catch (TimeoutException) { Check(mode is "headers" or "body", "bounded " + mode + " stall"); }
                catch (OperationCanceledException) { Check(mode == "cancel", "user cancellation remains cancellation"); }
                catch (CryptographicException) { Check(mode == "corrupt", "integrity rejection preserved"); }
                Check(!File.Exists(destination + ".authenticated-download"), "quarantine cleaned " + mode);
                if (mode != "success") Check(!File.Exists(destination), "failed bytes never admitted " + mode);
            }
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"Feedback contracts: {passed} passed");
    }
    sealed class PackageTransport(string mode, byte[] bytes) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            if (mode is "headers" or "cancel") await Task.Delay(Timeout.Infinite, cancellation);
            return new(HttpStatusCode.OK) { Content = mode == "body" ? new StreamContent(new StalledStream()) : new ByteArrayContent(mode == "corrupt" ? new byte[bytes.Length] : bytes) };
        }
    }
    sealed class StalledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default) { await Task.Delay(Timeout.Infinite, cancellation); return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
