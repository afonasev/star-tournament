using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class RenderingBaselineCpuCapturePlayTests
    {
        [UnityTest] public IEnumerator OptInCaptureClosesFileAndRestoresProfiler()
        {
            string history=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");
            Assert.IsNotEmpty(history,"Owned evidence path required");
            string path=RenderingBaselineCpuCapture.Output(Path.Combine(Path.GetDirectoryName(history),"cpu-capture-fixture"),"frozen4");
            int memory=Profiler.maxUsedMemory;
            using(var capture=RenderingBaselineCpuCapture.Begin(path))
            {
                Assert.IsTrue(Profiler.enabled);Assert.IsTrue(Profiler.enableBinaryLog);
                for(int n=0;n<4;n++)yield return null;
            }
            Assert.IsFalse(Profiler.enabled);Assert.IsFalse(Profiler.enableBinaryLog);
            Assert.IsEmpty(Profiler.logFile);Assert.AreEqual(memory,Profiler.maxUsedMemory);
            Assert.IsTrue(File.Exists(path));Assert.Greater(new FileInfo(path).Length,0);
        }
    }
}
