using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StarTournament.Distribution;

public sealed record BootstrapProgressStatus(string State, int Progress = 0, string Error = "", string Version = "");

public sealed record BootstrapProgressContent(string Heading, string Detail, bool Marquee, bool CanCancel)
{
    public static BootstrapProgressContent From(BootstrapProgressStatus status) => status.State switch
    {
        "available" => new("Доступно обновление " + status.Version, "Выберите, установить обновление или играть сейчас.", true, true),
        "downloading" => new($"Скачиваем обновление — {Math.Clamp(status.Progress, 0, 100)}%", "Загружаем и проверяем пакет " + status.Version + ". Пожалуйста, дождитесь завершения.", false, true),
        "applying" => new("Устанавливаем обновление", "Подготавливаем установку. Подтвердите запрос Windows, если он появится.", true, false),
        "launching" => new("Запускаем игру", "Пожалуйста, дождитесь появления окна игры.", true, false),
        "error" or "apply-error" or "unavailable" => new("Не удалось обновить игру", status.Error + "\n\nЖурнал: " + UpdateFeedback.LogPath + "\n\nОК — запустить текущую версию. Отмена — закрыть лаунчер.", false, true),
        _ => new("Проверяем обновления", "Получаем сведения о новой версии. Пожалуйста, подождите.", true, true)
    };
}

/// <summary>Lives independently of network/verification threads, until handoff or Player readiness.</summary>
public sealed class BootstrapStartupWindow : IAsyncDisposable, IDisposable
{
    readonly CancellationTokenSource cancellation = new();
    readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource<bool> acknowledged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    BootstrapProgressStatus snapshot = new("checking");
    volatile bool closing, errorPending;
    IntPtr handle;
    public CancellationToken Token => cancellation.Token;

    [SupportedOSPlatform("windows")]
    public static async Task<BootstrapStartupWindow> Open(BootstrapProgressStatus? initial = null)
    {
        var window = new BootstrapStartupWindow();
        if (initial != null) window.Publish(initial);
        var thread = new Thread(window.Show) { IsBackground = true, Name = "Launcher progress" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await window.ready.Task;
        return window;
    }

    public void Publish(BootstrapProgressStatus value) => Volatile.Write(ref snapshot, value);
    public Task<bool> Confirm(string version) => BootstrapWindowsDialog.Confirm(version,
        "Доступна новая версия Star Tournament: " + version + ". Установить обновление перед запуском игры?", handle);
    public async Task<bool> ShowError(BootstrapProgressStatus failure)
    {
        Publish(failure);
        errorPending = true;
        return await acknowledged.Task;
    }

    void Show()
    {
        BootstrapWindowsDialog.Callback callback = null!;
        BootstrapProgressStatus? rendered = null;
        bool renderedErrorPending = false;
        callback = (window, notification, parameter, _, _) =>
        {
            try
            {
                if (notification == 0) { handle = window; ready.TrySetResult(); } // TDN_CREATED
                if (notification is 0 or 4) // TDN_TIMER; all UI changes run on this STA.
                {
                    if (closing)
                    {
                        SendMessageW(window, 0x46f, new IntPtr(2), new IntPtr(1));
                        SendMessageW(window, 0x466, new IntPtr(2), IntPtr.Zero);
                        return 0;
                    }
                    var current = Volatile.Read(ref snapshot);
                    var showError = errorPending;
                    if (current != rendered || showError != renderedErrorPending)
                    {
                        var content = BootstrapProgressContent.From(current);
                        SetText(window, 3, content.Heading); // TDE_MAIN_INSTRUCTION
                        SetText(window, 0, content.Detail); // TDE_CONTENT
                        SendMessageW(window, 0x467, new IntPtr(content.Marquee ? 1 : 0), IntPtr.Zero);
                        SendMessageW(window, 0x46b, new IntPtr(content.Marquee ? 1 : 0), new IntPtr(30));
                        SendMessageW(window, 0x46a, new IntPtr(Math.Clamp(current.Progress, 0, 100)), IntPtr.Zero);
                        SendMessageW(window, 0x46f, new IntPtr(1), new IntPtr(showError ? 1 : 0)); // OK only after error
                        SendMessageW(window, 0x46f, new IntPtr(2), new IntPtr(content.CanCancel ? 1 : 0));
                        rendered = current;
                        renderedErrorPending = showError;
                    }
                }
                if (notification == 2) // TDN_BUTTON_CLICKED; S_FALSE keeps the same window alive.
                {
                    if (closing) return 0;
                    if (parameter.ToUInt64() == 1 && errorPending)
                    {
                        errorPending = false;
                        Publish(new("launching"));
                        acknowledged.TrySetResult(true);
                    }
                    else if (parameter.ToUInt64() == 2 && BootstrapProgressContent.From(Volatile.Read(ref snapshot)).CanCancel)
                    {
                        cancellation.Cancel();
                        acknowledged.TrySetResult(false);
                        closing = true;
                        return 0;
                    }
                    return 1;
                }
                return 0;
            }
            catch (Exception error)
            {
                UpdateFeedback.Log("launcher-progress", error);
                ready.TrySetException(error);
                cancellation.Cancel();
                acknowledged.TrySetResult(false);
                closing = true;
                return 0;
            }
        };
        try
        {
            var content = BootstrapProgressContent.From(snapshot);
            var config = new BootstrapWindowsDialog.Config {
                Size = (uint)Marshal.SizeOf<BootstrapWindowsDialog.Config>(),
                Flags = 0x0008 | 0x0200 | 0x0800, // close, progress bar, timer
                CommonButtons = 0x0001 | 0x0008, // OK, Cancel
                Title = "Star Tournament — запуск", MainInstruction = content.Heading,
                Content = content.Detail, DefaultButton = 2, Callback = callback, Width = 340
            };
            Marshal.ThrowExceptionForHR(BootstrapWindowsDialog.TaskDialogIndirect(ref config, out _, out _, out _));
            ready.TrySetException(new IOException("Launcher progress window closed before startup"));
        }
        catch (Exception error) { ready.TrySetException(error); cancellation.Cancel(); UpdateFeedback.Log("launcher-progress", error); }
        finally
        {
            GC.KeepAlive(callback);
            if (!closing) cancellation.Cancel();
            acknowledged.TrySetResult(false);
            stopped.TrySetResult();
        }
    }

    static void SetText(IntPtr window, int element, string text)
    {
        var value = Marshal.StringToHGlobalUni(text);
        try { SendMessageW(window, 0x46c, new IntPtr(element), value); }
        finally { Marshal.FreeHGlobal(value); }
    }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public async ValueTask DisposeAsync()
    {
        closing = true;
        await stopped.Task;
        // Idempotent: the window may be closed on Player readiness and again by the owning scope.
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern IntPtr SendMessageW(IntPtr window, uint message, IntPtr parameter, IntPtr value);
}
