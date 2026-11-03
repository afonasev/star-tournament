using System.Diagnostics;
using Velopack;

namespace StarTournament.Distribution;

public static class BootstrapChangelog
{
    public const string Label = "Что нового на GitHub";

    public static Uri ReleaseUri(string version)
    {
        if (!SemanticVersion.TryParse(version, out var parsed) || parsed.ToString() != version)
            throw new ArgumentException("Canonical selected release version required", nameof(version));
        return new Uri("https://github.com/afonasev/star-tournament/releases/tag/v" + Uri.EscapeDataString(version));
    }

    public static string Link(string version) => $"<a href=\"{ReleaseUri(version).AbsoluteUri}\">{Label}</a>";

    // A hyperlink notification only opens the selected release. It never returns
    // consent or performs download, staging or activation.
    public static void Open(string version, string? target, Action<Uri>? open = null, Action<Exception>? log = null)
    {
        var expected = ReleaseUri(version);
        if (target != expected.AbsoluteUri) return;
        try
        {
            if (open != null) open(expected);
            else using (Process.Start(new ProcessStartInfo(expected.AbsoluteUri) { UseShellExecute = true })) { }
        }
        catch (Exception error) { (log ?? (ex => UpdateFeedback.Log("bootstrap-changelog-open", ex)))(error); }
    }
}
