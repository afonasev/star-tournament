using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeBotBehaviorTests
    {
        sealed class Stats { public int Shots, Damaged, Deaths, Respawns, MaxQueries; public double FirstContact=-1, CpuMilliseconds, MaxDriverMilliseconds; }
        Scene scene; ProvingGround ground;
        Button Button(string name) => ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);

        static NativeMatchComposition BotsAndHuman(bool teams, int bots=7)
        {
            int count=bots+1;
            var assignments=Enumerable.Range(0,count).Select(i=>i%2==0?NativeTeam.TeamA:NativeTeam.TeamB).ToArray();
            var roster=teams ? new NativeMatchRoster(NativeMatchMode.Teams,assignments) : NativeMatchRoster.Ffa(count);
            var metadata=Enumerable.Range(0,count).Select(p=>new NativeParticipantInfo(p<bots?NativeParticipantKind.Bot:NativeParticipantKind.LocalHuman,
                p<bots?"Бот "+(p+1):"Игрок",teams?NativeStandingsView.TeamColor(assignments[p],false):NativeParticipantColors.For(p),p<bots?p%3:-1)).ToArray();
            return new NativeMatchComposition(roster,metadata,new[]{count-1});
        }
        IEnumerator Load(NativeMatchComposition composition,uint seed,int fixedTickHz=0)
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            if(fixedTickHz>0)ground.Profile.Set("simulation.fixedTickHz",fixedTickHz);
            ground.StartBotReview(composition,seed);ground.enabled=false;
            Assert.That(ground.Running,Is.True,string.Join(" | ",ground.GetComponentsInChildren<UnityEngine.UI.Text>(true).Select(x=>x.text)));Assert.That(ground.BotDriver,Is.Not.Null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(ground)ground.enabled=true;
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        void PlaceEight()
        {
            float floor=AuthoredPhysicsFixture.Value("fixture.floorHeight");
            float[] x={-7,-2,2,7,-7,-2,2,7};
            for(int p=0;p<8;p++) ground.PlaceCombatReviewSeat(p,new Vector3(x[p],floor,p<4?30:34),0,p<4?0:180);
            Physics.SyncTransforms();
        }
        Stats Drive(float maximumSeconds)
        {
            var stats=new Stats();var session=ground.Session;var driver=ground.BotDriver;float dt=1f/ground.Profile.Get("simulation.fixedTickHz");
            session.Fired+=(participant,damage)=>{stats.Shots++;if(damage>0)stats.Damaged++;};session.RifleBulletHit+=e=>{if(e.AppliedDamage>0)stats.Damaged++;};session.Died+=_=>stats.Deaths++;session.Respawned+=_=>stats.Respawns++;
            var watch=Stopwatch.StartNew();var actions=new LocalAction[session.ParticipantCount];
            while(session.Time<maximumSeconds && (stats.Shots==0||stats.Damaged==0||stats.Deaths==0||stats.Respawns==0))
            {
                Array.Clear(actions,0,actions.Length);driver.ProduceActions(actions,dt);
                if(stats.FirstContact<0 && Enumerable.Range(0,session.ParticipantCount).Any(p=>driver.Planner(p)!=null&&driver.Perception.Read(p).Enemies.Any(e=>e.Visible)))stats.FirstContact=session.Time;
                session.Tick(actions,dt);
            }
            watch.Stop();stats.CpuMilliseconds=watch.Elapsed.TotalMilliseconds;
            UnityEngine.Debug.Log($"BOT_RUNTIME_DIAGNOSTIC mode={session.Match.Roster.Mode} firstContact={stats.FirstContact:F3} shots={stats.Shots} damaged={stats.Damaged} deaths={stats.Deaths} respawns={stats.Respawns} ticks={driver.Ticks} plannerCpuMs={stats.CpuMilliseconds:F3}");
            return stats;
        }
        static void AssertCombat(Stats stats)
        {
            Assert.That(stats.FirstContact,Is.GreaterThanOrEqualTo(0),"No filtered line-of-sight contact occurred.");
            Assert.That(stats.Shots,Is.GreaterThan(0),"No actual bot weapon action fired.");Assert.That(stats.Damaged,Is.GreaterThan(0),"No actual shot applied damage.");
            Assert.That(stats.Deaths,Is.GreaterThan(0),"No actual combat death occurred.");Assert.That(stats.Respawns,Is.GreaterThan(0),"No actual respawn occurred.");
        }
        IEnumerator NaturalTerminalSmoke(bool teams)
        {
            yield return Load(BotsAndHuman(teams),teams?157u:151u);var stats=new Stats();var session=ground.Session;var driver=ground.BotDriver;float dt=1f/ground.Profile.Get("simulation.fixedTickHz");var actions=new LocalAction[session.ParticipantCount];
            session.Fired+=(p,d)=>{stats.Shots++;if(d>0)stats.Damaged++;};session.RifleBulletHit+=e=>{if(e.AppliedDamage>0)stats.Damaged++;};session.Died+=_=>stats.Deaths++;session.Respawned+=_=>stats.Respawns++;
            var watch=Stopwatch.StartNew();int ticks=0;while(session.Time<600&&session.Match.Phase!=NativeMatchPhase.Finished)
            {
                Array.Clear(actions,0,actions.Length);driver.ProduceActions(actions,dt);stats.MaxDriverMilliseconds=Math.Max(stats.MaxDriverMilliseconds,driver.LastMilliseconds);stats.MaxQueries=Math.Max(stats.MaxQueries,driver.LastQueries);session.Tick(actions,dt);ticks++;
                for(int participant=0;participant<session.ParticipantCount;participant++)Assert.That(session.Pose(participant).Position.y,Is.GreaterThan(-2),$"Participant {participant} fell through authored support at tick {ticks}: {session.Pose(participant).Position}");
                if(ticks%500==0)yield return null;
            }
            watch.Stop();var bounds=ground.GetComponentInChildren<ProvingArena>().Definition.Solids;float w=bounds.Max(x=>Mathf.Abs(x.Position.x)+x.Size.x/2)*2,d=bounds.Max(x=>Mathf.Abs(x.Position.z)+x.Size.z/2)*2,h=bounds.Max(x=>x.Position.y+x.Size.y/2);
            for(int p=0;p<session.ParticipantCount;p++){var pose=session.Pose(p).Position;Assert.That(float.IsNaN(pose.x)||float.IsNaN(pose.y)||float.IsNaN(pose.z),Is.False);Assert.That(Mathf.Abs(pose.x),Is.LessThanOrEqualTo(w/2+1));Assert.That(Mathf.Abs(pose.z),Is.LessThanOrEqualTo(d/2+1));Assert.That(pose.y,Is.InRange(-1.3f,h+1));}
            UnityEngine.Debug.Log($"BOT_NATURAL_SMOKE mode={session.Match.Roster.Mode} sim={session.Time:F2} wallMs={watch.Elapsed.TotalMilliseconds:F2} shots={stats.Shots} damage={stats.Damaged} deaths={stats.Deaths} respawns={stats.Respawns} maxDriverMs={stats.MaxDriverMilliseconds:F3} maxQueries={stats.MaxQueries}");
            Assert.That(session.Match.Phase,Is.EqualTo(NativeMatchPhase.Finished));Assert.That(stats.Damaged,Is.GreaterThan(0));Assert.That(stats.Deaths,Is.GreaterThan(0));Assert.That(stats.Respawns,Is.GreaterThan(0));
        }

        [UnityTest] public IEnumerator EightParticipantFfaProducesActualBotCombatWithinSixtySimulationSeconds()
        {
            yield return Load(BotsAndHuman(false),101);PlaceEight();AssertCombat(Drive(60));
        }

        [UnityTest] public IEnumerator NaturalDefaultEightFfaReachesTerminalWithoutInvalidRuntimeState(){yield return NaturalTerminalSmoke(false);}
        [UnityTest] public IEnumerator NaturalDefaultEightTeamsReachesTerminalWithoutInvalidRuntimeState(){yield return NaturalTerminalSmoke(true);}

        [UnityTest] public IEnumerator DefaultEightParticipantAllocatorProducesUnrelocatedBotContactAndDamage()
        {
            yield return Load(BotsAndHuman(false),102);var stats=Drive(60);
            Assert.That(stats.FirstContact,Is.GreaterThanOrEqualTo(0),"The native initial allocator did not yield filtered bot contact.");
            Assert.That(stats.Shots,Is.GreaterThan(0));Assert.That(stats.Damaged,Is.GreaterThan(0),"No actual damage followed the unrelocated initial placement.");
        }

        [UnityTest] public IEnumerator EightParticipantTeamsProducesActualBotCombatWithinSixtySimulationSeconds()
        {
            yield return Load(BotsAndHuman(true),103);PlaceEight();AssertCombat(Drive(60));
        }

        [UnityTest] public IEnumerator FacingBotUsesRealDriverShotReleaseAndLifecycleKeepsFrozenBehavior()
        {
            yield return Load(BotsAndHuman(false,1),107);float floor=AuthoredPhysicsFixture.Value("fixture.floorHeight");
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,floor,26),0,0);ground.PlaceCombatReviewSeat(1,new Vector3(-8,floor,30),0,180);Physics.SyncTransforms();
            var driver=ground.BotDriver;var session=ground.Session;
            // This scenario verifies discrete shot press/release lifecycle; continuous Cutter is covered separately.
            var discrete=session.Capture();discrete.Lives[0].CutterEnergy=0;session.Restore(discrete);
            var actions=new LocalAction[2];float dt=1f/ground.Profile.Get("simulation.fixedTickHz");bool pressed=false,released=false,damaged=false;
            session.Fired+=(p,d)=>damaged|=d>0;session.RifleBulletHit+=e=>damaged|=e.AppliedDamage>0;
            for(int i=0;i<500&&!(damaged&&released);i++)
            {
                Array.Clear(actions,0,actions.Length);driver.ProduceActions(actions,dt);
                if(pressed&&!actions[0].Fire)released=true;if(actions[0].Fire)pressed=true;
                session.Tick(actions,dt);
            }
            Assert.That(pressed,Is.True,$"bot life={session.Life(0).Life} ammo={session.Life(0).Ammo} weapon={session.Life(0).SelectedWeapon} shots={session.ShotCount} targetHealth={session.Life(1).Health}");
            Assert.That(released,Is.True,"The real driver must emit a false action between presses.");
            Assert.That(damaged,Is.True,$"shots={session.ShotCount} targetHealth={session.Life(1).Health}");
            string frozen=driver.Planner(0).Capture().Configuration;ground.enabled=true;yield return new WaitForFixedUpdate();
            ground.SendMessage("Pause","bot lifecycle test");long pausedTick=session.Match.Read().Tick;double pausedTime=session.Time;
            yield return new WaitForFixedUpdate();Assert.That(session.Match.Read().Tick,Is.EqualTo(pausedTick));Assert.That(session.Time,Is.EqualTo(pausedTime));ground.enabled=false;
            Assert.That(ground.Running,Is.False);Assert.That(driver.Planner(0).Capture().Pressed,Is.False);
            ground.BotBehaviorProfile.Set("bots.easy.reactionSeconds",2);Button("Повторить матч").onClick.Invoke();yield return null;
            Assert.That(ground.BotDriver,Is.Not.SameAs(driver));Assert.That(ground.BotDriver.Planner(0).Capture().Configuration,Is.EqualTo(frozen));
            Button("В главное меню").onClick.Invoke();Button("main-action-0").onClick.Invoke();yield return null;Assert.That(ground.BotDriver,Is.Null);
        }

        [UnityTest] public IEnumerator MatchAndRepeatUseFrozenProfileOwnedFixedTickCadence()
        {
            yield return Load(BotsAndHuman(false,1),111,100);Assert.That(Time.fixedDeltaTime,Is.EqualTo(.01f).Within(.000001f));
            ground.Profile.Set("simulation.fixedTickHz",50);Button("Повторить матч").onClick.Invoke();yield return null;
            Assert.That(Time.fixedDeltaTime,Is.EqualTo(.01f).Within(.000001f));
        }

        [UnityTest] public IEnumerator NativeTacticsRejectsWallAndLowHeadroomAndAllowsSafeFloorProbe()
        {
            yield return Load(BotsAndHuman(false,1),109);var arena=ground.GetComponentInChildren<ProvingArena>();
            var tactics=new NativeBotTactics(arena,scene.GetPhysicsScene(),new NativeNavigationProvider(arena,ground.Profile,ground.BotNavigationProfile),ground.Profile,ground.CombatProfile,ground.BotBehaviorProfile);
            float floor=AuthoredPhysicsFixture.Value("fixture.floorHeight"),w=AuthoredPhysicsFixture.Value("fixture.width"),d=AuthoredPhysicsFixture.Value("fixture.depth");
            Assert.That(tactics.CanMove(new Vector3(35,4,18),Vector3.right,1),Is.False,"World wall must reject a capsule move.");
            // The spawn flank is open; probe beneath the retained north entry roof.
            Assert.That(tactics.CanJump(new ParticipantState{Position=new Vector3(-15,0,26),Grounded=true},Vector3.forward),Is.False,"Low headroom must reject a jump arc.");
            Assert.That(tactics.CanMove(arena.LowerRoutePoint,Vector3.right,.2f),Is.True,"An unobstructed lower-floor probe must remain usable.");
        }

        [UnityTest] public IEnumerator SafeNativeJumpProbeMatchesAnActualMotorAirborneAndLandingCycle()
        {
            yield return Load(BotsAndHuman(false,1),113);var arena=ground.GetComponentInChildren<ProvingArena>();
            var behavior=ProvingProfile.CreateBotBehaviorDefault();behavior.Set("bots.easy.jumpChance",1);behavior.Set("bots.easy.jumpCooldownSeconds",1);
            behavior.Set("bots.easy.strafeSeconds",.2f);behavior.Set("bots.easy.strafeWeight",1);
            var routes=new NativeNavigationProvider(arena,ground.Profile,ground.BotNavigationProfile);
            var tactics=new NativeBotTactics(arena,scene.GetPhysicsScene(),routes,ground.Profile,ground.CombatProfile,behavior);
            var start=new Vector3(19,0,8);
            var actor=new GameObject("jump-proof");actor.transform.SetParent(ground.transform);actor.layer=ProvingArena.ParticipantLayer;actor.AddComponent<CharacterController>();var motor=actor.AddComponent<CharacterMotor>();motor.Initialize(ground.Profile,start);
            float dt=1f/ground.Profile.Get("simulation.fixedTickHz");motor.Tick(new LocalAction{Move=Vector2.up},dt);var predicted=motor.State;
            Assert.That(predicted.Velocity.z,Is.GreaterThan(0));Assert.That(tactics.CanJump(predicted,Vector3.right),Is.True,"Selected lower-floor patch must permit the profile-owned jump.");bool airborne=false,landed=false;
            motor.Tick(new LocalAction{Jump=true,Move=Vector2.right},dt);Physics.SyncTransforms();
            for(int i=0;i<300;i++)
            {
                motor.Tick(new LocalAction{Move=Vector2.right},dt);Physics.SyncTransforms();
                airborne|=!motor.State.Grounded&&motor.State.Position.y>start.y+.01f;
                if(airborne&&motor.State.Grounded){landed=true;break;}
            }
            Assert.That(airborne,Is.True);Assert.That(landed,Is.True);Assert.That(routes.TryLocate(motor.State.Position,out var support),Is.True);
            Assert.That(support.Support,Is.EqualTo("support:lower-loop"));Object.Destroy(actor);
        }

        [UnityTest] public IEnumerator VisibleTargetKeepsRealStairAndRampTransitionsUnderPlannerControl()
        {
            yield return Load(BotsAndHuman(false,1),127);var arena=ground.GetComponentInChildren<ProvingArena>();
            int side=0;
            foreach(var transition in arena.Definition.Transitions.Where(t=>t.Id=="transition:west-rise"||t.Id=="transition:northwest-outer-rise"))
            {
                side++;var along=(transition.OrderedFeet.Last()-transition.OrderedFeet[0]);along.y=0;along.Normalize();
                var start=transition.OrderedFeet[0]-along*.5f;var target=transition.OrderedFeet.Last()+along*.5f;
                var actor=new GameObject("planner-transition");actor.transform.SetParent(ground.transform);actor.layer=ProvingArena.ParticipantLayer;actor.AddComponent<CharacterController>();var motor=actor.AddComponent<CharacterMotor>();motor.Initialize(ground.Profile,start);
                var routes=new NativeNavigationProvider(arena,ground.Profile,ground.BotNavigationProfile);var tactics=new NativeBotTactics(arena,scene.GetPhysicsScene(),routes,ground.Profile,ground.CombatProfile,ground.BotBehaviorProfile);
                var planner=new NativeBotPlanner(NativeBotDifficulty.Easy,(uint)(211+side),ground.BotBehaviorProfile,ground.BotNavigationProfile,routes,tactics,new NativeShotgunPolicy(ground.Profile,ground.CombatProfile),100);
                bool entered=false,expired=false,preservedAfterExpiry=false;Vector3 exit=default;float dt=1f/60f;
                for(int i=0;i<2400&&planner.NavigationStatus!=NativeNavigationStatus.Arrived&&planner.NavigationStatus!=NativeNavigationStatus.Blocked;i++)
                {
                    var frame=new NativeBotFrame{Pose=motor.State,Life=new CombatLifeState{Life=1,Health=100,Ammo=20},Knowledge=new NativeBotKnowledge{OwnLife=1,Alive=true,Enemies=expired?Array.Empty<NativeBotMemoryEntry>():new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,target),ObservedAt=0,Visible=true}}}};
                    var action=planner.Tick(i/60d,dt,frame);var state=planner.Capture();
                    if(!string.IsNullOrEmpty(state.Navigation.ActiveTransition))
                    {
                        if(!entered){entered=true;exit=state.Navigation.TransitionExit;}else Assert.That(state.Navigation.TransitionExit,Is.EqualTo(exit));
                        if(expired)preservedAfterExpiry=true; else expired=true;
                        Assert.That(action.Jump,Is.False);Assert.That(planner.NavigationStatus,Is.Not.EqualTo(NativeNavigationStatus.Recovering));
                    }
                    motor.Tick(action,dt);Physics.SyncTransforms();
                }
                Assert.That(entered,Is.True,$"side={side} never entered declared transition");Assert.That(preservedAfterExpiry,Is.True,"Expired target memory must retain the already-entered transition exit.");Assert.That(planner.NavigationStatus,Is.EqualTo(NativeNavigationStatus.Arrived));
                Assert.That(planner.Capture().Navigation.Recoveries,Is.Zero);Object.Destroy(actor);
            }
        }

        [UnityTest] public IEnumerator CloseVisibleEnemyOnOtherFloorUsesRouteInsteadOfLocalStrafe()
        {
            yield return Load(BotsAndHuman(false,1),131);var arena=ground.GetComponentInChildren<ProvingArena>();float low=AuthoredPhysicsFixture.Value("fixture.floorHeight"),high=AuthoredPhysicsFixture.Value("fixture.upperFloorHeight");
            var routes=new NativeNavigationProvider(arena,ground.Profile,ground.BotNavigationProfile);var tactics=new NativeBotTactics(arena,scene.GetPhysicsScene(),routes,ground.Profile,ground.CombatProfile,ground.BotBehaviorProfile);
            var planner=new NativeBotPlanner(NativeBotDifficulty.Easy,137,ground.BotBehaviorProfile,ground.BotNavigationProfile,routes,tactics,new NativeShotgunPolicy(ground.Profile,ground.CombatProfile),100);
            var pose=new ParticipantState{Position=new Vector3(-25,0,0),Grounded=true};var target=new Vector3(-25,4,0);
            var action=planner.Tick(0,1f/60f,new NativeBotFrame{Pose=pose,Life=new CombatLifeState{Life=1,Health=100,Ammo=20},Knowledge=new NativeBotKnowledge{OwnLife=1,Alive=true,Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,target),ObservedAt=0,Visible=true}}}});
            Assert.That(action.Move.sqrMagnitude,Is.GreaterThan(0));Assert.That(action.Jump,Is.False);Assert.That(planner.Jumps,Is.Zero);
            Assert.That(planner.Capture().Navigation.Status,Is.EqualTo(NativeNavigationStatus.Moving));
        }
    }
}
