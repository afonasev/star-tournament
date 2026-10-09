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
        sealed class UnreachableNearRoute : INativeNavigation
        {
            public string Identity=>"blocked-near-map";
            public bool TryLocate(Vector3 feet,out NativeNavigationPoint point){point=new NativeNavigationPoint(feet,"floor:lower");return true;}
            public bool ValidTransition(string transition,string exit)=>false;
            public bool TryRoute(Vector3 from,Vector3 goal,out NativeNavigationPoint[] points,out string failure)
            {bool blocked=goal==Vector3.forward*10;points=blocked?Array.Empty<NativeNavigationPoint>():new[]{new NativeNavigationPoint(goal,"floor:lower")};failure=blocked?"unreachable":null;return !blocked;}
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
            public bool BlockLongApproach, JumpAllowed=true;
            public Vector3[] Anchors { get; set; } = { Vector3.forward * 20 };
            public bool CanMove(Vector3 feet, Vector3 direction, float distance) => !BlockLongApproach||distance<=3;
            public bool CanJump(ParticipantState pose, Vector3 direction) => JumpAllowed;
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
            ProvingProfile behavior = null, Tactics tactics = null, INativeBotWeaponPolicy weapon = null, INativeNavigation routes = null) => new NativeBotPlanner(difficulty, seed,
                behavior ?? ProvingProfile.CreateBotBehaviorDefault(), ProvingProfile.CreateNavigationDefault(), routes ?? new Route(),
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
            Assert.That(actual.DamageBoostActive, Is.EqualTo(expected.DamageBoostActive)); Assert.That(actual.Pressed, Is.EqualTo(expected.Pressed)); Assert.That(actual.Personality, Is.EqualTo(expected.Personality)); Assert.That(actual.AimPhase, Is.EqualTo(expected.AimPhase));
            Assert.That(actual.Navigation.Goal, Is.EqualTo(expected.Navigation.Goal)); Assert.That(actual.Navigation.Cursor, Is.EqualTo(expected.Navigation.Cursor));
            Assert.That(actual.Navigation.Enemy, Is.EqualTo(expected.Navigation.Enemy)); Assert.That(actual.Navigation.EnemyLife, Is.EqualTo(expected.Navigation.EnemyLife));
        }

        [TestCase(WeaponId.Rifle)] [TestCase(WeaponId.Shotgun)] [TestCase(WeaponId.Cutter)]
        public void VisibleArmoredTargetIsTrackedWithoutFiringUntilShotPathClears(WeaponId selected)
        {
            var planner=Make(NativeBotDifficulty.Hard);var frame=Frame();
            frame.Life.SelectedWeapon=selected;frame.Life.RifleAmmo=20;frame.Life.ShotgunOwned=true;frame.Life.ShotgunAmmo=20;frame.Life.CutterOwned=true;frame.Life.CutterEnergy=20;
            frame.Knowledge.Enemies[0].Sighting.ShotBlocked=true;
            for(int i=0;i<60;i++)
            {
                var action=planner.Tick(i*.02,.02f,frame);
                Assert.That(action.Fire||action.FireHeld,Is.False);Assert.That(action.LookDegrees,Is.Not.EqualTo(Vector2.zero));
                var world=Quaternion.Euler(0,action.LookDegrees.x,0)*new Vector3(action.Move.x,0,action.Move.y);
                Assert.That(world.z,Is.GreaterThan(0));Assert.That(world.x,Is.EqualTo(0).Within(.01f),"Follow the route around protection instead of local strafing");
            }
            Assert.That(planner.Capture().Target,Is.EqualTo(2));Assert.That(planner.FireAttempts,Is.Zero);
            frame.Knowledge.Enemies[0].Sighting.ShotBlocked=false;
            var open=planner.Tick(1.2,.02f,frame);Assert.That(open.Fire||open.FireHeld,Is.True);
            frame.Knowledge.Enemies[0].Visible=false;
            var memory=planner.Tick(1.22,.02f,frame);Assert.That(memory.Fire||memory.FireHeld,Is.False);
        }

        [Test] public void ProfileCopiesAllBehaviorTuningAndDifficultyDefaultsReachWeaponPolicy()
        {
            var profile=ProvingProfile.CreateBotBehaviorDefault(); Assert.That(profile.Validate(), Is.Empty);
            Assert.That(profile.Descriptors, Has.Count.EqualTo(82));
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

        static NativeBotFrame Boosted(float health=100, float enemyHealth=100)
        {
            var frame=Frame(health:health);frame.DamageRemaining=10;frame.Life.RifleAmmo=20;
            frame.Knowledge.Enemies[0].Sighting=new NativeBotSighting(2,1,Vector3.forward*10,enemyHealth,0);
            return frame;
        }

        [TestCase(NativeBotDifficulty.Easy)] [TestCase(NativeBotDifficulty.Normal)] [TestCase(NativeBotDifficulty.Hard)]
        public void BoostCancelsPreviousRetreatAndHealingWithoutWaitingForDecision(NativeBotDifficulty difficulty)
        {
            var planner=Make(difficulty);var frame=Frame(health:10);frame.Life.RifleAmmo=20;
            frame.Pickups=new[]{new NativeBotPickupEvent{Id="heal",Kind=NativeBotPickupKind.Heal,Available=true,Position=Vector3.back}};
            planner.Tick(0,.01f,frame);Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Pickup));
            frame.DamageRemaining=10;frame.Knowledge.Enemies[0].Sighting=new NativeBotSighting(2,1,Vector3.forward*10,5,0);
            planner.Tick(.01,.01f,frame);
            Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Engage));Assert.That(planner.PickupId,Is.Null);
            Assert.That(planner.Capture().RetreatUntil,Is.Zero);Assert.That(planner.Capture().Navigation.Enemy,Is.EqualTo(2));
        }

        [TestCase(NativeBotDifficulty.Easy)] [TestCase(NativeBotDifficulty.Normal)]
        public void BoostedNonVeteranFightsEvenWhenOutmatchedAndCriticallyWounded(NativeBotDifficulty difficulty)
        {
            var planner=Make(difficulty);var frame=Boosted(1,100);
            frame.Pickups=new[]{new NativeBotPickupEvent{Id="heal",Kind=NativeBotPickupKind.Heal,Available=true,Position=Vector3.back}};
            planner.Tick(0,.01f,frame);Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Engage));Assert.That(planner.PickupId,Is.Null);
        }

        [Test] public void VeteranRetreatUsesBoostMultiplierAndStopsWhenFightBecomesFavorable()
        {
            var planner=Make(NativeBotDifficulty.Hard);var frame=Boosted(10,100);
            planner.Tick(0,.01f,frame);Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Retreat));
            frame.Knowledge.Enemies[0].Sighting=new NativeBotSighting(2,1,Vector3.forward*10,5,0);
            planner.Tick(.01,.01f,frame);Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Engage));
            var profile=ProvingProfile.CreateBotBehaviorDefault();float ratio=profile.Get("bots.strategy.strongerRatio");
            frame=Boosted(50,50*ratio*1.5f);planner=Make(NativeBotDifficulty.Hard);
            planner.Tick(0,.01f,frame);Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Engage),"The same enemy would exceed unboosted strength, but not doubled strength.");
        }

        [Test] public void BoostChoosesNearestRatherThanMoreVulnerableTargetThenMovesToNextEnemy()
        {
            var profile=ProvingProfile.CreateBotBehaviorDefault();float gap=profile.Get("bots.strategy.vulnerableWeight")/2;
            var frame=Boosted();frame.Knowledge.Enemies=new[]{
                new NativeBotMemoryEntry{Visible=true,Sighting=new NativeBotSighting(2,1,Vector3.forward*10,100,0)},
                new NativeBotMemoryEntry{Visible=true,Sighting=new NativeBotSighting(3,1,Vector3.forward*(10+gap),1,0)}};
            var planner=Make();planner.Tick(0,.01f,frame);Assert.That(planner.Capture().Target,Is.EqualTo(2));
            frame.Knowledge.Enemies=new[]{frame.Knowledge.Enemies[1]};planner.Tick(.01,.01f,frame);Assert.That(planner.Capture().Target,Is.EqualTo(3));
            frame.Knowledge.Enemies[0].Visible=false;var action=planner.Tick(.7,.01f,frame);
            Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Pursue));Assert.That(action.Fire||action.FireHeld,Is.False);
        }

        [Test] public void BoostSkipsUnreachableNearTargetAndDoesNotChooseSupport()
        {
            var profile=ProvingProfile.CreateBotBehaviorDefault();profile.Set("bots.easy.supportChance",1);
            var planner=Make(behavior:profile,routes:new UnreachableNearRoute());var frame=Boosted();
            frame.Allies=new[]{new NativeBotAlly{Participant=4,Position=Vector3.forward*19}};
            frame.Knowledge.Enemies=new[]{frame.Knowledge.Enemies[0],new NativeBotMemoryEntry{Visible=true,Sighting=new NativeBotSighting(3,1,Vector3.forward*20,100,0)}};
            planner.Tick(0,.01f,frame);Assert.That(planner.Capture().Target,Is.EqualTo(3));Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Engage));
        }

        [Test] public void BoostExpiryReevaluatesLowHealthImmediatelyAndRestorePreservesBoostTransition()
        {
            var planner=Make();var frame=Boosted(1,100);planner.Tick(0,.01f,frame);
            var restored=Make();restored.Restore(planner.Capture());
            frame.DamageRemaining=0;Same(planner.Tick(.01,.01f,frame),restored.Tick(.01,.01f,frame));
            Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Retreat));Assert.That(restored.Intent,Is.EqualTo(planner.Intent));
            Assert.That(restored.Capture().DamageBoostActive,Is.False);
        }

        [Test] public void BoostedBotWithoutAmmoOnlyCollectsWeaponThenResumesPursuit()
        {
            var planner=Make();var frame=Boosted(1,100);frame.Life.RifleAmmo=0;
            frame.Pickups=new[]{
                new NativeBotPickupEvent{Id="heal",Kind=NativeBotPickupKind.Heal,Available=true,Position=Vector3.back},
                new NativeBotPickupEvent{Id="shotgun",Kind=NativeBotPickupKind.Weapon,Weapon=WeaponId.Shotgun,Available=true,Position=Vector3.right}};
            planner.Tick(0,.01f,frame);Assert.That(planner.PickupId,Is.EqualTo("shotgun"));
            frame.Life.RifleAmmo=20;planner.Tick(.01,.01f,frame);
            Assert.That(planner.PickupId,Is.Null);Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Engage));
            Assert.That(planner.Capture().Navigation.Enemy,Is.EqualTo(2));
        }

        [Test] public void BoostKeepsSearchingWithoutKnownEnemyInsteadOfCollectingOptionalPickup()
        {
            var planner=Make();var frame=Boosted();frame.Knowledge.Enemies=Array.Empty<NativeBotMemoryEntry>();
            frame.Pickups=new[]{new NativeBotPickupEvent{Id="armor",Kind=NativeBotPickupKind.Armor,Available=true,Position=Vector3.back}};
            var action=planner.Tick(0,.01f,frame);Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Search));
            Assert.That(planner.PickupId,Is.Null);Assert.That(action.Move.sqrMagnitude,Is.GreaterThan(0));
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

        [Test] public void RetreatFiresWhileMovingAwayAndTracksDatedThreatWithoutBlindFire()
        {
            var planner=Make(NativeBotDifficulty.Hard);var frame=Frame(health:10);
            planner.Tick(0,.01f,frame);
            var action=planner.Tick(.2,.01f,frame);
            Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Retreat));
            Assert.That(action.Fire,Is.True,"Retreat must keep the normal ready/visible fire opportunity");
            var world=Quaternion.Euler(0,action.LookDegrees.x,0)*new Vector3(action.Move.x,0,action.Move.y);
            Assert.That(world.z,Is.LessThan(0),"Cover progress must remain away from this threat");
            Assert.That(Mathf.Abs(world.x),Is.GreaterThan(0),"Retreat also evades laterally");
            var saved=planner.Capture();var restored=Make(NativeBotDifficulty.Hard);restored.Restore(saved);
            frame.Knowledge.Enemies[0].Visible=false;
            action=planner.Tick(.3,.01f,frame);Same(action,restored.Tick(.3,.01f,frame));
            Assert.That(action.LookDegrees,Is.EqualTo(new Vector2(7,-3)),"Keep aiming from the dated memory rather than turn down the escape route");
            Assert.That(action.Fire||action.FireHeld,Is.False,"A remembered position cannot authorize a shot");
            frame.Knowledge.Enemies=Array.Empty<NativeBotMemoryEntry>();
            action=planner.Tick(.4,.01f,frame);Assert.That(action.Fire,Is.False);
        }

        [Test] public void PickupRetainsThreatFacingThroughVisibilityLossAndMemoryExpiry()
        {
            var planner=Make(NativeBotDifficulty.Hard);var frame=Frame(health:10);
            frame.Pickups=new[]{new NativeBotPickupEvent{Id="heal-behind",Kind=NativeBotPickupKind.Heal,Available=true,Position=Vector3.back*2}};
            planner.Tick(0,.01f,frame);
            Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Pickup),"Healing replaces the retreat intention in this regression");
            frame.Knowledge.Enemies[0].Visible=false;
            var action=planner.Tick(.2,.01f,frame);
            Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Pickup));
            Assert.That(action.LookDegrees,Is.EqualTo(new Vector2(7,-3)),"A pickup route must not take facing away from the known threat");
            var world=Quaternion.Euler(0,action.LookDegrees.x,0)*new Vector3(action.Move.x,0,action.Move.y);
            Assert.That(world.z,Is.LessThan(0),"Healing still makes route progress backwards");
            Assert.That(action.Fire||action.FireHeld,Is.False);
            var restored=Make(NativeBotDifficulty.Hard);restored.Restore(planner.Capture());
            Same(planner.Tick(.3,.01f,frame),restored.Tick(.3,.01f,frame));
            frame.Knowledge.Enemies=Array.Empty<NativeBotMemoryEntry>();
            action=planner.Tick(.4,.01f,frame);
            Assert.That(Mathf.Abs(action.LookDegrees.x),Is.EqualTo(180).Within(.001),"After memory expiry navigation may face the pickup route again");
            Assert.That(action.Fire||action.FireHeld,Is.False);
        }

        [Test] public void ExpiredRetreatDoesNotGiveFacingBackToAnIndirectPursuitRoute()
        {
            var planner=Make(NativeBotDifficulty.Hard);var frame=Frame(health:10);
            planner.Tick(0,.01f,frame);
            frame.Knowledge.Enemies[0].Visible=false;
            var action=planner.Tick(1.5,.01f,frame);
            Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Pursue));
            Assert.That(action.LookDegrees,Is.EqualTo(new Vector2(7,-3)),"Retreat timer expiration must not drop remembered threat tracking");
            Assert.That(action.Fire||action.FireHeld,Is.False);
        }

        [Test] public void ActualAimPolicyFacesThreatWhileMovingBackwardsToHeal()
        {
            var planner=new NativeBotPlanner(NativeBotDifficulty.Hard,7,ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateNavigationDefault(),new Route(),new Tactics(),
                new NativeShotgunPolicy(ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault()),100);
            var frame=Frame(health:10);
            frame.Pickups=new[]{new NativeBotPickupEvent{Id="heal-behind",Kind=NativeBotPickupKind.Heal,Available=true,Position=Vector3.back*2}};
            planner.Tick(0,.02f,frame);frame.Knowledge.Enemies[0].Visible=false;
            var action=planner.Tick(.2,.02f,frame);
            Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Pickup));
            Assert.That(Mathf.Abs(action.LookDegrees.x),Is.LessThan(90),"Actual aim must retain front-facing threat rather than snap 180 degrees towards healing");
            var forward=Quaternion.Euler(0,action.LookDegrees.x,0)*Vector3.forward;
            var movement=Quaternion.Euler(0,action.LookDegrees.x,0)*new Vector3(action.Move.x,0,action.Move.y);
            Assert.That(Vector3.Dot(forward,Vector3.forward),Is.GreaterThan(0));
            Assert.That(movement.z,Is.LessThan(0));Assert.That(action.Fire||action.FireHeld,Is.False);
        }

        [Test] public void WorkingRangeStrafeUsesDifficultyProfileAndOnlyLocalClearance()
        {
            float previous=0;
            foreach(var difficulty in new[]{NativeBotDifficulty.Easy,NativeBotDifficulty.Normal,NativeBotDifficulty.Hard})
            {
                var profile=ProvingProfile.CreateBotBehaviorDefault();profile.Set("bots.cooperation.personalitySpread",0);
                var planner=Make(difficulty,behavior:profile,tactics:new Tactics{BlockLongApproach=true});
                var action=planner.Tick(0,.01f,Frame());
                var world=Quaternion.Euler(0,action.LookDegrees.x,0)*new Vector3(action.Move.x,0,action.Move.y);
                float lateral=Mathf.Abs(world.x);
                Assert.That(lateral,Is.GreaterThan(previous),"Higher difficulty has a stronger configured lateral component at range");previous=lateral;
                Assert.That(world.z,Is.GreaterThan(0),"Closing on the target still makes progress");
            }
        }

        [TestCase(true)] [TestCase(false)]
        public void WorkingRangeJumpRequiresSafeTrajectoryAndRespectsGroundedCooldown(bool safe)
        {
            var profile=ProvingProfile.CreateBotBehaviorDefault();profile.Set("bots.cooperation.personalitySpread",0);profile.Set("bots.hard.jumpChance",1);
            var planner=Make(NativeBotDifficulty.Hard,behavior:profile,tactics:new Tactics{JumpAllowed=safe});var frame=Frame();
            planner.Tick(0,.01f,frame);var action=planner.Tick(3.1,.01f,frame);
            Assert.That(action.Jump,Is.EqualTo(safe));
            Assert.That(planner.Tick(3.8,.01f,frame).Jump,Is.False,"A new grounded manoeuvre cannot skip the jump cooldown");
        }

        [Test] public void NoviceNeverCombatJumpsEvenWithCustomMaximumChance()
        {
            var profile=ProvingProfile.CreateBotBehaviorDefault();profile.Set("bots.easy.jumpChance",1);profile.Set("bots.easy.surpriseJumpChance",1);
            var planner=Make(behavior:profile);var frame=Frame();frame.Pose.Yaw=90;
            for(int i=0;i<2000;i++)Assert.That(planner.Tick(i*.02,.02f,frame).Jump,Is.False);
        }
        [Test] public void DefaultCadenceSeparatesFighterAndVeteranWithoutContinuousJumps()
        {
            int fighter=0,veteran=0;
            foreach(var difficulty in new[]{NativeBotDifficulty.Normal,NativeBotDifficulty.Hard})
            {
                var profile=ProvingProfile.CreateBotBehaviorDefault();profile.Set("bots.cooperation.personalitySpread",0);
                for(uint seed=1;seed<=16;seed++)
                {
                    var planner=Make(difficulty,seed,profile);var frame=Frame();double last=-100;
                    for(int i=0;i<3000;i++)if(planner.Tick(i*.02,.02f,frame).Jump)
                    {
                        double now=i*.02;Assert.That(now-last,Is.GreaterThanOrEqualTo(profile.Get(difficulty==NativeBotDifficulty.Hard?"bots.hard.jumpCooldownSeconds":"bots.normal.jumpCooldownSeconds")-.001));last=now;
                        if(difficulty==NativeBotDifficulty.Hard)veteran++;else fighter++;
                    }
                }
            }
            Assert.That(fighter,Is.GreaterThan(0));Assert.That(veteran,Is.GreaterThan(fighter));Assert.That(veteran,Is.LessThan(16*21));
        }
        [Test] public void VeteranSurpriseCanJumpBeforeNextStrafeButCannotBypassCooldown()
        {
            var profile=ProvingProfile.CreateBotBehaviorDefault();profile.Set("bots.cooperation.personalitySpread",0);profile.Set("bots.hard.jumpChance",0);profile.Set("bots.hard.surpriseJumpChance",1);
            var planner=Make(NativeBotDifficulty.Hard,behavior:profile);var frame=Frame(target:-1);planner.Tick(0,.02f,frame);
            frame=Frame();frame.Pose.Yaw=90;
            Assert.That(planner.Tick(.12,.02f,frame).Jump,Is.True);
            frame.Knowledge.Enemies[0].Sighting.Participant=3;
            Assert.That(planner.Tick(.24,.02f,frame).Jump,Is.False);
        }
        [Test] public void EmptyRememberedPositionImmediatelyReturnsToSearchAndFreshSightCanReacquire()
        {
            var planner=Make();var frame=Frame(visible:false);planner.Tick(0,.02f,frame);
            frame.Pose.Position=frame.Knowledge.Enemies[0].Sighting.Position;
            var action=planner.Tick(.02,.02f,frame);
            Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Search));Assert.That(action.Move.sqrMagnitude,Is.GreaterThan(0));Assert.That(action.Fire,Is.False);
            Assert.That(planner.Capture().Target,Is.EqualTo(-1));
            planner.Tick(.04,.02f,frame);Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Search));
            frame.Knowledge.Enemies[0].Visible=true;frame.Knowledge.Enemies[0].ObservedAt=.5;
            planner.Tick(.5,.02f,frame);Assert.That(planner.Capture().Target,Is.EqualTo(2));
        }
        [Test] public void RememberedCloseTargetUsesLateralMovementWithoutBlindFire()
        {
            var planner=Make(NativeBotDifficulty.Hard);var action=planner.Tick(0,.02f,Frame(visible:false));
            var world=Quaternion.Euler(0,action.LookDegrees.x,0)*new Vector3(action.Move.x,0,action.Move.y);
            Assert.That(Mathf.Abs(world.x),Is.GreaterThan(.1f));Assert.That(action.Fire||action.FireHeld,Is.False);
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
