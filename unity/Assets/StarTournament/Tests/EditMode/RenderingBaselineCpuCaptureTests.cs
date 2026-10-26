using System;
using System.IO;
using NUnit.Framework;
using StarTournament.ProvingGround;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class RenderingBaselineCpuCaptureTests
    {
        [Test] public void DefaultAndOtherSamplesDoNotStartProfiling()
        {
            Assert.IsNull(RenderingBaselineCpuCapture.Output(null,"frozen4"));
            Assert.IsNull(RenderingBaselineCpuCapture.Begin(null));
            Assert.IsNull(RenderingBaselineCpuCapture.Output("relative","ordinary1"));
            Assert.IsNull(RenderingBaselineCpuCapture.Output("relative","frozen1"));
        }
        [Test] public void Frozen4RequiresAbsoluteOwnedOutput()
        {
            Assert.Throws<ArgumentException>(()=>RenderingBaselineCpuCapture.Output("relative","frozen4"));
            string root=Path.Combine(Path.GetTempPath(),"profile path");
            Assert.AreEqual(Path.Combine(root,"cpu.raw"),RenderingBaselineCpuCapture.Output(root,"frozen4"));
        }
        [Test] public void ExistingRawCannotBeReplaced()
        {
            string file=Path.GetTempFileName();
            try{File.WriteAllText(file,"immutable");Assert.Throws<IOException>(()=>RenderingBaselineCpuCapture.Begin(file));Assert.AreEqual("immutable",File.ReadAllText(file));}
            finally{File.Delete(file);}
        }
    }
}
