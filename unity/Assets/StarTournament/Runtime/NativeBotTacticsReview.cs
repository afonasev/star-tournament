#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    public sealed class NativeBotTacticsReview:MonoBehaviour
    {
        [Serializable] public sealed class BotRow
        {public int participant,difficulty,shots,rifleShots,shotgunShots,pulseShots,beamStarts,damagingShots,collections,incidentalCollections,heal,armor,speed,boost,weapons,objectives,unlocks,refills,switches,rocketRejected;public double aliveSeconds,idleSeconds,blockedSeconds;public NativeStanding standing;}
        [Serializable] public sealed class Run
        {public string mode,pair,set;public uint seed;public int permutation;public int maxQueries;public double seconds,cpuMilliseconds,maxDriverMilliseconds;public BotRow[] bots;public string arena,behavior,perception,evaluation;}
        [Serializable] sealed class State
        {public string label;public uint seed;public bool muted,focused,running;public int width,height;public double time;public NativeCompositionSnapshot composition;public ParticipantState[] poses;public CombatLifeState[] lives;public NativeBotPlannerState[] planners;public double[] damageRemaining;public NativeBotPerceptionSnapshot perception;public NativeBotPickupSnapshot pickups;}
        ProvingGround ground;string directory; readonly List<double> frameMs=new List<double>();double previous; bool ordinary;
        public static readonly uint[] ControlSeeds={931001,931019,931037,931051,931069,931087,931109,931127};
        public static readonly uint[] TuningSeeds={271001,271019,271037,271051};
        public static NativeMatchComposition Composition(bool teams,NativeBotDifficulty high,NativeBotDifficulty low,int permutation)
        {
            var team=new[]{NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamA,NativeTeam.TeamB};
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,team):NativeMatchRoster.Ffa(4);
            var rows=Enumerable.Range(0,4).Select(p=>new NativeParticipantInfo(NativeParticipantKind.Bot,"AI "+p,NativeStandingsView.Identity(roster.Read(),p,false),(int)((p+permutation)%2==0?high:low))).ToArray();
            return new NativeMatchComposition(roster,rows,new[]{0});
        }
        void Update(){if(ordinary&&ground.Running){double now=Time.realtimeSinceStartupAsDouble;if(previous>0)frameMs.Add((now-previous)*1000);previous=now;}else previous=0;}
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();Application.runInBackground=true;var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-botTacticsEvidence");
            if(flag<0||flag+1>=args.Length||!Path.IsPathFullyQualified(args[flag+1]))throw new ArgumentException("Absolute bot tactics evidence required");
            directory=args[flag+1];Directory.CreateDirectory(directory);
            // Batch evaluation has no rendered splash/focus cycle. Ordinary PNG capture still requires focus.
            if(!Application.isBatchMode)while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            if(args.Contains("-damageBoostAggressionReview"))
            {yield return DamageBoostAggression();Application.Quit(0);yield break;}
            if(args.Contains("-botTacticsEvaluation"))
            {
                bool control=args.Contains("-botTacticsControl");var seeds=control?ControlSeeds:TuningSeeds;
                foreach(bool teams in new[]{false,true})foreach(var pair in new[]{new[]{NativeBotDifficulty.Hard,NativeBotDifficulty.Normal},new[]{NativeBotDifficulty.Normal,NativeBotDifficulty.Easy}})
                foreach(uint seed in seeds)for(int permutation=0;permutation<2;permutation++)
                {
                    string name=(teams?"teams":"ffa")+"-"+pair[0]+"-"+pair[1]+"-"+seed+"-"+permutation;
                    var config=ground.Configuration;config.TargetEnabled=false;ground.ConfigureBotTacticsReview(config);
                    ground.StartBotReview(Composition(teams,pair[0],pair[1],permutation),seed);ground.enabled=false;
                    if(!File.Exists(Path.Combine(directory,"frozen-ai.json")))File.WriteAllText(Path.Combine(directory,"frozen-ai.json"),JsonUtility.ToJson(ground.BotDriver.Capture(),true));
                    var session=ground.Session;var driver=ground.BotDriver;float dt=1/ground.Profile.Get("simulation.fixedTickHz");var actions=new LocalAction[4];
                    var rows=Enumerable.Range(0,4).Select(p=>new BotRow{participant=p,difficulty=ground.Composition.Participant(p).Difficulty}).ToArray();
                    session.Fired+=(p,d)=>{if(d>0)rows[p].damagingShots++;};
                    session.RifleBulletHit+=e=>{if(e.AppliedDamage>0)rows[e.Bullet.Owner].damagingShots++;};
                    session.ShotResolved+=shot=>{var r=rows[shot.Shooter];r.shots++;switch(shot.Weapon){case WeaponId.Rifle:r.rifleShots++;break;case WeaponId.Shotgun:r.shotgunShots++;break;case WeaponId.RocketLauncher:r.pulseShots++;break;case WeaponId.Cutter:r.beamStarts++;break;}};
                    session.PickupCollected+=(p,id,kind)=>{var r=rows[p];r.collections++;if(driver.Planner(p).PickupId!=id)r.incidentalCollections++;switch(kind){case NativeBotPickupKind.Heal:r.heal++;break;case NativeBotPickupKind.Armor:r.armor++;break;case NativeBotPickupKind.Speed:r.speed++;break;case NativeBotPickupKind.Damage:r.boost++;break;case NativeBotPickupKind.Weapon:r.weapons++;break;}};
                    double cpu=0,maxCpu=0;int maxQueries=0,ticks=0;double limit=ProvingProfile.CreateBotEvaluationDefault().Get("bots.evaluation.seconds");
                    while(session.Time<limit&&session.Match.Phase!=NativeMatchPhase.Finished)
                    {
                        Array.Clear(actions,0,4);driver.ProduceActions(actions,dt);cpu+=driver.LastMilliseconds;maxCpu=Math.Max(maxCpu,driver.LastMilliseconds);maxQueries=Math.Max(maxQueries,driver.LastQueries);
                        for(int p=0;p<4;p++)if(!session.Life(p).Dead){rows[p].aliveSeconds+=dt;if(actions[p].Move.sqrMagnitude==0&&!actions[p].Fire&&!actions[p].FireHeld)rows[p].idleSeconds+=dt;if(driver.Planner(p).NavigationStatus==NativeNavigationStatus.Blocked)rows[p].blockedSeconds+=dt;}
                        session.Tick(actions,dt);if(++ticks%300==0)yield return null;
                    }
                    var standings=session.Match.Read().Standings;
                    foreach(var r in rows){var planner=driver.Planner(r.participant);r.standing=standings.Single(s=>s.Seat==r.participant);r.objectives=planner.PickupDecisions;r.unlocks=planner.UnlockDecisions;r.refills=planner.RefillDecisions;r.switches=planner.WeaponDecisions;r.rocketRejected=planner.RocketRejected;}
                    var result=new Run{mode=teams?"Teams":"FFA",pair=pair[0]+">"+pair[1],set=control?"control":"tuning",seed=seed,permutation=permutation,seconds=session.Time,cpuMilliseconds=cpu,maxDriverMilliseconds=maxCpu,maxQueries=maxQueries,bots=rows,arena=session.ArenaIdentity,behavior=ground.BotBehaviorProfile.Id+"@"+ground.BotBehaviorProfile.Version,perception=ground.BotPerceptionProfile.Id+"@"+ground.BotPerceptionProfile.Version,evaluation="native-bot-evaluation-v1@1"};
                    File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(result,true));Debug.Log("BOT_TACTICS_EVALUATION_RUN "+name);ground.enabled=true;yield return null;
                }
                File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"status\":\"SERIES_COMPLETE_METRIC_GATE_REQUIRES_ANALYSIS\"}");Application.Quit(0);yield break;
            }
            ordinary=true;
            foreach(bool teams in new[]{false,true})
            {
                ground.StartBotReview(NativeBotBehaviorReview.Mixed(1,teams),TuningSeeds[0]);
                bool died=false,respawned=false;ground.Session.Died+=_=>died=true;ground.Session.Respawned+=_=>respawned=true;
                float waited=0;while(waited<45&&(!died||!respawned)){yield return new WaitForSeconds(1);waited++;if(died&&!respawned)yield return Capture(teams?"teams-death":"ffa-death",TuningSeeds[0]);}
                if(!died||!respawned)throw new InvalidOperationException("No natural death and respawn in ordinary QA");
                yield return Capture(teams?"teams-respawn":"ffa-respawn",TuningSeeds[0]);yield return Capture(teams?"mixed-teams":"mixed-ffa",TuningSeeds[0]);
                ground.SetParticipantReviewStandings(true);yield return Capture(teams?"teams-standings":"ffa-standings",TuningSeeds[0]);ground.SetParticipantReviewStandings(false);
            }
            ground.StartBotReview(Composition(false,NativeBotDifficulty.Hard,NativeBotDifficulty.Easy,0),TuningSeeds[1]);yield return new WaitForSeconds(15);yield return Capture("ai-viewport",TuningSeeds[1]);
            var old=ground.BotDriver;var saved=old.Capture();old.Restore(saved);yield return Capture("restored",TuningSeeds[1]);
            ground.SendMessage("Pause","Bot tactics review");double time=ground.Session.Time;int pausedTicks=old.Ticks;yield return new WaitForSecondsRealtime(.3f);
            if(ground.Session.Time!=time||old.Ticks!=pausedTicks)throw new InvalidOperationException("AI clock advanced in pause");yield return Capture("paused",TuningSeeds[1]);
            Button("Продолжить").onClick.Invoke();yield return new WaitForSeconds(1);yield return Capture("resumed",TuningSeeds[1]);
            Button("Повторить матч").onClick.Invoke();yield return new WaitForSeconds(1);if(old==ground.BotDriver)throw new InvalidOperationException("Repeat reused AI");yield return Capture("repeat",TuningSeeds[1]);
            File.WriteAllLines(Path.Combine(directory,"frames-ms.txt"),frameMs.Select(f=>f.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"status\":\"NATIVE_TACTICS_REVIEW_PASS\",\"muted\":true,\"physical_controller_acceptance\":false,\"target_hardware_performance_acceptance\":false}");Application.Quit(0);
        }
        IEnumerator DamageBoostAggression()
        {
            // Labelled state/placement fixture exercises the actual driver, FOV/LOS and native motor.
            // Natural match completion is covered separately by the full PlayMode gate.
            AudioListener.volume=0;
            foreach(var difficulty in new[]{NativeBotDifficulty.Easy,NativeBotDifficulty.Normal,NativeBotDifficulty.Hard})
            {
                ground.StartBotReview(Composition(false,difficulty,difficulty,0),TuningSeeds[1]);
                ground.FullHealReviewManualTick=true;
                var floorSolid=ground.GetComponentsInChildren<ProvingArena>().Single().Definition.Solids.Single(s=>s.Id=="foundation-north");
                float floor=floorSolid.Position.y+floorSolid.Size.y/2;
                ground.PlaceCombatReviewSeat(0,new Vector3(-8,floor,26),0,0);
                ground.PlaceCombatReviewSeat(1,new Vector3(-8,floor,30),0,180);
                ground.PlaceCombatReviewSeat(2,new Vector3(-30,floor,-30),0);
                ground.PlaceCombatReviewSeat(3,new Vector3(30,floor,-30),0);
                var session=ground.Session;var driver=ground.BotDriver;var snapshot=session.Capture();
                snapshot.Lives[0].Health=10;snapshot.DamageRemaining[0]=ground.LifeProfile.Get("damageBoost.durationSeconds");session.Restore(snapshot);
                var actions=new LocalAction[4];float dt=1/ground.Profile.Get("simulation.fixedTickHz");
                void Step()
                {
                    Array.Clear(actions,0,actions.Length);driver.ProduceActions(actions,dt);
                    // Opponents are stationary in this fixture so the intended strength remains reviewable.
                    for(int p=1;p<actions.Length;p++)actions[p]=default;
                    session.Tick(actions,dt);
                }
                Step();Step();yield return null;
                var expected=difficulty==NativeBotDifficulty.Hard?NativeBotIntent.Retreat:NativeBotIntent.Engage;
                if(driver.Planner(0).Intent!=expected)throw new InvalidOperationException("Boosted "+difficulty+" expected "+expected+" but got "+driver.Planner(0).Intent);
                yield return Capture("boost-"+difficulty+"-outmatched-placement-fixture",TuningSeeds[1]);
                if(difficulty==NativeBotDifficulty.Hard)
                {
                    snapshot=session.Capture();snapshot.Lives[1].Health=5;session.Restore(snapshot);
                    // Direct knowledge still obeys the difficulty's sampling cadence.
                    for(int i=0;i<30&&driver.Planner(0).Intent!=NativeBotIntent.Engage;i++)Step();yield return null;
                    if(driver.Planner(0).Intent!=NativeBotIntent.Engage)throw new InvalidOperationException("Veteran did not resume favorable boosted fight");
                    yield return Capture("boost-Hard-favorable-placement-fixture",TuningSeeds[1]);
                }
                snapshot=session.Capture();snapshot.DamageRemaining[0]=0;snapshot.Lives[1].Health=100;session.Restore(snapshot);Step();yield return null;
                if(driver.Planner(0).Capture().DamageBoostActive||difficulty!=NativeBotDifficulty.Hard&&driver.Planner(0).Intent!=NativeBotIntent.Retreat)
                    throw new InvalidOperationException("Normal low-health tactics did not return at boost expiry");
                yield return Capture("boost-"+difficulty+"-expired-placement-fixture",TuningSeeds[1]);
            }
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"status\":\"DAMAGE_BOOST_AGGRESSION_PLAYER_PASS\",\"muted\":true,\"fixture\":\"placement-and-state\",\"humanAcceptance\":false}");
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Capture(string label,uint seed)
        {
            while(!Application.isFocused)yield return null;yield return new WaitForEndOfFrame();
            var data=new State{label=label,seed=seed,muted=AudioListener.volume==0,focused=Application.isFocused,running=ground.Running,width=Screen.width,height=Screen.height,time=ground.Session.Time,composition=ground.Composition.Read(),poses=Enumerable.Range(0,ground.Session.ParticipantCount).Select(ground.Session.Pose).ToArray(),lives=Enumerable.Range(0,ground.Session.ParticipantCount).Select(ground.Session.Life).ToArray(),damageRemaining=Enumerable.Range(0,ground.Session.ParticipantCount).Select(ground.Session.DamageBoostRemaining).ToArray(),planners=Enumerable.Range(0,ground.Session.ParticipantCount).Select(p=>ground.BotDriver.Planner(p)?.Capture()).ToArray(),perception=ground.BotDriver.Perception.Capture(),pickups=ground.BotDriver.Pickups.Capture()};
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(data,true));ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return null;
        }
    }
}
#endif

#endif
