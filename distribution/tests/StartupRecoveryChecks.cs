using System.Text.Json;
using StarTournament.Distribution;

static class StartupRecoveryChecks
{
    static readonly RecoveryIdentity Identity = new("tech.test", "osx", "arm64", "production");
    static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    static readonly string Backup = new('a', 32);
    static int passed;
    public static void Run()
    {
        // Current worktree path has no macOS /tmp or /var compatibility symlinks.
        var root = Path.Combine(Directory.GetCurrentDirectory(), ".local", "recovery-contracts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            void Test(string label, Action<StartupRecovery, string> test)
            {
                var path = Path.Combine(root, passed.ToString(), "journal.json");
                test(new(path, Identity), path);
                passed++; Console.WriteLine("PASS " + label);
            }
            void Healthy(StartupRecovery log, string version = "1.0.0")
            {
                var ticket = log.BeginLaunch(version, Now.AddMinutes(1));
                Require(log.AcceptReady(Ready(ticket), Now));
            }
            void Prepare(StartupRecovery log)
            { Healthy(log); log.Prepare("1.0.0", "1.1.0", Backup); }

            Test("empty installation does not assert health", (log, path) =>
            { Require(log.Read().HealthyVersion == null); Reject(() => log.Prepare("1.0.0", "1.1.0", Backup)); Require(!File.Exists(path)); });
            Test("only exact menu-ready marks baseline healthy", (log, _) =>
            {
                var ticket = log.BeginLaunch("1.0.0", Now.AddMinutes(1));
                foreach (var report in new[] { Ready(ticket) with { Schema = 2 }, Ready(ticket) with { Phase = "installed" }, Ready(ticket) with { Version = "1.1.0" }, Ready(ticket) with { Token = new string('b',32) } })
                    Require(!log.AcceptReady(report, Now));
                Require(log.Read().HealthyVersion == null && log.Read().Launch == ticket);
                Require(log.AcceptReady(Ready(ticket), Now)); Require(!log.AcceptReady(Ready(ticket), Now));
            });
            Test("successful target keeps backup through startup and confirmation", (log, path) =>
            {
                Prepare(log);
                var reopened = new StartupRecovery(path, Identity);
                Require(reopened.Read().Attempt?.Phase == "prepared");
                reopened.RecoverInterrupted("1.1.0");
                var ticket = reopened.BeginLaunch("1.1.0", Now.AddMinutes(1));
                Reject(() => reopened.ConfirmBackupRemoved(Backup));
                Require(reopened.Read().Attempt?.BackupId == Backup && reopened.Read().HealthyVersion == "1.0.0");
                Require(reopened.AcceptReady(Ready(ticket), Now));
                Require(reopened.Read().Attempt?.Phase == "confirmed" && reopened.Read().HealthyVersion == "1.1.0");
                reopened.EndBeforeReady(ticket); // Exit after ready must not fail a healthy release.
                Reject(() => reopened.Prepare("1.1.0", "1.2.0", Backup));
                Reject(() => reopened.ConfirmBackupRemoved(new string('c',32)));
                reopened.ConfirmBackupRemoved(Backup);
                reopened.Prepare("1.1.0", "1.2.0", new string('d',32));
            });
            Test("exit zero before ready also requires recovery", (log, path) =>
            {
                Prepare(log); var ticket = log.BeginLaunch("1.1.0", Now.AddMinutes(1));
                log.EndBeforeReady(ticket);
                var reopened = new StartupRecovery(path, Identity);
                Require(reopened.Read().Attempt?.Phase == "recovery-required" && reopened.IsBlocked("1.1.0"));
                Require(!reopened.AcceptReady(Ready(ticket), Now));
                Reject(() => reopened.BeginLaunch("1.1.0", Now.AddMinutes(1)));
                Reject(() => reopened.BeginLaunch("1.0.0", Now.AddMinutes(1)));
                Reject(() => reopened.PermitRetry("1.1.0"));
            });
            Test("interrupted launch persists failed release", (log, path) =>
            {
                Prepare(log); log.BeginLaunch("1.1.0", Now.AddMinutes(1));
                var reopened = new StartupRecovery(path, Identity); reopened.RecoverInterrupted("1.1.0");
                Require(reopened.Read().Attempt?.Phase == "recovery-required" && reopened.IsBlocked("1.1.0"));
                reopened.RecoverInterrupted("1.1.0");
                Require(reopened.Read().Attempt?.BackupId == Backup);
            });
            Test("interrupted apply while old installed never silently retries", (log, path) =>
            {
                Prepare(log); var reopened = new StartupRecovery(path, Identity); reopened.RecoverInterrupted("1.0.0");
                Require(reopened.IsBlocked("1.1.0")); reopened.MarkRestored("1.0.0", Backup);
                Require(reopened.IsBlocked("1.1.0") && reopened.Read().HealthyVersion == "1.0.0");
                Require(reopened.Read().Attempt?.Phase == "restored");
                reopened.ConfirmBackupRemoved(Backup);
                Reject(() => reopened.Prepare("1.0.0", "1.1.0", Backup));
                reopened.Prepare("1.0.0", "1.2.0", Backup); // A different release remains eligible.
            });
            Test("failed apply retains authenticated snapshot identity", (log, _) =>
            {
                Prepare(log); Reject(() => log.InstallationFailed("1.2.0", "disk full"));
                log.InstallationFailed("1.1.0", "disk full");
                Require(log.Read().Attempt?.Failure == "disk full" && log.Read().Attempt?.BackupId == Backup);
                Reject(() => log.ConfirmBackupRemoved(Backup));
            });
            Test("restore matches previous version and exact snapshot", (log, path) =>
            {
                Prepare(log); log.InstallationFailed("1.1.0", "apply failed");
                Reject(() => log.MarkRestored("1.1.0", Backup));
                Reject(() => log.MarkRestored("1.0.0", new string('e',32)));
                log.MarkRestored("1.0.0", Backup);
                var reopened = new StartupRecovery(path, Identity);
                Require(reopened.Read().Attempt?.Phase == "restored" && reopened.IsBlocked("1.1.0"));
                Reject(() => reopened.PermitRetry("1.1.0"));
                reopened.ConfirmBackupRemoved(Backup);
                reopened.PermitRetry("1.1.0");
                reopened.Prepare("1.0.0", "1.1.0", new string('f',32));
            });
            Test("deadline boundary rejects late ready durably", (log, path) =>
            {
                Prepare(log); var ticket = log.BeginLaunch("1.1.0", Now.AddMinutes(1));
                Require(!log.Expire(Now.AddSeconds(59)));
                Require(!log.AcceptReady(Ready(ticket), Now.AddMinutes(1)));
                Require(new StartupRecovery(path, Identity).IsBlocked("1.1.0"));
            });
            Test("timeout cannot be undone by late ready", (log, _) =>
            {
                Prepare(log); var ticket = log.BeginLaunch("1.1.0", Now.AddMinutes(1));
                Require(log.Expire(Now.AddMinutes(2))); Require(!log.Expire(Now.AddMinutes(3)));
                Require(!log.AcceptReady(Ready(ticket), Now.AddSeconds(1)));
            });
            Test("stale observer cannot fail newer launch", (log, _) =>
            {
                var old = log.BeginLaunch("1.0.0", Now.AddMinutes(1));
                log.EndBeforeReady(old);
                var ticket = log.BeginLaunch("1.0.0", Now.AddMinutes(2));
                Require(ticket.Token != old.Token); log.EndBeforeReady(old);
                Require(!log.AcceptReady(Ready(old), Now)); Require(log.AcceptReady(Ready(ticket), Now));
            });
            Test("unknown installed transaction identity fails closed", (log, _) =>
            { Prepare(log); Reject(() => log.RecoverInterrupted("2.0.0")); Require(log.Read().Attempt?.Phase == "prepared"); });
            Test("prepare cannot overwrite pending or unconfirmed state", (log, _) =>
            {
                Prepare(log); Reject(() => log.Prepare("1.0.0", "1.2.0", Backup));
                Reject(() => log.Prepare("1.0.0", "0.9.0", Backup)); Reject(() => log.Prepare("1.0.0", "1.0.0", Backup));
                Reject(() => log.Prepare("1.0.0", "1.2.0", "../backup"));
                Reject(() => log.BeginLaunch("1.0.0", Now.AddMinutes(1)));
            });
            Test("foreign product platform architecture channel rejected", (log, path) =>
            {
                Healthy(log);
                foreach (var other in new[] { Identity with { AppId="foreign" }, Identity with { Platform="win" }, Identity with { Architecture="x64" }, Identity with { Channel="test" } })
                    Reject(() => new StartupRecovery(path,other).Read());
            });
            Test("corrupt empty oversize unknown-schema journals fail closed", (log, path) =>
            {
                Healthy(log); var valid = File.ReadAllText(path);
                foreach (var bad in new[] { "", "null", "{}", "{", valid.Replace("\"Schema\":1", "\"Schema\":2"), valid.TrimEnd('}')+",\"Unknown\":true}", new string('x',131073) })
                { File.WriteAllText(path,bad); Reject(() => log.Read()); Reject(() => log.BeginLaunch("1.0.0",Now.AddMinutes(1))); }
            });
            Test("invalid phase and relational invariants fail closed", (log, path) =>
            {
                Prepare(log); var state = log.Read();
                foreach (var bad in new[] {
                    state with { Attempt=state.Attempt! with { Phase="other" } },
                    state with { Attempt=state.Attempt! with { Phase="starting" } },
                    state with { HealthyVersion="0.9.0" },
                    state with { Attempt=state.Attempt! with { Phase="recovery-required",Failure="failure" } },
                    state with { BlockedVersions=["1.1.0"] },
                    state with { BlockedVersions=["1.2.0","1.2.0"] },
                    state with { BlockedVersions=null! },
                    state with { Attempt=state.Attempt! with { BackupId="unsafe" } } })
                { File.WriteAllText(path,JsonSerializer.Serialize(bad)); Reject(() => log.Read()); }
            });
            Test("orphan temporary write cannot replace durable journal", (log, path) =>
            {
                Prepare(log); File.WriteAllText(path+".next-orphan", "partial");
                Require(new StartupRecovery(path,Identity).Read().Attempt?.Target=="1.1.0");
            });
            Test("link cannot redirect recovery writes", (log, path) =>
            {
                Healthy(log); var other=path+".other"; File.Move(path,other); File.CreateSymbolicLink(path,other);
                var content=File.ReadAllText(other);
                Reject(() => log.Read()); Reject(() => log.BeginLaunch("1.0.0",Now.AddMinutes(1)));
                Require(File.ReadAllText(other)==content); File.Delete(path);
            });
            Test("numeric prerelease ordering permits test.10 after test.9", (_, path) =>
            {
                var test = new StartupRecovery(path,Identity with { Channel="test" });
                Healthy(test,"1.0.0-test.9"); test.Prepare("1.0.0-test.9","1.0.0-test.10",Backup);
            });
            Test("wrong-channel versions fail before any mutation", (log, path) =>
            {
                Reject(() => log.BeginLaunch("1.0.0-test.1",Now.AddMinutes(1))); Require(!File.Exists(path));
                var test=new StartupRecovery(path,Identity with { Channel="test" });
                Reject(() => test.BeginLaunch("1.0.0",Now.AddMinutes(1))); Require(!File.Exists(path));
            });
            Test("directory or dangling link cannot be treated as absent journal", (log, path) =>
            {
                Directory.CreateDirectory(path); Reject(() => log.Read()); Directory.Delete(path);
                File.CreateSymbolicLink(path,path+".missing"); Reject(() => log.Read()); File.Delete(path);
            });
            Test("empty or noncanonical versions cannot enter journal", (log, _) =>
            {
                foreach (var version in new[] { "", "1.0", "v1.0.0", "../1.0.0", new string('1',129) })
                    Reject(() => log.BeginLaunch(version,Now.AddMinutes(1)));
            });
            Test("confirmed release mismatch retains backup", (log, _) =>
            {
                Prepare(log); var ticket=log.BeginLaunch("1.1.0",Now.AddMinutes(1));
                Require(log.AcceptReady(Ready(ticket),Now));
                Reject(() => log.RecoverInterrupted("1.0.0"));
                Require(log.Read().Attempt?.BackupId==Backup);
            });
            Test("stale object rereads durable state before mutating", (log, path) =>
            {
                var other=new StartupRecovery(path,Identity); Prepare(log);
                Reject(() => other.Prepare("1.0.0","1.2.0",Backup));
                var ticket=other.BeginLaunch("1.1.0",Now.AddMinutes(1));
                log.EndBeforeReady(ticket); Require(other.IsBlocked("1.1.0"));
            });
            Test("failed target set is retained across subsequent upgrades", (log, _) =>
            {
                Prepare(log); log.InstallationFailed("1.1.0","error"); log.MarkRestored("1.0.0",Backup); log.ConfirmBackupRemoved(Backup);
                log.Prepare("1.0.0","1.2.0",Backup); var ticket=log.BeginLaunch("1.2.0",Now.AddMinutes(1));
                Require(log.AcceptReady(Ready(ticket),Now)); log.ConfirmBackupRemoved(Backup);
                Require(log.IsBlocked("1.1.0") && !log.IsBlocked("1.2.0"));
            });
            Test("invalid failure or state cannot overwrite valid bytes", (log, path) =>
            {
                Prepare(log); var before=File.ReadAllBytes(path);
                Reject(() => log.InstallationFailed("1.1.0",""));
                Reject(() => log.InstallationFailed("1.1.0",new string('x',2049)));
                Require(File.ReadAllBytes(path).SequenceEqual(before));
                Require(!Directory.EnumerateFiles(Path.GetDirectoryName(path)!,"*.next-*").Any());
            });
            Test("directory link cannot redirect absent journal", (_, path) =>
            {
                var parent=Path.GetDirectoryName(path)!; Directory.CreateDirectory(parent);
                var outside=parent+"-outside"; Directory.CreateDirectory(outside);
                var link=Path.Combine(parent,"linked"); Directory.CreateSymbolicLink(link,outside);
                var log=new StartupRecovery(Path.Combine(link,"journal.json"),Identity);
                Reject(() => log.Read()); Reject(() => log.BeginLaunch("1.0.0",Now.AddMinutes(1)));
                Require(!File.Exists(Path.Combine(outside,"journal.json"))); Directory.Delete(link);
            });
            Test("invalid startup ticket and blocked health fail closed", (log, path) =>
            {
                Healthy(log); var state=log.Read();
                foreach(var bad in new[] {
                    state with { Launch=new("1.0.0","bad",Now.UtcTicks) },
                    state with { Launch=new("1.0.0",Backup,0) },
                    state with { Launch=new("1.0.0",Backup,long.MaxValue) },
                    state with { BlockedVersions=["1.0.0"] } })
                { File.WriteAllText(path,JsonSerializer.Serialize(bad)); Reject(() => log.Read()); }
            });
            Test("restored backup stays recorded until cleanup acknowledgement", (log,path) =>
            {
                Prepare(log);log.InstallationFailed("1.1.0","error");log.MarkRestored("1.0.0",Backup);
                var reopened=new StartupRecovery(path,Identity);reopened.RecoverInterrupted("1.0.0");
                Require(reopened.Read().Attempt?.BackupId==Backup&&reopened.Read().Attempt?.Phase=="restored");
                Reject(()=>reopened.RecoverInterrupted("1.1.0"));
                Reject(()=>reopened.ConfirmBackupRemoved(new string('f',32)));
                reopened.ConfirmBackupRemoved(Backup);Require(reopened.Read().Attempt==null&&reopened.IsBlocked("1.1.0"));
            });
            Console.WriteLine($"Startup recovery: {passed} passed");
        }
        finally { Directory.Delete(root,true); }
    }
    static StartupReady Ready(StartupTicket ticket) => new(1,ticket.Version,ticket.Token,"menu-ready");
    static void Require(bool value) { if(!value) throw new Exception("Startup recovery contract failed"); }
    static void Reject(Action action)
    {
        try { action(); } catch(Exception ex) when(ex is IOException or ArgumentException or InvalidOperationException) { return; }
        throw new Exception("Unsafe recovery state was admitted");
    }
}
