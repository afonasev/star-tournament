using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeBotPlannerTests
    {
        sealed class Route : INativeNavigation
        {
            public string Identity => "planner-test-map";
            public bool TryLocate(Vector3 feet, out NativeNavigationPoint point) { point = new NativeNavigationPoint(feet, "floor:lower"); return true; }
            public bool ValidTransition(string transition, string exitSupport) => false;
            public bool TryRoute(Vector3 from, Vector3 goal, out NativeNavigationPoint[] points, out string failure)
            { points = new[] { new NativeNavigationPoint(goal, "floor:lower") }; failure = null; return true; }
        }
        sealed class TransitionRoute : INativeNavigation
        {
            public int Calls; public string Identity=>"transition-retry-map";
            public bool TryLocate(Vector3 feet,out NativeNavigationPoint point){point=new NativeNavigationPoint(feet,feet.z>=1.5f&&feet.y<1?"transition:ramp":feet.y>=1?"floor:upper":"floor:lower");return true;}
            public bool ValidTransition(string transition,string exit)=>transition=="transition:ramp"&&exit=="floor:upper";
            public bool TryRoute(Vector3 from,Vector3 goal,out NativeNavigationPoint[] points,out string failure){Calls++;points=new[]{new NativeNavigationPoint(new Vector3(0,0,2),"transition:ramp"),new NativeNavigationPoint(new Vector3(0,3,2),"floor:upper")};failure=null;return true;}
        }
        sealed class Tactics : INativeBotTactics
        {
            public Vector3 HiddenEnemyPosition;
            public Vector3[] Anchors { get; set; } = { Vector3.forward * 20 };
            public bool CanMove(Vector3 feet, Vector3 direction, float distance) => true;
            public bool CanJump(ParticipantState pose, Vector3 direction) => true;
            public bool Covered(Vector3 feet, Vector3 observedEnemyFeet) => true;
        }
        sealed class Weapon : INativeBotWeaponPolicy
        {
            public float Range => 100;
            public string Identity => "planner-test-weapon-v1";
            public float Rate, Error, Period;
            public LocalAction Aim(ParticipantState own, Vector3 observedFeet, double time, float seconds, float phase,
                float turnRate, float error, float period, out float residualDegrees)
            { Rate=turnRate; Error=error; Period=period; residualDegrees=0; return new LocalAction { LookDegrees = new Vector2(7, -3) }; }
        }

        static NativeBotPlanner Make(NativeBotDifficulty difficulty = NativeBotDifficulty.Easy, uint seed = 7,
            ProvingProfile behavior = null, Tactics tactics = null, Weapon weapon = null) => new NativeBotPlanner(difficulty, seed,
                behavior ?? ProvingProfile.CreateBotBehaviorDefault(), ProvingProfile.CreateNavigationDefault(), new Route(),
                tactics ?? new Tactics(), weapon ?? new Weapon(), 100);
        static NativeBotFrame Frame(int ownLife = 1, bool dead = false, int target = 2, int targetLife = 1,
            bool visible = true, float health = 100, NativeBotAlly[] allies = null) => new NativeBotFrame
        {
            Pose = new ParticipantState { Position = Vector3.zero, Grounded = true },
            Life = new CombatLifeState { Life=ownLife, Dead=dead, Health=health, Ammo=20, CooldownRemaining=0 },
            Knowledge = new NativeBotKnowledge { OwnLife=ownLife, Alive=!dead, Enemies = target < 0 ? Array.Empty<NativeBotMemoryEntry>() : new[]
            { new NativeBotMemoryEntry { Sighting = new NativeBotSighting(target,targetLife,new Vector3(0,0,10)), ObservedAt=0, Visible=visible } } },
            Allies = allies ?? Array.Empty<NativeBotAlly>()
        };
        static string Json(NativeBotPlanner planner) => JsonUtility.ToJson(planner.Capture());
        static void Same(LocalAction actual, LocalAction expected)
        {
            Assert.That(actual.Move, Is.EqualTo(expected.Move)); Assert.That(actual.LookDegrees, Is.EqualTo(expected.LookDegrees));
            Assert.That(actual.Jump, Is.EqualTo(expected.Jump)); Assert.That(actual.Fire, Is.EqualTo(expected.Fire));
        }
        static void SamePlannerStateExceptNativeRouteRevalidation(NativeBotPlannerState actual, NativeBotPlannerState expected)
        {
            Assert.That(actual.Version, Is.EqualTo(expected.Version)); Assert.That(actual.Configuration, Is.EqualTo(expected.Configuration));
            Assert.That(actual.OwnLife, Is.EqualTo(expected.OwnLife)); Assert.That(actual.Target, Is.EqualTo(expected.Target));
            Assert.That(actual.TargetLife, Is.EqualTo(expected.TargetLife)); Assert.That(actual.Anchor, Is.EqualTo(expected.Anchor));
            Assert.That(actual.StrafeSign, Is.EqualTo(expected.StrafeSign)); Assert.That(actual.Random, Is.EqualTo(expected.Random));
            Assert.That(actual.Intent, Is.EqualTo(expected.Intent)); Assert.That(actual.Time, Is.EqualTo(expected.Time));
            Assert.That(actual.NextDecision, Is.EqualTo(expected.NextDecision)); Assert.That(actual.Noticed, Is.EqualTo(expected.Noticed));
            Assert.That(actual.NextStrafe, Is.EqualTo(expected.NextStrafe)); Assert.That(actual.NextJump, Is.EqualTo(expected.NextJump));
            Assert.That(actual.RetreatUntil, Is.EqualTo(expected.RetreatUntil)); Assert.That(actual.RetreatReady, Is.EqualTo(expected.RetreatReady));
            Assert.That(actual.SupportUntil, Is.EqualTo(expected.SupportUntil)); Assert.That(actual.SupportReady, Is.EqualTo(expected.SupportReady));
            Assert.That(actual.Goal, Is.EqualTo(expected.Goal)); Assert.That(actual.HasGoal, Is.EqualTo(expected.HasGoal));
            Assert.That(actual.Pressed, Is.EqualTo(expected.Pressed)); Assert.That(actual.Personality, Is.EqualTo(expected.Personality)); Assert.That(actual.AimPhase, Is.EqualTo(expected.AimPhase));
            Assert.That(actual.Navigation.Goal, Is.EqualTo(expected.Navigation.Goal)); Assert.That(actual.Navigation.Cursor, Is.EqualTo(expected.Navigation.Cursor));
            Assert.That(actual.Navigation.Enemy, Is.EqualTo(expected.Navigation.Enemy)); Assert.That(actual.Navigation.EnemyLife, Is.EqualTo(expected.Navigation.EnemyLife));
        }

        [Test] public void ProfileCopiesAllBehaviorTuningAndDifficultyDefaultsReachWeaponPolicy()
        {
            var profile=ProvingProfile.CreateBotBehaviorDefault(); Assert.That(profile.Validate(), Is.Empty);
            Assert.That(profile.Descriptors, Has.Count.EqualTo(51));
            Assert.That(profile.Descriptors.Any(d=>d.Path.Contains("fieldOfView")||d.Path.Contains("memorySeconds")), Is.False);
            var expected = new[] { new[]{.6f,90f,12f,1.4f}, new[]{.28f,160f,5f,1f}, new[]{.12f,230f,1.5f,.7f} };
            for(int i=0;i<3;i++)
            {
                var weapon=new Weapon(); var planner=Make((NativeBotDifficulty)i, weapon:weapon);
                planner.Tick(0,.01f,Frame());
                Assert.That(profile.Get("bots."+((NativeBotDifficulty)i).ToString().ToLowerInvariant()+".reactionSeconds"), Is.EqualTo(expected[i][0]));
                Assert.That(weapon.Rate, Is.EqualTo(expected[i][1])); Assert.That(weapon.Error, Is.EqualTo(expected[i][2]));
                Assert.That(weapon.Period, Is.EqualTo(expected[i][3]));
                Assert.That(planner.Tick(expected[i][0]-.01f,.01f,Frame()).Fire, Is.False);
                Assert.That(planner.Tick(expected[i][0]+.01f,.01f,Frame()).Fire, Is.True);
            }
        }

        [Test] public void SnapshotIsCopiedRestoresIdenticalFutureAndInvalidRestoreIsAtomic()
        {
            var original=Make(seed:19); original.Tick(0,.01f,Frame()); var saved=original.Capture();
            var restored=Make(seed:19); restored.Restore(saved);
            saved.Goal=Vector3.one*999; saved.Navigation.Goal=Vector3.one*999;
            Assert.That(restored.Capture().Goal, Is.Not.EqualTo(saved.Goal));
            var next=Frame(); var a=original.Tick(.1f,.1f,next); var b=restored.Tick(.1f,.1f,next);
            Same(a,b); SamePlannerStateExceptNativeRouteRevalidation(restored.Capture(),original.Capture());
            var before=Json(restored);
            foreach(Action<NativeBotPlannerState> corrupt in new Action<NativeBotPlannerState>[] {
                s=>s.Version++, s=>s.Configuration="wrong", s=>s.Time=double.NaN, s=>s.Noticed=-1, s=>s.Navigation.Time=s.Time+.1,
                s=>s.SupportUntil=double.PositiveInfinity, s=>s.RetreatReady=double.PositiveInfinity, s=>{s.Target=-1;s.TargetLife=1;}, s=>s.Navigation=null, s=>s.Random=0 })
            {
                var invalid=restored.Capture(); corrupt(invalid);
                Assert.Throws<ArgumentException>(()=>restored.Restore(invalid)); Assert.That(Json(restored), Is.EqualTo(before));
            }
        }

        [Test] public void PlannerUsesOnlyIdenticalFilteredKnowledgeNotHiddenWorldState()
        {
            var leftTactics=new Tactics { HiddenEnemyPosition=Vector3.left*999 };
            var rightTactics=new Tactics { HiddenEnemyPosition=Vector3.right*999 };
            var left=Make(seed:31,tactics:leftTactics); var right=Make(seed:31,tactics:rightTactics);
            var frame=Frame(); Same(left.Tick(0,.01f,frame),right.Tick(0,.01f,frame));
            Same(left.Tick(.61f,.01f,frame),right.Tick(.61f,.01f,frame));
            Assert.That(Json(left), Is.EqualTo(Json(right)));
        }

        [Test] public void ReactionRestartsForNewTargetLifeAndFireHasAFalseReleaseTick()
        {
            var planner=Make(); var lifeOne=Frame(targetLife:1);
            Assert.That(planner.Tick(0,.01f,lifeOne).Fire, Is.False);
            Assert.That(planner.Tick(.59f,.01f,lifeOne).Fire, Is.False);
            Assert.That(planner.Tick(.6f,.01f,lifeOne).Fire, Is.True);
            Assert.That(planner.Tick(.61f,.01f,lifeOne).Fire, Is.False, "A release tick follows every true press.");
            Assert.That(planner.Tick(.62f,.01f,lifeOne).Fire, Is.True);
            var lifeTwo=Frame(targetLife:2);
            Assert.That(planner.Tick(.7f,.01f,lifeTwo).Fire, Is.False);
            Assert.That(planner.Tick(1.29f,.01f,lifeTwo).Fire, Is.False);
            Assert.That(planner.Tick(1.31f,.01f,lifeTwo).Fire, Is.True);
        }

        [Test] public void RetreatAndSupportExpireBeforeTheirCooldownAllowsRenewal()
        {
            var retreat=Make(seed:2); var low=Frame(health:10);
            retreat.Tick(0,.01f,low); Assert.That(retreat.Intent, Is.EqualTo(NativeBotIntent.Retreat));
            retreat.Tick(.8f,.01f,low); Assert.That(retreat.Intent, Is.EqualTo(NativeBotIntent.Engage));
            retreat.Tick(4.6f,.01f,low); Assert.That(retreat.Intent, Is.EqualTo(NativeBotIntent.Engage));

            var profile=ProvingProfile.CreateBotBehaviorDefault(); profile.Set("bots.easy.supportChance",1);
            var support=Make(seed:2,behavior:profile); var allies=new[]{new NativeBotAlly{Participant=3,Position=new Vector3(1,0,3)}};
            support.Tick(0,.01f,Frame(allies:allies)); Assert.That(support.Intent, Is.EqualTo(NativeBotIntent.Support));
            support.Tick(3.1f,.01f,Frame(allies:allies)); Assert.That(support.Intent, Is.EqualTo(NativeBotIntent.Engage));
            support.Tick(7.9f,.01f,Frame(allies:allies)); Assert.That(support.Intent, Is.EqualTo(NativeBotIntent.Engage));
        }

        [Test] public void DeathAndNewLifeClearPlannerTargetNavigationAndPressedState()
        {
            var planner=Make(); planner.Tick(0,.01f,Frame()); planner.Tick(.6f,.01f,Frame());
            Assert.That(planner.Capture().Pressed, Is.True);
            var dead=planner.Tick(.7f,.01f,Frame(dead:true,target:-1));
            Assert.That(dead.Fire, Is.False); Assert.That(planner.Intent, Is.EqualTo(NativeBotIntent.Search));
            var afterDeath=planner.Capture(); Assert.That(afterDeath.Target, Is.EqualTo(-1)); Assert.That(afterDeath.HasGoal, Is.False); Assert.That(afterDeath.Pressed, Is.False);
            planner.Tick(.8f,.01f,Frame(ownLife:2,target:-1)); var respawn=planner.Capture();
            Assert.That(respawn.OwnLife, Is.EqualTo(2)); Assert.That(respawn.Target, Is.EqualTo(-1)); Assert.That(respawn.Pressed, Is.False);
            Assert.That(respawn.Navigation.Enemy, Is.EqualTo(-1), "A fresh Search goal must not retain a stale enemy route.");
        }

        [Test] public void WeaponCapabilityCanBeSubstitutedWithoutChangingPlannerOwnership()
        {
            var weapon=new Weapon(); var planner=Make(weapon:weapon);
            var action=planner.Tick(0,.01f,Frame());
            Assert.That(action.LookDegrees, Is.EqualTo(new Vector2(7,-3))); Assert.That(weapon.Range, Is.EqualTo(100));
        }

        [Test] public void ExpiredMemoryRetriesBlockedActiveTransitionOnlyAfterBoundedDeadline()
        {
            var behavior=ProvingProfile.CreateBotBehaviorDefault();var nav=ProvingProfile.CreateNavigationDefault();nav.Set("bots.navigation.maximumRecoveries",1);
            var route=new TransitionRoute();var planner=new NativeBotPlanner(NativeBotDifficulty.Easy,41,behavior,nav,route,new Tactics(),new Weapon(),100);
            var target=new Vector3(0,3,2);var live=Frame();live.Knowledge.Enemies[0].Sighting.Position=target;
            planner.Tick(0,.1f,live);live.Pose=new ParticipantState{Position=new Vector3(0,0,2),Grounded=true};planner.Tick(.1,.1f,live);
            var locked=planner.Capture();Assert.That(locked.Navigation.ActiveTransition,Is.EqualTo("transition:ramp"));var exit=locked.Navigation.TransitionExit;
            locked.Navigation.Status=NativeNavigationStatus.Blocked;locked.Navigation.Recoveries=1;locked.RouteRetryAt=.2;planner.Restore(locked);int calls=route.Calls;
            var expired=Frame(target:-1);expired.Pose=live.Pose;planner.Tick(.15,.05f,expired);Assert.That(route.Calls,Is.EqualTo(calls));Assert.That(planner.Capture().Navigation.TransitionExit,Is.EqualTo(exit));
            planner.Tick(.21,.06f,expired);Assert.That(route.Calls,Is.EqualTo(calls),"Retry also waits for decision cadence");
            planner.Tick(.15+behavior.Get("bots.easy.decisionSeconds")+.01,.01f,expired);Assert.That(route.Calls,Is.GreaterThan(calls));var after=planner.Capture();Assert.That(after.Navigation.ActiveTransition,Is.EqualTo("transition:ramp"));Assert.That(after.Navigation.TransitionExit,Is.EqualTo(exit));
        }
    }
}
