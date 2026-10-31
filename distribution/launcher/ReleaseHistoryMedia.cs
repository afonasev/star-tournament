using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace StarTournament.Distribution;

public sealed record ReleaseHistoryBlock(string Kind, string Text, string Sha256 = "", string Path = "", int Width = 0, int Height = 0, string State = "pending", int Offset = 0, int Length = 0);

// No arbitrary URLs or paths cross this boundary: only content-addressed PNGs of this release.
public sealed class ReleaseHistoryMedia : IDisposable
{
    public const int MaxBytes = 2 * 1024 * 1024, MaxImages = 12, MaxWidth = 1600, MaxHeight = 1200;
    const long CacheBytes = 64L * 1024 * 1024;
    const string Base = "https://github.com/afonasev/star-tournament/releases/download/v";
    readonly string directory;
    readonly HttpClient http;
    public ReleaseHistoryMedia(string directory, HttpMessageHandler? transport = null)
    {
        this.directory = directory;
        http = new(transport ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("StarTournament-HistoryMedia/1");
    }
    public static ReleaseHistoryBlock[] Parse(string notes, string version, bool compact = false)
    {
        var blocks = new List<ReleaseHistoryBlock>(); int start = 0, images = 0;
        foreach (Match match in Regex.Matches(notes, @"(?m)^[ \t]*!\[([^\]\r\n]{1,300})\]\((https://[^\s)]+)\)[ \t]*\r?$"))
        {
            var url = match.Groups[2].Value;
            var sha = Regex.Match(url, @"/history-([0-9a-f]{64})\.png$").Groups[1].Value;
            if (sha.Length != 64 || url != Base + version + "/history-" + sha + ".png" || ++images > MaxImages) continue;
            if (match.Index > start) blocks.Add(new("text", compact ? "" : Clean(notes[start..match.Index]), Offset: start, Length: match.Index - start));
            blocks.Add(new("image", match.Groups[1].Value, sha)); start = match.Index + match.Length;
        }
        if (start < notes.Length) blocks.Add(new("text", compact ? "" : Clean(notes[start..]), Offset: start, Length: notes.Length - start));
        return blocks.ToArray();
    }
    static string Clean(string text) => Regex.Replace(Regex.Replace(text.Trim(), @"(?m)^### +", ""), @"(?m)^- +", "• ");
    public static (int Width, int Height) Validate(byte[] data, string sha)
    {
        if (data.Length < 45 || data.Length > MaxBytes || !Convert.ToHexString(SHA256.HashData(data)).Equals(sha, StringComparison.OrdinalIgnoreCase) ||
            !data.AsSpan(0, 16).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82})) throw new InvalidDataException("Invalid history PNG identity");
        int width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16)), height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20));
        if (width <= 0 || height <= 0 || width > MaxWidth || height > MaxHeight || data[24] != 8 || (data[25] != 2 && data[25] != 6) || data[26] != 0 || data[27] != 0 || data[28] != 0)
            throw new InvalidDataException("History PNG exceeds profile");
        using var compressed = new MemoryStream(); bool ended = false;
        for (int offset = 8; offset + 12 <= data.Length;)
        {
            int count = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            if (count < 0 || count > data.Length - offset - 12) throw new InvalidDataException("Truncated history PNG");
            var kind = System.Text.Encoding.ASCII.GetString(data, offset + 4, 4);
            if (kind is not ("IHDR" or "IDAT" or "IEND" or "sRGB" or "gAMA" or "pHYs" or "cHRM") || (kind == "IHDR" && offset != 8)) throw new InvalidDataException("Unsupported history PNG chunks");
            uint crc = 0xffffffff;
            for (int i = offset + 4; i < offset + 8 + count; i++)
            { crc ^= data[i]; for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
            if (~crc != BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + count + 8))) throw new InvalidDataException("History PNG CRC mismatch");
            if (kind == "IDAT") compressed.Write(data, offset + 8, count);
            offset += count + 12;
            if (kind == "IEND") { ended = count == 0 && offset == data.Length; break; }
        }
        if (!ended) throw new InvalidDataException("Invalid history PNG ending");
        compressed.Position = 0;
        using var decoder = new ZLibStream(compressed, CompressionMode.Decompress);
        long expected = (long)height * (1 + width * (data[25] == 2 ? 3 : 4)), total = 0;
        var buffer = new byte[8192]; int read;
        while ((read = decoder.Read(buffer)) > 0) { total += read; if (total > expected) throw new InvalidDataException("History PNG pixels exceed profile"); }
        if (total != expected) throw new InvalidDataException("Invalid history PNG pixels");
        return (width, height);
    }
    static bool Redirect(Uri uri, string original) => uri.Scheme == "https" && uri.Port == 443 && uri.UserInfo.Length == 0 &&
        (uri.AbsoluteUri == original || uri.Host == "release-assets.githubusercontent.com");
    async Task<byte[]> Download(string url, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        token = timeout.Token;
        var uri = new Uri(url);
        for (int redirect = 0; redirect <= 3; redirect++)
        {
            if (!Redirect(uri, url)) throw new InvalidDataException("Unexpected history image redirect");
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            { uri = new Uri(uri, response.Headers.Location ?? throw new InvalidDataException("Missing media redirect")); continue; }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidDataException("History image too large");
            await using var input = await response.Content.ReadAsStreamAsync(token); using var output = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count = await input.ReadAsync(buffer, token)) > 0)
            { if (output.Length + count > MaxBytes) throw new InvalidDataException("History image too large"); output.Write(buffer, 0, count); }
            return output.ToArray();
        }
        throw new InvalidDataException("Too many history image redirects");
    }
    void Trim(long incoming, string keep)
    {
        var files = new DirectoryInfo(directory).GetFiles("history-*.png").OrderBy(x => x.LastWriteTimeUtc).ToList();
        long size = files.Sum(x => x.Length);
        foreach (var file in files)
        {
            if (size + incoming <= CacheBytes) break;
            if (file.FullName == keep || file.LinkTarget != null) continue;
            long length = file.Length; file.Delete(); size -= length;
        }
        if (size + incoming > CacheBytes) throw new IOException("History cache full");
    }
    public async Task<ReleaseHistoryBlock> Load(ReleaseHistoryBlock block, string version, CancellationToken token)
    {
        if (block.Kind != "image" || !Regex.IsMatch(block.Sha256, "^[0-9a-f]{64}$")) return block;
        var path = System.IO.Path.Combine(directory, "history-" + block.Sha256 + ".png");
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            byte[]? data = null;
            if (File.Exists(path) && new FileInfo(path) is { LinkTarget: null, Length: <= MaxBytes })
            {
                try { data = await File.ReadAllBytesAsync(path, token); Validate(data, block.Sha256); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { data = null; }
            }
            if (data == null)
            {
                data = await Download(Base + version + "/history-" + block.Sha256 + ".png", token);
                Validate(data, block.Sha256); token.ThrowIfCancellationRequested();
                if (new FileInfo(path).LinkTarget != null || new FileInfo(temporary).LinkTarget != null) throw new IOException("Unsafe history cache path");
                Trim(data.Length, path); await File.WriteAllBytesAsync(temporary, data, token); token.ThrowIfCancellationRequested(); File.Move(temporary, path, true);
            }
            var size = Validate(data, block.Sha256); File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            return block with { Path = path, Width = size.Width, Height = size.Height, State = "ready" };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or HttpRequestException or UnauthorizedAccessException or OperationCanceledException)
        { return block with { Path = "", State = "unavailable" }; }
        finally { try { if (File.Exists(temporary) && new FileInfo(temporary).LinkTarget == null) File.Delete(temporary); } catch (IOException) { } }
    }
    public void Dispose() => http.Dispose();
}
