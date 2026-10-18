using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests
{
    // Comparable historical/current stock runtime; no direct art or postprocess override.
    public sealed class WellDaylightPresentationTests
    {
        [UnityTest] public IEnumerator CaptureRuntimeAndVerifyIndependentEffects()
        {
            var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-daylight"));Directory.CreateDirectory(directory);
            var preferenceKeys=new List<string>();
            foreach(VisualEffect effect in Enum.GetValues(typeof(VisualEffect)))
            {preferenceKeys.Add(NativeVisualPreferences.Key(effect)+".Enabled");preferenceKeys.Add(NativeVisualPreferences.Key(effect)+".Strength");}
            preferenceKeys.Add(NativeVisualPreferences.AntialiasingKey);
            foreach(var path in NativeVisualPreferences.ColorAdjustmentPaths)preferenceKeys.Add("StarTournament.Graphics."+path);
            var existed=preferenceKeys.ToDictionary(k=>k,PlayerPrefs.HasKey);
            var floats=preferenceKeys.ToDictionary(k=>k,k=>PlayerPrefs.GetFloat(k));
            var ints=preferenceKeys.ToDictionary(k=>k,k=>PlayerPrefs.GetInt(k));
            var report=new System.Text.StringBuilder();
            try
            {
                foreach(var key in preferenceKeys)PlayerPrefs.DeleteKey(key); // In-memory defaults; restored below, never saved.
                foreach(var variant in new[]{"before","after"})
                {
                    var root=new GameObject("Lighting study "+variant);NativeVisualPresentation visual=null;
                    var rt=new RenderTexture(1280,720,24,RenderTextureFormat.DefaultHDR);VolumeProfile treatment=null;
                    var originalPipeline=QualitySettings.renderPipeline;
                    Camera camera=null;
                    try
                    {
                        var movement=ProvingProfile.CreateDefault();var art=ProvingProfile.CreateWellDefault();
                        if(variant=="before")art=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWellDaylight",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(art,null);
                        var arenaGo=new GameObject("Arena");arenaGo.transform.SetParent(root.transform);var arena=arenaGo.AddComponent<ProvingArena>();
                        arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement,art);yield return null;
                        var cameraGo=new GameObject("Review camera");cameraGo.transform.SetParent(root.transform);camera=cameraGo.AddComponent<Camera>();
                        camera.targetTexture=rt;camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
                        var keyGo=new GameObject("Daylight");keyGo.transform.SetParent(root.transform);var key=keyGo.AddComponent<Light>();WellArenaPresentation.ConfigureDaylight(key,art);
                        visual=root.AddComponent<NativeVisualPresentation>();visual.Bind(movement,arena.transform,key,new[]{camera});yield return null;yield return null;
                        var skin=arena.GetComponentInChildren<WellArenaPresentation>();var lamps=skin.GetComponentsInChildren<Light>();
                        yield return null;yield return null;
                        Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())));
                        Assert.That(lamps.Length,Is.EqualTo(24));Assert.That(skin.GetComponentsInChildren<Collider>(),Is.Empty);
                        Assert.That(arena.Definition.Solids.Count(WellFurniturePlan.IsFurniture),Is.EqualTo(56));
                        report.AppendLine(variant+" colorSpace="+QualitySettings.activeColorSpace+" ambient="+RenderSettings.ambientLight+" key="+key.intensity+" keyRotation="+key.transform.eulerAngles+" lamp="+lamps[0].intensity+" lamps=24 geometry=exact colliders=0 furniture=56");
                        var eyes=new List<Vector3>();var targets=new List<Vector3>();var names=new List<string>();
                        for(int floor=0;floor<3;floor++)
                        {
                            float y=floor*WellArenaCatalog.Storey;
                            eyes.Add(new Vector3(17.5f,y+1.7f,20.5f));targets.Add(new Vector3(19.3f,y+1.4f,23.85f));names.Add("room-"+floor);
                            eyes.Add(new Vector3(6,y+1.7f,15));targets.Add(new Vector3(0,y+1.5f,23.5f));names.Add("lift-"+floor);
                        }
                        eyes.Add(new Vector3(7,1.7f,-7));targets.Add(new Vector3(9.6f,4,9.6f));names.Add("atrium");
                        var output=Path.Combine(directory,variant);Directory.CreateDirectory(output);
                        for(int i=0;i<eyes.Count;i++)
                        {
                            camera.transform.position=eyes[i];camera.transform.LookAt(targets[i]);camera.Render();yield return null;
                            var previous=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                            try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,names[i]+".png"),image.EncodeToPNG());}
                            finally{RenderTexture.active=previous;Object.Destroy(image);}
                        }
                        if(variant=="after")
                        {
                            var volume=visual.GetComponent<Volume>();Assert.That(volume.sharedProfile.TryGet(out Tonemapping tone),Is.True);
                            var ownedPipeline=QualitySettings.renderPipeline;
                            Assert.That(((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).colorGradingMode,Is.EqualTo(ColorGradingMode.HighDynamicRange));
                            Assert.That(ownedPipeline,Is.Not.SameAs(originalPipeline));
                            volume.sharedProfile.TryGet(out ColorAdjustments grade);volume.sharedProfile.TryGet(out Bloom bloom);
                            Assert.That(tone.active,Is.True);Assert.That(tone.mode.value,Is.EqualTo(TonemappingMode.Neutral));
                            Assert.That(grade.postExposure.value,Is.EqualTo(.35f).Within(.00001f));
                            Assert.That(lamps.Count(l=>l.shadows!=LightShadows.None),Is.EqualTo(3));
                            var before=lamps.Select(l=>l.intensity).ToArray();visual.Apply();visual.Apply();
                            Assert.That(QualitySettings.renderPipeline,Is.SameAs(ownedPipeline));
                            Assert.That(lamps.Select(l=>l.intensity),Is.EqualTo(before));
                            PlayerPrefs.SetFloat(NativeVisualPreferences.Key(VisualEffect.Lighting)+".Strength",0);visual.Apply();
                            Assert.That(lamps.All(l=>Mathf.Approximately(l.intensity,art.Get("well.lamp"))),Is.True);
                            PlayerPrefs.SetFloat(NativeVisualPreferences.Key(VisualEffect.Lighting)+".Strength",100);visual.Apply();
                            Assert.That(lamps.All(l=>Mathf.Approximately(l.intensity,art.Get("well.lamp")*art.Get("well.localLightScale"))),Is.True);
                            PlayerPrefs.SetInt(NativeVisualPreferences.Key(VisualEffect.ColorGrading)+".Enabled",0);visual.Apply();
                            Assert.That(tone.active,Is.False);Assert.That(grade.active,Is.False);Assert.That(grade.postExposure.value,Is.Zero);Assert.That(bloom.active,Is.True);
                            PlayerPrefs.SetInt(NativeVisualPreferences.Key(VisualEffect.ColorGrading)+".Enabled",1);
                            PlayerPrefs.SetFloat(NativeVisualPreferences.Key(VisualEffect.ColorGrading)+".Strength",0);visual.Apply();Assert.That(tone.active,Is.False);
                            PlayerPrefs.SetFloat(NativeVisualPreferences.Key(VisualEffect.ColorGrading)+".Strength",100);visual.Apply();Assert.That(tone.active,Is.True);
                            Assert.That(grade.postExposure.value,Is.EqualTo(.7f).Within(.00001f));
                            visual.ResetWorld();Assert.That(visual.GetComponents<Volume>(),Is.Empty);Assert.That(lamps.All(l=>Mathf.Approximately(l.intensity,art.Get("well.lamp"))),Is.True);
                            Assert.That(QualitySettings.renderPipeline,Is.SameAs(originalPipeline));
                        }
                    }
                    finally
                    {
                        if(visual)visual.ResetWorld();if(camera)camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(root);
                        if(treatment){foreach(var component in treatment.components)Object.Destroy(component);Object.Destroy(treatment);}
                    }
                    yield return null;
                }
                File.WriteAllText(Path.Combine(directory,"render-state.txt"),report.ToString());
                Assert.That(new[]{"before","after"}.Sum(v=>Directory.GetFiles(Path.Combine(directory,v),"*.png").Length),Is.EqualTo(14));
            }
            finally
            {
                foreach(var key in preferenceKeys)
                {
                    if(!existed[key])PlayerPrefs.DeleteKey(key);
                    else if(key.EndsWith(".Enabled")||key==NativeVisualPreferences.AntialiasingKey)PlayerPrefs.SetInt(key,ints[key]);
                    else PlayerPrefs.SetFloat(key,floats[key]);
                }
            }
        }
    }
}
