#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine.Profiling;
namespace StarTournament.ProvingGround
{
    // Opt-in diagnostic only. Never records arena construction or serialization.
    public sealed class RenderingBaselineCpuCapture : IDisposable
    {
        bool active;
        readonly int oldMemory;
        public static string Output(string directory,string sample)
        {
            if(string.IsNullOrEmpty(directory)||sample!="frozen4")return null;
            if(!Path.IsPathFullyQualified(directory))throw new ArgumentException("Absolute owned profiler directory required");
            return Path.Combine(directory,"cpu.raw");
        }
        public static RenderingBaselineCpuCapture Begin(string path)
        {
            if(path==null)return null;
            if(File.Exists(path))throw new IOException("Refuse overwrite of profiler evidence");
            if(Profiler.enabled||Profiler.enableBinaryLog||!string.IsNullOrEmpty(Profiler.logFile))
                throw new InvalidOperationException("Refuse replacing an active profiler");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return new RenderingBaselineCpuCapture(path);
        }
        RenderingBaselineCpuCapture(string path)
        {
            oldMemory=Profiler.maxUsedMemory;
            Profiler.maxUsedMemory=256*1024*1024;
            Profiler.logFile=path;Profiler.enableBinaryLog=true;Profiler.enabled=true;active=true;
        }
        public void Dispose()
        {
            if(!active)return;
            active=false;Profiler.enabled=false;Profiler.logFile="";Profiler.maxUsedMemory=oldMemory;
        }
    }
}
#endif
