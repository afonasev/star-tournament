namespace StarTournament.Distribution;

/// <summary>Current Unity Player baseline. Detached signature/package inspection is host-independent.</summary>
public static class PlatformRequirements
{
    public static Version MinimumVersion(string platform) => platform switch
    {
        "win" => new Version(10, 0, 19043),
        "osx" => new Version(12, 0),
        _ => throw new PlatformNotSupportedException("Неподдерживаемая платформа игры: " + platform)
    };

    internal static bool NativeProbe(string platform, Version minimum) => platform switch
    {
        "win" => OperatingSystem.IsWindowsVersionAtLeast(minimum.Major, minimum.Minor, minimum.Build),
        "osx" => OperatingSystem.IsMacOSVersionAtLeast(minimum.Major, minimum.Minor, Math.Max(0, minimum.Build)),
        _ => false
    };

    public static void RequireSupported(string platform, Func<string, Version, bool>? probe = null)
    {
        var minimum = MinimumVersion(platform);
        if (!(probe ?? NativeProbe)(platform, minimum))
            throw new PlatformNotSupportedException(platform == "win"
                ? "Для Star Tournament требуется Windows 10 21H1 (сборка 19043) или новее."
                : "Для Star Tournament требуется macOS 12 или новее.");
    }
}
