#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    // Only the opt-in diagnostic calls this. It changes this Player's own windows,
    // never another process, desktop preferences, input devices or system volume.
    public static class RenderingBaselineMacBackground
    {
#if UNITY_STANDALONE_OSX
        const string ObjC="/usr/lib/libobjc.A.dylib";
        [DllImport(ObjC)] static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern IntPtr Get(IntPtr receiver,IntPtr selector);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern UIntPtr Count(IntPtr receiver,IntPtr selector);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern IntPtr At(IntPtr receiver,IntPtr selector,UIntPtr index);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] [return:MarshalAs(UnmanagedType.I1)] static extern bool Policy(IntPtr receiver,IntPtr selector,long value);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern void Boolean(IntPtr receiver,IntPtr selector,[MarshalAs(UnmanagedType.I1)] bool value);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern void Call(IntPtr receiver,IntPtr selector);
#endif
        public static int Apply()
        {
            if(Application.isEditor||Application.platform!=RuntimePlatform.OSXPlayer||Application.isBatchMode)throw new NotSupportedException("Own-window background requires a windowed macOS QA Player");
#if UNITY_STANDALONE_OSX
            var app=Get(objc_getClass("NSApplication"),sel_registerName("sharedApplication"));
            if(app==IntPtr.Zero||!Policy(app,sel_registerName("setActivationPolicy:"),1))throw new InvalidOperationException("Cannot set accessory policy on own QA app");
            var windows=Get(app,sel_registerName("windows"));ulong count=Count(windows,sel_registerName("count")).ToUInt64();
            if(count==0)throw new InvalidOperationException("Own QA window is missing");
            for(ulong n=0;n<count;n++)Boolean(At(windows,sel_registerName("objectAtIndex:"),new UIntPtr(n)),sel_registerName("setIgnoresMouseEvents:"),true);
            Call(app,sel_registerName("deactivate"));return checked((int)count);
#else
            throw new NotSupportedException("macOS Player required");
#endif
        }
    }
}
#endif
