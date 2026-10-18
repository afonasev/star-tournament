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
        bool hadAntialiasing;int priorAntialiasing;
        readonly Dictionary<string,Tuple<bool,float,int>> preferences=new Dictionary<string,Tuple<bool,float,int>>();
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        T Control<T>(string name) where T:Component=>ground.GetComponentsInChildren<T>(true).Single(c=>c.name==name);
        [UnitySetUp] public IEnumerator Setup()
        {
            hadAntialiasing=PlayerPrefs.HasKey(NativeVisualPreferences.AntialiasingKey);priorAntialiasing=PlayerPrefs.GetInt(NativeVisualPreferences.AntialiasingKey);
            NativeVisualPreferences.SetAntialiasingLevel(NativeVisualPreferences.AntialiasingOff);
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
            Time.timeScale=1;
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            if(hadAntialiasing)PlayerPrefs.SetInt(NativeVisualPreferences.AntialiasingKey,priorAntialiasing);else PlayerPrefs.DeleteKey(NativeVisualPreferences.AntialiasingKey);PlayerPrefs.Save();
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
            B("main-action-4").onClick.Invoke();B("settings-section-4").onClick.Invoke();
            var antialiasing=B("settings-antialiasing");Assert.That(antialiasing.GetComponentInChildren<Text>().text,Is.EqualTo(NativeVisualPreferences.AntialiasingNames[NativeVisualPreferences.AntialiasingOff]));
            Assert.That(Control<Toggle>("settings-vsync").navigation.selectOnDown,Is.EqualTo(B("settings-frame-cap")));
            Assert.That(antialiasing.navigation.selectOnDown,Is.EqualTo(Control<Toggle>("settings-shadows")));
            Assert.That(Control<Toggle>("settings-shadows").navigation.selectOnDown,Is.EqualTo(Control<Toggle>("settings-visual-Lighting-enabled")));
            for(int level=NativeVisualPreferences.AntialiasingLow;level<=NativeVisualPreferences.AntialiasingHigh;level++)
            {antialiasing.onClick.Invoke();Assert.That(NativeVisualPreferences.AntialiasingLevel,Is.EqualTo(level));Assert.That(antialiasing.GetComponentInChildren<Text>().text,Is.EqualTo(NativeVisualPreferences.AntialiasingNames[level]));}
            var graphicsScroll=Control<ScrollRect>("settings-graphics");graphicsScroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();antialiasing.GetComponent<MenuScrollFocus>().Reveal();Canvas.ForceUpdateCanvases();Assert.That(graphicsScroll.verticalNormalizedPosition,Is.GreaterThan(.5f));
            yield return NativeLoadingScreenTests.Capture("antialiasing-setting",ground.transform.Find("native-ui").gameObject,()=>antialiasing.GetComponent<MenuScrollFocus>().Reveal());
            antialiasing.onClick.Invoke();Assert.That(NativeVisualPreferences.AntialiasingLevel,Is.EqualTo(NativeVisualPreferences.AntialiasingOff));
            foreach(VisualEffect effect in Enum.GetValues(typeof(VisualEffect)))
            {
                var toggle=Control<Toggle>("settings-visual-"+effect+"-enabled");var slider=Control<Slider>("settings-visual-"+effect+"-strength");
                Assert.That(toggle.gameObject.activeInHierarchy,Is.True);Assert.That(slider.gameObject.activeInHierarchy,Is.False,"Fine art coefficients are outside ordinary settings");
                slider.value=75;toggle.isOn=true;Assert.That(NativeVisualPreferences.Strength(ground.Profile,effect),Is.EqualTo(75));
                toggle.isOn=false;Assert.That(slider.value,Is.EqualTo(75));Assert.That(NativeVisualPreferences.Enabled(effect),Is.False);
            }
            Control<Slider>("settings-color-0").value=.3f;Control<Slider>("settings-color-1").value=115;
            var last=Control<Toggle>("settings-visual-ColorGrading-enabled");
            Assert.That(Control<Slider>("settings-color-0").gameObject.activeInHierarchy,Is.False);
            Assert.That(Control<Slider>("settings-color-1").gameObject.activeInHierarchy,Is.False);Assert.That(last.navigation.selectOnDown,Is.EqualTo(B("settings-reset-all")));
            last.GetComponent<MenuScrollFocus>().Reveal();Canvas.ForceUpdateCanvases();
            var scroll=Control<ScrollRect>("settings-graphics");var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,last.transform);
            Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin-.1f));Assert.That(bounds.max.y,Is.LessThanOrEqualTo(scroll.viewport.rect.yMax+.1f));
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(last.gameObject);yield return NativeLoadingScreenTests.Capture("graphics-scrolled",ground.transform.Find("native-ui").gameObject,()=>last.GetComponent<MenuScrollFocus>().Reveal());
            scroll.verticalNormalizedPosition=1;yield return NativeLoadingScreenTests.Capture("graphics-top",ground.transform.Find("native-ui").gameObject);
            B("settings-back").onClick.Invoke();B("main-action-4").onClick.Invoke();Assert.That(B("settings-antialiasing").GetComponentInChildren<Text>().text,Is.EqualTo(NativeVisualPreferences.AntialiasingNames[NativeVisualPreferences.AntialiasingOff]));Assert.That(Control<Slider>("settings-color-1").value,Is.EqualTo(115));Assert.That(Control<Slider>("settings-color-0").value,Is.EqualTo(.3f).Within(.00001f));
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
        [UnityTest] public IEnumerator WellDaylightDoesNotLeakAcrossMapRebuilds()
        {
            var originalPipeline=QualitySettings.renderPipeline;
            B("main-action-0").onClick.Invoke();yield return Begin(WellArenaCatalog.Id);
            var key=ground.GetComponentsInChildren<Light>().Single(l=>l.type==LightType.Directional);
            Assert.That(key.color,Is.EqualTo(new Color(1,.94f,.82f)));
            foreach(VisualEffect effect in Enum.GetValues(typeof(VisualEffect)))NativeVisualPreferences.SetStrength(ground.Profile,effect,50);
            Effects(true);var visual=ground.GetComponentInChildren<NativeVisualPresentation>();
            var lamps=ground.GetComponentInChildren<WellArenaPresentation>().GetComponentsInChildren<Light>();
            Assert.That(lamps.Length,Is.EqualTo(24));
            bool originalShadows=QualitySettings.shadows!=UnityEngine.ShadowQuality.Disable;
            if(!originalShadows)ground.SendMessage("ToggleShadows");
            Assert.That(lamps.Count(l=>l.shadows!=LightShadows.None),Is.EqualTo(3));
            ground.SendMessage("ToggleShadows");Assert.That(lamps.All(l=>l.shadows==LightShadows.None),Is.True);
            ground.SendMessage("ToggleShadows");Assert.That(lamps.Count(l=>l.shadows!=LightShadows.None),Is.EqualTo(3));
            if(!originalShadows)ground.SendMessage("ToggleShadows");
            Assert.That(visual.GetComponent<Volume>().sharedProfile.TryGet(out Tonemapping tone),Is.True);
            Assert.That(tone.active,Is.True);
            ground.SendMessage("Menu");B("main-action-0").onClick.Invoke();yield return Begin(CombatBowlCatalog.Id);
            key=ground.GetComponentsInChildren<Light>().Single(l=>l.type==LightType.Directional);
            Assert.That(key.color,Is.EqualTo(Color.white));Assert.That(Quaternion.Angle(key.transform.rotation,Quaternion.Euler(50,-30,0)),Is.LessThan(.001f));
            Effects(true);visual=ground.GetComponentInChildren<NativeVisualPresentation>();
            Assert.That(visual.GetComponent<Volume>().sharedProfile.TryGet(out Tonemapping otherTone),Is.False);
            Assert.That(QualitySettings.renderPipeline,Is.SameAs(originalPipeline));
            visual.GetComponent<Volume>().sharedProfile.TryGet(out ColorAdjustments grade);Assert.That(grade.postExposure.value,Is.Zero);
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
        [UnityTest] public IEnumerator AntialiasingIsIndependentAndComparesIdenticalViewsOnAllArenas()
        {
            B("main-action-0").onClick.Invoke();
            foreach(var map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id})
            {
                yield return Begin(map);Effects(false);var visual=ground.GetComponentInChildren<NativeVisualPresentation>();
                Assert.That(ground.transform.Find("native-ui").GetComponent<Canvas>().renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay),"HUD stays outside the camera post-process pass");
                Time.timeScale=0;NativeVisualPreferences.SetAntialiasingLevel(NativeVisualPreferences.AntialiasingOff);visual.Apply();yield return CaptureWorld(map+"-antialias-none");
                NativeVisualPreferences.SetAntialiasingLevel(NativeVisualPreferences.AntialiasingMedium);visual.Apply();
                var cameras=ground.GetComponentsInChildren<Camera>().Where(camera=>camera.enabled).ToArray();Assert.That(cameras.Length,Is.EqualTo(4));
                foreach(var camera in cameras)
                {
                    var data=camera.GetUniversalAdditionalCameraData();Assert.That(data.antialiasing,Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                    Assert.That(data.antialiasingQuality,Is.EqualTo(AntialiasingQuality.Medium));Assert.That(data.renderPostProcessing,Is.True);
                }
                Assert.That(visual.GetComponent<Volume>(),Is.Null,"AA must not create or require a volume profile");
                yield return CaptureWorld(map+"-antialias-smaa-medium");
                if(map==CombatBowlCatalog.Id)
                {
                    foreach(var pair in new[]{Tuple.Create(NativeVisualPreferences.AntialiasingHigh,AntialiasingQuality.High),Tuple.Create(NativeVisualPreferences.AntialiasingLow,AntialiasingQuality.Low)})
                    {
                        NativeVisualPreferences.SetAntialiasingLevel(pair.Item1);visual.Apply();
                        foreach(var camera in cameras)Assert.That(camera.GetUniversalAdditionalCameraData().antialiasingQuality,Is.EqualTo(pair.Item2));
                    }
                    NativeVisualPreferences.SetAntialiasingLevel(NativeVisualPreferences.AntialiasingMedium);visual.Apply();
                }
                NativeVisualPreferences.SetAntialiasingLevel(NativeVisualPreferences.AntialiasingOff);visual.Apply();
                foreach(var camera in cameras){var data=camera.GetUniversalAdditionalCameraData();Assert.That(data.antialiasing,Is.EqualTo(AntialiasingMode.None));Assert.That(data.renderPostProcessing,Is.False);}
                Time.timeScale=1;ground.SendMessage("Menu");B("main-action-0").onClick.Invoke();
            }
        }
        [UnityTest] public IEnumerator ArenaLightAndSurfaceRolesCompareFrozenZonesAndRestoreSources()
        {
            B("main-action-0").onClick.Invoke();
            string[] paths={"visual.fillScale","visual.keyScale","visual.wallSmoothness","visual.floorSmoothness","visual.metalSmoothness","visual.wallMetallic","visual.floorMetallic","visual.metalMetallic"};
            float[] previous={.75f,1.25f,.2f,.35f,.55f,.05f,.1f,.65f};
            var defaults=ProvingProfile.CreateDefault();
            foreach(var map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id})
            {
                yield return Begin(map);Time.timeScale=0;
                foreach(VisualEffect effect in Enum.GetValues(typeof(VisualEffect)))NativeVisualPreferences.SetStrength(ground.Profile,effect,50);
                NativeVisualPreferences.SetAntialiasingLevel(NativeVisualPreferences.AntialiasingMedium);Effects(true);
                var visual=ground.GetComponentInChildren<NativeVisualPresentation>();var arena=ground.GetComponentInChildren<ProvingArena>();
                var cameras=ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled).OrderBy(c=>c.name).ToArray();
                var key=ground.GetComponentsInChildren<Light>().Single(l=>l.type==LightType.Directional);
                var lamps=arena.GetComponentsInChildren<Light>(true);Effects(false);var intensities=lamps.Select(l=>l.intensity).ToArray();var sourceAmbient=RenderSettings.ambientLight;
                var materials=arena.GetComponentsInChildren<NativeVisualMaterials>().SelectMany(r=>r.Roles).ToArray();
                var source=materials.Select(pair=>new Vector2(pair.Key.GetFloat("_Metallic"),pair.Key.GetFloat("_Smoothness"))).ToArray();
                int meshes=ground.GetComponentsInChildren<MeshFilter>(true).Length,colliders=ground.GetComponentsInChildren<Collider>(true).Length,lights=ground.GetComponentsInChildren<Light>(true).Length;
                var fills=cameras.Select(c=>c.transform.Find("equipment-fill").GetComponent<Light>()).ToArray();var fillValues=fills.Select(l=>l.intensity).ToArray();
                Assert.That(lamps.Length,Is.GreaterThan(0));
                Vector3[][] regions=map==CombatBowlCatalog.Id?new[]{
                    new[]{new Vector3(-10,1.7f,14),new Vector3(6,1.7f,28),new Vector3(-6,1.7f,-28),new Vector3(10,1.7f,-14)},
                    new[]{new Vector3(0,.5f,-5),new Vector3(0,.5f,5),new Vector3(-6,5.7f,0),new Vector3(6,5.7f,0)}}:
                    map==IndustrialTunnelsCatalog.Id?new[]{
                    new[]{new Vector3(0,1.7f,-19),new Vector3(-23,1.7f,-3),new Vector3(-10,1.7f,13),new Vector3(10,1.7f,-13)},
                    new[]{new Vector3(-20,1.1f,16),new Vector3(20,1.1f,16),new Vector3(-20,1.1f,-16),new Vector3(20,1.1f,-16)}}:new[]{
                    new[]{new Vector3(-4,1.7f,18),new Vector3(4,1.7f,-18),new Vector3(-26,1.7f,0),new Vector3(26,1.7f,0)},
                    new[]{new Vector3(-7,1.7f,-8),new Vector3(7,1.7f,8),new Vector3(-7,6.2f,10),new Vector3(7,6.2f,10)}};
                double clock=ground.Session.Time;
                foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
                {
                    var snapshot=ground.Session.Capture();
                    for(int i=0;i<snapshot.Lives.Length;i++){var life=snapshot.Lives[i];life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;life.ShotgunOwned=life.RocketOwned=life.CutterOwned=true;life.Ammo=CombatLife.AmmoFor(life,weapon);snapshot.Lives[i]=life;}
                    ground.Session.Restore(snapshot);ground.SendMessage("LateUpdate");
                    for(int zone=0;zone<regions.Length;zone++)
                    {
                        var positions=regions[zone];
                        Vector3 Target(int i)=>map==IndustrialTunnelsCatalog.Id?(zone==1?new Vector3(positions[i].x<0?-14:14,positions[i].y,positions[i].z):i==0?new Vector3(14,positions[i].y,-19):i==1?new Vector3(-23,positions[i].y,10):new Vector3(positions[i].x<0?-20:20,1.1f,positions[i].z<0?-16:16)):map==CombatBowlCatalog.Id&&zone==0?new Vector3(0,4,0):new Vector3(0,positions[i].y,0);
                        Action pose=()=>{for(int i=0;i<cameras.Length;i++){cameras[i].transform.position=positions[i];cameras[i].transform.rotation=Quaternion.LookRotation(Target(i)-positions[i]);}};
                        // Reuse the four frozen participants as readable subjects, one per view.
                        // Their identities/materials and the before/after poses remain identical.
                        var subjects=ground.Session.Capture();
                        for(int i=0;i<cameras.Length;i++)
                        {
                            int subject=(i+1)%cameras.Length;var state=subjects.Poses[subject];
                            Vector3 direction=(Target(i)-positions[i]).normalized;direction.y=0;direction.Normalize();
                            state.Position=positions[i]+direction*3-Vector3.up*1.7f;state.Velocity=Vector3.zero;state.Yaw=Quaternion.LookRotation(-direction).eulerAngles.y;subjects.Poses[subject]=state;
                        }
                        ground.Session.Restore(subjects);ground.SendMessage("LateUpdate");
                        ground.Profile.Set("visual.ambientLift",0);
                        for(int i=0;i<paths.Length;i++)ground.Profile.Set(paths[i],previous[i]);
                        foreach(string path in new[]{"visual.bowlLocalLightScale","visual.tunnelsLocalLightScale","visual.lunarLocalLightScale"})ground.Profile.Set(path,1);
                        foreach(var registry in arena.GetComponentsInChildren<NativeVisualMaterials>())
                            foreach(var material in registry.Roles.Keys.ToArray())if(material.name=="Dust-grey paving")registry.Roles[material]=VisualMaterialRole.Wall;
                        visual.Bind(ground.Profile,arena.transform,key,cameras);Effects(true);
                        yield return CaptureWorld(map+"-zone"+zone+"-"+weapon+"-before",pose);
                        ground.Profile.Set("visual.ambientLift",defaults.Get("visual.ambientLift"));
                        for(int i=0;i<paths.Length;i++)ground.Profile.Set(paths[i],defaults.Get(paths[i]));
                        foreach(string path in new[]{"visual.bowlLocalLightScale","visual.tunnelsLocalLightScale","visual.lunarLocalLightScale"})ground.Profile.Set(path,defaults.Get(path));
                        foreach(var registry in arena.GetComponentsInChildren<NativeVisualMaterials>())
                            foreach(var material in registry.Roles.Keys.ToArray())if(material.name=="Dust-grey paving")registry.Roles[material]=VisualMaterialRole.Floor;
                        visual.Bind(ground.Profile,arena.transform,key,cameras);Effects(true);
                        yield return CaptureWorld(map+"-zone"+zone+"-"+weapon+"-after",pose);
                        Assert.That(ground.Session.Capture().Poses,Is.EqualTo(subjects.Poses));
                        Color tint=sourceAmbient/sourceAmbient.maxColorComponent;
                        Assert.That(RenderSettings.ambientLight,Is.EqualTo(sourceAmbient*.975f+tint*.09f));
                        foreach(var registry in arena.GetComponentsInChildren<NativeVisualMaterials>())foreach(var pair in registry.Roles)
                        {
                            string role=pair.Value==VisualMaterialRole.Wall?"wall":pair.Value==VisualMaterialRole.Floor?"floor":"metal";
                            int index=Array.FindIndex(materials,m=>m.Key==pair.Key);
                            Assert.That(pair.Key.GetFloat("_Smoothness"),Is.EqualTo(Mathf.Lerp(source[index].y,defaults.Get("visual."+role+"Smoothness"),.5f)).Within(.00001f));
                            Assert.That(pair.Key.GetFloat("_Metallic"),Is.EqualTo(Mathf.Lerp(source[index].x,defaults.Get("visual."+role+"Metallic"),.5f)).Within(.00001f));
                        }
                        for(int i=0;i<lamps.Length;i++)Assert.That(lamps[i].intensity,Is.EqualTo(intensities[i]*.9f).Within(.00001f));
                    }
                }
                NativeVisualPreferences.SetStrength(ground.Profile,VisualEffect.Lighting,100);visual.Apply();
                for(int i=0;i<lamps.Length;i++)Assert.That(lamps[i].intensity,Is.EqualTo(intensities[i]*.8f).Within(.00001f));
                NativeVisualPreferences.SetStrength(ground.Profile,VisualEffect.Lighting,0);visual.Apply();
                for(int i=0;i<lamps.Length;i++)Assert.That(lamps[i].intensity,Is.EqualTo(intensities[i]));
                Assert.That(materials[0].Key.GetFloat("_Smoothness"),Is.Not.EqualTo(source[0].y),"Lighting zero does not disable materials");
                Effects(false);Assert.That(RenderSettings.ambientLight,Is.EqualTo(sourceAmbient));
                for(int i=0;i<materials.Length;i++){Assert.That(materials[i].Key.GetFloat("_Metallic"),Is.EqualTo(source[i].x));Assert.That(materials[i].Key.GetFloat("_Smoothness"),Is.EqualTo(source[i].y));}
                for(int i=0;i<fills.Length;i++)Assert.That(fills[i].intensity,Is.EqualTo(fillValues[i]));
                Assert.That(ground.Session.Time,Is.EqualTo(clock));Assert.That(ground.GetComponentsInChildren<MeshFilter>(true).Length,Is.EqualTo(meshes));Assert.That(ground.GetComponentsInChildren<Collider>(true).Length,Is.EqualTo(colliders));Assert.That(ground.GetComponentsInChildren<Light>(true).Length,Is.EqualTo(lights));
                Time.timeScale=1;ground.SendMessage("Menu");B("main-action-0").onClick.Invoke();
            }
        }
        [UnityTest] public IEnumerator EquipmentFillComparesAllWeaponsWithoutLightingTheWorld()
        {
            B("main-action-0").onClick.Invoke();
            foreach(var map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id})
            {
                yield return Begin(map);Time.timeScale=0;
                foreach(VisualEffect effect in Enum.GetValues(typeof(VisualEffect)))NativeVisualPreferences.SetStrength(ground.Profile,effect,50);
                NativeVisualPreferences.SetAntialiasingLevel(NativeVisualPreferences.AntialiasingMedium);Effects(true);
                var cameras=ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled).OrderBy(c=>c.name).ToArray();
                var fills=cameras.Select(c=>c.transform.Find("equipment-fill").GetComponent<Light>()).ToArray();
                var views=cameras.Select(c=>c.transform.Find("trooper-view").gameObject).ToArray();
                int meshes=ground.GetComponentsInChildren<MeshFilter>(true).Length,colliders=ground.GetComponentsInChildren<Collider>(true).Length;
                Assert.That(cameras.Length,Is.EqualTo(4));
                foreach(var fill in fills)Assert.That(fill.intensity,Is.EqualTo(ProvingProfile.CreateTrooperDefault().Get("view.fillIntensity")));
                foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
                {
                    var snapshot=ground.Session.Capture();
                    for(int i=0;i<snapshot.Lives.Length;i++)
                    {
                        var life=snapshot.Lives[i];life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;
                        life.ShotgunOwned=life.RocketOwned=life.CutterOwned=true;life.Ammo=CombatLife.AmmoFor(life,weapon);snapshot.Lives[i]=life;
                    }
                    ground.Session.Restore(snapshot);ground.SendMessage("LateUpdate");
                    var matrices=cameras.Select(c=>c.transform.localToWorldMatrix).ToArray();
                    double clock=ground.Session.Time;
                    foreach(var fill in fills)fill.intensity=.05f;
                    yield return CaptureWorld(map+"-"+weapon+"-before");
                    if(map==IndustrialTunnelsCatalog.Id&&weapon==WeaponId.Rifle)
                        foreach(float level in new[]{.65f,.95f})
                        {
                            foreach(var fill in fills)fill.intensity=level;
                            yield return CaptureWorld(map+"-"+weapon+"-candidate-"+level.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
                        }
                    foreach(var fill in fills)fill.intensity=ProvingProfile.CreateTrooperDefault().Get("view.fillIntensity");
                    yield return CaptureWorld(map+"-"+weapon+"-after");
                    Assert.That(ground.Session.Time,Is.EqualTo(clock));
                    for(int i=0;i<cameras.Length;i++)Assert.That(cameras[i].transform.localToWorldMatrix,Is.EqualTo(matrices[i]));
                    Assert.That(ground.GetComponentsInChildren<MeshFilter>(true).Length,Is.EqualTo(meshes));
                    Assert.That(ground.GetComponentsInChildren<Collider>(true).Length,Is.EqualTo(colliders));
                }
                // With own equipment hidden, its light must not alter world/participant pixels.
                foreach(var view in views)view.SetActive(false);
                foreach(var fill in fills)fill.intensity=.05f;
                var baseline=cameras.Select(RenderWorldPixels).ToArray();
                foreach(var fill in fills)fill.intensity=ProvingProfile.CreateTrooperDefault().Get("view.fillIntensity");
                for(int i=0;i<cameras.Length;i++)Assert.That(RenderWorldPixels(cameras[i]),Is.EqualTo(baseline[i]),"Equipment fill must not light the world or another participant");
                foreach(var view in views)view.SetActive(true);
                Time.timeScale=1;ground.SendMessage("Menu");B("main-action-0").onClick.Invoke();
            }
        }
        static byte[] RenderWorldPixels(Camera camera)
        {
            var target=new RenderTexture(800,450,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var pixels=new Texture2D(800,450,TextureFormat.RGB24,false);
            var prior=RenderTexture.active;var old=camera.targetTexture;var rect=camera.rect;
            try
            {
                camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,800,450),0,0);pixels.Apply();return pixels.GetRawTextureData();
            }
            finally
            {
                camera.targetTexture=old;camera.rect=rect;RenderTexture.active=prior;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);
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
        IEnumerator CaptureWorld(string name,Action pose=null)
        {
            string dir=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LOADING_SCREENSHOTS");if(string.IsNullOrEmpty(dir))yield break;
            yield return null;yield return null;pose?.Invoke();Directory.CreateDirectory(dir);
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
