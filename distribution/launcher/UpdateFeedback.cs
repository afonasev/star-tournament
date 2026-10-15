using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Velopack;

namespace StarTournament.Distribution;

public static class UpdateFeedback
{
    public static string LogRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StarTournament", "launcher");
    public static string LogPath => Path.Combine(LogRoot, "errors.log");
    public static string Explain(Exception error) => error switch
    {
        UnauthorizedAccessException or Win32Exception { NativeErrorCode: 5 } => "Нет доступа к папке игры или обновления. Проверьте права доступа.",
        CryptographicException => "Пакет обновления не прошёл проверку подлинности. Скачайте обновление заново.",
        TimeoutException or OperationCanceledException => "Сервер обновлений не отвечает. Проверьте подключение и повторите попытку.",
        HttpRequestException => "Не удалось связаться с GitHub. Проверьте подключение и повторите попытку.",
        Win32Exception { NativeErrorCode: 1223 } => "Установка отменена: Windows не получила разрешение администратора. Повторите установку и подтвердите запрос Windows.",
        FileNotFoundException or DirectoryNotFoundException or Win32Exception { NativeErrorCode: 2 or 3 } => "Не найден нужный файл игры или обновления. Проверьте карантин антивируса; при необходимости переустановите игру.",
        IOException => "Не удалось прочитать или записать файлы. Проверьте свободное место и доступ к папке игры.",
        _ => "Не удалось завершить операцию. Подробности сохранены в журнале."
    };
    public static void Log(string stage, Exception? error = null)
    {
        try
        {
            Directory.CreateDirectory(LogRoot);
            File.AppendAllText(LogPath, $"{DateTimeOffset.UtcNow:O} pid={Environment.ProcessId} {stage} {error}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Console.Error.WriteLine(stage + " " + error); }
    }
    public static void Show(string message)
    {
        Log(message);
        var text = message + "\n\nЖурнал: " + LogPath;
        if (OperatingSystem.IsWindows()) MessageBoxW(IntPtr.Zero, text, "Star Tournament — ошибка обновления или запуска", 0x10);
        else Console.Error.WriteLine(text);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
}

// A handoff is not successful activation. Persist before exiting so a failed updater
// cannot trap every subsequent shortcut launch in the same automatic apply attempt.
public sealed class UpdateAttemptJournal
{
    public sealed record Attempt(string Version, string Message);
    readonly string path;
    readonly Action<string, Exception> log;
    public UpdateAttemptJournal(string path, Action<string, Exception>? log = null) { this.path = path; this.log = log ?? ((stage, error) => UpdateFeedback.Log(stage, error)); }
    public Attempt? Read()
    {
        if (!File.Exists(path)) return null;
        try
        {
            if (new FileInfo(path).Length > 65536) throw new JsonException("Apply journal exceeds limit");
            var attempt = JsonSerializer.Deserialize<Attempt>(File.ReadAllText(path));
            if (attempt == null || string.IsNullOrEmpty(attempt.Version) || !SemanticVersion.TryParse(attempt.Version, out _) || string.IsNullOrWhiteSpace(attempt.Message))
                throw new JsonException("Invalid apply journal identity");
            return attempt;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            log("read-apply-journal", ex);
            return new("", "Не удалось прочитать результат предыдущей установки. Повторите установку вручную.");
        }
    }
    public bool AlreadyAttempted(string version) => Read() is { } attempt && (attempt.Version == version || attempt.Version == "");
    public void Begin(string version) => Fail(version, "Предыдущая установка не завершилась. Можно продолжить играть или повторить установку.");
    public void Fail(string version, string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new Attempt(version, message)));
        File.Move(path + ".tmp", path, true);
    }
    public void ClearIfInstalled(string installed)
    {
        var attempt = Read();
        if (attempt != null && SemanticVersion.TryParse(attempt.Version, out var target) && SemanticVersion.Parse(installed) >= target)
            File.Delete(path);
    }
    public static UpdateAttemptJournal ForApp(string appId) => new(Path.Combine(UpdateFeedback.LogRoot, Path.GetFileName(appId) + ".apply.json"));
}
