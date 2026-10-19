using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("Launcher.Contracts")]
namespace StarTournament.Distribution;

public sealed record PayloadEntry(string Path, string Kind, string? Sha256, string? Link, int Mode);
public sealed record PayloadSnapshot(int Schema, string Id, RecoveryIdentity Identity, string Version,
    string InstallPath, PayloadEntry[] Entries);

/// <summary>
/// Retains and verifies complete immutable payloads. The caller owns the per-install lease,
/// process-exit checks and (on Windows) a protected store and elevated restore authority.
/// Metadata never supplies a destination or executable: Restore requires the caller's fixed path.
/// User data is deliberately outside the replaceable payload.
/// </summary>
public sealed class PayloadRecovery
{
    sealed record CaptureOwner(int Schema, string Id, RecoveryIdentity Identity, string Version, string InstallPath);
    sealed record RestoreIntent(int Schema, string Id, RecoveryIdentity Identity, string InstallPath, string ManifestSha256, string Phase);
    static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    readonly string root;
    readonly RecoveryIdentity identity;
    readonly Action<string> checkpoint;
    readonly Action<string>? protectedMkdir;
    readonly Func<string, FileStream>? protectedCreate;
    public PayloadRecovery(string store, RecoveryIdentity identity) : this(store, identity, _ => { }, null, null) { }
    public PayloadRecovery(string store, RecoveryIdentity identity, Action<string> mkdir, Func<string, FileStream> create)
        : this(store, identity, _ => { }, mkdir, create) { }
    internal PayloadRecovery(string store, RecoveryIdentity identity, Action<string> checkpoint) : this(store, identity, checkpoint, null, null) { }
    PayloadRecovery(string store, RecoveryIdentity identity, Action<string> checkpoint, Action<string>? mkdir, Func<string, FileStream>? create)
    {
        root = Absolute(store); this.identity = identity; this.checkpoint = checkpoint; protectedMkdir = mkdir; protectedCreate = create;
        // Reuse identity validation without creating a journal.
        _ = new StartupRecovery(Path.Combine(root, "identity-check.json"), identity);
        RegularAncestors(root); MakeDirectory(root); RegularAncestors(root);
    }

    public PayloadSnapshot Capture(string installedPath, string version, string id)
    {
        Id(id); var installed = InstallPath(installedPath);
        if (!Directory.Exists(installed)) throw new DirectoryNotFoundException("Installed payload missing");
        if (string.IsNullOrEmpty(version) || !Velopack.SemanticVersion.TryParse(version, out var parsed) || parsed.ToString() != version ||
            parsed.IsPrerelease != (identity.Channel == "test")) throw new ArgumentException("Canonical same-channel version required");
        var final = Path.Combine(root, id);
        if (File.Exists(Path.Combine(root, ".delete-" + id + ".json"))) throw new IOException("Snapshot removal is pending");
        if (Directory.Exists(final))
        {
            var prior = Verify(id, installed);
            if (prior.Version != version) throw new IOException("Snapshot id belongs to another release");
            return prior;
        }
        RegularAncestors(final);
        var next = Path.Combine(root, ".capture-" + id);
        var owner = new CaptureOwner(1, id, identity, version, installed);
        RegularAncestors(next);
        if (Directory.Exists(next))
        {
            RegularAncestors(next);
            if (Read<CaptureOwner>(Path.Combine(next, "owner.json")) != owner) throw new IOException("Unowned partial capture");
            Directory.Delete(next, true);
        }
        MakeDirectory(next);
        Write(Path.Combine(next, "owner.json"), owner, createNew: true);
        VerifyRelease(installed, version);
        var before = Inventory(installed);
        Copy(installed, Path.Combine(next, "payload"), before);
        checkpoint("capture-copied");
        var snapshot = new PayloadSnapshot(1, id, identity, version, installed, before);
        Equal(before, Inventory(Path.Combine(next, "payload")));
        Equal(before, Inventory(installed)); // A changing live payload is not a working backup.
        Write(Path.Combine(next, "manifest.json"), snapshot, createNew: true);
        checkpoint("capture-verified");
        Directory.Move(next, final);
        checkpoint("capture-committed");
        return Verify(id, installed);
    }

    public PayloadSnapshot Verify(string id, string installedPath)
    {
        Id(id); var installed = InstallPath(installedPath);
        var directory = Path.Combine(root, id); RegularAncestors(directory);
        var snapshot = Read<PayloadSnapshot>(Path.Combine(directory, "manifest.json"));
        if (snapshot.Schema != 1 || snapshot.Id != id || snapshot.Identity != identity || snapshot.InstallPath != installed ||
            snapshot.Entries == null || snapshot.Entries.Length is < 1 or > 200000 ||
            string.IsNullOrEmpty(snapshot.Version) || !Velopack.SemanticVersion.TryParse(snapshot.Version, out var version) ||
            version.ToString() != snapshot.Version || version.IsPrerelease != (identity.Channel == "test"))
            throw new IOException("Foreign or invalid payload snapshot");
        // No manifest entry is used to open or delete a path. Compare with independently enumerated files.
        Equal(snapshot.Entries, Inventory(Path.Combine(directory, "payload")));
        VerifyRelease(Path.Combine(directory, "payload"), snapshot.Version);
        return snapshot;
    }

    public void VerifyInstalled(string id, string installedPath)
    {
        var snapshot = Verify(id, installedPath);
        Equal(snapshot.Entries, Inventory(snapshot.InstallPath));
        VerifyRelease(snapshot.InstallPath, snapshot.Version);
    }

    public void Restore(string id, string installedPath)
    {
        var snapshot = Verify(id, installedPath);
        var installed = snapshot.InstallPath;
        var parent = Path.GetDirectoryName(installed)!;
        var prefix = Path.Combine(parent, ".star-recovery-" + Path.GetFileName(installed) + "-" + id);
        var next = prefix + ".next"; var failed = prefix + ".failed";
        var intentPath = Path.Combine(root, ".restore-" + id + ".json");
        var digest = Hash(Path.Combine(root, id, "manifest.json"));
        RegularAncestors(next); RegularAncestors(failed); RegularAncestors(intentPath);
        RestoreIntent intent;
        if (File.Exists(intentPath))
        {
            intent = Read<RestoreIntent>(intentPath);
            if (intent.Schema != 1 || intent.Id != id || intent.Identity != identity || intent.InstallPath != installed ||
                intent.ManifestSha256 != digest || intent.Phase is not ("copying" or "prepared" or "restored"))
                throw new IOException("Foreign or damaged restore intent");
        }
        else
        {
            if (Exists(next) || Exists(failed)) throw new IOException("Unowned recovery sibling; refusing to replace it");
            intent = new(1, id, identity, installed, digest, "copying");
            Write(intentPath, intent, createNew: true);
        }
        if (intent.Phase == "copying")
        {
            if (Exists(failed)) throw new IOException("Restore state is inconsistent; retained all payloads");
            if (Directory.Exists(next)) Directory.Delete(next, true);
            else if (File.Exists(next)) throw new IOException("Recovery staging path is a file");
            Copy(Path.Combine(root, id, "payload"), next, snapshot.Entries);
            Equal(snapshot.Entries, Inventory(next));
            intent = intent with { Phase = "prepared" }; Write(intentPath, intent);
            checkpoint("restore-prepared");
        }
        if (intent.Phase == "prepared")
        {
            if (Directory.Exists(next))
            {
                Equal(snapshot.Entries, Inventory(next));
                if (Exists(installed))
                {
                    if (!Directory.Exists(installed) || Exists(failed)) throw new IOException("Ambiguous restore destinations; retained all payloads");
                    Directory.Move(installed, failed);
                    checkpoint("candidate-renamed");
                }
                Directory.Move(next, installed);
                checkpoint("backup-activated");
            }
            // A process may have stopped immediately after the rename but before recording it.
            Equal(snapshot.Entries, Inventory(installed));
            intent = intent with { Phase = "restored" }; Write(intentPath, intent);
            checkpoint("restore-verified");
        }
        Equal(snapshot.Entries, Inventory(installed));
        if (Exists(next)) throw new IOException("Unexpected staging remains after restore");
        if (Directory.Exists(failed)) Directory.Delete(failed, true);
        File.Delete(intentPath);
        // Original snapshot is retained until StartupRecovery.MarkRestored or successful ready/cleanup.
    }

    public void Remove(string id, string installedPath)
    {
        // Caller authorizes this only after confirmed health or verified restoration.
        Id(id); var installed = InstallPath(installedPath);
        if (File.Exists(Path.Combine(root, ".restore-" + id + ".json"))) throw new IOException("Restore is still in progress");
        var directory = Path.Combine(root, id); RegularAncestors(directory);
        var removal = Path.Combine(root, ".delete-" + id + ".json");
        if (File.Exists(removal))
        {
            var owner = Read<CaptureOwner>(removal);
            if (owner.Schema != 1 || owner.Id != id || owner.Identity != identity || owner.InstallPath != installed)
                throw new IOException("Foreign snapshot deletion intent");
        }
        else
        {
            if (!Directory.Exists(directory)) return; // Removal finished before journal acknowledgement.
            var snapshot = Verify(id, installed);
            Write(removal, new CaptureOwner(1, id, identity, snapshot.Version, installed), createNew: true);
        }
        checkpoint("snapshot-delete-authorized");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        checkpoint("snapshot-deleted");
        File.Delete(removal);
    }

    string InstallPath(string value)
    {
        var path = Absolute(value); RegularAncestors(path);
        if (Path.GetDirectoryName(path) == null || Below(root, path) || Below(path, root))
            throw new ArgumentException("Snapshot store must be outside the installed payload");
        return path;
    }
    static string Absolute(string value)
    {
        if (!Path.IsPathFullyQualified(value)) throw new ArgumentException("Absolute path required");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    }
    static bool Below(string path, string parent) => path.Equals(parent, PathComparison) || path.StartsWith(parent + Path.DirectorySeparatorChar, PathComparison);
    static StringComparison PathComparison => OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    static void Id(string id) { if (!Regex.IsMatch(id ?? "", @"\A[0-9a-f]{32}\z")) throw new ArgumentException("Snapshot id required"); }
    static void RegularAncestors(string path)
    {
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
        {
            try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked recovery path is forbidden"); }
            catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
        }
    }
    static int Mode(string path) => OperatingSystem.IsWindows() ? 0 : (int)File.GetUnixFileMode(path);
    static PayloadEntry[] Inventory(string directory)
    {
        RegularAncestors(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Recovery payload missing");
        var result = new List<PayloadEntry> { new("", "directory", null, null, Mode(directory)) };
        void Walk(string parent)
        {
            foreach (var item in new DirectoryInfo(parent).EnumerateFileSystemInfos().OrderBy(v => v.Name, StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(directory, item.FullName).Replace(Path.DirectorySeparatorChar, '/');
                if (item.LinkTarget is { } link)
                {
                    if (OperatingSystem.IsWindows() || Path.IsPathFullyQualified(link)) throw new IOException("Unsafe payload link");
                    ResolveInside(directory, parent, link);
                    result.Add(new(relative, item is DirectoryInfo ? "directory-link" : "file-link", null, link, 0));
                }
                else if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Unsupported payload reparse point");
                else if (item is DirectoryInfo)
                { result.Add(new(relative, "directory", null, null, Mode(item.FullName))); Walk(item.FullName); }
                else result.Add(new(relative, "file", Hash(item.FullName), null, Mode(item.FullName)));
                if (result.Count > 200000) throw new IOException("Payload entry count exceeds limit");
            }
        }
        Walk(directory);
        return result.OrderBy(v => v.Path, StringComparer.Ordinal).ToArray();
    }
    static void ResolveInside(string root, string parent, string target)
    {
        // Resolve each actual link before '..': lexical GetFullPath normalization alone can
        // hide an escape through an intermediate symlink. Valid sq.version uses ../Resources.
        var remaining = new LinkedList<string>(target.Split('/'));
        var current = parent; int expansions = 0;
        while (remaining.Count != 0)
        {
            var part = remaining.First!.Value; remaining.RemoveFirst();
            if (part is "" or ".") continue;
            if (part == "..")
            {
                current = Path.GetDirectoryName(current) ?? throw new IOException("Payload link escapes root");
                if (!Below(current, root)) throw new IOException("Payload link escapes root");
                continue;
            }
            var candidate = Path.Combine(current, part);
            FileSystemInfo item = Directory.Exists(candidate) ? new DirectoryInfo(candidate) : new FileInfo(candidate);
            if (item.LinkTarget is { } link)
            {
                if (++expansions > 40 || Path.IsPathFullyQualified(link)) throw new IOException("Cyclic or absolute payload link");
                foreach (var component in link.Split('/').Reverse()) remaining.AddFirst(component);
            }
            else
            {
                if (!Exists(candidate) || (item.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Dangling/unsafe payload link");
                current = candidate;
                if (remaining.Count != 0 && !Directory.Exists(current)) throw new IOException("Payload link traverses a file");
            }
        }
        if (!Below(current, root) || !Exists(current)) throw new IOException("Payload link is outside the bundle or missing");
    }
    void Copy(string source, string destination, PayloadEntry[] entries)
    {
        RegularAncestors(destination);
        if (Exists(destination)) throw new IOException("Fresh snapshot destination required");
        if (OperatingSystem.IsMacOS())
        {
            // Preserve application bundle metadata, extended attributes, resource forks and ACLs.
            var start = new ProcessStartInfo("/usr/bin/ditto") { UseShellExecute = false, RedirectStandardError = true };
            foreach (var arg in new[] { "--rsrc", "--extattr", "--acl", source, destination }) start.ArgumentList.Add(arg);
            using var copy = Process.Start(start) ?? throw new IOException("ditto did not start");
            var error = copy.StandardError.ReadToEnd(); copy.WaitForExit();
            if (copy.ExitCode != 0) throw new IOException("Payload copy failed: " + error);
            return;
        }
        MakeDirectory(destination);
        foreach (var entry in entries.Where(v => v.Path != ""))
        {
            var target = Path.Combine(destination, entry.Path);
            if (entry.Kind == "directory") MakeDirectory(target);
            else if (entry.Kind == "file")
            {
                if (protectedCreate == null) File.Copy(Path.Combine(source, entry.Path), target, overwrite: false);
                else
                {
                    using var input = new FileStream(Path.Combine(source, entry.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var output = protectedCreate(target); input.CopyTo(output); output.Flush(true);
                }
            }
            else throw new IOException("Managed payload copy does not admit links");
        }
        if (!OperatingSystem.IsWindows())
            foreach (var entry in entries.Reverse()) File.SetUnixFileMode(Path.Combine(destination, entry.Path), (UnixFileMode)entry.Mode);
    }
    void VerifyRelease(string directory, string version)
    {
        var config = Path.Combine(directory, identity.Platform == "osx" ? "Contents/MacOS/release.json" : "release.json");
        RegularAncestors(config);
        using var input = new FileStream(config, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 65536) throw new IOException("Release identity exceeds limit");
        try
        {
            using var document = JsonDocument.Parse(input);
            var value = document.RootElement;
            if (value.GetProperty("appId").GetString() != identity.AppId || value.GetProperty("platform").GetString() != identity.Platform ||
                value.GetProperty("architecture").GetString() != identity.Architecture || value.GetProperty("updateChannel").GetString() != identity.Channel ||
                value.GetProperty("version").GetString() != version) throw new IOException("Retained release identity mismatch");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new IOException("Invalid retained release identity", ex); }
    }
    void MakeDirectory(string path) { if (protectedMkdir != null) protectedMkdir(path); else Directory.CreateDirectory(path); }
    static string Hash(string path)
    { using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); return Convert.ToHexString(SHA256.HashData(file)); }
    static void Equal(PayloadEntry[] expected, PayloadEntry[] actual)
    { if (!expected.SequenceEqual(actual)) throw new IOException("Payload bytes/entries/links/modes differ from retained snapshot"); }
    static T Read<T>(string path)
    {
        RegularAncestors(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 32 * 1024 * 1024) throw new IOException("Recovery metadata exceeds limit");
        try { return JsonSerializer.Deserialize<T>(input, Json) ?? throw new JsonException("Empty recovery metadata"); }
        catch (JsonException ex) { throw new IOException("Invalid recovery metadata", ex); }
    }
    void Write<T>(string path, T value, bool createNew = false)
    {
        RegularAncestors(path);
        var next = path + ".write-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var output = protectedCreate?.Invoke(next) ?? new FileStream(next, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(output, value, Json); output.Flush(true); }
            File.Move(next, path, overwrite: !createNew);
        }
        finally { File.Delete(next); }
    }
}
