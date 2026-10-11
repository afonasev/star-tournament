using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class VisualPolishTests
    {
        Scene scene;ProvingGround ground;
        readonly Dictionary<string,Tuple<bool,float,int>> preferences=new Dictionary<string,Tuple<bool,float,int>>();
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        T Control<T>(string name) where T:Component=>ground.GetComponentsInChildren<T>(true).Single(c=>c.name==name);
        [UnitySetUp] public IEnumerator Setup()
        {
            foreach(VisualEffect effect in Enum.GetValues(typeof(VisualEffect)))
            {
                string key=NativeVisualPreferences.Key(effect);
                preferences[key+".Strength"]=Tuple.Create(PlayerPrefs.HasKey(key+".Strength"),PlayerPrefs.GetFloat(key+".Strength"),0);
                preferences[key+".Enabled"]=Tuple.Create(PlayerPrefs.HasKey(key+".Enabled"),0f,PlayerPrefs.GetInt(key+".Enabled"));
                NativeVisualPreferences.SetEnabled(effect,false);
            }
            foreach(string path in NativeVisualPreferences.ColorAdjustmentPaths)
            {string key="StarTournament.Graphics."+path;preferences[key]=Tuple.Create(PlayerPrefs.HasKey(key),PlayerPrefs.GetFloat(key),0);PlayerPrefs.DeleteKey(key);}
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            foreach(var p in preferences){if(!p.Value.Item1)PlayerPrefs.DeleteKey(p.Key);else if(!p.Key.EndsWith(".Enabled"))PlayerPrefs.SetFloat(p.Key,p.Value.Item2);else PlayerPrefs.SetInt(p.Key,p.Value.Item3);}preferences.Clear();PlayerPrefs.Save();
        }
        [OneTimeTearDown] public void RestoreNewKeysFromInterruptedOwnedQa()
        {
            // Explicit task-only cleanup, only when requested by an owned QA run.
            // Normal future test runs preserve user preferences in Cleanup above.
            if(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_RESET_NEW_VISUAL_KEYS")!="1")return;
            foreach(VisualEffect effect in Enum.GetValues(typeof(VisualEffect)))
            {PlayerPrefs.DeleteKey(NativeVisualPreferences.Key(effect)+".Strength");PlayerPrefs.DeleteKey(NativeVisualPreferences.Key(effect)+".Enabled");}PlayerPrefs.Save();
        }
        void Effects(bool enabled)
        {foreach(VisualEffect e in Enum.GetValues(typeof(VisualEffect)))NativeVisualPreferences.SetEnabled(e,enabled);ground.GetComponentInChildren<NativeVisualPresentation>().Apply();}
        IEnumerator Begin(string map)
        {ground.SelectAuthoredMap(map);NativeSetupFixture.UnboundHumans(ground);B("Диагностика четырёх камер · без управления").onClick.Invoke();yield return NativeLoadingTestScene.Wait(ground);yield return null;}
        [UnityTest] public IEnumerator GraphicsControlsPersistAndScrollToLastEffect()
        {
            B("main-action-4").onClick.Invoke();B("settings-section-0").onClick.Invoke();
            foreach(VisualEffect effect in Enum.GetValues(typeof(VisualEffect)))
            {
                var toggle=Control<Toggle>("settings-visual-"+effect+"-enabled");var slider=Control<Slider>("settings-visual-"+effect+"-strength");
                slider.value=75;toggle.isOn=true;Assert.That(NativeVisualPreferences.Strength(ground.Profile,effect),Is.EqualTo(75));
                toggle.isOn=false;Assert.That(slider.value,Is.EqualTo(75));Assert.That(NativeVisualPreferences.Enabled(effect),Is.False);
            }
            Control<Slider>("settings-color-0").value=.3f;Control<Slider>("settings-color-1").value=115;
            var last=Control<Slider>("settings-color-1");
            Assert.That(Control<Slider>("settings-visual-ColorGrading-strength").navigation.selectOnDown,Is.EqualTo(Control<Slider>("settings-color-0")));
            Assert.That(Control<Slider>("settings-color-0").navigation.selectOnDown,Is.EqualTo(last));Assert.That(last.navigation.selectOnDown,Is.EqualTo(B("settings-display-mode")));
            last.GetComponent<MenuScrollFocus>().Reveal();Canvas.ForceUpdateCanvases();
            var scroll=Control<ScrollRect>("settings-image");Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(.2f));
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(last.gameObject);yield return NativeLoadingScreenTests.Capture("graphics-scrolled",ground.transform.Find("native-ui").gameObject,()=>last.GetComponent<MenuScrollFocus>().Reveal());
            scroll.verticalNormalizedPosition=1;yield return NativeLoadingScreenTests.Capture("graphics-top",ground.transform.Find("native-ui").gameObject);
            B("settings-back").onClick.Invoke();B("main-action-4").onClick.Invoke();Assert.That(last.value,Is.EqualTo(115));Assert.That(Control<Slider>("settings-color-0").value,Is.EqualTo(.3f).Within(.00001f));
        }
        [UnityTest] public IEnumerator SwitchingEffectsRestoresBaselineAndKeepsGeometry()
        {
            B("main-action-0").onClick.Invoke();yield return Begin(CombatBowlCatalog.Id);
            var visual=ground.GetComponentInChildren<NativeVisualPresentation>();var arena=ground.GetComponentInChildren<ProvingArena>();
            int geometry=arena.GetComponentsInChildren<MeshFilter>().Length,colliders=arena.GetComponentsInChildren<Collider>().Length;
            var baseline=RenderSettings.ambientLight;var registered=arena.GetComponentsInChildren<NativeVisualMaterials>().SelectMany(r=>r.Roles.Keys).Distinct().ToArray();
            var values=registered.Select(m=>new Vector2(m.GetFloat("_Metallic"),m.GetFloat("_Smoothness"))).ToArray();
            Assert.That(visual.SurfaceCount,Is.GreaterThan(0));
            for(int i=0;i<3;i++){Effects(true);yield return null;Effects(false);}
            Assert.That(RenderSettings.ambientLight,Is.EqualTo(baseline));
            for(int i=0;i<registered.Length;i++){Assert.That(registered[i].GetFloat("_Metallic"),Is.EqualTo(values[i].x));Assert.That(registered[i].GetFloat("_Smoothness"),Is.EqualTo(values[i].y));}
            Assert.That(arena.GetComponentsInChildren<MeshFilter>().Length,Is.EqualTo(geometry));Assert.That(arena.GetComponentsInChildren<Collider>().Length,Is.EqualTo(colliders));
            Assert.That(visual.GetComponentsInChildren<ReflectionProbe>(true).Length,Is.EqualTo(1));Assert.That(visual.GetComponents<Volume>().Length,Is.EqualTo(1));
            foreach(var camera in ground.GetComponentsInChildren<Camera>())Assert.That(camera.GetUniversalAdditionalCameraData().renderPostProcessing,Is.False);
        }
        [UnityTest] public IEnumerator GradingAndBloomStayIndependentAndZeroDisablesGrading()
        {
            B("main-action-0").onClick.Invoke();yield return Begin(CombatBowlCatalog.Id);
            var visual=ground.GetComponentInChildren<NativeVisualPresentation>();
            NativeVisualPreferences.SetStrength(ground.Profile,VisualEffect.ColorGrading,100);NativeVisualPreferences.SetEnabled(VisualEffect.ColorGrading,true);
            NativeVisualPreferences.SetAdjustment(ground.Profile,NativeVisualPreferences.ExposurePath,.4f);NativeVisualPreferences.SetAdjustment(ground.Profile,NativeVisualPreferences.SaturationPath,120);visual.Apply();
            var volume=visual.GetComponent<Volume>();Assert.That(volume.sharedProfile.TryGet(out ColorCurves curves),Is.True);Assert.That(volume.sharedProfile.TryGet(out ColorAdjustments color),Is.True);volume.sharedProfile.TryGet(out Bloom bloom);
            Assert.That(curves.active,Is.True);Assert.That(color.postExposure.value,Is.EqualTo(.4f).Within(.00001f));Assert.That(color.saturation.value,Is.EqualTo(20));Assert.That(bloom.active,Is.False);
            foreach(var camera in ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled))Assert.That(camera.GetUniversalAdditionalCameraData().renderPostProcessing,Is.True);
            NativeVisualPreferences.SetStrength(ground.Profile,VisualEffect.ColorGrading,50);visual.Apply();Assert.That(color.postExposure.value,Is.EqualTo(.2f).Within(.00001f));Assert.That(color.saturation.value,Is.EqualTo(10));
            var authored=Resources.Load<VolumeProfile>("VisualPolishVolume");authored.TryGet(out ColorCurves sourceCurves);authored.TryGet(out ColorAdjustments sourceColor);
            Assert.That(sourceCurves.active,Is.False);Assert.That(sourceColor.active,Is.False);Assert.That(sourceCurves.master.value.Evaluate(.5f),Is.EqualTo(.5f).Within(.00001f));
            NativeVisualPreferences.SetStrength(ground.Profile,VisualEffect.ColorGrading,0);NativeVisualPreferences.SetEnabled(VisualEffect.Bloom,true);visual.Apply();
            Assert.That(curves.active,Is.False);Assert.That(color.active,Is.False);Assert.That(color.postExposure.value,Is.Zero);Assert.That(bloom.active,Is.True);
            NativeVisualPreferences.SetEnabled(VisualEffect.Bloom,false);visual.Apply();Assert.That(volume.enabled,Is.False);
            foreach(var camera in ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled))Assert.That(camera.GetUniversalAdditionalCameraData().renderPostProcessing,Is.False);
        }
        [UnityTest] public IEnumerator RenderNeutralGradingOnAllArenas()
        {
            B("main-action-0").onClick.Invoke();
            foreach(var map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id})
            {
                yield return Begin(map);
                foreach(VisualEffect e in Enum.GetValues(typeof(VisualEffect))){NativeVisualPreferences.SetStrength(ground.Profile,e,50);NativeVisualPreferences.SetEnabled(e,e!=VisualEffect.ColorGrading);}
                var visual=ground.GetComponentInChildren<NativeVisualPresentation>();visual.Apply();yield return CaptureWorld(map+"-neutral-before");
                NativeVisualPreferences.SetEnabled(VisualEffect.ColorGrading,true);visual.Apply();yield return CaptureWorld(map+"-neutral-after");
                Assert.That(ground.GetComponentsInChildren<Camera>().Count(c=>c.enabled),Is.EqualTo(4));
                NativeVisualPreferences.SetEnabled(VisualEffect.ColorGrading,false);visual.Apply();yield return CaptureWorld(map+"-neutral-disabled");
                ground.SendMessage("Menu");B("main-action-0").onClick.Invoke();
            }
        }
        [UnityTest] public IEnumerator RenderAllArenasBeforeAndAfterWithFourViewports()
        {
            B("main-action-0").onClick.Invoke();
            foreach(var map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id})
            {
                yield return Begin(map);Effects(false);yield return CaptureWorld(map+"-before");
                foreach(VisualEffect effect in Enum.GetValues(typeof(VisualEffect)))
                {NativeVisualPreferences.SetEnabled(effect,true);ground.GetComponentInChildren<NativeVisualPresentation>().Apply();yield return CaptureWorld(map+"-"+effect);NativeVisualPreferences.SetEnabled(effect,false);}
                Effects(true);yield return CaptureWorld(map+"-after");
                Assert.That(ground.GetComponentsInChildren<Camera>().Count(c=>c.enabled),Is.EqualTo(4));
                ground.SendMessage("Menu");B("main-action-0").onClick.Invoke();
            }
        }
        IEnumerator CaptureWorld(string name)
        {
            string dir=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LOADING_SCREENSHOTS");if(string.IsNullOrEmpty(dir))yield break;
            yield return null;yield return null;Directory.CreateDirectory(dir);
            var texture=new RenderTexture(800,450,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                foreach(var camera in ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled).OrderBy(c=>c.depth))
                {var old=camera.targetTexture;var oldRect=camera.rect;try{camera.targetTexture=texture;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,800,450),Mathf.RoundToInt(oldRect.x*1600),Mathf.RoundToInt(oldRect.y*900));}finally{camera.targetTexture=old;camera.rect=oldRect;}}
                pixels.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;texture.Release();UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(pixels);}
        }
    }
}
