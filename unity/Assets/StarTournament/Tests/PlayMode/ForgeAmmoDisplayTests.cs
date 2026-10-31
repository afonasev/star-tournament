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
    public sealed class ForgeAmmoDisplayTests
    {
        Scene scene;Gamepad first,second;ProvingGround ground;bool asyncShaders;string output;
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        void Ammo(int value)
        {
            var snap=ground.Session.Capture();snap.Lives[0].SelectedWeapon=WeaponId.Plasma;snap.Lives[0].PendingWeapon=default;snap.Lives[0].SwitchRemaining=0;
            snap.Lives[0].PlasmaAmmo=snap.Lives[0].Ammo=value;snap.Lives[0].PlasmaCooldownRemaining=snap.Lives[0].CooldownRemaining=0;
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
            output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_FORGE_DISPLAY_CAPTURE");
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();yield return NativeLoadingTestScene.WaitLabHistory(ground);ground.enabled=false;
            first=InputSystem.AddDevice<Gamepad>();second=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{first,second});yield return null;
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
            var snap=ground.Session.Capture();
            for(int i=0;i<2;i++){snap.Lives[i].PlasmaOwned=true;snap.Lives[i].SelectedWeapon=WeaponId.Plasma;snap.Lives[i].PlasmaAmmo=snap.Lives[i].Ammo=i==0?48:88;}
            ground.Session.Restore(snap);Render();
            var cameras=ground.GetComponentsInChildren<Camera>().Where(c=>c.name.StartsWith("seat-camera-")).OrderBy(c=>c.name).ToArray();
            var model=cameras[0].transform.Find("trooper-view").GetComponent<WeaponModelPresentation>();var display=model.ForgeDisplay;
            var other=cameras[1].transform.Find("trooper-view").GetComponent<WeaponModelPresentation>().ForgeDisplay;
            Assert.That(display.DisplayText,Is.EqualTo("48"));Assert.That(other.DisplayText,Is.EqualTo("88"));
            Assert.That(display.Screen.sharedMaterial,Is.SameAs(other.Screen.sharedMaterial));
            var block=new MaterialPropertyBlock();other.Screen.GetPropertyBlock(block);Assert.That(block.GetVector("_DigitsA").x,Is.EqualTo(8));Assert.That(block.GetVector("_DigitsA").y,Is.EqualTo(8));
            Assert.That(display.GetComponentsInChildren<Collider>().Length,Is.Zero);
            Assert.That(display.Screen.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(display.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.EqualTo(203));
            Assert.That(display.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3,Is.EqualTo(200));
            Assert.That(display.gameObject.layer,Is.EqualTo(model.gameObject.layer));
            Assert.That(cameras[1].cullingMask&(1<<display.gameObject.layer),Is.Zero);
            var material=display.Screen.sharedMaterial;TrooperIdentityPresentation.ApplyBody(model.gameObject,Color.red,ground.ParticipantPaletteProfile);
            model.Tint(Color.red,true);display.Screen.GetPropertyBlock(block);Assert.That(block.GetVector("_DigitsA").y,Is.EqualTo(8));Assert.That(display.Screen.sharedMaterial,Is.SameAs(material));Assert.That(material.shader.name,Is.EqualTo("StarTournament/ForgeDisplay"));model.Tint(Color.cyan,false);TrooperIdentityPresentation.ApplyBody(model.gameObject,Color.cyan,ground.ParticipantPaletteProfile);
            Assert.That(display.ReserveFraction,Is.EqualTo(48f/ground.Session.PlasmaProfile.Get("plasma.pickupAmmo")));
            display.SetVisible(false);yield return Capture(cameras[0],"00-baseline");display.SetVisible(true);yield return Capture(cameras[0],"01-ammo-48");
            var detailObject=new GameObject("forge-instrument-detail-camera");var detail=detailObject.AddComponent<Camera>();detail.CopyFrom(cameras[0]);detail.enabled=false;
            detail.transform.position=cameras[0].transform.position;detail.transform.rotation=Quaternion.LookRotation(display.transform.position-detail.transform.position,cameras[0].transform.up);detail.fieldOfView=26;
            yield return Capture(detail,"01-detail-48");
            var frozen=JsonUtility.ToJson(ground.Session.Capture());Render();Render();Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(frozen));
            // Restore intentionally requires a released input frame before accepting fire.
            var actions=new LocalAction[ground.Session.ParticipantCount];ground.Session.Tick(actions,.02f);
            int beforeShot=ground.Session.ShotCount;actions[0].Fire=true;ground.Session.Tick(actions,.02f);actions[0]=default;Render();
            Assert.That(ground.Session.ShotCount,Is.EqualTo(beforeShot+1));Assert.That(display.DisplayText,Is.EqualTo("47"));Assert.That(other.DisplayText,Is.EqualTo("88"));yield return Capture(cameras[0],"02-shot-47");
            Ammo(8);Assert.That(display.DisplayText,Is.EqualTo("8"));Assert.That(display.LitSegments,Is.EqualTo(2));yield return Capture(cameras[0],"03-low-8");yield return Capture(detail,"03-detail-low-8");
            Ammo(0);ground.Session.Tick(actions,.02f);int shots=ground.Session.ShotCount;actions[0].Fire=true;ground.Session.Tick(actions,.02f);actions[0]=default;Render();
            Assert.That(ground.Session.ShotCount,Is.EqualTo(shots));Assert.That(display.DisplayText,Is.EqualTo("0"));Assert.That(display.ReserveFraction,Is.Zero);display.Screen.GetPropertyBlock(block);Assert.That(block.GetFloat("_Empty"),Is.EqualTo(1));yield return Capture(cameras[0],"04-empty-0");yield return Capture(detail,"04-detail-empty-0");
            var pickup=ground.Session.WeaponPickups.First(p=>p.Weapon==WeaponId.Plasma&&p.Available);
            ground.PlaceCombatReviewSeat(0,pickup.Anchor,0);ground.Session.Tick(actions,.02f);Render();
            Assert.That(ground.Session.WeaponPickups.Single(p=>p.InstanceId==pickup.InstanceId).Available,Is.False);
            Assert.That(display.DisplayText,Is.EqualTo("100"),"real weapon pickup replenishes instrument");
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);Render();yield return Capture(cameras[0],"05-pickup-100");
            Ammo(100);Assert.That(display.DisplayText,Is.EqualTo("100"));Assert.That(display.ReserveFraction,Is.EqualTo(1));display.Screen.GetPropertyBlock(block);Assert.That(block.GetFloat("_Empty"),Is.Zero);
            actions[0].SelectWeapon=WeaponSelection.Rifle;ground.Session.Tick(actions,.02f);actions[0]=default;Render();Assert.That(display.Screen.enabled,Is.True,"stays with outgoing FORGE on ascent");
            for(int i=0;i<100;i++){ground.Session.Tick(actions,.02f);Render();Assert.That(display.Screen.enabled,Is.EqualTo(model.ShownWeapon==WeaponId.Plasma));}
            Assert.That(model.ShownWeapon,Is.EqualTo(WeaponId.Rifle));Assert.That(display.Screen.enabled,Is.False);
            Ammo(48);Assert.That(display.Screen.enabled,Is.True);yield return Capture(cameras[0],"06-return-48");
            var corpse=Object.Instantiate(model.gameObject);corpse.GetComponent<WeaponModelPresentation>().HideForCorpse();
            Assert.That(corpse.GetComponentInChildren<ForgeAmmoDisplay>(true).GetComponent<Renderer>().enabled,Is.False);Object.Destroy(corpse);
            model.HideForCorpse();Assert.That(display.Screen.enabled,Is.False);Render();
            var saved=ground.Session.Capture();ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,1000);Render();
            Assert.That(display.Screen.enabled,Is.False,"death hides local instrument");
            ground.Session.Restore(saved);Render();Assert.That(display.Screen.enabled,Is.True);
            Object.Destroy(detailObject);
            var mesh=display.GetComponent<MeshFilter>().sharedMesh;
            display.SetAmmo(123,100);Assert.That(display.DisplayText,Is.EqualTo("123"));Assert.That(display.LitSegments,Is.EqualTo(24));
            display.SetAmmo(50,200);Assert.That(display.ReserveFraction,Is.EqualTo(.25f));Assert.That(display.LitSegments,Is.EqualTo(6));
            Render();Assert.That(display.DisplayText,Is.EqualTo("48"));
            if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"asset-pass.json"),"{\"vertices\":203,\"triangles\":200,\"materials\":1,\"drawsPerVisibleDisplay\":1,\"textureShared\":true,\"colliders\":0,\"shadowPasses\":0,\"lod\":\"world LOD0 only; FPS existing layer culling\",\"humanAcceptance\":false}");
            yield return SceneManager.UnloadSceneAsync(scene);scene=default;yield return null;
            Assert.That(material==null,Is.True,"last owner's material released");Assert.That(mesh==null,Is.True,"last owner's mesh released");
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            UnityEditor.ShaderUtil.allowAsyncCompilation=asyncShaders;
            if(first!=null&&first.added)InputSystem.RemoveDevice(first);if(second!=null&&second.added)InputSystem.RemoveDevice(second);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
