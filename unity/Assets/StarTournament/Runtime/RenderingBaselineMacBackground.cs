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
        [Serializable] public sealed class PolicyEvidence{public long before,after;public string switchReturn;}
        [Serializable] sealed class WindowEvidence{public PolicyEvidence policy;public bool[] ignoresMouseEvents;public string deactivate="void";public bool activeBefore,activeAfter;}
        public static string LastEvidence {get;private set;}
        public static PolicyEvidence EnsureAccessory(Func<long> read,Func<bool> change)
        {
            var evidence=new PolicyEvidence{before=read(),switchReturn="not-called-already-accessory"};
            if(evidence.before!=1)evidence.switchReturn=change()?"true":"false";
            evidence.after=read();Debug.Log("RENDERING_BASELINE_NATIVE_POLICY "+JsonUtility.ToJson(evidence));
            if(evidence.after!=1)throw new InvalidOperationException("Own QA app actual activationPolicy is not accessory");
            return evidence;
        }
#if UNITY_STANDALONE_OSX
        const string ObjC="/usr/lib/libobjc.A.dylib";
        [DllImport(ObjC)] static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern IntPtr Get(IntPtr receiver,IntPtr selector);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern long Integer(IntPtr receiver,IntPtr selector);
        [DllImport(ObjC,EntryPoint="objc_msgSend")] [return:MarshalAs(UnmanagedType.I1)] static extern bool ReadBoolean(IntPtr receiver,IntPtr selector);
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
            if(app==IntPtr.Zero)throw new InvalidOperationException("Own QA NSApplication is missing");
            var evidence=new WindowEvidence{activeBefore=ReadBoolean(app,sel_registerName("isActive")),policy=EnsureAccessory(()=>Integer(app,sel_registerName("activationPolicy")),()=>Policy(app,sel_registerName("setActivationPolicy:"),1))};
            var windows=Get(app,sel_registerName("windows"));ulong count=Count(windows,sel_registerName("count")).ToUInt64();
            if(count==0)throw new InvalidOperationException("Own QA window is missing");
            evidence.ignoresMouseEvents=new bool[checked((int)count)];
            for(ulong n=0;n<count;n++){var window=At(windows,sel_registerName("objectAtIndex:"),new UIntPtr(n));Boolean(window,sel_registerName("setIgnoresMouseEvents:"),true);evidence.ignoresMouseEvents[n]=ReadBoolean(window,sel_registerName("ignoresMouseEvents"));if(!evidence.ignoresMouseEvents[n])throw new InvalidOperationException("Own QA window is not click-through");}
            Call(app,sel_registerName("deactivate"));evidence.activeAfter=ReadBoolean(app,sel_registerName("isActive"));LastEvidence=JsonUtility.ToJson(evidence);Debug.Log("RENDERING_BASELINE_NATIVE_WINDOW "+LastEvidence);return checked((int)count);
#else
            throw new NotSupportedException("macOS Player required");
#endif
        }
    }
}
#endif
