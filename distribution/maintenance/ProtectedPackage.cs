using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Velopack;

namespace StarTournament.Distribution;

/// <summary>Platform-neutral copy-and-reverify contract used before any updater execution.</summary>
public static class ProtectedPackage
{
    public static async Task<(string EnvelopePath, string PackagePath)> CopyAndVerifyAsync(
        AuthenticatedSource verifier, string signedEnvelope, VelopackAsset asset,
        string sourcePackage, string protectedDirectory, CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathFullyQualified(sourcePackage) || !Path.IsPathFullyQualified(protectedDirectory))
            throw new CryptographicException("Absolute paths required for protected package copy");
        if (!Directory.Exists(protectedDirectory) || (File.GetAttributes(protectedDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new CryptographicException("Protected destination must be an existing normal directory");
        var envelopePath = Path.Combine(protectedDirectory, "star-authenticated.json");
        var packagePath = Path.Combine(protectedDirectory, Path.GetFileName(asset.FileName));
        if (!string.Equals(Path.GetFileName(asset.FileName), asset.FileName, StringComparison.Ordinal))
            throw new CryptographicException("Package filename is not a basename");
        await File.WriteAllTextAsync(envelopePath, signedEnvelope, cancellationToken);
        await using (var input = new FileStream(sourcePackage, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.SequentialScan))
        await using (var output = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.SequentialScan))
        {
            if (input.Length != asset.Size) throw new CryptographicException("Source package size mismatch");
            await input.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
        verifier.Admit(await File.ReadAllTextAsync(envelopePath, cancellationToken));
        if (verifier.Asset == null || verifier.Asset.FileName != asset.FileName || verifier.Asset.Version != asset.Version || verifier.Asset.Size != asset.Size || verifier.Asset.SHA256 != asset.SHA256)
            throw new CryptographicException("Copied metadata does not describe admitted package");
        await verifier.VerifyPackage(packagePath, verifier.Asset, cancellationToken);
        return (envelopePath, packagePath);
    }
}

/// <summary>Distinguishes an installed legacy payload from the SDK's package-only cache.</summary>
public static class LegacyInstallationProbe
{
    const string LegacyAppId = "tech.afonasev.star-tournament.win-x64";

    public static bool IsInstalled(string root, bool hasUninstallEntry = false)
    {
        if (hasUninstallEntry) return true;
        var current = Path.Combine(root, "current");
        var release = Path.Combine(current, "release.json");
        if (File.Exists(release) && IsValidRelease(release)) return true;
        return File.Exists(Path.Combine(current, "StarTournamentLauncher.exe")) ||
            File.Exists(Path.Combine(current, "StarTournament.exe")) ||
            File.Exists(Path.Combine(current, "game", "StarTournament.exe"));
    }

    static bool IsValidRelease(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > 65536) return false;
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            var version = root.GetProperty("version").GetString();
            return root.GetProperty("appId").GetString() == LegacyAppId && root.GetProperty("platform").GetString() == "win" &&
                root.GetProperty("architecture").GetString() == "x64" && version != null &&
                Regex.IsMatch(version, @"\A(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?\z");
        }
        catch { return false; }
    }
}
