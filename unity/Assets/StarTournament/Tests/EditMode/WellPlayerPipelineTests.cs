using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Rendering.Universal;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WellPlayerPipelineTests
    {
        [TestCase(BuildTarget.StandaloneOSX)]
        [TestCase(BuildTarget.StandaloneWindows64)]
        public void BuildIncludesBothExistingLdrAndRuntimeWellHdrGrading(BuildTarget target)
        {
            var assets=new List<UniversalRenderPipelineAsset>();
            Assert.That(target.TryGetRenderPipelineAssets(assets),Is.True);
            Assert.That(assets.Any(a=>a.colorGradingMode==ColorGradingMode.LowDynamicRange),Is.True,"Other arenas retain their existing LDR pipeline");
            Assert.That(assets.Any(a=>a.colorGradingMode==ColorGradingMode.HighDynamicRange),Is.True,
                "Well switches to HDR at runtime: URP must see an HDR asset while collecting build features, otherwise _HDR_GRADING is stripped");
        }
    }
}
