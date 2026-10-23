#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StarTournament.ProvingGround
{
    /// <summary>Explicit synthetic native Player evidence. No human acceptance implied.</summary>
    public sealed class VectorVisualReview : MonoBehaviour
    {
        Gamepad[] pads;string directory;ProvingGround ground;
        IEnumerator Start()
        {
            directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_VECTOR_EVIDENCE");
            if(string.IsNullOrEmpty(directory))throw new InvalidOperationException("Vector evidence path required");
            Directory.CreateDirectory(directory);AudioListener.volume=0;Application.runInBackground=true;
            ground=GetComponent<ProvingGround>();pads=new[]{InputSystem.AddDevice<Gamepad>(),InputSystem.AddDevice<Gamepad>()};
            yield return new WaitForSecondsRealtime(.5f);
            var roster=NativeMatchRoster.Ffa(2);
            var composition=new NativeMatchComposition(roster,new[]{new NativeParticipantInfo(NativeParticipantKind.LocalHuman,"Игрок 1",NativeStandingsView.Palette[0]),new NativeParticipantInfo(NativeParticipantKind.DiagnosticFixture,"Fixture",NativeStandingsView.Palette[1])},new[]{0});
            ground.StartParticipantReview(composition);yield return new WaitForSeconds(.2f);
            if(!ground.transform.Find("seat-camera-1/trooper-view"))throw new InvalidOperationException("Equipment review did not create a first-person view");
            var anchor=ground.Session.Pose(0).Position;
            ground.PlaceCombatReviewSeat(0,anchor,0,45);yield return new WaitForSeconds(.2f);
            yield return Capture("01-idle");
            // Advance the real combat session to exact diagnostic sample times, independent of
            // macOS background throttling and screenshot stalls. No fabricated fire/impact event.
            ground.enabled=false;
            ground.Session.Tick(new[]{new LocalAction{Fire=true},new LocalAction()},Time.fixedDeltaTime);
            ground.Session.Tick(new LocalAction[2],.02f);ground.SendMessage("LateUpdate");
            yield return Capture("02-fire");
            ground.Session.Tick(new LocalAction[2],.14f);ground.SendMessage("LateUpdate");
            yield return Capture("03-smoke");
            ground.Session.Tick(new LocalAction[2],.5f);ground.SendMessage("LateUpdate");
            yield return Capture("04-settled");
            ground.Session.Tick(new[]{new LocalAction{Move=new Vector2(0,1)},new LocalAction()},.1f);ground.SendMessage("LateUpdate");
            yield return Capture("06-motion");ground.enabled=true;
            yield return AllWeapons();
            ground.StartCombatReview(pads,true);yield return new WaitForSeconds(.2f);
            anchor=ground.Session.Pose(0).Position;
            ground.PlaceCombatReviewSeat(0,anchor,0,0);ground.PlaceCombatReviewSeat(1,anchor+Vector3.forward*2,0,180);
            yield return new WaitForSeconds(.3f);yield return Capture("05-two-seats-world");
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"status\":\"DIAGNOSTIC_NOT_ACCEPTANCE\",\"muted\":true,\"equipment\":\"VECTOR-F3\",\"weapons\":4,\"realSessionJumpAndLanding\":true}");
            Debug.Log("VECTOR_REVIEW_COMPLETE "+directory);
            yield return new WaitForSecondsRealtime(.5f);Application.Quit();
        }
        IEnumerator AllWeapons()
        {
            ground.enabled=false;
            var baseline=ground.Session.Capture();
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {
                ground.Session.Restore(baseline);
                var fixture=ground.Session.Capture();var life=fixture.Lives[0];
                life.ShotgunOwned=life.RocketOwned=life.CutterOwned=true;
                life.ShotgunAmmo=(int)ground.LifeProfile.Get("combat.startingAmmo");life.RocketAmmo=(int)ground.LifeProfile.Get("rocket.startingAmmo");life.CutterEnergy=ground.CutterProfile.Get("cutter.energyCapacity");
                life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;
                life.Ammo=CombatLife.AmmoFor(life,weapon);fixture.Lives[0]=life;ground.Session.Restore(fixture);
                for(int tick=0;tick<100;tick++)ground.Session.Tick(new LocalAction[2],.02f);
                ground.SendMessage("LateUpdate");yield return Capture(weapon+"-aim");
                bool airborne=false,landed=false;
                for(int tick=0;tick<100;tick++)
                {
                    ground.Session.Tick(new[]{new LocalAction{Jump=tick==0,Move=tick<20?Vector2.up:Vector2.zero},new LocalAction()},.02f);
                    ground.SendMessage("LateUpdate");
                    var pose=ground.Session.Pose(0);airborne|=!pose.Grounded;landed|=airborne&&pose.Grounded;
                    if(tick==5||tick==15||tick==35||tick==99)yield return Capture(weapon+"-jump-"+tick.ToString("D2"));
                }
                if(!airborne||!landed)throw new InvalidOperationException(weapon+" real session jump/landing not witnessed");
                int shots=ground.Session.ShotCount;
                ground.Session.Tick(new[]{new LocalAction{Fire=true,FireHeld=true},new LocalAction()},.02f);
                if(weapon==WeaponId.Cutter?!ground.Session.Beam(0).Active:ground.Session.ShotCount!=shots+1)throw new InvalidOperationException(weapon+" real shot was not accepted");
                ground.SendMessage("LateUpdate");yield return Capture(weapon+"-fire");
            }
            ground.Session.Restore(baseline);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            CaptureCameras(ground,Path.Combine(directory,name+".png"));
        }
        internal static void CaptureCameras(ProvingGround ground,string path)
        {
            // Native batchmode has no window backbuffer. Render the actual seat cameras
            // directly; these evidence frames omit screen-space overlay UI.
            int width=Screen.width,height=Screen.height;
            var target=new RenderTexture(width,height,24);target.Create();
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                foreach(var camera in ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled).OrderBy(c=>c.depth))
                {
                    var old=camera.targetTexture;
                    try{camera.targetTexture=target;camera.Render();}finally{camera.targetTexture=old;}
                }
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;target.Release();Destroy(target);Destroy(pixels);}
        }
        void OnDestroy(){if(pads!=null)foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);}
    }
}
#endif

#endif
