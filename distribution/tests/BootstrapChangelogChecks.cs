using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using StarTournament.Distribution;

static class BootstrapChangelogChecks
{
    public static void Run()
    {
        var passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        foreach (var version in new[] { "0.1.14", "0.1.14-qa.bootstrap.2" })
        {
            var uri = BootstrapChangelog.ReleaseUri(version);
            Check(uri.AbsoluteUri == "https://github.com/afonasev/star-tournament/releases/tag/v" + Uri.EscapeDataString(version), "selected exact release tag: " + version);
            Check(BootstrapChangelog.Link(version) == $"<a href=\"{uri.AbsoluteUri}\">Что нового на GitHub</a>", "trusted hyperlink markup: " + version);
        }
        foreach (var version in new[] { "", "v1.2.3", "1.2.3+build.4", "1.2.3/../../evil", "1.2.3\" onclick=\"bad", "https://evil.invalid/" })
        {
            try { BootstrapChangelog.ReleaseUri(version); throw new Exception("Unsafe version accepted"); }
            catch (ArgumentException) { Check(true, "invalid/injectable version rejected: " + version); }
        }
        var expected = BootstrapChangelog.ReleaseUri("0.1.14");
        var opened = new List<Uri>();
        BootstrapChangelog.Open("0.1.14", expected.AbsoluteUri, opened.Add);
        Check(opened.SequenceEqual(new[] { expected }), "click opens selected GitHub changelog exactly once");
        foreach (var target in new string?[] { null, "javascript:alert(1)", "https://github.com/other/repo/releases/tag/v0.1.14", expected.AbsoluteUri + "?extra=1", BootstrapChangelog.ReleaseUri("0.1.13").AbsoluteUri })
            BootstrapChangelog.Open("0.1.14", target, opened.Add);
        Check(opened.Count == 1, "callback cannot redirect to another URL or another release");
        var errors = new List<Exception>();
        BootstrapChangelog.Open("0.1.14", expected.AbsoluteUri, _ => throw new IOException("Browser unavailable"), errors.Add);
        Check(errors.Count == 1, "browser failure is logged and does not become update consent or close the question");
        var native = typeof(BootstrapPrompt).Assembly.GetType("StarTournament.Distribution.BootstrapWindowsDialog+Config", throwOnError: true)!;
        Check(Marshal.SizeOf(native) == 32 + 16 * IntPtr.Size, "TaskDialog ABI matches Windows SDK packed size");
        Check(Marshal.OffsetOf(native, "Content").ToInt64() == 12 + 5 * IntPtr.Size, "TaskDialog content pointer has native packed offset");
        var manifest = XDocument.Load("distribution/launcher/app.manifest");
        var common = manifest.Descendants().Single(e => e.Name.LocalName == "assemblyIdentity" && (string?)e.Attribute("name") == "Microsoft.Windows.Common-Controls");
        Check((string?)common.Attribute("version") == "6.0.0.0", "Common Controls v6 dependency enables native hyperlinks");
        Check(manifest.Descendants().Single(e => e.Name.LocalName == "requestedExecutionLevel").Attribute("level")!.Value == "asInvoker", "browser/update question keep normal-user launch level");
        var maintenanceProject = XDocument.Load("distribution/maintenance/Maintenance.csproj");
        Check(maintenanceProject.Descendants("ApplicationManifest").Single().Value == "../launcher/app.manifest",
            "maintenance shares the same v6 TaskDialog manifest for visible install handoff");
        Console.WriteLine($"PASS {passed} bootstrap changelog contracts; native Windows UI/click NotRun");
    }
}
