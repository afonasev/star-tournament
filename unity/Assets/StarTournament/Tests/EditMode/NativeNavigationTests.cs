using System;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class NativeNavigationTests
    {
        sealed class Route : INativeNavigation
        {
            public string Identity => "test-map";
            public Vector3 LastGoal;
            public bool TryLocate(Vector3 feet, out NativeNavigationPoint point) { point=new NativeNavigationPoint(feet,"floor:lower");return true; }
            public bool ValidTransition(string transition,string exitSupport)=>false;
            public bool TryRoute(Vector3 from, Vector3 to, out NativeNavigationPoint[] points, out string failure)
            { LastGoal = to; points = new[] { new NativeNavigationPoint(to, "floor:lower") }; failure = null; return true; }
        }
        static NativeBotKnowledge Alive(int life = 1) => new NativeBotKnowledge { Alive = true, OwnLife = life };
        [Test] public void Profile_IsCompleteFrozenAndRangeValidated()
        {
            var p = ProvingProfile.CreateNavigationDefault(); Assert.That(p.Validate(), Is.Empty);
            foreach (var d in p.Descriptors) Assert.That(d.Validate(out _), Is.True);
            var bot = new NativeBotNavigation(new Route(), p); bot.SetStaticGoal(Vector3.forward * 10);
            p.Set("bots.navigation.stuckSeconds", .5f);
            bot.Tick(0, default, Alive()); bot.Tick(.6, default, Alive());
            Assert.That(bot.Status, Is.EqualTo(NativeNavigationStatus.Moving));
            p.Set("bots.navigation.maximumRecoveries", 1.5f);
            Assert.Throws<ArgumentException>(() => new NativeBotNavigation(new Route(), p));
        }
        [Test] public void ClearAllowsImmediateReverseGoalBeforeOldRepathDeadlineAndAfterRestore()
        {
            var profile=ProvingProfile.CreateNavigationDefault();var route=new Route();var bot=new NativeBotNavigation(route,profile);
            bot.SetStaticGoal(Vector3.forward*10);var pose=new ParticipantState{Grounded=true};bot.Tick(0,pose,Alive());
            var clone=new NativeBotNavigation(new Route(),profile);clone.Restore(bot.Capture());
            bot.Clear();clone.Clear();bot.SetStaticGoal(Vector3.back*10);clone.SetStaticGoal(Vector3.back*10);
            double next=profile.Get("bots.navigation.repathSeconds")/2;
            var actual=bot.Tick(next,pose,Alive());var restored=clone.Tick(next,pose,Alive());
            Assert.That(route.LastGoal,Is.EqualTo(Vector3.back*10));Assert.That(actual.Move.y,Is.GreaterThan(0),"Local movement follows the new backwards-facing route");
            Assert.That(actual.Move,Is.EqualTo(restored.Move));Assert.That(actual.LookDegrees,Is.EqualTo(restored.LookDegrees));
        }

        [Test] public void Knowledge_UsesOriginalPositionAndExpiresWithoutRawWorld()
        {
            var route = new Route(); var bot = new NativeBotNavigation(route, ProvingProfile.CreateNavigationDefault());
            bot.FollowEnemy(2); var k = Alive();
            k.Enemies = new[] { new NativeBotMemoryEntry { Sighting = new NativeBotSighting(2, 1, Vector3.forward * 5), ObservedAt = 0 } };
            bot.Tick(0, default, k); Assert.That(route.LastGoal, Is.EqualTo(Vector3.forward * 5));
            bot.Tick(1, default, k); Assert.That(bot.Capture().ObservedAt, Is.Zero);
            k.Enemies = Array.Empty<NativeBotMemoryEntry>(); bot.Tick(1.1, default, k);
            Assert.That(bot.Status, Is.EqualTo(NativeNavigationStatus.Idle)); Assert.That(bot.Capture().HasGoal, Is.False);
        }
        [Test] public void Snapshot_IsIndependentAndRejectsInvalidAtomically()
        {
            var p = ProvingProfile.CreateNavigationDefault(); var bot = new NativeBotNavigation(new Route(), p);
            bot.SetStaticGoal(Vector3.forward * 8); bot.Tick(0, default, Alive());
            var saved = bot.Capture(); var clone = new NativeBotNavigation(new Route(), p); clone.Restore(saved);
            saved.Route[0].Position = Vector3.one * 999;
            Assert.That(clone.Capture().Route[0].Position.z, Is.EqualTo(8));
            var before = JsonUtility.ToJson(clone.Capture()); saved.Time = double.NaN;
            Assert.Throws<ArgumentException>(() => clone.Restore(saved)); Assert.That(JsonUtility.ToJson(clone.Capture()), Is.EqualTo(before));
            clone.Tick(.1, default, Alive()); Assert.That(clone.Capture().Replans, Is.EqualTo(2));
        }
        [Test] public void Recovery_IsBoundedAndNewLifeClearsIntent()
        {
            var bot = new NativeBotNavigation(new Route(), ProvingProfile.CreateNavigationDefault()); bot.SetStaticGoal(Vector3.forward * 20);
            for (int i = 0; i < 1000 && bot.Status != NativeNavigationStatus.Blocked; i++) bot.Tick(i / 60d, default, Alive());
            Assert.That(bot.Status, Is.EqualTo(NativeNavigationStatus.Blocked)); Assert.That(bot.Capture().Recoveries, Is.EqualTo(3));
            bot.Tick(20, default, Alive(2)); Assert.That(bot.Status, Is.EqualTo(NativeNavigationStatus.Idle));
        }
        [Test] public void AimAndMovement_AreIndependentAndHeightPreventsFalseArrival()
        {
            var bot = new NativeBotNavigation(new Route(), ProvingProfile.CreateNavigationDefault()); bot.SetStaticGoal(new Vector3(0, 3, 5));
            var action = bot.Tick(0, default, Alive(), 90);
            Assert.That(action.Move.x, Is.LessThan(-.9f)); Assert.That(action.LookDegrees.x, Is.EqualTo(90));
            bot.Tick(.1, new ParticipantState { Position = new Vector3(0, 0, 5) }, Alive());
            Assert.That(bot.Status, Is.Not.EqualTo(NativeNavigationStatus.Arrived));
        }
        [Test] public void MovingKnownGoal_CannotResetStationaryRecoveryBudget()
        {
            var bot = new NativeBotNavigation(new Route(), ProvingProfile.CreateNavigationDefault()); bot.FollowEnemy(1);
            var k=Alive();
            for(int i=0;i<1000 && bot.Status!=NativeNavigationStatus.Blocked;i++)
            {
                k.Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,new Vector3(i*.01f,0,20)),ObservedAt=i/60d,Visible=true}};
                bot.Tick(i/60d,new ParticipantState{Grounded=true},k);
            }
            Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Blocked));
            Assert.That(bot.Capture().Recoveries,Is.EqualTo(3));
        }
        [Test] public void NewGoalDuringRepathCooldown_DoesNotArriveOnOldRoute()
        {
            var bot=new NativeBotNavigation(new Route(),ProvingProfile.CreateNavigationDefault());
            bot.SetStaticGoal(Vector3.zero);bot.Tick(0,new ParticipantState{Grounded=true},Alive());
            Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived));
            bot.SetStaticGoal(Vector3.forward*5);bot.Tick(.01,new ParticipantState{Grounded=true},Alive());
            Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Moving));
            Assert.That(bot.Tick(.31,new ParticipantState{Grounded=true},Alive()).Move.y,Is.GreaterThan(0));
        }
        [Test] public void NewGoalAfterLongArrivalWait_StartsFreshProgressWindow()
        {
            var bot=new NativeBotNavigation(new Route(),ProvingProfile.CreateNavigationDefault());var pose=new ParticipantState{Grounded=true};
            bot.SetStaticGoal(Vector3.zero);bot.Tick(0,pose,Alive());bot.Tick(10,pose,Alive());
            bot.SetStaticGoal(Vector3.forward*10);var action=bot.Tick(10.1,pose,Alive());
            Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Moving));Assert.That(action.Move.y,Is.GreaterThan(0));
            Assert.That(bot.Capture().Recoveries,Is.Zero);
        }
        [Test] public void SnapshotRejectsUnknownAndMismatchedTransitionAtomically()
        {
            var bot=new NativeBotNavigation(new Route(),ProvingProfile.CreateNavigationDefault());bot.SetStaticGoal(Vector3.forward*10);bot.Tick(0,default,Alive());
            var before=JsonUtility.ToJson(bot.Capture());var snapshot=bot.Capture();snapshot.ActiveTransition="unknown";
            Assert.Throws<ArgumentException>(()=>bot.Restore(snapshot));snapshot.ExitSupport="floor:lower";
            Assert.Throws<ArgumentException>(()=>bot.Restore(snapshot));Assert.That(JsonUtility.ToJson(bot.Capture()),Is.EqualTo(before));
        }
        [Test] public void ExplicitSameGoalRetry_ReplansInsteadOfEmptyMoving()
        {
            var bot=new NativeBotNavigation(new Route(),ProvingProfile.CreateNavigationDefault());var pose=new ParticipantState{Grounded=true};
            bot.SetStaticGoal(Vector3.zero);bot.Tick(0,pose,Alive());bot.SetStaticGoal(Vector3.zero);bot.Tick(1,pose,Alive());
            Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived));Assert.That(bot.Capture().Replans,Is.EqualTo(2));
        }
        [Test] public void FreshEnemyGoalAfterArrival_StartsNewProgressWindow()
        {
            var bot=new NativeBotNavigation(new Route(),ProvingProfile.CreateNavigationDefault());var pose=new ParticipantState{Grounded=true};var k=Alive();
            k.Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,Vector3.zero)}};
            bot.FollowEnemy(1);bot.Tick(0,pose,k);bot.Tick(10,pose,k);Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived));
            k.Enemies[0].Sighting.Position=Vector3.forward*10;var action=bot.Tick(10.1,pose,k);
            Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Moving));Assert.That(action.Move.y,Is.GreaterThan(0));Assert.That(bot.Capture().Recoveries,Is.Zero);
        }
    }
}
