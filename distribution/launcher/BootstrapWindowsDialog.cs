using System.Runtime.InteropServices;

namespace StarTournament.Distribution;

internal static class BootstrapWindowsDialog
{
    public static Task<bool> Confirm(string version, string message)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Discovery resumes on a pool thread; Task Dialog requires its own STA.
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Show(version, message)); }
            catch (Exception error) { completion.SetException(error); }
        }) { IsBackground = true, Name = "Bootstrap update question" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    static bool Show(string version, string message)
    {
        Callback callback = (_, notification, _, target, _) =>
        {
            if (notification == 3) // TDN_HYPERLINK_CLICKED; return value is ignored.
                BootstrapChangelog.Open(version, Marshal.PtrToStringUni(target));
            return 0;
        };
        var config = new Config {
            Size = (uint)Marshal.SizeOf<Config>(),
            Flags = 0x0001 | 0x0002 | 0x0008, // hyperlinks, shared question icon, Escape/close
            CommonButtons = 0x0002 | 0x0004, // Yes/No
            Title = "Star Tournament — обновление",
            MainIcon = LoadIconW(IntPtr.Zero, new IntPtr(32514)), // IDI_QUESTION; shared, do not destroy
            Content = message + "\n\n" + BootstrapChangelog.Link(version),
            DefaultButton = 7, // IDNO remains the safe default.
            Callback = callback
        };
        var result = TaskDialogIndirect(ref config, out var button, out _, out _);
        GC.KeepAlive(callback);
        Marshal.ThrowExceptionForHR(result);
        return button == 6; // Only IDYES grants update consent.
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int Callback(IntPtr window, uint notification, UIntPtr parameter, IntPtr target, IntPtr data);

    // The Windows SDK's Task Dialog section explicitly uses pshpack1.h.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    struct Config
    {
        public uint Size;
        public IntPtr Parent, Instance;
        public uint Flags, CommonButtons;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Title;
        public IntPtr MainIcon;
        [MarshalAs(UnmanagedType.LPWStr)] public string? MainInstruction;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Content;
        public uint ButtonCount;
        public IntPtr Buttons;
        public int DefaultButton;
        public uint RadioButtonCount;
        public IntPtr RadioButtons;
        public int DefaultRadioButton;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Verification;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ExpandedInformation;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ExpandedControl;
        [MarshalAs(UnmanagedType.LPWStr)] public string? CollapsedControl;
        public IntPtr FooterIcon;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Footer;
        [MarshalAs(UnmanagedType.FunctionPtr)] public Callback? Callback;
        public IntPtr CallbackData;
        public uint Width;
    }

    [DllImport("comctl32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern int TaskDialogIndirect([In] ref Config config, out int button, out int radioButton, [MarshalAs(UnmanagedType.Bool)] out bool verification);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern IntPtr LoadIconW(IntPtr instance, IntPtr resource);
}
