using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEditor.Rendering;
using UnityEngine.Rendering.Universal;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WellPlayerPipelineTests
    {
        [Test]
        public void PlayerBuildIncludesTheRuntimeGrassCutoutMaterial()
        {
            var material=Resources.Load<Material>("WellMeadowGrassCutout");
            Assert.That(material,Is.Not.Null,"Runtime grass needs an included material: shader_feature _ALPHATEST_ON cannot be inferred from new Material at runtime");
            Assert.That(material.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"),Is.True);
            Assert.That(material.GetFloat("_AlphaClip"),Is.EqualTo(1));
            Assert.That(material.GetFloat("_Cutoff"),Is.EqualTo(.38f));
            Assert.That(material.GetFloat("_Cull"),Is.Zero);
            Assert.That(material.GetFloat("_Smoothness"),Is.EqualTo(.1f));
            Assert.That(material.GetTexture("_BaseMap"),Is.SameAs(Resources.Load<Texture2D>("WellMeadowGrass")));
            Assert.That(material.renderQueue,Is.EqualTo(2450));
        }
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
