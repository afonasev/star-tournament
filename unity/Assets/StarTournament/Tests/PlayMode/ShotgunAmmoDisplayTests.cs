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
    public sealed class ShotgunAmmoDisplayTests
    {
        Scene scene;Gamepad first,second;ProvingGround ground;bool asyncShaders;string output;
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        void Ammo(int value)
        {
            var snap=ground.Session.Capture();snap.Lives[0].SelectedWeapon=WeaponId.Shotgun;snap.Lives[0].PendingWeapon=default;snap.Lives[0].SwitchRemaining=0;
            snap.Lives[0].ShotgunAmmo=snap.Lives[0].Ammo=value;snap.Lives[0].ShotgunCooldownRemaining=snap.Lives[0].CooldownRemaining=0;
            ground.Session.Restore(snap);Render();
        }
        IEnumerator Capture(Camera camera,string name)
        {
            if(string.IsNullOrEmpty(output))yield break;
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            Directory.CreateDirectory(output);var rt=new RenderTexture(1600,1000,24);var prev=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try
            {
                camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;
                for(int i=0;i<3;i++){camera.Render();yield return null;}
                RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());
            }
            finally{camera.targetTexture=prev;camera.rect=rect;RenderTexture.active=active;Object.Destroy(tex);rt.Release();Object.Destroy(rt);}
        }
        [UnityTest] public IEnumerator AuthoritativeAmmoIsReadableIsolatedAndFollowsWeapon()
        {
            asyncShaders=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_AMMO_CAPTURE");
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();ground.enabled=false;
            first=InputSystem.AddDevice<Gamepad>();second=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{first,second});yield return null;
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
            var snap=ground.Session.Capture();
            for(int i=0;i<2;i++){snap.Lives[i].ShotgunOwned=true;snap.Lives[i].SelectedWeapon=WeaponId.Shotgun;snap.Lives[i].ShotgunAmmo=snap.Lives[i].Ammo=i==0?8:12;}
            ground.Session.Restore(snap);Render();
            var cameras=ground.GetComponentsInChildren<Camera>().Where(c=>c.name.StartsWith("seat-camera-")).OrderBy(c=>c.name).ToArray();
            var model=cameras[0].transform.Find("trooper-view").GetComponent<WeaponModelPresentation>();var display=model.ShotgunDisplay;
            var other=cameras[1].transform.Find("trooper-view").GetComponent<WeaponModelPresentation>().ShotgunDisplay;
            Assert.That(display.DisplayText,Is.EqualTo("08"));Assert.That(other.DisplayText,Is.EqualTo("12"));
            Assert.That(display.Screen.sharedMaterial,Is.SameAs(other.Screen.sharedMaterial));
            var block=new MaterialPropertyBlock();other.Screen.GetPropertyBlock(block);Assert.That(block.GetVector("_DigitsA").x,Is.EqualTo(1));Assert.That(block.GetVector("_DigitsA").y,Is.EqualTo(2));
            Assert.That(display.GetComponentsInChildren<Collider>().Length,Is.Zero);
            Assert.That(display.Screen.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(display.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.EqualTo(8));
            Assert.That(display.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3,Is.EqualTo(6));
            Assert.That(display.gameObject.layer,Is.EqualTo(model.gameObject.layer));
            Assert.That(cameras[1].cullingMask&(1<<display.gameObject.layer),Is.Zero);
            var material=display.Screen.sharedMaterial;TrooperIdentityPresentation.ApplyBody(model.gameObject,Color.red,ground.ParticipantPaletteProfile);
            model.Tint(Color.red,true);Assert.That(display.Screen.sharedMaterial,Is.SameAs(material));Assert.That(material.shader.name,Is.EqualTo("StarTournament/ShotgunDisplay"));model.Tint(Color.cyan,false);TrooperIdentityPresentation.ApplyBody(model.gameObject,Color.cyan,ground.ParticipantPaletteProfile);
            display.SetVisible(false);yield return Capture(cameras[0],"00-baseline");display.SetVisible(true);yield return Capture(cameras[0],"01-ammo-08");
            var frozen=JsonUtility.ToJson(ground.Session.Capture());Render();Render();Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(frozen));
            // Restore intentionally requires a released input frame before accepting fire.
            var actions=new LocalAction[ground.Session.ParticipantCount];ground.Session.Tick(actions,.02f);
            int beforeShot=ground.Session.ShotCount;actions[0].Fire=true;ground.Session.Tick(actions,.02f);actions[0]=default;Render();
            Assert.That(ground.Session.ShotCount,Is.EqualTo(beforeShot+1));Assert.That(display.DisplayText,Is.EqualTo("07"));Assert.That(other.DisplayText,Is.EqualTo("12"));yield return Capture(cameras[0],"02-shot-07");
            Ammo(0);ground.Session.Tick(actions,.02f);int shots=ground.Session.ShotCount;actions[0].Fire=true;ground.Session.Tick(actions,.02f);actions[0]=default;Render();
            Assert.That(ground.Session.ShotCount,Is.EqualTo(shots));Assert.That(display.DisplayText,Is.EqualTo("00"));yield return Capture(cameras[0],"03-empty-00");
            Ammo(100);Assert.That(display.DisplayText,Is.EqualTo("100"));yield return Capture(cameras[0],"04-restored-100");
            Ammo(20);Assert.That(display.DisplayText,Is.EqualTo("20"));
            actions[0].SelectWeapon=WeaponSelection.Rifle;ground.Session.Tick(actions,.02f);actions[0]=default;Render();Assert.That(display.Screen.enabled,Is.True,"stays with outgoing shotgun on ascent");
            for(int i=0;i<100;i++){ground.Session.Tick(actions,.02f);Render();Assert.That(display.Screen.enabled,Is.EqualTo(model.ShownWeapon==WeaponId.Shotgun));}
            Assert.That(model.ShownWeapon,Is.EqualTo(WeaponId.Rifle));Assert.That(display.Screen.enabled,Is.False);
            Ammo(8);Assert.That(display.Screen.enabled,Is.True);yield return Capture(cameras[0],"05-return-08");
            var corpse=Object.Instantiate(model.gameObject);corpse.GetComponent<WeaponModelPresentation>().HideForCorpse();
            Assert.That(corpse.GetComponentInChildren<ShotgunAmmoDisplay>(true).GetComponent<Renderer>().enabled,Is.False);Object.Destroy(corpse);
            model.HideForCorpse();Assert.That(display.Screen.enabled,Is.False);Render();
            if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"asset-pass.json"),"{\"vertices\":8,\"triangles\":6,\"materials\":1,\"drawsPerVisibleDisplay\":1,\"textureShared\":true,\"colliders\":0,\"shadowPasses\":0,\"lod\":\"not needed for six triangles; camera culling retained\",\"humanAcceptance\":false}");
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            UnityEditor.ShaderUtil.allowAsyncCompilation=asyncShaders;
            if(first!=null&&first.added)InputSystem.RemoveDevice(first);if(second!=null&&second.added)InputSystem.RemoveDevice(second);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
