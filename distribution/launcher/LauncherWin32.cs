using System.Runtime.InteropServices;

namespace StarTournament.Distribution;

internal static class LauncherWin32
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate IntPtr WindowProc(IntPtr window,uint message,UIntPtr parameter,IntPtr data);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct WindowClass {
        public uint Size,Style;public IntPtr Procedure;public int ClassExtra,WindowExtra;public IntPtr Instance,Icon,Cursor,Background,Menu;
        [MarshalAs(UnmanagedType.LPWStr)]public string ClassName;public IntPtr SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)]internal struct Point {public int X,Y;public Point(int x,int y){X=x;Y=y;}}
    [StructLayout(LayoutKind.Sequential)]internal struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]internal struct Message {public IntPtr Window;public uint Kind;public UIntPtr Parameter;public IntPtr Data;public uint Time;public Point Position;public uint Private;}
    [StructLayout(LayoutKind.Sequential)]internal struct Paint {public IntPtr Dc;public int Erase;public Rect Bounds;public int Restore,Update;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Reserved;}
    [StructLayout(LayoutKind.Sequential)]internal struct DrawItem {public uint Type,ControlId,Id,Action,State;public IntPtr Window,Dc;public Rect Bounds;public UIntPtr Data;}
    [StructLayout(LayoutKind.Sequential)]internal struct MinMax {public Point Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack;}
    [StructLayout(LayoutKind.Sequential)]internal struct CommonControls {public uint Size,Classes;}
    [StructLayout(LayoutKind.Sequential)]internal struct Pad {public ushort Buttons;public byte LeftTrigger,RightTrigger;public short X,Y,RightX,RightY;}
    [StructLayout(LayoutKind.Sequential)]internal struct PadState {public uint Packet;public Pad Pad;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]internal static extern IntPtr GetModuleHandleW(string? name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)]internal static extern ushort RegisterClassExW(ref WindowClass windowClass);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]internal static extern bool UnregisterClassW(string name,IntPtr instance);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)]internal static extern IntPtr CreateWindowExW(uint extended,string type,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr data);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern IntPtr DefWindowProcW(IntPtr window,uint message,UIntPtr parameter,IntPtr data);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern int GetMessageW(out Message message,IntPtr window,uint minimum,uint maximum);
    [DllImport("user32.dll")]internal static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern IntPtr DispatchMessageW(ref Message message);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern bool IsDialogMessageW(IntPtr window,ref Message message);
    [DllImport("user32.dll")]internal static extern void PostQuitMessage(int result);
    [DllImport("user32.dll")]internal static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")]internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]internal static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")]internal static extern bool EnableWindow(IntPtr window,bool enabled);
    [DllImport("user32.dll")]internal static extern bool UpdateWindow(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]internal static extern bool SetWindowTextW(IntPtr window,string text);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern IntPtr SendMessageW(IntPtr window,uint message,IntPtr parameter,IntPtr data);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern IntPtr GetWindowLongPtrW(IntPtr window,int index);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern IntPtr SetWindowLongPtrW(IntPtr window,int index,IntPtr value);
    [DllImport("user32.dll")]internal static extern bool MoveWindow(IntPtr window,int x,int y,int width,int height,bool repaint);
    [DllImport("user32.dll")]internal static extern bool GetClientRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")]internal static extern IntPtr SetTimer(IntPtr window,UIntPtr id,uint interval,IntPtr callback);
    [DllImport("user32.dll")]internal static extern bool InvalidateRect(IntPtr window,IntPtr rect,bool erase);
    [DllImport("user32.dll")]internal static extern IntPtr BeginPaint(IntPtr window,out Paint paint);
    [DllImport("user32.dll")]internal static extern bool EndPaint(IntPtr window,ref Paint paint);
    [DllImport("user32.dll")]internal static extern int FillRect(IntPtr dc,ref Rect rect,IntPtr brush);
    [DllImport("user32.dll")]internal static extern IntPtr GetFocus();
    [DllImport("user32.dll")]internal static extern IntPtr SetFocus(IntPtr window);
    [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern IntPtr LoadCursorW(IntPtr instance,IntPtr resource);
    [DllImport("user32.dll")]internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]internal static extern uint GetDpiForSystem();
    [DllImport("user32.dll")]internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")]internal static extern bool AdjustWindowRectExForDpi(ref Rect rect,uint style,bool menu,uint extended,uint dpi);
    [DllImport("user32.dll",ExactSpelling=true)]internal static extern bool SystemParametersInfoW(uint action,uint parameter,out Rect area,uint flags);
    [DllImport("user32.dll")]internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]internal static extern int DrawTextW(IntPtr dc,string text,int count,ref Rect rect,uint flags);
    [DllImport("gdi32.dll")]internal static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")]internal static extern IntPtr CreatePen(int style,int width,uint color);
    [DllImport("gdi32.dll")]internal static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")]internal static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
    [DllImport("gdi32.dll")]internal static extern IntPtr GetStockObject(int index);
    [DllImport("gdi32.dll")]internal static extern uint SetTextColor(IntPtr dc,uint color);
    [DllImport("gdi32.dll")]internal static extern int SetBkMode(IntPtr dc,int mode);
    [DllImport("gdi32.dll")]internal static extern bool RoundRect(IntPtr dc,int left,int top,int right,int bottom,int ellipseWidth,int ellipseHeight);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]internal static extern IntPtr CreateFontW(int height,int width,int escapement,int orientation,int weight,uint italic,uint underline,uint strike,uint charset,uint output,uint clip,uint quality,uint pitch,string face);
    [DllImport("comctl32.dll")]internal static extern bool InitCommonControlsEx(ref CommonControls controls);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]internal static extern int SetWindowTheme(IntPtr window,string subApplication,string subId);
    [DllImport("dwmapi.dll")]internal static extern int DwmSetWindowAttribute(IntPtr window,uint attribute,ref int value,uint size);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]internal static extern uint ExtractIconExW(string file,int index,out IntPtr large,out IntPtr small,uint count);
    [DllImport("user32.dll",SetLastError=true)]internal static extern IntPtr CreateIconFromResourceEx(byte[] bits,uint size,bool icon,uint version,int width,int height,uint flags);
    [DllImport("user32.dll",SetLastError=true)]internal static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr icon,int width,int height,uint step,IntPtr brush,uint flags);
    [DllImport("user32.dll")]internal static extern bool DestroyIcon(IntPtr icon);
    [DllImport("xinput1_4.dll")]internal static extern uint XInputGetState(uint index,out PadState state);
}
