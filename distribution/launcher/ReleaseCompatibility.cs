using System.Security.Cryptography;
using System.Text.Json;

namespace StarTournament.Distribution;

public sealed class ReleaseCompatibilityException(string message) : Exception(message);

public sealed record ReleaseRequirements(string MinimumOsVersion, int MinimumUpdaterProtocol, string PayloadFormat, string UserDataPolicy);

public static class ReleaseCompatibility
{
    public const int ReaderProtocol = 2;
    public const string PayloadFormat = "velopack-full-v1", DataPolicy = "profiles2-lab3-prefs1";
    public static ReleaseRequirements Legacy(string platform) => new(PlatformRequirements.MinimumVersion(platform).ToString(), 1, PayloadFormat, "legacy-preserved-v1");

    public static ReleaseRequirements Parse(JsonElement release, string platform)
    {
        var schema = release.GetProperty("schema").GetInt32();
        if (schema == 1)
        {
            if (release.TryGetProperty("compatibility", out _)) throw new CryptographicException("Schema1 cannot override its historical compatibility contract");
            return Legacy(platform);
        }
        if (schema != 2) throw new ReleaseCompatibilityException("Формат обновления требует нового установщика Star Tournament.");
        try
        {
            var data = release.GetProperty("compatibility");
            var fields = data.EnumerateObject().Select(p => p.Name).ToArray();
            if (fields.Length != 4 || fields.Distinct().Count() != 4 || fields.Except(new[] { "minimumOsVersion", "minimumUpdaterProtocol", "payloadFormat", "userDataPolicy" }).Any())
                throw new CryptographicException("Invalid compatibility fields");
            var requirements = new ReleaseRequirements(data.GetProperty("minimumOsVersion").GetString()!, data.GetProperty("minimumUpdaterProtocol").GetInt32(),
                data.GetProperty("payloadFormat").GetString()!, data.GetProperty("userDataPolicy").GetString()!);
            Validate(requirements, platform);
            if (requirements.MinimumUpdaterProtocol < 2) throw new CryptographicException("Schema2 requires reader2 or newer");
            if (requirements.UserDataPolicy == "legacy-preserved-v1") throw new CryptographicException("Legacy policy is reserved for historical schema1");
            return requirements;
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new CryptographicException("Invalid signed compatibility descriptor", error); }
    }

    public static void Validate(ReleaseRequirements requirements, string platform)
    {
        if (!Version.TryParse(requirements.MinimumOsVersion, out var minimum) || minimum.ToString() != requirements.MinimumOsVersion ||
            minimum.Revision != -1 || (platform == "win" && minimum.Build < 0) || minimum < PlatformRequirements.MinimumVersion(platform) ||
            requirements.MinimumUpdaterProtocol < 1 || string.IsNullOrEmpty(requirements.PayloadFormat) || requirements.PayloadFormat.Length > 64 ||
            string.IsNullOrEmpty(requirements.UserDataPolicy) || requirements.UserDataPolicy.Length > 64)
            throw new CryptographicException("Invalid release compatibility requirements");
    }

    public static void Require(ReleaseRequirements requirements, string platform, int readerProtocol = ReaderProtocol, Func<string, Version, bool>? hostProbe = null)
    {
        Validate(requirements, platform);
        if (requirements.MinimumUpdaterProtocol > readerProtocol)
            throw new ReleaseCompatibilityException("Для этого обновления требуется новый установщик Star Tournament.");
        if (requirements.PayloadFormat != PayloadFormat || requirements.UserDataPolicy is not (DataPolicy or "legacy-preserved-v1"))
            throw new ReleaseCompatibilityException("Формат пакета или данных этого обновления не поддерживается установленной игрой.");
        var minimum = Version.Parse(requirements.MinimumOsVersion);
        if (!(hostProbe ?? PlatformRequirements.NativeProbe)(platform, minimum))
            throw new ReleaseCompatibilityException("Для этого обновления требуется " + (platform == "win" ? "Windows " : "macOS ") + minimum + " или новее.");
    }
}
