using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class QuarryWarmArtTests
    {
        [Test] public void WarmArtIsScopedAndHasValidatedProfileAndBoundedTextures()
        {
            var d=QuarryArenaCatalog.Build();Assert.That(QuarryWarmArt.Applies(d),Is.True);
            foreach(var revision in new[]{"1","2","3","4","5"})
            {
                var old=AuthoredArenaCatalog.Resolve(QuarryArenaCatalog.Id,revision);Assert.That(QuarryWarmArt.Applies(old),Is.False);
                Assert.That(ProvingProfile.CreateQuarryAuthoring(old).Descriptors.Any(x=>x.Path.StartsWith("quarry.warm.")),Is.False);
            }
            var p=ProvingProfile.CreateQuarryAuthoring(d);Assert.That(p.Validate(),Is.Empty);
            Assert.That(p.Descriptors.Count(x=>x.Path.StartsWith("quarry.warm.")),Is.EqualTo(33));
            foreach(string name in new[]{"limestone","gravel"})
            {
                var texture=Resources.Load<Texture2D>("QuarryWarmArt/"+name);Assert.That(texture,Is.Not.Null);
                Assert.That(texture.width,Is.LessThanOrEqualTo(1024));Assert.That(texture.height,Is.LessThanOrEqualTo(1024));
                Assert.That(texture.mipmapCount,Is.GreaterThan(1));Assert.That(texture.isReadable,Is.False);
                Assert.That(texture.wrapMode,Is.EqualTo(TextureWrapMode.Repeat));
            }
            var variants=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/StarTournament/Settings/QuarryShadowBuildVariants.asset");
            Assert.That(variants,Is.Not.Null);Assert.That(variants.shadowCascadeCount,Is.EqualTo(4));
            Assert.That(AssetDatabase.GetLabels(variants),Does.Contain("StarTournament.RuntimePipeline"),"Retain the runtime cascade shader variant for builds");
            var shader=Shader.Find("StarTournament/QuarryWarmSurface");Assert.That(shader,Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader),Is.False);
        }
    }
}
