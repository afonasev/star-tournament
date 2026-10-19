using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarTournament.Distribution;

/// <summary>One actual Player's readiness observation. A timeout never kills a live game.</summary>
public sealed class StartupHealthMonitor
{
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    readonly StartupRecovery recovery;
    readonly Action confirmed;
    readonly Func<bool>? beforeConfirm;
    public StartupTicket Ticket { get; }
    public string ReadyPath { get; }
    public bool IsHealthy { get; private set; }
    public StartupHealthMonitor(StartupRecovery recovery, string version, string readyPath, DateTimeOffset now, Action confirmed, Func<bool>? beforeConfirm = null)
    {
        if (!Path.IsPathFullyQualified(readyPath) || File.Exists(readyPath) || Directory.Exists(readyPath))
            throw new ArgumentException("A fresh absolute readiness path is required");
        this.recovery = recovery; this.confirmed = confirmed; this.beforeConfirm = beforeConfirm; ReadyPath = readyPath;
        Ticket = recovery.BeginLaunch(version, now + StartupTimeout);
    }
    public void Poll(DateTimeOffset now)
    {
        if (IsHealthy) return;
        StartupReady? report = null;
        try
        {
            using var input = new FileStream(ReadyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length <= 4096) report = JsonSerializer.Deserialize<StartupReady>(input, Json);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
        // Journal writes are outside the marker-read catch: durable failure is never health.
        if (report != null && recovery.AcceptReady(report, now, beforeConfirm))
        { IsHealthy = true; confirmed(); return; }
        recovery.Expire(now);
    }
    public void End(DateTimeOffset now)
    { Poll(now); if (!IsHealthy) recovery.EndBeforeReady(Ticket); }
}
