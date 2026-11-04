using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static StarTournament.Distribution.LauncherWin32;

namespace StarTournament.Distribution;

/// <summary>Native labels/buttons/progress; no browser, network, game or installer owns this UI thread.</summary>
[SupportedOSPlatform("windows")]
internal sealed class LauncherWindowsHost
{
    readonly BootstrapStartupWindow owner;
    readonly Dictionary<string, IntPtr> labels = new();
    readonly Dictionary<LauncherAction, IntPtr> buttons = new();
    readonly Dictionary<IntPtr, string> textColors = new();
    readonly Dictionary<(int,bool,bool,uint),IntPtr> fonts = new();
    readonly LauncherPadEdges[] pads = Enumerable.Range(0,4).Select(_=>new LauncherPadEdges()).ToArray();
    readonly string className = "StarTournamentLauncher-" + Guid.NewGuid().ToString("N");
    readonly WindowProc procedure;
    IntPtr window, instance, background, progress, largeIcon, smallIcon;
    LauncherView? rendered;
    LauncherFrame? frame;
    Exception? fault;
    LauncherAction? lastFocus;
    bool loaded, xinput = true;
    uint dpi = 96;
    const uint Style = 0x02CE0000; // caption/system/minimize/resize + clip children; no maximize
    public LauncherWindowsHost(BootstrapStartupWindow owner) { this.owner=owner;procedure=WndProc; }
    static int Px(int value,uint dpi) => (int)Math.Round(value*dpi/96d);
    public void Run(Action shown)
    {
        instance=GetModuleHandleW(null);
        background=CreateSolidBrush(Color(LauncherPresentation.Background));
        var type=new WindowClass { Size=(uint)Marshal.SizeOf<WindowClass>(),Style=3,Procedure=Marshal.GetFunctionPointerForDelegate(procedure),Instance=instance,
            Cursor=LoadCursorW(IntPtr.Zero,new IntPtr(32512)),Background=background,ClassName=className };
        if(RegisterClassExW(ref type)==0){var error=Marshal.GetLastWin32Error();DeleteObject(background);background=IntPtr.Zero;throw new Win32Exception(error);}
        try
        {
            SetThreadDpiAwarenessContext(new IntPtr(-4));
            dpi=GetDpiForSystem();if(dpi==0)dpi=96;
            var size=new Rect{Right=Px(620,dpi),Bottom=Px(350,dpi)};AdjustWindowRectExForDpi(ref size,Style,false,0,dpi);
            SystemParametersInfoW(0x30,0,out var area,0);
            int width=Math.Min(size.Right-size.Left,area.Right-area.Left),height=Math.Min(size.Bottom-size.Top,area.Bottom-area.Top);
            window=CreateWindowExW(0,className,"Star Tournament",Style,area.Left+(area.Right-area.Left-width)/2,area.Top+(area.Bottom-area.Top-height)/2,width,height,IntPtr.Zero,IntPtr.Zero,instance,IntPtr.Zero);
            if(window==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            if(Environment.ProcessPath is { } executable && ExtractIconExW(executable,0,out largeIcon,out smallIcon,1)==1)
            {
                if(largeIcon!=IntPtr.Zero)SendMessageW(window,0x80,new IntPtr(1),largeIcon);
                if(smallIcon!=IntPtr.Zero)SendMessageW(window,0x80,IntPtr.Zero,smallIcon);
            }
            var common=new CommonControls{Size=8,Classes=0x20};InitCommonControlsEx(ref common);
            progress=Child("msctls_progress32","",1,200);
            SetWindowTheme(progress,"","");
            SendMessageW(progress,0x409,IntPtr.Zero,new IntPtr(Color(LauncherPresentation.Accent)));
            SendMessageW(progress,0x2001,IntPtr.Zero,new IntPtr(Color(LauncherPresentation.Button)));
            foreach(var action in Enum.GetValues<LauncherAction>())buttons[action]=Child("BUTTON",LauncherPresentation.ActionText(action),0x10000|0x000b,100+(int)action);
            loaded=true;Render(true);
            int dark=1;DwmSetWindowAttribute(window,20,ref dark,4);
            int caption=(int)Color(LauncherPresentation.Background);DwmSetWindowAttribute(window,35,ref caption,4);
            int foreground=(int)Color(LauncherPresentation.Foreground);DwmSetWindowAttribute(window,36,ref foreground,4);
            ShowWindow(window,5);UpdateWindow(window);RestoreFocus();
            SetTimer(window,1,16,IntPtr.Zero);shown();
            int result;
            while((result=GetMessageW(out var message,IntPtr.Zero,0,0))>0)
            {
                if(Keyboard(message))continue;
                if(IsDialogMessageW(window,ref message))continue;
                TranslateMessage(ref message);DispatchMessageW(ref message);
            }
            if(result<0)throw new Win32Exception(Marshal.GetLastWin32Error());
            if(fault!=null)throw new IOException("Launcher window failed",fault);
        }
        finally
        {
            if(window!=IntPtr.Zero&&IsWindow(window))DestroyWindow(window);
            UnregisterClassW(className,instance);
            foreach(var font in fonts.Values)DeleteObject(font);
            if(largeIcon!=IntPtr.Zero)DestroyIcon(largeIcon);if(smallIcon!=IntPtr.Zero)DestroyIcon(smallIcon);
            if(background!=IntPtr.Zero)DeleteObject(background);
            GC.KeepAlive(procedure);
        }
    }
    IntPtr Child(string type,string text,uint style,int id)
    {
        var control=CreateWindowExW(0,type,text,0x40000000|style,0,0,1,1,window,new IntPtr(id),instance,IntPtr.Zero);
        if(control==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        return control;
    }
    IntPtr Font(int size,bool bold,bool underline=false)
    {
        var key=(size,bold,underline,dpi);
        if(!fonts.TryGetValue(key,out var font))fonts[key]=font=CreateFontW(-Px(size,dpi),0,0,0,bold?600:400,0,underline?1u:0u,0,1,0,0,5,0,"Segoe UI");
        if(font==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
        return font;
    }
    void Place(IntPtr control,LauncherBox r) => MoveWindow(control,Px(r.X,dpi),Px(r.Y,dpi),Px(r.Width,dpi),Px(r.Height,dpi),true);
    void Render(bool force=false)
    {
        if(!loaded)return;
        var view=owner.Surface.View;
        if(!force&&rendered?.Revision==view.Revision)return;
        var previous=rendered;rendered=view;
        dpi=GetDpiForWindow(window);if(dpi==0)dpi=96;
        GetClientRect(window,out var client);
        var oldFrame=frame;
        frame=LauncherPresentation.Frame(view,Math.Max(332,(int)(client.Right*96d/dpi)),Math.Max(300,(int)(client.Bottom*96d/dpi)));
        foreach(var entry in labels.Where(p=>!frame.Labels.Any(l=>l.Key==p.Key)))ShowWindow(entry.Value,0);
        foreach(var label in frame.Labels)
        {
            if(!labels.TryGetValue(label.Key,out var control))labels[label.Key]=control=Child("STATIC","",0x80,300+labels.Count);
            var before=oldFrame?.Labels.FirstOrDefault(l=>l.Key==label.Key);
            textColors[control]=label.Color;
            if(force||before?.Box!=label.Box)Place(control,label.Box);
            if(force||before?.Size!=label.Size||before?.Bold!=label.Bold)SendMessageW(control,0x30,Font(label.Size,label.Bold),new IntPtr(1));
            if(before?.Text!=label.Text)SetWindowTextW(control,label.Text);
            ShowWindow(control,5);
        }
        var selected=GetFocus();
        foreach(var entry in buttons)
        {
            var spec=frame.Buttons.FirstOrDefault(b=>b.Action==entry.Key);
            if(spec==null){EnableWindow(entry.Value,false);ShowWindow(entry.Value,0);continue;}
            SetWindowTextW(entry.Value,spec.Text);Place(entry.Value,spec.Box);EnableWindow(entry.Value,true);ShowWindow(entry.Value,5);
            InvalidateRect(entry.Value,IntPtr.Zero,false);
        }
        if(previous?.Id!=view.Id && !buttons.Where(b=>frame.Buttons.Any(f=>f.Action==b.Key)).Any(b=>b.Value==selected))
        {
            var first=frame.Buttons.FirstOrDefault(b=>b.Action==LauncherAction.Play)??frame.Buttons.FirstOrDefault();
            if(first!=null)SetFocus(buttons[first.Action]);
        }
        Place(progress,frame.Progress);
        if(frame.ShowProgress)
        {
            long style=GetWindowLongPtrW(progress,-16).ToInt64();
            SetWindowLongPtrW(progress,-16,new IntPtr(frame.Marquee?style|8:style&~8L));
            SendMessageW(progress,0x40a,new IntPtr(frame.Marquee?1:0),new IntPtr(30));
            if(!frame.Marquee)SendMessageW(progress,0x402,new IntPtr(frame.Percent),IntPtr.Zero);
            ShowWindow(progress,5);
        }
        else ShowWindow(progress,0);
        SetWindowTextW(window,"Star Tournament — "+frame.Labels.Single(l=>l.Key=="title").Text);
        InvalidateRect(window,IntPtr.Zero,false);
    }
    void Activate(LauncherAction action)
    {
        if(rendered==null)return;
        owner.Choose(rendered.Id,action);
        Render(); // Consume/disable immediately, before another mouse/key event can grant consent.
    }
    bool Keyboard(Message message)
    {
        if(message.Kind!=0x100)return false;
        int key=(int)message.Parameter;
        if(key is 13 or 32)
        {
            var focused=buttons.FirstOrDefault(p=>p.Value==GetFocus());
            if(focused.Value!=IntPtr.Zero)
            { if((message.Data.ToInt64()&(1L<<30))==0)Activate(focused.Key);return true; }
        }
        if(key is 37 or 38 or 39 or 40){MoveFocus(key is 37 or 38?-1:1);return true;}
        if(key==27&&owner.Surface.CloseAction==LauncherAction.Cancel){Activate(LauncherAction.Cancel);return true;}
        return false;
    }
    void RestoreFocus()
    {
        if(frame==null)return;
        var target=frame.Buttons.FirstOrDefault(b=>b.Action==lastFocus)??frame.Buttons.FirstOrDefault(b=>b.Action==LauncherAction.Play)??frame.Buttons.FirstOrDefault();
        if(target!=null)SetFocus(buttons[target.Action]);
    }
    void MoveFocus(int direction)
    {
        if(frame==null||frame.Buttons.Length==0)return;
        int index=Array.FindIndex(frame.Buttons,b=>buttons[b.Action]==GetFocus());
        index=(Math.Max(index,0)+direction+frame.Buttons.Length)%frame.Buttons.Length;
        SetFocus(buttons[frame.Buttons[index].Action]);
    }
    void PollPads()
    {
        if(!xinput)return;
        bool focused=GetForegroundWindow()==window;
        for(uint i=0;i<4;i++)
        {
            try
            {
                bool connected=XInputGetState(i,out var state)==0;
                var edge=pads[i].Read(connected,focused,state.Pad.Buttons,state.Pad.X,state.Pad.Y);
                if(edge.Move!=0)MoveFocus(edge.Move);
                if(edge.Confirm&&frame!=null)
                {var target=frame.Buttons.FirstOrDefault(b=>buttons[b.Action]==GetFocus());if(target!=null)Activate(target.Action);}
                if(edge.Back&&owner.Surface.CloseAction==LauncherAction.Cancel)Activate(LauncherAction.Cancel);
            }
            catch(DllNotFoundException){xinput=false;return;}
            catch(EntryPointNotFoundException){xinput=false;return;}
        }
    }
    IntPtr WndProc(IntPtr handle,uint message,UIntPtr parameter,IntPtr data)
    {
        try
        {
            switch(message)
            {
                case 0x10: // WM_CLOSE never interrupts install/cancellation writes.
                    if(owner.Closing)DestroyWindow(handle);
                    else if(owner.Surface.CloseAction is { } close)Activate(close);
                    return IntPtr.Zero;
                case 0x2: PostQuitMessage(0);return IntPtr.Zero;
                case 0x113:
                    if(owner.Closing){DestroyWindow(handle);return IntPtr.Zero;}
                    Render();PollPads();return IntPtr.Zero;
                case 0x5: Render(true);return IntPtr.Zero;
                case 0x7: RestoreFocus();return IntPtr.Zero;
                case 0x2e0:
                    var rect=Marshal.PtrToStructure<Rect>(data);
                    SetWindowPos(handle,IntPtr.Zero,rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top,0x14);Render(true);return IntPtr.Zero;
                case 0x111:
                    int id=(int)(parameter.ToUInt64()&0xffff);
                    if((parameter.ToUInt64()>>16)==0&&id>=100&&id<100+Enum.GetValues<LauncherAction>().Length&&buttons.TryGetValue((LauncherAction)(id-100),out var button)&&data==button)Activate((LauncherAction)(id-100));
                    return IntPtr.Zero;
                case 0x133: case 0x138: // Accessible native STATIC controls keep the branded palette.
                    SetTextColor(new IntPtr(unchecked((long)parameter.ToUInt64())),Color(textColors.GetValueOrDefault(data,LauncherPresentation.Muted)));
                    SetBkMode(new IntPtr(unchecked((long)parameter.ToUInt64())),1);return background;
                case 0x2b: DrawButton(Marshal.PtrToStructure<DrawItem>(data));return new IntPtr(1);
                case 0x14: GetClientRect(handle,out var bg);FillRect(new IntPtr(unchecked((long)parameter.ToUInt64())),ref bg,background);return new IntPtr(1);
                case 0xf:
                    var dc=BeginPaint(handle,out var paint);GetClientRect(handle,out var bounds);FillRect(dc,ref bounds,background);EndPaint(handle,ref paint);return IntPtr.Zero;
                case 0x24: // Keep controls readable, but never force a window beyond the available desktop.
                    var limits=Marshal.PtrToStructure<MinMax>(data);var minimum=new Rect{Right=Px(380,dpi),Bottom=Px(350,dpi)};
                    AdjustWindowRectExForDpi(ref minimum,Style,false,0,dpi);SystemParametersInfoW(0x30,0,out var area,0);
                    limits.MinTrack=new Point(Math.Min(minimum.Right-minimum.Left,area.Right-area.Left),Math.Min(minimum.Bottom-minimum.Top,area.Bottom-area.Top));Marshal.StructureToPtr(limits,data,false);return IntPtr.Zero;
            }
        }
        catch(Exception error){fault=error;owner.Closing=true;DestroyWindow(handle);return IntPtr.Zero;}
        return DefWindowProcW(handle,message,parameter,data);
    }
    void DrawButton(DrawItem item)
    {
        var action=(LauncherAction)(item.ControlId-100);
        if((item.State&0x10)!=0)lastFocus=action;
        var spec=frame?.Buttons.FirstOrDefault(b=>b.Action==action);
        var rect=item.Bounds;FillRect(item.Dc,ref rect,background);
        bool link=spec?.Link==true,primary=spec?.Primary==true,pressed=(item.State&1)!=0;
        var fill=CreateSolidBrush(Color(link?LauncherPresentation.Background:primary?pressed?"#D2A45B":LauncherPresentation.Accent:pressed?"#2D4A60":LauncherPresentation.Button));
        var pen=CreatePen(0,Px(1,dpi),Color(link?LauncherPresentation.Background:primary?LauncherPresentation.Accent:LauncherPresentation.Border));
        var oldBrush=SelectObject(item.Dc,fill);var oldPen=SelectObject(item.Dc,pen);
        RoundRect(item.Dc,rect.Left,rect.Top,rect.Right,rect.Bottom,Px(8,dpi),Px(8,dpi));
        SelectObject(item.Dc,oldBrush);SelectObject(item.Dc,oldPen);DeleteObject(fill);DeleteObject(pen);
        if((item.State&0x10)!=0)
        {
            var focus=CreatePen(0,Px(2,dpi),Color(primary?LauncherPresentation.Background:LauncherPresentation.Accent));var prior=SelectObject(item.Dc,focus);var brush=SelectObject(item.Dc,GetStockObject(5));
            int inset=Px(3,dpi);RoundRect(item.Dc,rect.Left+inset,rect.Top+inset,rect.Right-inset,rect.Bottom-inset,Px(6,dpi),Px(6,dpi));
            SelectObject(item.Dc,brush);SelectObject(item.Dc,prior);DeleteObject(focus);
        }
        var font=SelectObject(item.Dc,Font(spec?.FontSize??14,!link,link));
        SetBkMode(item.Dc,1);SetTextColor(item.Dc,Color(link?LauncherPresentation.Accent:primary?LauncherPresentation.Background:LauncherPresentation.Foreground));
        rect.Left+=Px(link?0:6,dpi);rect.Right-=Px(6,dpi);
        DrawTextW(item.Dc,spec?.Text??LauncherPresentation.ActionText(action),-1,ref rect,0x800|0x20|0x4|(link?0u:1u));
        SelectObject(item.Dc,font);
    }
    static uint Color(string hex)
    {uint rgb=Convert.ToUInt32(hex.TrimStart('#'),16);return ((rgb&0xff)<<16)|(rgb&0xff00)|((rgb>>16)&0xff);}
}
