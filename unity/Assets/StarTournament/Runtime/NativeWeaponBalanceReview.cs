#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace StarTournament.ProvingGround
{
    /// <summary>Bounded muted native diagnostics; never physical input or performance acceptance.</summary>
    public sealed class NativeWeaponBalanceReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad[] pads;InputSettings.BackgroundBehavior previousBackground;bool backgroundChanged;
        [Serializable] sealed class MatchResult
        {
            public uint seed;public string mode,arena,profile;public double seconds;public bool finished;
            public int[] shots=new int[4],kills=new int[4],ownedTicks=new int[4],selectedTicks=new int[4];public float[] damage=new float[4];public int deaths,respawns,pickups;
            public NativeMatchSnapshot standings;
        }
        void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        IEnumerator Capture(string name){Render();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.2f);}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();directory=args[Array.IndexOf(args,"-weaponBalanceReview")+1];Directory.CreateDirectory(directory);
            ground=GetComponent<ProvingGround>();AudioListener.volume=0;Application.runInBackground=true;yield return new WaitForSecondsRealtime(1);ground.enabled=false;
            Require(ground.CombatProfile.Get("rifle.damage")==10&&ground.CombatProfile.Get("shot.damage")==85&&ground.CombatProfile.Get("shot.pellets")==18,"Shipped weapon defaults stale");
            Require(ground.LifeProfile.Get("rifle.startingAmmo")==200&&ground.LifeProfile.Get("weapon.switchSeconds")==.5f&&ground.LifeProfile.Get("damageBoost.multiplier")==1.5f,"Shipped life defaults stale");
            for(int run=0;run<(args.Contains("-weaponBalanceVisualOnly")?0:4);run++)
            {
                bool teams=run>=2;uint seed=202610021u+(uint)run;
                var assignments=Enumerable.Range(0,8).Select(i=>i%2==0?NativeTeam.TeamA:NativeTeam.TeamB).ToArray();
                var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,assignments):NativeMatchRoster.Ffa(8);
                var metadata=Enumerable.Range(0,8).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Бот "+(i+1),teams?NativeStandingsView.TeamColor(assignments[i],false):NativeParticipantColors.For(i),1)).ToArray();
                ground.StartBotReview(new NativeMatchComposition(roster,metadata,new[]{0}),seed);Require(ground.Running,"Bot match failed to start");
                var session=ground.Session;var result=new MatchResult{seed=seed,mode=roster.Mode.ToString(),arena=session.ArenaIdentity,profile=session.DesignProfile.Hash};
                session.ShotResolved+=n=>result.shots[(int)n.Weapon-1]++;
                session.Damaged+=n=>{if(n.Impact.Valid)result.damage[(int)n.Impact.Weapon-1]+=n.HealthLost+n.ArmorLost;};
                session.Died+=n=>{result.deaths++;if(n.Impact.Valid)result.kills[(int)n.Impact.Weapon-1]++;};session.Respawned+=_=>result.respawns++;session.PickupCollected+=(seat,id,kind)=>result.pickups++;
                float dt=1f/ground.Profile.Get("simulation.fixedTickHz");var actions=new LocalAction[8];int tick=0;
                while(session.Time<600&&session.Match.Phase!=NativeMatchPhase.Finished)
                {
                    Array.Clear(actions,0,actions.Length);
                    for(int seat=0;seat<8;seat++){var state=session.Life(seat);if(state.Dead)continue;for(int slot=0;slot<4;slot++)if(CombatLife.Owned(state,(WeaponId)(slot+1)))result.ownedTicks[slot]++;result.selectedTicks[(int)state.SelectedWeapon-1]++;}
                    ground.BotDriver.ProduceActions(actions,dt);session.Tick(actions,dt);
                    for(int i=0;i<8;i++)Require(session.Pose(i).Position.y>-2,"Bot fell outside authored arena");
                    if(++tick%500==0)yield return null;
                }
                result.seconds=session.Time;result.finished=session.Match.Phase==NativeMatchPhase.Finished;result.standings=session.Match.Read();
                File.WriteAllText(Path.Combine(directory,"bots-"+run+".json"),JsonUtility.ToJson(result,true));
                Require(result.finished&&result.deaths>0&&result.respawns>0&&result.damage.Sum()>0,"Natural bot match did not produce terminal combat");
                if(!args.Contains("-weaponBalanceBotsOnly"))yield return Capture("bots-"+run+"-terminal");
            }
            if(args.Contains("-weaponBalanceBotsOnly")){File.WriteAllText(Path.Combine(directory,"bots-complete.json"),"{\"matches\":4,\"allBots\":true,\"muted\":true}");Application.Quit();yield break;}
            ground.SendMessage("ToMainMenu");yield return null;
            ground.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="main-action-2").onClick.Invoke();yield return null;
            foreach(string group in new[]{"rifle","shotgun","rocket","damage-boost","cutter"})
            {ground.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="group-"+group).onClick.Invoke();yield return null;yield return Capture("lab-"+group);}
            ground.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="lab-back").onClick.Invoke();
            previousBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;backgroundChanged=true;
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();foreach(var pad in pads)if(!pad.enabled)InputSystem.EnableDevice(pad);
            foreach(int seats in new[]{1,4})
            {
                ground.StartCombatReview(pads.Take(seats).ToArray(),backgroundDiagnostic:true,ensureOpponent:true);var session=ground.Session;
                foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
                {
                    for(int i=0;i<session.ParticipantCount;i++)ground.PlaceCombatReviewSeat(i,new Vector3(i<2?0:i==2?-.9f:.9f,-1.2f,i==0?2:i==1?5:i<4?1:-2-i),i==0?13:0,i==0?0:180);
                    var snapshot=session.Capture();snapshot.Lives[0].SelectedWeapon=weapon;snapshot.Lives[0].Ammo=weapon==WeaponId.Cutter?30:weapon==WeaponId.Rifle?200:20;snapshot.Lives[0].RifleAmmo=200;snapshot.Lives[0].ShotgunAmmo=20;snapshot.Lives[0].RocketAmmo=20;snapshot.Lives[0].CutterEnergy=30;
                    snapshot.Lives[0].ShotgunOwned=true;snapshot.Lives[0].RocketOwned=true;snapshot.Lives[0].CutterOwned=true;
                    snapshot.Lives[1].Health=100;snapshot.Lives[1].Armor=0;session.Restore(snapshot);session.Tick(new LocalAction[session.ParticipantCount],.02f);
                    // Aim the diagnostic fixture at the actual body center after native motor placement.
                    var feet=session.Pose(0).Position;var center=session.Pose(1).Position+Vector3.up*ground.Profile.Get("player.capsule.height")*ground.CombatProfile.Get("zone.torsoY");
                    var delta=center-(feet+Vector3.up*ground.Profile.Get("camera.eyeHeight"));
                    ground.PlaceCombatReviewSeat(0,feet,-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);
                    var actions=new LocalAction[session.ParticipantCount];actions[0].Fire=true;session.Tick(actions,.02f);Render();
                    if(weapon==WeaponId.Cutter){actions[0].Fire=false;actions[0].FireHeld=true;} // One press, then held fire preserves beam growth.
                    for(int tick=0;tick<7;tick++){session.Tick(weapon==WeaponId.Cutter?actions:new LocalAction[session.ParticipantCount],.02f);Render();}
                    Require(session.Life(1).Health<100,"Native weapon did not contact target: "+weapon);yield return Capture("weapon-"+weapon+"-"+seats+"-views");
                }
                // Existing viewmodel gesture reads authoritative .5s timer.
                var select=new LocalAction[session.ParticipantCount];select[0].SelectWeapon=WeaponSelection.Rifle;session.Tick(select,.02f);
                for(int tick=0;tick<12;tick++){session.Tick(new LocalAction[session.ParticipantCount],.02f);Render();}
                yield return Capture("switch-mid-"+seats+"-views");
                for(int tick=0;tick<13;tick++){session.Tick(new LocalAction[session.ParticipantCount],.02f);Render();}
                Require(session.Life(0).SelectedWeapon==WeaponId.Rifle&&session.Life(0).SwitchRemaining==0,"Native switch not ready after25ticks");
            }
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"muted\":true,\"viewports\":[1,4],\"physicalAcceptance\":false}");Debug.Log("WEAPON_BALANCE_REVIEW_COMPLETE");Application.Quit();
        }
        void OnDestroy(){if(backgroundChanged)InputSystem.settings.backgroundBehavior=previousBackground;if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
    }
}

#endif
