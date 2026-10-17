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
    // Review-only experiment: never invoked by game startup; runtime source/assets stay unchanged.
    public sealed class WellLightingStudyTests
    {
        [UnityTest] public IEnumerator CaptureComparableLightingDirections()
        {
            var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-light-study"));Directory.CreateDirectory(directory);
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
                foreach(var variant in new[]{"0-current","1-laboratory","2-daylight","3-practicals"})
                {
                    var root=new GameObject("Lighting study "+variant);NativeVisualPresentation visual=null;
                    var rt=new RenderTexture(1280,720,24,RenderTextureFormat.DefaultHDR);VolumeProfile treatment=null;
                    Camera camera=null;
                    try
                    {
                        var movement=ProvingProfile.CreateDefault();var art=ProvingProfile.CreateWellDefault();
                        var arenaGo=new GameObject("Arena");arenaGo.transform.SetParent(root.transform);var arena=arenaGo.AddComponent<ProvingArena>();
                        arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement);yield return null;
                        var cameraGo=new GameObject("Review camera");cameraGo.transform.SetParent(root.transform);camera=cameraGo.AddComponent<Camera>();
                        camera.targetTexture=rt;camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
                        var keyGo=new GameObject("Daylight");keyGo.transform.SetParent(root.transform);var key=keyGo.AddComponent<Light>();WellArenaPresentation.ConfigureDaylight(key,art);
                        visual=root.AddComponent<NativeVisualPresentation>();visual.Bind(movement,arena.transform,key,new[]{camera});yield return null;yield return null;
                        var skin=arena.GetComponentInChildren<WellArenaPresentation>();var lamps=skin.GetComponentsInChildren<Light>();
                        if(variant!="0-current")
                        {
                            bool day=variant=="2-daylight",practical=variant=="3-practicals";
                            RenderSettings.ambientLight=(day?new Color(.76f,.85f,1):practical?new Color(.75f,.81f,.9f):new Color(.94f,.97f,1))*(day?.32f:practical?.30f:.40f);
                            key.intensity=day?2f:practical?.6f:1.1f;key.shadowStrength=day?.85f:.7f;
                            key.transform.rotation=Quaternion.Euler(day?48:50,day?-55:-30,0);
                            key.color=day?new Color(1,.94f,.82f):Color.white;
                            foreach(var lamp in lamps)
                            {
                                lamp.intensity=day?65:practical?110:80;lamp.range=18;lamp.spotAngle=practical?115:140;lamp.innerSpotAngle=practical?65:95;
                                lamp.shadows=LightShadows.Soft;lamp.shadowStrength=.65f;lamp.shadowBias=.025f;lamp.shadowNormalBias=.12f;
                                int floor=Mathf.RoundToInt((lamp.transform.position.y-WellArenaCatalog.ClearHeight+.35f)/WellArenaCatalog.Storey);
                                lamp.color=practical?(floor==0?new Color(1,.88f,.65f):floor==1?new Color(.67f,.85f,1):new Color(1,.74f,.43f)):new Color(1,.97f,.9f);
                            }
                            var volume=root.AddComponent<Volume>();volume.isGlobal=true;volume.priority=200;treatment=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=treatment;
                            treatment.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
                            var grade=treatment.Add<ColorAdjustments>();grade.postExposure.Override(day?.35f:practical?.45f:.35f);
                            var bloom=treatment.Add<Bloom>();bloom.intensity.Override(practical?.35f:.12f);bloom.threshold.Override(1.15f);bloom.scatter.Override(.35f);
                            // Refresh the existing probe after the experiment's light changes.
                            if(visual.EnvironmentProbe)visual.EnvironmentProbe.RenderProbe();
                        }
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
                    }
                    finally
                    {
                        if(visual)visual.ResetWorld();if(camera)camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(root);
                        if(treatment){foreach(var component in treatment.components)Object.Destroy(component);Object.Destroy(treatment);}
                    }
                    yield return null;
                }
                File.WriteAllText(Path.Combine(directory,"render-state.txt"),report.ToString());
                Assert.That(new[]{"0-current","1-laboratory","2-daylight","3-practicals"}.Sum(v=>Directory.GetFiles(Path.Combine(directory,v),"*.png").Length),Is.EqualTo(28));
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
