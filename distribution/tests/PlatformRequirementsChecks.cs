using StarTournament.Distribution;

static class PlatformRequirementsChecks
{
    public static void Run()
    {
        int passed = 0;
        void Check(bool value, string label)
        { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        foreach (var (platform, host, version, expected) in new[]
        {
            ("win", "win", new Version(10, 0, 19042), false),
            ("win", "win", new Version(10, 0, 19043), true),
            ("win", "win", new Version(10, 0, 22631), true),
            ("win", "win", new Version(6, 3, 9600), false),
            ("win", "osx", new Version(15, 0), false),
            ("osx", "osx", new Version(11, 7), false),
            ("osx", "osx", new Version(12, 0), true),
            ("osx", "osx", new Version(15, 0), true),
            ("osx", "win", new Version(10, 0, 22631), false)
        })
        {
            bool allowed = false;
            try
            {
                PlatformRequirements.RequireSupported(platform, (target, minimum) => host == target && version >= minimum);
                allowed = true;
            }
            catch (PlatformNotSupportedException) { }
            Check(allowed == expected, $"{platform} on {host} {version}: admission={expected}");
        }
        bool probed = false, rejected = false;
        try { PlatformRequirements.RequireSupported("linux", (_, _) => { probed = true; return true; }); }
        catch (PlatformNotSupportedException) { rejected = true; }
        Check(rejected && !probed, "unknown target rejected before host probe");
        try { PlatformRequirements.RequireSupported("win", (_, _) => false); }
        catch (PlatformNotSupportedException error)
        { Check(UpdateFeedback.Explain(error).Contains("19043"), "startup feedback identifies the rejected OS floor"); }
        PlatformRequirements.RequireSupported(OperatingSystem.IsMacOS() ? "osx" : "win");
        Check(true, "actual test host satisfies native OS API baseline");
        Console.WriteLine($"{passed} platform requirements checks passed");
    }
}
