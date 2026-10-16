using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Velopack;

namespace StarTournament.Distribution;

public sealed record WindowsBootstrap(string Name, string Sha256, long Size);
public sealed record WindowsRecoveryAttempt(string Id, LauncherConfig Previous, string Target, string Phase, WindowsBootstrap[] Bootstrap);
public sealed record WindowsRecoveryState(int Schema, string RootId, string AppId, string[] Blocked, WindowsRecoveryAttempt? Attempt);

/// <summary>
/// Fixed-root protected transaction. Maintenance MUST validate ownership/ACLs, elevation,
/// exact supervisor and idle processes and hold WindowsInstallLease before any mutation.
/// No path or executable is taken from user metadata. Optional creators provide atomic Windows ACLs.
/// </summary>
public sealed class WindowsRecoveryStore
{
    static readonly string[] Names = [".portable", "Star Tournament.exe", "Update.exe"];
    static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    readonly string root, store, current, statePath;
    readonly LauncherConfig identity;
    readonly PayloadRecovery payload;
    readonly Action<string> mkdir;
    readonly Func<string, FileStream> create, createReady;
    public WindowsRecoveryStore(string protectedRoot, LauncherConfig protectedIdentity, Action<string>? mkdir = null, Func<string, FileStream>? create = null, Func<string, FileStream>? createReady = null)
    {
        if (!Path.IsPathFullyQualified(protectedRoot) || protectedIdentity.Platform != "win" || protectedIdentity.Architecture != "x64")
            throw new ArgumentException("Fixed absolute Windows x64 installation required");
        root = Path.GetFullPath(protectedRoot); identity = protectedIdentity;
        current = Path.Combine(root, "current"); store = Path.Combine(root, "maintenance", "recovery"); statePath = Path.Combine(store, "state.json");
        Plain(root); Plain(store);
        this.mkdir = mkdir ?? (p => Directory.CreateDirectory(p)); this.create = create ?? (p => new FileStream(p, FileMode.CreateNew, FileAccess.Write, FileShare.None));
        this.createReady = createReady ?? this.create;
        this.mkdir(store);
        var releaseIdentity = new RecoveryIdentity(identity.AppId, identity.Platform, identity.Architecture, identity.UpdateChannel ?? throw new ArgumentException("Channel required"));
        payload = mkdir != null && create != null ? new(store, releaseIdentity, mkdir, create) : new(store, releaseIdentity);
    }
    public WindowsRecoveryState Read()
    {
        Plain(statePath);
        if (Directory.Exists(statePath)) throw new IOException("Protected state is a directory");
        if (!File.Exists(statePath)) return new(1, StartupRecovery.InstallationId(root), identity.AppId, [], null);
        using var input = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 131072) throw new IOException("Protected recovery state exceeds limit");
        WindowsRecoveryState state;
        try { state = JsonSerializer.Deserialize<WindowsRecoveryState>(input, Json) ?? throw new JsonException(); }
        catch (JsonException e) { throw new IOException("Protected recovery state is invalid", e); }
        Validate(state); return state;
    }
    public WindowsRecoveryAttempt Prepare(LauncherConfig installed, string target, bool explicitRetry)
    {
        var state = Read();
        if (state.Attempt != null) throw new IOException("A protected recovery transaction is still pending");
        if (state.Blocked.Length >= 256 && !state.Blocked.Contains(target)) throw new IOException("Failure history capacity reached before activation");
        if (state.Blocked.Contains(target) && !explicitRetry) throw new IOException("Explicit retry is required for this failed release");
        AssertConfig(installed, installed.Version);
        var attempt = new WindowsRecoveryAttempt(Guid.NewGuid().ToString("N"), installed, target, "capturing", []);
        state = state with { Attempt = attempt }; Validate(state); Write(state); // No updater may run in capturing.
        payload.Capture(current, installed.Version, attempt.Id);
        var boot = Path.Combine(store, attempt.Id + ".bootstrap"); Plain(boot); mkdir(boot);
        var entries = new List<WindowsBootstrap>();
        foreach (var name in Names)
        {
            var source = Path.Combine(root, name); Plain(source);
            var before = Hash(source); Copy(source, Path.Combine(boot, name));
            if (before != Hash(source) || before != Hash(Path.Combine(boot, name))) throw new IOException("Bootstrap changed during capture");
            entries.Add(new(name, before, new FileInfo(source).Length));
        }
        using (var ready = createReady(ReadyPath(attempt.Id))) ready.Flush(true);
        attempt = attempt with { Phase = "prepared", Bootstrap = entries.ToArray() };
        Write(state with { Attempt = attempt }); return attempt;
    }
    public void Apply(Action applyVerifiedPackage)
    {
        var state = Read();
        if (state.Attempt is not { Phase: "prepared" } attempt) throw new IOException("No complete prepared recovery snapshot");
        payload.Verify(attempt.Id, current); VerifyBootstrap(attempt);
        try
        {
            applyVerifiedPackage();
            AssertConfig(attempt.Previous, attempt.Target);
            Write(state with { Attempt = attempt with { Phase = "installed" } });
        }
        catch { Restore(); throw; }
    }
    public WindowsRecoveryAttempt Restore(string? expectedId = null)
    {
        var state = Read(); var attempt = state.Attempt ?? throw new IOException("No protected previous release");
        if (expectedId != null && attempt.Id != expectedId) throw new IOException("Recovery transaction changed during elevation");
        if (attempt.Phase == "installed" && (!File.Exists(ReadyPath(attempt.Id)) || new FileInfo(ReadyPath(attempt.Id)).Length != 0))
            throw new IOException("Startup was confirmed or its latch is missing; use repair instead of rollback");
        if (attempt.Phase == "restored") { AssertConfig(attempt.Previous, attempt.Previous.Version); return attempt; }
        if (attempt.Phase == "capturing")
        {
            // Interrupted BEFORE activation permission: original protected files were never replaced.
            AssertConfig(attempt.Previous, attempt.Previous.Version);
        }
        else
        {
            payload.Verify(attempt.Id, current); VerifyBootstrap(attempt);
            Write(state with { Attempt = attempt with { Phase = "restoring" }, Blocked = Add(state.Blocked, attempt.Target) });
            payload.Restore(attempt.Id, current);
            var boot = Path.Combine(store, attempt.Id + ".bootstrap");
            foreach (var entry in attempt.Bootstrap)
            {
                var destination = Path.Combine(root, entry.Name); Plain(destination);
                var next = destination + ".restore-" + Guid.NewGuid().ToString("N");
                try { Copy(Path.Combine(boot, entry.Name), next); File.Move(next, destination, true); }
                finally { File.Delete(next); }
            }
            AssertConfig(attempt.Previous, attempt.Previous.Version);
            foreach (var entry in attempt.Bootstrap) if (Hash(Path.Combine(root, entry.Name)) != entry.Sha256) throw new IOException("Bootstrap restore readback failed");
        }
        attempt = attempt with { Phase = "restored" };
        Write(state with { Attempt = attempt, Blocked = Add(state.Blocked, attempt.Target) }); return attempt;
    }
    /// <summary>Called in an authorized elevated operation after normal supervisor observed exact startup health.</summary>
    public void Cleanup(string confirmedInstalledVersion)
    {
        var state = Read(); if (state.Attempt is not { } attempt) return;
        var expected = attempt.Phase == "installed" ? attempt.Target : attempt.Phase == "restored" ? attempt.Previous.Version : null;
        if (expected == null || expected != confirmedInstalledVersion || attempt.Phase == "installed" && (!File.Exists(ReadyPath(attempt.Id)) || new FileInfo(ReadyPath(attempt.Id)).Length == 0)) throw new IOException("Confirm or restore this transaction before cleanup");
        AssertConfig(attempt.Previous, expected);
        payload.Remove(attempt.Id, current);
        // Capturing was journaled before these owned directories could be created.
        foreach (var directory in new[] { Path.Combine(store, ".capture-" + attempt.Id), Path.Combine(store, attempt.Id + ".bootstrap") })
        { Plain(directory); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Write(state with { Attempt = null, Blocked = attempt.Phase == "installed" ? state.Blocked.Where(v => v != attempt.Target).ToArray() : state.Blocked }); // A failure before this write is safely retried.
        File.Delete(ReadyPath(attempt.Id));
    }
    string ReadyPath(string id) => Path.Combine(store, id + ".ready");
    void VerifyBootstrap(WindowsRecoveryAttempt attempt)
    {
        var boot = Path.Combine(store, attempt.Id + ".bootstrap"); Plain(boot);
        foreach (var entry in attempt.Bootstrap)
        {
            var path = Path.Combine(boot, entry.Name); Plain(path);
            if (new FileInfo(path).Length != entry.Size || Hash(path) != entry.Sha256) throw new IOException("Retained bootstrap failed verification");
        }
    }
    void AssertConfig(LauncherConfig expected, string version)
    {
        Plain(current); var path = Path.Combine(current, "release.json"); Plain(path);
        if (new FileInfo(path).Length > 65536) throw new IOException("Installed config exceeds limit");
        var actual = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (actual == null || actual.AppId != expected.AppId || actual.Platform != expected.Platform || actual.Architecture != expected.Architecture ||
            actual.UpdateChannel != expected.UpdateChannel || actual.Version != version) throw new IOException("Protected installed identity mismatch");
    }
    void Validate(WindowsRecoveryState state)
    {
        if (state.Schema != 1 || state.RootId != StartupRecovery.InstallationId(root) || state.AppId != identity.AppId ||
            state.Blocked == null || state.Blocked.Length > 256 || state.Blocked.Distinct().Count() != state.Blocked.Length ||
            state.Blocked.Any(v => !Canonical(v))) throw new IOException("Foreign protected recovery state");
        if (state.Attempt is { } a)
        {
            if (a.Id == null || !Regex.IsMatch(a.Id, "\\A[0-9a-f]{32}\\z") || a.Previous == null || a.Previous.AppId != identity.AppId ||
                a.Previous.Platform != "win" || a.Previous.Architecture != "x64" || a.Previous.UpdateChannel != identity.UpdateChannel ||
                !Canonical(a.Previous.Version) || !Canonical(a.Target) || SemanticVersion.Parse(a.Target) <= SemanticVersion.Parse(a.Previous.Version) ||
                SemanticVersion.Parse(a.Target).IsPrerelease != (a.Previous.UpdateChannel == "test") || a.Bootstrap == null ||
                a.Phase is not ("capturing" or "prepared" or "installed" or "restoring" or "restored")) throw new IOException("Invalid protected transaction");
            if (a.Phase == "capturing" || a.Phase == "restored" && a.Bootstrap.Length == 0)
            { if (a.Bootstrap.Length != 0) throw new IOException("Incomplete bootstrap capture"); }
            else if (a.Bootstrap.Length != Names.Length || !a.Bootstrap.Select(e => e?.Name).SequenceEqual(Names) ||
                a.Bootstrap.Any(e => e == null || e.Size < 0 || !Regex.IsMatch(e.Sha256 ?? "", "\\A[0-9A-F]{64}\\z"))) throw new IOException("Invalid bootstrap allow-list");
        }
    }
    static bool Canonical(string? version) => version != null && version.Length <= 128 && SemanticVersion.TryParse(version, out var parsed) && parsed.ToString() == version;
    static string[] Add(string[] versions, string target) => versions.Contains(target) ? versions : versions.Append(target).ToArray();
    void Write(WindowsRecoveryState state)
    {
        Validate(state); var next = statePath + ".next-" + Guid.NewGuid().ToString("N");
        try { using (var output = create(next)) { JsonSerializer.Serialize(output, state, Json); output.Flush(true); } File.Move(next, statePath, true); }
        finally { File.Delete(next); }
    }
    void Copy(string source, string destination)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = create(destination); input.CopyTo(output); output.Flush(true);
    }
    static string Hash(string path) { Plain(path); using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)); }
    static void Plain(string path)
    {
        for (string? part = path; part != null; part = Path.GetDirectoryName(part))
        {
            try { if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new IOException("Recovery path is a reparse point"); }
            catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
        }
    }
}
