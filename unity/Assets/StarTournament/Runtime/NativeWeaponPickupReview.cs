#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace StarTournament.ProvingGround
{
    /// <summary>Muted native Development Player evidence; scripted contact is not human/device acceptance.</summary>
    public sealed class NativeWeaponPickupReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad pad;readonly List<string> checks=new List<string>();
        void Require(bool value,string message){if(!value)throw new InvalidOperationException("WEAPON_PICKUP_REVIEW: "+message);}
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        void Tick(float seconds=.02f,WeaponSelection select=WeaponSelection.None,bool fire=false)
        {var a=new LocalAction[ground.Session.ParticipantCount];a[0]=new LocalAction{SelectWeapon=select,Fire=fire,FireHeld=fire};ground.Session.Tick(a,seconds);Render();}
        void Advance(double seconds)
        {while(seconds>0){float step=(float)Math.Min(.02,seconds);Tick(step);seconds-=step;}}
        void Park()=>ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);
        void NewMatch(){ground.SendMessage("Menu");while(ground.SetupBotCount>0)ground.RemoveBot(0);ground.StartCombatReview(new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);Park();ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,5),0,180);Render();}
        IEnumerator Capture(string name)
        {
            Render();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(ground.Session.Capture(),true));yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();directory=args[Array.IndexOf(args,"-weaponPickupReview")+1];Directory.CreateDirectory(directory);
            AudioListener.volume=0;Application.runInBackground=true;ground=GetComponent<ProvingGround>();
            yield return new WaitForSecondsRealtime(1);ground.enabled=false;yield return Capture("00-main-menu");pad=InputSystem.AddDevice<Gamepad>();NewMatch();
            Require(ground.Session.WeaponPickups.Length==6&&ground.Session.WeaponPickups.All(p=>p.Available),"six initial objects");
            foreach(var selection in new[]{WeaponSelection.Shotgun,WeaponSelection.RocketLauncher,WeaponSelection.Cutter,WeaponSelection.Next,WeaponSelection.Previous})Tick(select:selection);
            Require(ground.Session.Life(0).SwitchRemaining==0&&ground.Session.Life(0).SelectedWeapon==WeaponId.Rifle,"locked selections");
            yield return Capture("01-rifle-only-start");checks.Add("human and bot rifle-only start; direct and cyclic locked slot rejection");
            int index=2;
            foreach(var pickup in ground.Session.WeaponPickups)
            {
                NewMatch();
                var view=pickup.Anchor+Vector3.back*3;ground.PlaceCombatReviewSeat(0,view,8);Tick();
                var model=ground.transform.Find(pickup.InstanceId+"-presentation");Require(model&&model.gameObject.activeInHierarchy,"world model missing "+pickup.InstanceId);
                var rotation=model.rotation;Advance(.2);Require(Quaternion.Angle(rotation,model.rotation)>0,"world rotation");
                Require(model.GetComponentsInChildren<Collider>().Length==0,"blocking pickup model");
                yield return Capture((index++).ToString("D2")+"-"+pickup.InstanceId+"-available");
                ground.PlaceCombatReviewSeat(0,pickup.Anchor,0);Tick();
                Require(CombatLife.Owned(ground.Session.Life(0),pickup.Weapon),"physical unlock "+pickup.InstanceId);
                Require(!ground.Session.WeaponPickups.Single(p=>p.InstanceId==pickup.InstanceId).Available,"instance disappearance");
                Require(ground.Session.WeaponPickups.Count(p=>p.Available)==5,"unrelated timer changed");
                Require(ground.Session.Life(0).SelectedWeapon==WeaponId.Rifle,"pickup auto-switched");
                var select=pickup.Weapon==WeaponId.Shotgun?WeaponSelection.Shotgun:pickup.Weapon==WeaponId.Cutter?WeaponSelection.Cutter:WeaponSelection.RocketLauncher;
                Park();Tick(select:select);Advance(ground.Session.WeaponSwitchSeconds);Tick(fire:true);Tick();
                Require(CombatLife.AmmoFor(ground.Session.Life(0),pickup.Weapon)>0,"selected resource");
                var afterFire=ground.Session.Capture();double timer=afterFire.WeaponPickups.Single(p=>p.InstanceId==pickup.InstanceId).Remaining;
                ground.SendMessage("Pause","pickup-review");yield return new WaitForSecondsRealtime(.3f);Render();Require(ground.Session.WeaponPickups.Single(p=>p.InstanceId==pickup.InstanceId).Remaining==timer,"pause advanced timer");ground.SendMessage("Resume");
                Advance(2);ground.Session.Restore(afterFire);Render();Require(ground.Session.WeaponPickups.Single(p=>p.InstanceId==pickup.InstanceId).Remaining==timer,"restore lost timer");
                Advance(timer+.01);Require(ground.Session.WeaponPickups.Single(p=>p.InstanceId==pickup.InstanceId).Available,"15 second return");
                var before=ground.Session.Life(0);ground.PlaceCombatReviewSeat(0,pickup.Anchor,0);Tick();var after=ground.Session.Life(0);
                double max=pickup.Weapon==WeaponId.Cutter?ground.CutterProfile.Get("cutter.energyCapacity"):ground.LifeProfile.Get(pickup.Weapon==WeaponId.Shotgun?"combat.startingAmmo":"rocket.startingAmmo");
                Require(pickup.Weapon==WeaponId.Cutter?after.CutterEnergy==max:CombatLife.AmmoFor(after,pickup.Weapon)==max,"refill maximum");Require(after.SelectedWeapon==before.SelectedWeapon,"refill changed selected slot");
                yield return Capture((index++).ToString("D2")+"-"+pickup.InstanceId+"-collected");
                Park();Advance(15.01);ground.PlaceCombatReviewSeat(0,pickup.Anchor,0);Tick();Require(ground.Session.WeaponPickups.Single(p=>p.InstanceId==pickup.InstanceId).Available,"full resource consumed object");
                checks.Add(pickup.InstanceId+": physical first/refill pickup, full-resource retention, hover/rotation, nonblocking model, pause/restore and 15s independent lifecycle");
            }
            NewMatch();var west=ground.Session.WeaponPickups.Single(x=>x.InstanceId=="shotgun-west");var north=ground.Session.WeaponPickups.Single(x=>x.InstanceId=="pulse-north");
            ground.PlaceCombatReviewSeat(0,west.Anchor,0);Tick();Park();Advance(4);ground.PlaceCombatReviewSeat(0,north.Anchor,0);Tick();Park();
            Require(ground.Session.WeaponPickups.Count(x=>!x.Available)==2,"independent simultaneous cooldowns");
            double westRemaining=ground.Session.WeaponPickups.Single(x=>x.InstanceId==west.InstanceId).Remaining;Advance(westRemaining+.001);
            Require(ground.Session.WeaponPickups.Single(x=>x.InstanceId==west.InstanceId).Available&&!ground.Session.WeaponPickups.Single(x=>x.InstanceId==north.InstanceId).Available,"timers returned together");
            yield return Capture("13b-independent-timers");checks.Add("two staggered collections return independently; six instances do not share a periodic spawn");
            // Fractional charge and preserved cooldown during an unfinished switch.
            NewMatch();var cutter=ground.Session.WeaponPickups.First(x=>x.Weapon==WeaponId.Cutter);ground.PlaceCombatReviewSeat(0,cutter.Anchor,0);Tick();Park();Advance(15.01);
            var snap=ground.Session.Capture();snap.Lives[0].CutterEnergy=29.95;ground.Session.Restore(snap);
            Tick(fire:true);Tick(select:WeaponSelection.Cutter);var beforePickup=ground.Session.Life(0);ground.PlaceCombatReviewSeat(0,cutter.Anchor,0);Tick();
            var filled=ground.Session.Life(0);Require(filled.CutterEnergy==30&&filled.SwitchRemaining>0&&filled.SwitchRemaining<beforePickup.SwitchRemaining,"fractional refill/pending switch");
            Require(Math.Abs(filled.RifleCooldownRemaining-Math.Max(0,beforePickup.RifleCooldownRemaining-.02))<.00001,"refill reset cooldown");yield return Capture("14-refill-during-switch");
            ground.Session.ApplyDamage(0,filled.Life,100);Advance(ground.LifeProfile.Get("combat.killcamSeconds")+.02);
            Require(!ground.Session.Life(0).Dead&&!ground.Session.Life(0).CutterOwned&&ground.Session.Life(0).CutterEnergy==0&&ground.Session.Life(0).SelectedWeapon==WeaponId.Rifle,"respawn rifle only");
            yield return Capture("15-rifle-only-respawn");checks.Add("fractional Cutter refill preserves pending switch and fire cooldown; death/respawn clears owned resources");
            NewMatch();var pulse=ground.Session.WeaponPickups.First(x=>x.Weapon==WeaponId.RocketLauncher);ground.PlaceCombatReviewSeat(0,new Vector3(pulse.Anchor.x,0,pulse.Anchor.z),0);Tick();
            Require(!ground.Session.Life(0).RocketOwned&&ground.Session.WeaponPickups.Single(x=>x.InstanceId==pulse.InstanceId).Available,"pickup through upper floor");
            yield return Capture("16-below-pulse-no-collection");ground.SendMessage("Repeat");Render();Require(ground.Session.WeaponPickups.All(x=>x.Available)&&!ground.Session.Life(0).RocketOwned&&!ground.Session.Life(0).CutterOwned,"Repeat inventory and pickups");checks.Add("no pickup through upper balcony floor; actual Repeat resets all six objects and rifle-only inventory");
            // Existing planner receives only useful available pickup objectives, walks to the instance and unlocks normally.
            var roster=NativeMatchRoster.Ffa(2);
            var metadata=new[]{new NativeParticipantInfo(NativeParticipantKind.LocalHuman,"human",NativeParticipantColors.For(0),-1),new NativeParticipantInfo(NativeParticipantKind.Bot,"bot",NativeParticipantColors.For(1),1)};
            ground.StartBotReview(new NativeMatchComposition(roster,metadata,new[]{0}),20260929);ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,0),0);
            ground.PlaceCombatReviewSeat(1,new Vector3(-33,0,-5),0);Render();bool acquired=false,used=false;
            for(int i=0;i<3000;i++)
            {
                var actions=new LocalAction[2];ground.BotDriver.ProduceActions(actions,.02f);ground.Session.Tick(actions,.02f);
                acquired|=ground.Session.Life(1).ShotgunOwned;used|=ground.Session.Life(1).SelectedWeapon!=WeaponId.Rifle;
                if(acquired)break;
            }
            Require(acquired,"bot failed to physically acquire nearby weapon objective");ground.PlaceCombatReviewSeat(0,ground.Session.Pose(1).Position+Vector3.back*3,0);Render();yield return Capture("17-bot-physical-pickup");checks.Add("bot walked to nearby available weapon objective and physically unlocked Shotgun; no free weapons");
            // Expose a visible rival to exercise use of that collected slot at close range.
            ground.PlaceCombatReviewSeat(0,new Vector3(-33,0,5),0,180);ground.PlaceCombatReviewSeat(1,new Vector3(-33,0,0),0);
            for(int i=0;i<250;i++){var actions=new LocalAction[2];ground.BotDriver.ProduceActions(actions,.02f);ground.Session.Tick(actions,.02f);used|=ground.Session.Life(1).SelectedWeapon==WeaponId.Shotgun;}
            Require(used,"bot did not select acquired weapon");yield return Capture("18-bot-uses-collected-weapon");checks.Add("bot selected acquired Shotgun in visible close encounter");
            ground.SendMessage("Menu");while(ground.SetupBotCount>0)ground.RemoveBot(0);
            var pads=new[]{pad,InputSystem.AddDevice<Gamepad>(),InputSystem.AddDevice<Gamepad>(),InputSystem.AddDevice<Gamepad>()};ground.StartCombatReview(pads,backgroundDiagnostic:true);
            var points=ground.Session.WeaponPickups;
            ground.PlaceCombatReviewSeat(0,points.Single(x=>x.InstanceId=="shotgun-west").Anchor,0,90);
            ground.PlaceCombatReviewSeat(1,points.Single(x=>x.InstanceId=="pulse-north").Anchor,0,180);
            ground.PlaceCombatReviewSeat(2,points.Single(x=>x.InstanceId=="cutter-north").Anchor,0,180);
            ground.PlaceCombatReviewSeat(3,new Vector3(0,-1.2f,-5),0);
            Tick();var fourActions=new LocalAction[4];fourActions[0].SelectWeapon=WeaponSelection.Shotgun;fourActions[1].SelectWeapon=WeaponSelection.RocketLauncher;fourActions[2].SelectWeapon=WeaponSelection.Cutter;fourActions[3].SelectWeapon=WeaponSelection.Next;
            ground.Session.Tick(fourActions,.02f);Advance(ground.Session.WeaponSwitchSeconds);
            Require(ground.Session.Life(0).SelectedWeapon==WeaponId.Shotgun&&ground.Session.Life(1).SelectedWeapon==WeaponId.RocketLauncher&&ground.Session.Life(2).SelectedWeapon==WeaponId.Cutter&&ground.Session.Life(3).SelectedWeapon==WeaponId.Rifle,"four independent slot states");
            yield return Capture("19-four-seat-independent-inventory");checks.Add("four native viewports retain independent Rifle/Shotgun/Pulse/Cutter inventory and slot selection; synthetic assigned devices, not physical-controller acceptance");
            foreach(var extra in pads.Skip(1))InputSystem.RemoveDevice(extra);
            File.WriteAllText(Path.Combine(directory,"checks.json"),JsonUtility.ToJson(new Results{arena=ground.Session.ArenaIdentity,seed=20260929,checks=checks.ToArray(),note="Muted native Development Player; scripted placement and synthetic device. Human acceptance remains separate."},true));
            Debug.Log("WEAPON_PICKUP_REVIEW_PASS");InputSystem.RemoveDevice(pad);Application.Quit();
        }
        [Serializable]sealed class Results{public string arena,note;public uint seed;public string[] checks;}
    }
}
#endif

#endif
