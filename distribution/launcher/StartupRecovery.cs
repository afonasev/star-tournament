using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Velopack;

namespace StarTournament.Distribution;

public sealed record RecoveryIdentity(string AppId, string Platform, string Architecture, string Channel);
public sealed record StartupTicket(string Version, string Token, long DeadlineUtcTicks);
public sealed record StartupReady(int Schema, string Version, string Token, string Phase);
public sealed record RecoveryAttempt(string Previous, string Target, string BackupId, string Phase, string? Failure = null);
public sealed record RecoveryState(int Schema, RecoveryIdentity Identity, string? HealthyVersion,
    RecoveryAttempt? Attempt, StartupTicket? Launch, string[] BlockedVersions);

/// <summary>
/// Durable decision log only: the platform supervisor owns payload/data snapshots and restoration.
/// All callers MUST hold the same per-install exclusive lease and establish that an old Player
/// is stopped before RecoverInterrupted/MarkRestored. No method kills or replaces a process.
/// Corrupt/foreign state is an error, never permission to install again.
/// </summary>
public sealed class StartupRecovery
{
    const int MaxBytes = 131072;
    static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    readonly string path;
    readonly RecoveryIdentity identity;

    public StartupRecovery(string path, RecoveryIdentity identity)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Absolute recovery journal path required");
        if (identity.Platform is not ("osx" or "win") || identity.Architecture is not ("arm64" or "x64") ||
            identity.Channel is not ("test" or "production") || !Regex.IsMatch(identity.AppId, @"\A[A-Za-z0-9.-]{1,160}\z"))
            throw new ArgumentException("Invalid recovery identity");
        this.path = path; this.identity = identity;
    }

    public static string InstallationId(string installedRoot)
    {
        if (!Path.IsPathFullyQualified(installedRoot)) throw new ArgumentException("Absolute installation root required");
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installedRoot));
        if (OperatingSystem.IsWindows()) path = path.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path))).ToLowerInvariant();
    }
    public static StartupRecovery ForInstallation(LauncherConfig config, string logs, string installedRoot) =>
        new(Path.Combine(logs, config.AppId + "." + InstallationId(installedRoot) + ".startup.json"),
            new(config.AppId, config.Platform, config.Architecture, config.UpdateChannel ?? throw new ArgumentException("Release channel required")));

    public RecoveryState Read()
    {
        CheckRegularPath();
        if (Directory.Exists(path)) throw new IOException("Recovery journal is a directory");
        if (!File.Exists(path)) return new(1, identity, null, null, null, []);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaxBytes) throw new IOException("Recovery journal exceeds limit");
        RecoveryState state;
        try { state = JsonSerializer.Deserialize<RecoveryState>(input, Json) ?? throw new JsonException("Empty journal"); }
        catch (JsonException ex) { throw new IOException("Invalid recovery journal; automatic activation is disabled", ex); }
        Validate(state);
        return state;
    }

    public bool IsBlocked(string version) { Version(version); return Read().BlockedVersions.Contains(version, StringComparer.Ordinal); }

    public void Prepare(string previous, string target, string backupId)
    {
        Version(previous); Version(target);
        if (SemanticVersion.Parse(target) <= SemanticVersion.Parse(previous) || !Token(backupId))
            throw new InvalidOperationException("A newer release and completed snapshot identity are required");
        var state = Read();
        if (state.HealthyVersion != previous || state.Attempt != null || state.Launch != null || state.BlockedVersions.Contains(target))
            throw new InvalidOperationException("Previous release is unconfirmed, recovery/cleanup is pending, or target is blocked");
        Write(state with { Attempt = new(previous, target, backupId, "prepared") });
    }

    /// <summary>Run on launcher restart, after checking the exact previous process is no longer live.</summary>
    public void RecoverInterrupted(string installed)
    {
        Version(installed);
        var state = Read();
        if (state.Attempt is { } attempt && installed != attempt.Previous && installed != attempt.Target)
            throw new IOException("Installed release is outside the recovery transaction");
        if (state.Attempt is { Phase: "confirmed" } confirmed && installed != confirmed.Target ||
            state.Attempt is { Phase: "restored" } restored && installed != restored.Previous)
            throw new IOException("Confirmed/restored release no longer matches installed payload; retain backup");
        if (state.Launch != null)
        {
            Fail(state, "Запуск прерван до подтверждения готовности меню.");
            return;
        }
        if (state.Attempt is { Phase: "prepared" } prepared && installed == prepared.Previous)
            Fail(state, "Установка не завершилась; сохранена предыдущая рабочая версия.");
    }

    public StartupTicket BeginLaunch(string installed, DateTimeOffset deadline)
    {
        Version(installed);
        var state = Read();
        if (state.Launch != null) throw new InvalidOperationException("Previous launch must be reconciled first");
        if (state.Attempt is { } attempt && (attempt.Phase != "prepared" || installed != attempt.Target))
            throw new InvalidOperationException("Restore/cleanup is required before another launch");
        if (state.BlockedVersions.Contains(installed)) throw new InvalidOperationException("This release requires explicit retry after restore");
        var ticket = new StartupTicket(installed, Guid.NewGuid().ToString("N"), deadline.UtcTicks);
        Write(state with { Launch = ticket, Attempt = state.Attempt is { } pending ? pending with { Phase = "starting" } : null });
        return ticket;
    }

    public bool AcceptReady(StartupReady ready, DateTimeOffset now, Func<bool>? beforeCommit = null)
    {
        var state = Read();
        if (state.Launch is not { } launch || ready.Schema != 1 || ready.Phase != "menu-ready" ||
            ready.Version != launch.Version || ready.Token != launch.Token) return false;
        if (now.UtcTicks >= launch.DeadlineUtcTicks)
        { Fail(state, "Истекло время подтверждения готовности меню."); return false; }
        // A protected platform latch must be durable before per-user confirmation.
        // False keeps this exact launch pending; invalid tokens never invoke the hook.
        if (beforeCommit != null && !beforeCommit()) return false;
        Write(state with { HealthyVersion = launch.Version, Launch = null,
            Attempt = state.Attempt is { } attempt ? attempt with { Phase = "confirmed" } : null });
        return true;
    }

    public bool Expire(DateTimeOffset now)
    {
        var state = Read();
        if (state.Launch == null || now.UtcTicks < state.Launch.DeadlineUtcTicks) return false;
        Fail(state, "Истекло время подтверждения готовности меню."); return true;
    }

    public void EndBeforeReady(StartupTicket ticket)
    {
        var state = Read();
        if (state.Launch != ticket) return; // A stale observer cannot fail a newer launch.
        Fail(state, "Игра завершилась до подтверждения готовности меню.");
    }

    public void InstallationFailed(string target, string reason)
    {
        var state = Read();
        if (state.Attempt is not { Phase: "prepared" } attempt || attempt.Target != target)
            throw new InvalidOperationException("No matching prepared installation");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2048) throw new ArgumentException("Bounded failure reason required");
        Fail(state, reason);
    }

    /// <summary>Only AFTER platform restore verified the snapshot and the actual installed version.</summary>
    public void MarkRestored(string installed, string backupId)
    {
        var state = Read();
        if (state.Attempt is not { Phase: "recovery-required" } attempt || attempt.Previous != installed || attempt.BackupId != backupId)
            throw new InvalidOperationException("Restore must match the retained snapshot and previous release");
        Write(state with { HealthyVersion = installed, Attempt = attempt with { Phase = "restored" }, Launch = null });
        // Target stays blocked. Never clear it just because old files were restored.
    }

    /// <summary>Only AFTER successful ready or verified restore AND deletion of that backup by the supervisor.</summary>
    public void ConfirmBackupRemoved(string backupId)
    {
        var state = Read();
        if (state.Attempt is not { } attempt || attempt.Phase is not ("confirmed" or "restored") || attempt.BackupId != backupId ||
            state.HealthyVersion != (attempt.Phase == "confirmed" ? attempt.Target : attempt.Previous))
            throw new InvalidOperationException("An unconfirmed/unrestored backup must be retained");
        Write(state with { Attempt = null });
    }

    /// <summary>Explicit user retry only; the platform must create a fresh snapshot before Prepare.</summary>
    public void PermitRetry(string target)
    {
        Version(target);
        var state = Read();
        if (state.Attempt != null || state.Launch != null) throw new InvalidOperationException("Finish restoration/cleanup before retry");
        Write(state with { BlockedVersions = state.BlockedVersions.Where(v => v != target).ToArray() });
    }

    void Fail(RecoveryState state, string reason)
    {
        var attempt = state.Attempt;
        var blocked = state.BlockedVersions;
        if (attempt != null && !blocked.Contains(attempt.Target)) blocked = blocked.Append(attempt.Target).ToArray();
        Write(state with { Launch = null, HealthyVersion = attempt?.Previous,
            Attempt = attempt is null ? null : attempt with { Phase = "recovery-required", Failure = reason }, BlockedVersions = blocked });
    }

    void Validate(RecoveryState state)
    {
        try
        {
            if (state.Schema != 1 || state.Identity != identity || state.BlockedVersions == null || state.BlockedVersions.Length > 256 ||
                state.BlockedVersions.Distinct(StringComparer.Ordinal).Count() != state.BlockedVersions.Length) throw new InvalidOperationException();
            foreach (var version in state.BlockedVersions) Version(version);
            if (state.HealthyVersion != null) { Version(state.HealthyVersion); if (state.BlockedVersions.Contains(state.HealthyVersion)) throw new InvalidOperationException(); }
            if (state.Launch is { } launch)
            { Version(launch.Version); if (!Token(launch.Token) || launch.DeadlineUtcTicks <= 0 || launch.DeadlineUtcTicks > DateTimeOffset.MaxValue.UtcTicks) throw new InvalidOperationException(); }
            if (state.Attempt is { } attempt)
            {
                Version(attempt.Previous); Version(attempt.Target);
                if (SemanticVersion.Parse(attempt.Target) <= SemanticVersion.Parse(attempt.Previous) || !Token(attempt.BackupId) ||
                    attempt.Phase is not ("prepared" or "starting" or "confirmed" or "recovery-required" or "restored") ||
                    attempt.Failure?.Length > 2048) throw new InvalidOperationException();
                if (attempt.Phase == "starting" ? state.Launch?.Version != attempt.Target : state.Launch != null) throw new InvalidOperationException();
                if (state.HealthyVersion != (attempt.Phase == "confirmed" ? attempt.Target : attempt.Previous)) throw new InvalidOperationException();
                if (attempt.Phase is "recovery-required" or "restored" ? !state.BlockedVersions.Contains(attempt.Target) || string.IsNullOrWhiteSpace(attempt.Failure) : state.BlockedVersions.Contains(attempt.Target) || attempt.Failure != null)
                    throw new InvalidOperationException();
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or NullReferenceException)
        { throw new IOException("Invalid/foreign recovery state; automatic activation is disabled", ex); }
    }

    static bool Token(string? value) => value != null && Regex.IsMatch(value, @"\A[0-9a-f]{32}\z");
    void Version(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128 || !SemanticVersion.TryParse(value, out var parsed) || parsed.ToString() != value || parsed.IsPrerelease != (identity.Channel == "test"))
            throw new ArgumentException("Canonical semantic version required");
    }
    void CheckRegularPath()
    {
        for (var current = path; current != null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Recovery journal path contains a link/reparse point");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
    void Write(RecoveryState state)
    {
        Validate(state);
        CheckRegularPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        CheckRegularPath();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Json);
        if (bytes.Length > MaxBytes) throw new IOException("Recovery journal exceeds limit");
        var next = path + ".next-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var output = new FileStream(next, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes); output.Flush(flushToDisk: true); }
            File.Move(next, path, overwrite: true);
        }
        finally { File.Delete(next); }
    }
}
