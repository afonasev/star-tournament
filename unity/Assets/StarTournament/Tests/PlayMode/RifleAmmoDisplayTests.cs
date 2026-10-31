using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class RifleAmmoDisplayTests
    {
        Scene scene;Gamepad first,second;ProvingGround ground;bool asyncShaders,changedShaders;string output;
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        void Ammo(int value)
        {
            var snap=ground.Session.Capture();snap.Lives[0].SelectedWeapon=WeaponId.Rifle;snap.Lives[0].PendingWeapon=default;snap.Lives[0].SwitchRemaining=0;
            snap.Lives[0].RifleAmmo=snap.Lives[0].Ammo=value;snap.Lives[0].RifleCooldownRemaining=snap.Lives[0].CooldownRemaining=0;
            ground.Session.Restore(snap);Render();
        }
        void Advance(float seconds)
        {
            var actions=new LocalAction[ground.Session.ParticipantCount];
            for(int i=0;i<Mathf.CeilToInt(seconds/.02f);i++){ground.Session.Tick(actions,.02f);Render();}
        }
        IEnumerator Capture(Camera camera,string name,int width=1600,int height=1000)
        {
            if(string.IsNullOrEmpty(output))yield break;
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(output,name+".png")));
            var rt=new RenderTexture(width,height,24);var prev=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;
                for(int i=0;i<3;i++){camera.Render();yield return null;}
                RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());
            }
            finally{camera.targetTexture=prev;camera.rect=rect;RenderTexture.active=active;Object.Destroy(tex);rt.Release();Object.Destroy(rt);}
        }
        IEnumerator Detail(Camera source,RifleAmmoDisplay display,string name)
        {
            if(string.IsNullOrEmpty(output))yield break;
            var go=new GameObject("COBALT detail evidence camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
            camera.transform.position=source.transform.position;camera.transform.LookAt(display.Screen.bounds.center,source.transform.up);camera.fieldOfView=18;
            try{yield return Capture(camera,name,1200,800);}finally{Object.Destroy(go);}
        }
        [Test] public void AnimationUsesGameTimeAndNeverTruncatesAmmo()
        {
            var root=new GameObject("display-test");
            try
            {
                var display=new GameObject("display").AddComponent<RifleAmmoDisplay>();display.Initialize(root.transform,ProvingProfile.CreateDefault(),0);
                display.Render(30,30,1,1);Assert.That(display.Fraction,Is.EqualTo(1));
                display.Render(15,30,2,1);Assert.That(display.DisplayText,Is.EqualTo("15"));Assert.That(display.Fraction,Is.EqualTo(1));
                display.Render(15,30,2.06,1);Assert.That(display.Fraction,Is.InRange(.70f,.80f));
                float paused=display.Fraction;display.Render(15,30,2.06,1);Assert.That(display.Fraction,Is.EqualTo(paused));
                display.Render(15,30,2.2,1);Assert.That(display.Fraction,Is.EqualTo(.5f));Assert.That(display.BarColor,Is.EqualTo(Color.yellow));
                display.Render(3,30,3,1);display.Render(3,30,3.2,1);Assert.That(display.DisplayText,Is.EqualTo("03"));Assert.That(display.BarColor.r,Is.GreaterThan(display.BarColor.g));
                display.Render(30,30,4,1);display.Render(30,30,4.175,1);Assert.That(display.Fraction,Is.InRange(.50f,.60f));
                display.Render(30,30,4.5,1);Assert.That(display.Fraction,Is.EqualTo(1));
                display.Render(0,30,5,1);display.Render(0,30,5.2,1);Assert.That(display.DisplayText,Is.EqualTo("00"));Assert.That(display.Fraction,Is.Zero);
                display.Render(30,30,6,2);Assert.That(display.Fraction,Is.Zero);display.Render(30,30,6.5,2);Assert.That(display.Fraction,Is.EqualTo(1));
                display.Render(120,240,1,2);Assert.That(display.Fraction,Is.EqualTo(.5f),"rewind snaps");
                display.Render(int.MaxValue,240,2,2);Assert.That(display.DisplayText,Is.EqualTo("2147483647"));display.Render(int.MaxValue,240,3,2);Assert.That(display.Fraction,Is.EqualTo(1));
            }
            finally{Object.DestroyImmediate(root);}
        }
        [UnityTest] public IEnumerator AuthoritativeAmmoAnimationAndModelLifecycle()
        {
            changedShaders=true;asyncShaders=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RIFLE_AMMO_CAPTURE");
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();ground.enabled=false;
            first=InputSystem.AddDevice<Gamepad>();second=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{first,second});yield return null;
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
            int capacity=ground.Session.RifleStartingAmmo;Assert.That(capacity%2,Is.Zero);
            var snap=ground.Session.Capture();
            for(int i=0;i<2;i++){snap.Lives[i].ShotgunOwned=true;snap.Lives[i].SelectedWeapon=WeaponId.Rifle;snap.Lives[i].RifleAmmo=snap.Lives[i].Ammo=i==0?capacity:capacity/2;}
            ground.Session.Restore(snap);Render();Advance(.5f);
            var cameras=ground.GetComponentsInChildren<Camera>().Where(c=>c.name.StartsWith("seat-camera-")).OrderBy(c=>c.name).ToArray();
            var model=cameras[0].transform.Find("trooper-view").GetComponent<WeaponModelPresentation>();var display=model.RifleDisplay;
            var other=cameras[1].transform.Find("trooper-view").GetComponent<WeaponModelPresentation>().RifleDisplay;
            Assert.That(display.DisplayText,Is.EqualTo(capacity.ToString("D2")));Assert.That(other.DisplayText,Is.EqualTo((capacity/2).ToString("D2")));
            Assert.That(display.Screen.sharedMaterial,Is.SameAs(other.Screen.sharedMaterial));
            Assert.That(display.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(other.GetComponent<MeshFilter>().sharedMesh));
            Assert.That(display.GetComponentsInChildren<Collider>().Length,Is.Zero);
            Assert.That(display.Screen.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(display.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.EqualTo(24));
            Assert.That(display.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3,Is.EqualTo(22));
            Assert.That(cameras[1].cullingMask&(1<<display.gameObject.layer),Is.Zero);
            var block=new MaterialPropertyBlock();display.Screen.GetPropertyBlock(block);var digits=block.GetVector("_DigitsA");
            TrooperIdentityPresentation.ApplyBody(model.gameObject,Color.red,ground.ParticipantPaletteProfile);model.Tint(Color.red,true);
            display.Screen.GetPropertyBlock(block);Assert.That(block.GetVector("_DigitsA"),Is.EqualTo(digits));Assert.That(display.Screen.sharedMaterial.shader.name,Is.EqualTo("StarTournament/RifleDisplay"));
            model.Tint(Color.cyan,false);TrooperIdentityPresentation.ApplyBody(model.gameObject,Color.cyan,ground.ParticipantPaletteProfile);
            display.SetVisible(false);yield return Capture(cameras[0],"00-baseline");display.SetVisible(true);yield return Capture(cameras[0],"01-full");yield return Detail(cameras[0],display,"detail-full");
            var frozen=JsonUtility.ToJson(ground.Session.Capture());Render();Render();Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(frozen));
            var actions=new LocalAction[ground.Session.ParticipantCount];ground.Session.Tick(actions,.02f);
            int before=ground.Session.ShotCount;actions[0].Fire=true;ground.Session.Tick(actions,.02f);actions[0]=default;Render();
            Assert.That(ground.Session.ShotCount,Is.EqualTo(before+1));Assert.That(display.DisplayText,Is.EqualTo((capacity-1).ToString("D2")));Assert.That(other.DisplayText,Is.EqualTo((capacity/2).ToString("D2")));Advance(.3f);
            Ammo(capacity/2);Advance(.3f);Assert.That(display.Fraction,Is.EqualTo(.5f));yield return Capture(cameras[0],"02-half");yield return Detail(cameras[0],display,"detail-half");
            Ammo(capacity/10);Advance(.3f);yield return Capture(cameras[0],"03-low");yield return Detail(cameras[0],display,"detail-low");
            Ammo(0);Advance(.3f);before=ground.Session.ShotCount;actions[0].Fire=true;ground.Session.Tick(actions,.02f);actions[0]=default;Render();Assert.That(ground.Session.ShotCount,Is.EqualTo(before));Assert.That(display.DisplayText,Is.EqualTo("00"));yield return Capture(cameras[0],"04-empty-00");
            Ammo(capacity);
            for(int i=0;i<12;i++){yield return Detail(cameras[0],display,"animation/fill-"+i.ToString("D2"));Advance(.04f);}
            Assert.That(display.Fraction,Is.EqualTo(1));
            Ammo(capacity/2);for(int i=0;i<7;i++){yield return Detail(cameras[0],display,"animation/drain-"+i.ToString("D2"));Advance(.02f);}
            Ammo(1000);Advance(.5f);Assert.That(display.DisplayText,Is.EqualTo("1000"));Assert.That(display.Fraction,Is.EqualTo(1));yield return Capture(cameras[0],"05-overflow-1000");
            Ammo(capacity);actions[0].SelectWeapon=WeaponSelection.Shotgun;ground.Session.Tick(actions,.02f);actions[0]=default;Render();Assert.That(display.Screen.enabled,Is.True);
            for(int i=0;i<100;i++){ground.Session.Tick(actions,.02f);Render();Assert.That(display.Screen.enabled,Is.EqualTo(model.ShownWeapon==WeaponId.Rifle));}
            Assert.That(model.ShownWeapon,Is.EqualTo(WeaponId.Shotgun));Assert.That(display.Screen.enabled,Is.False);Assert.That(model.ShotgunDisplay.Screen.enabled,Is.True);
            Ammo(capacity);Assert.That(display.Screen.enabled,Is.True);yield return Capture(cameras[0],"06-return");
            var corpse=Object.Instantiate(model.gameObject);corpse.GetComponent<WeaponModelPresentation>().HideForCorpse();
            Assert.That(corpse.GetComponentInChildren<RifleAmmoDisplay>(true).GetComponent<Renderer>().enabled,Is.False);Object.Destroy(corpse);
            if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"asset-pass.json"),"{\"vertices\":24,\"triangles\":22,\"materials\":1,\"drawsPerVisibleDisplay\":1,\"texture\":\"none; analytic digits and bar\",\"colliders\":0,\"shadowPasses\":0,\"lod\":\"22 triangles; seat culling and weapon visibility retained\",\"humanAcceptance\":false}");
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            if(changedShaders)UnityEditor.ShaderUtil.allowAsyncCompilation=asyncShaders;
            if(first!=null&&first.added)InputSystem.RemoveDevice(first);if(second!=null&&second.added)InputSystem.RemoveDevice(second);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
