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
        [Serializable] public sealed class Skin { public string Prefab,Renderer,Mesh;public int Quality,Vertices,MultiWeightVertices; }
        [Serializable] public sealed class Report { public string Unity;public List<Slot> Slots=new List<Slot>();public List<Skin> Skins=new List<Skin>(); }
        [Test] public void ExistingQualitySlotsHaveAnEffectiveSkeletalConsumerAndOneUrpAsset()
        {
            var report=new Report{Unity=Application.unityVersion};
            int original=QualitySettings.GetQualityLevel(),aa=QualitySettings.antiAliasing,vsync=QualitySettings.vSyncCount;
            var shadows=QualitySettings.shadows;
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
                    foreach(var skin in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        Assert.That(skin.sharedMesh,Is.Not.Null,skin.name);Assert.That(skin.sharedMesh.isReadable,Is.True,skin.name);
                        var weights=skin.sharedMesh.boneWeights;
                        report.Skins.Add(new Skin{Prefab=prefab,Renderer=skin.name,Mesh=AssetDatabase.GetAssetPath(skin.sharedMesh),Quality=(int)skin.quality,Vertices=weights.Length,
                            MultiWeightVertices=weights.Count(w=>(w.weight0>0?1:0)+(w.weight1>0?1:0)+(w.weight2>0?1:0)+(w.weight3>0?1:0)>1)});
                    }
                }
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
                QualitySettings.SetQualityLevel(original,false);QualitySettings.antiAliasing=aa;QualitySettings.vSyncCount=vsync;QualitySettings.shadows=shadows;
            }
            Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(original));Assert.That(QualitySettings.antiAliasing,Is.EqualTo(aa));Assert.That(QualitySettings.vSyncCount,Is.EqualTo(vsync));Assert.That(QualitySettings.shadows,Is.EqualTo(shadows));
        }
    }
}
