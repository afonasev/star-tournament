using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class GraphicsQualityApplicabilityTests
    {
        [Serializable] public sealed class Slot { public string Name,Pipeline;public int Index,SkinWeights,LegacyAA;public float ShadowDistance,RenderScale;public int Msaa; }
        [Serializable] public sealed class Skin { public string Prefab,Renderer,Mesh;public int Quality,Vertices,MultiWeightVertices,ThreeOrMoreWeightVertices,MaxWeights; }
        [Serializable] public sealed class TextureEntry { public string Source,Name,Asset,Filter;public int MipCount,Aniso;public bool IgnoreMipmapLimit; }
        [Serializable] public sealed class Report { public string Unity;public List<Slot> Slots=new List<Slot>();public List<Skin> Skins=new List<Skin>();public List<TextureEntry> Textures=new List<TextureEntry>();public int PrefabLodGroups; }
        [Test] public void ExistingQualitySlotsHaveAnEffectiveSkeletalConsumerAndOneUrpAsset()
        {
            var report=new Report{Unity=Application.unityVersion};
            var generated=new List<Texture2D>();var seenTextures=new HashSet<int>();
            void CaptureTexture(Texture texture,string source)
            {
                if(!(texture is Texture2D two)||!seenTextures.Add(two.GetInstanceID()))return;
                report.Textures.Add(new TextureEntry{Source=source,Name=two.name,Asset=AssetDatabase.GetAssetPath(two),Filter=two.filterMode.ToString(),MipCount=two.mipmapCount,Aniso=two.anisoLevel,IgnoreMipmapLimit=two.ignoreMipmapLimit});
            }
            int Influences(BoneWeight w)=>(w.weight0>0?1:0)+(w.weight1>0?1:0)+(w.weight2>0?1:0)+(w.weight3>0?1:0);
            int original=QualitySettings.GetQualityLevel(),aa=QualitySettings.antiAliasing,vsync=QualitySettings.vSyncCount;
            var shadows=QualitySettings.shadows;var skinWeights=QualitySettings.skinWeights;var aniso=QualitySettings.anisotropicFiltering;
            int mipLimit=QualitySettings.globalTextureMipmapLimit;float lodBias=QualitySettings.lodBias;bool realtimeProbes=QualitySettings.realtimeReflectionProbes;
            try
            {
                var fallback=GraphicsSettings.defaultRenderPipeline;Assert.That(fallback,Is.Not.Null);
                for(int i=0;i<QualitySettings.names.Length;i++)
                {
                    QualitySettings.SetQualityLevel(i,false);
                    var pipeline=QualitySettings.renderPipeline?QualitySettings.renderPipeline:fallback;
                    Assert.That(pipeline,Is.SameAs(fallback),"All current levels resolve to the same URP asset");
                    var serialized=new SerializedObject(pipeline);
                    report.Slots.Add(new Slot{Name=QualitySettings.names[i],Index=i,Pipeline=AssetDatabase.GetAssetPath(pipeline),SkinWeights=(int)QualitySettings.skinWeights,LegacyAA=QualitySettings.antiAliasing,
                        ShadowDistance=serialized.FindProperty("m_ShadowDistance").floatValue,RenderScale=serialized.FindProperty("m_RenderScale").floatValue,Msaa=serialized.FindProperty("m_MSAA").intValue});
                }
                foreach(string prefab in new[]{"Assets/StarTournament/Trooper/TrooperBody.prefab","Assets/StarTournament/Trooper/TrooperArms.prefab"})
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);Assert.That(asset,Is.Not.Null,prefab);
                    report.PrefabLodGroups+=asset.GetComponentsInChildren<LODGroup>(true).Length;
                    foreach(var skin in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        Assert.That(skin.sharedMesh,Is.Not.Null,skin.name);Assert.That(skin.sharedMesh.isReadable,Is.True,skin.name);
                        var weights=skin.sharedMesh.boneWeights;
                        report.Skins.Add(new Skin{Prefab=prefab,Renderer=skin.name,Mesh=AssetDatabase.GetAssetPath(skin.sharedMesh),Quality=(int)skin.quality,Vertices=weights.Length,
                            MultiWeightVertices=weights.Count(w=>Influences(w)>1),ThreeOrMoreWeightVertices=weights.Count(w=>Influences(w)>2),MaxWeights=weights.Length==0?0:weights.Max(Influences)});
                        foreach(var material in skin.sharedMaterials)if(material)
                            foreach(string property in material.GetTexturePropertyNames())CaptureTexture(material.GetTexture(property),prefab+"/"+skin.name+"/"+material.name+"/"+property);
                    }
                }
                foreach(bool normal in new[]{false,true})
                {
                    var texture=WellLabDetail.Texture("floor",ProvingProfile.CreateWellDefault(),normal);generated.Add(texture);CaptureTexture(texture,"WellLabDetail.Texture(floor,normal="+normal+")");
                }
                Assert.That(report.Textures.Count,Is.GreaterThan(0),"Record actual material/runtime textures rather than inferring from quality slot labels");
                Assert.That(report.Slots.Count,Is.EqualTo(6));
                Assert.That(report.Slots.Select(s=>s.SkinWeights).Distinct().Count(),Is.GreaterThan(1));
                Assert.That(report.Skins.Any(s=>s.Quality==(int)SkinQuality.Auto&&s.MultiWeightVertices>0),Is.True,"Meaningful global skin-weight consumer required; do not declare N/A from missing UI");
                Assert.That(report.Slots.Select(s=>s.ShadowDistance).Distinct().Count(),Is.EqualTo(1));
                Assert.That(report.Slots.Select(s=>s.RenderScale).Distinct().Count(),Is.EqualTo(1));
                Assert.That(report.Slots.Select(s=>s.Msaa).Distinct().Count(),Is.EqualTo(1));
                string path=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QUALITY_PROBE");
                if(!string.IsNullOrEmpty(path)){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));}
            }
            finally
            {
                foreach(var texture in generated)if(texture)UnityEngine.Object.DestroyImmediate(texture);
                QualitySettings.SetQualityLevel(original,false);QualitySettings.antiAliasing=aa;QualitySettings.vSyncCount=vsync;QualitySettings.shadows=shadows;
                QualitySettings.skinWeights=skinWeights;QualitySettings.anisotropicFiltering=aniso;QualitySettings.globalTextureMipmapLimit=mipLimit;QualitySettings.lodBias=lodBias;QualitySettings.realtimeReflectionProbes=realtimeProbes;
            }
            Assert.That(QualitySettings.skinWeights,Is.EqualTo(skinWeights));Assert.That(QualitySettings.anisotropicFiltering,Is.EqualTo(aniso));Assert.That(QualitySettings.globalTextureMipmapLimit,Is.EqualTo(mipLimit));Assert.That(QualitySettings.lodBias,Is.EqualTo(lodBias));Assert.That(QualitySettings.realtimeReflectionProbes,Is.EqualTo(realtimeProbes));
            Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(original));Assert.That(QualitySettings.antiAliasing,Is.EqualTo(aa));Assert.That(QualitySettings.vSyncCount,Is.EqualTo(vsync));Assert.That(QualitySettings.shadows,Is.EqualTo(shadows));
        }
    }
}
