using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace StarTournament.Distribution;

/// <summary>Fixed executable + argv, with only NUL in the inherited handle list.
/// In particular, the launcher's short-lived supervisor ACK pipe is never inherited.</summary>
public sealed class WindowsDetachedProcess : IDisposable
{
    readonly SafeWaitHandle handle;
    WindowsDetachedProcess(IntPtr process) => handle = new(process, true);
    [SupportedOSPlatform("windows")]
    public static WindowsDetachedProcess Start(string executable, IEnumerable<string> arguments, string workingDirectory)
    {
        if (!Path.IsPathFullyQualified(executable) || !Path.IsPathFullyQualified(workingDirectory)) throw new ArgumentException("Absolute launch paths required");
        var command = new StringBuilder(string.Join(" ", new[] { executable }.Concat(arguments).Select(Quote)));
        if (command.Length >= 32767) throw new ArgumentException("Windows command line exceeds limit");
        var security = new Security { Length = Marshal.SizeOf<Security>(), Inherit = true };
        using var nul = CreateFileW("NUL", 0xc0000000, 3, ref security, 3, 0x80, IntPtr.Zero);
        if (nul.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        nuint size = 0;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        if (size == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        var attributes = Marshal.AllocHGlobal(checked((int)size)); var handles = Marshal.AllocHGlobal(IntPtr.Size);
        bool initialized = false;
        try
        {
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size)) throw new Win32Exception(Marshal.GetLastWin32Error());
            initialized = true; Marshal.WriteIntPtr(handles, nul.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(attributes, 0, (nuint)0x20002, handles, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var info = new StartupEx { Startup = new Startup { Size = Marshal.SizeOf<StartupEx>(), Flags = 0x100,
                Input = nul.DangerousGetHandle(), Output = nul.DangerousGetHandle(), Error = nul.DangerousGetHandle() }, Attributes = attributes };
            // CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT. No shell, no elevation,
            // no runtime argument interpolation, same normal-user environment/token.
            if (!CreateProcessW(executable, command, IntPtr.Zero, IntPtr.Zero, true, 0x08080000, IntPtr.Zero, workingDirectory, ref info, out var process))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            CloseHandle(process.Thread);
            return new(process.Process);
        }
        finally
        {
            if (initialized) DeleteProcThreadAttributeList(attributes);
            Marshal.FreeHGlobal(attributes); Marshal.FreeHGlobal(handles);
        }
    }
    [SupportedOSPlatform("windows")]
    public bool IsAlive() => WaitForSingleObject(handle, 0) switch { 0 => false, 258 => true, _ => throw new Win32Exception(Marshal.GetLastWin32Error()) };
    public void Dispose() => handle.Dispose(); // Never kills the child.
    public static string Quote(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Contains('\0')) throw new ArgumentException("NUL in launch argument");
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in argument)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') { result.Append('\\', slashes * 2 + 1).Append(c); slashes = 0; continue; }
            result.Append('\\', slashes).Append(c); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    [StructLayout(LayoutKind.Sequential)] struct Security { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Startup
    {
        public int Size; public IntPtr Reserved, Desktop, Title; public int X,Y,XSize,YSize,XChars,YChars,Fill,Flags;
        public short Show, ReservedSize; public IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] struct StartupEx { public Startup Startup; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr Process, Thread; public int Pid, Tid; }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true, ExactSpelling=true)] static extern SafeFileHandle CreateFileW(string name,uint access,uint share,ref Security security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref nuint size);
    [DllImport("kernel32.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,nuint attribute,IntPtr value,nuint size,IntPtr previous,IntPtr returned);
    [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true, ExactSpelling=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool CreateProcessW(string application,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,[MarshalAs(UnmanagedType.Bool)] bool inherit,uint flags,IntPtr environment,string directory,ref StartupEx startup,out ProcessInfo info);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(SafeWaitHandle handle,uint milliseconds);
    [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool CloseHandle(IntPtr handle);
}
