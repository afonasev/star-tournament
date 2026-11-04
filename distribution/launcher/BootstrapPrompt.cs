using System.Diagnostics;

namespace StarTournament.Distribution;

public static class BootstrapPrompt
{
    public static async Task<bool> Confirm(string version)
    {
        // Never interpolate signed/server text into an executable script.
        var message = "Доступна новая версия Star Tournament: " + version +
            ". Установить обновление перед запуском игры?";
        if (OperatingSystem.IsWindows())
            return await BootstrapWindowsDialog.Confirm(version, message);
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("Update consent UI requires Windows or macOS");
        var start = new ProcessStartInfo("/usr/bin/osascript")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-e");
        start.ArgumentList.Add("on run argv\nset answer to display dialog (item 1 of argv) with title \"Star Tournament — обновление\" buttons {\"Играть сейчас\", \"Обновить\"} default button \"Играть сейчас\"\nreturn button returned of answer\nend run");
        start.ArgumentList.Add("--"); start.ArgumentList.Add(message);
        using var process = Process.Start(start) ?? throw new IOException("Update confirmation did not start");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var answer = (await output).Trim();
        if (process.ExitCode != 0) throw new IOException("Update confirmation unavailable: " + await error);
        return answer == "Обновить";
    }

    public static ProcessStartInfo FailureCommand(UpdateSnapshot state,bool retry)
    {
        var start=new ProcessStartInfo("/usr/bin/osascript")
        {UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("-e");
        start.ArgumentList.Add(retry
            ? "on run argv\nset answer to display dialog (item 1 of argv) with title \"Star Tournament — обновление\" buttons {\"Закрыть\", \"Играть сейчас\", \"Повторить\"} default button \"Играть сейчас\"\nreturn button returned of answer\nend run"
            : "on run argv\nset answer to display dialog (item 1 of argv) with title \"Star Tournament — обновление\" buttons {\"Закрыть\", \"Играть сейчас\"} default button \"Играть сейчас\"\nreturn button returned of answer\nend run");
        start.ArgumentList.Add("--");start.ArgumentList.Add(state.Error+"\n\nЖурнал: "+UpdateFeedback.LogPath);
        return start;
    }
    public static async Task<BootstrapFailureChoice> Failure(UpdateSnapshot state,bool retry)
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("Native failure prompt requires macOS");
        using var process=Process.Start(FailureCommand(state,retry))??throw new IOException("Update failure UI did not start");
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if(process.ExitCode!=0){UpdateFeedback.Log("bootstrap-failure-ui",new IOException(await error));return BootstrapFailureChoice.Quit;}
        return (await output).Trim() switch {"Повторить" when retry=>BootstrapFailureChoice.Retry,"Играть сейчас"=>BootstrapFailureChoice.Play,_=>BootstrapFailureChoice.Quit};
    }

}
