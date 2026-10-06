using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeBotPerceptionTests
    {
        static NativeMatchRoster Teams() => new NativeMatchRoster(NativeMatchMode.Teams,
            new[] { NativeTeam.TeamA, NativeTeam.TeamA, NativeTeam.TeamB });
        static NativeBotPerception Make(bool teams = true, ProvingProfile profile = null) => new NativeBotPerception(
            teams ? Teams() : NativeMatchRoster.Ffa(3), new[] { NativeBotDifficulty.Easy, NativeBotDifficulty.Normal, NativeBotDifficulty.Hard }, profile ?? ProvingProfile.CreateBotPerceptionDefault());
        static NativeBotSighting Seen(int target = 2, int life = 1, float x = 3) => new NativeBotSighting(target, life, new Vector3(x, 0, 5));
        static NativeBotObservationFrame[] Frames(params NativeBotSighting[] seen) => new[] {
            new NativeBotObservationFrame { OwnLife = 1, Alive = true, Direct = seen },
            new NativeBotObservationFrame { OwnLife = 1, Alive = true },
            new NativeBotObservationFrame { OwnLife = 1, Alive = true } };
        static string Json(NativeBotPerception k) => JsonUtility.ToJson(k.Capture());

        [Test] public void ProfilesAreFrozenAndUseExistingDifficultyMetadata()
        {
            var p = ProvingProfile.CreateBotPerceptionDefault(); var k = Make(profile:p);
            Assert.That(p.Validate(), Is.Empty); Assert.That(p.Descriptors.Count, Is.EqualTo(10));
            Assert.That(Enumerable.Range(0,3).Select(k.FieldOfView), Is.EqualTo(new[] {110f,130f,150f}));
            p.Set("bots.easy.fieldOfViewDegrees", 180); Assert.That(k.FieldOfView(0), Is.EqualTo(110));
            p.Set("bots.easy.memorySeconds", float.NaN); Assert.Throws<ArgumentException>(() => Make(profile:p));
            Assert.Throws<ArgumentException>(() => new NativeBotPerception(Teams(),new[]{(NativeBotDifficulty)99,NativeBotDifficulty.Easy,NativeBotDifficulty.Easy},ProvingProfile.CreateBotPerceptionDefault()));
        }
        [Test] public void HiddenEnemyHasOnlyOldPositionAndMemoryExpiresAtBoundary()
        {
            var k=Make(); k.Sample(0,Frames(Seen())); var copy=k.Read(0); copy.Enemies[0].Sighting.Position=Vector3.one*999;
            k.Sample(1,Frames()); var remembered=k.Read(0).Enemies.Single();
            Assert.That(remembered.Visible,Is.False); Assert.That(remembered.Sighting.Position.x,Is.EqualTo(3));
            Assert.That(remembered.ObservedAt,Is.Zero); k.Sample(2,Frames()); Assert.That(k.Read(0).Enemies,Is.Empty);
            // Receiver's normal difficulty remembers the original timestamp for four seconds, not arrival+four.
            Assert.That(k.Read(1).Enemies.Single().ObservedAt,Is.Zero); k.Sample(4,Frames());Assert.That(k.Read(1).Enemies,Is.Empty);
        }
        [Test] public void ReportDelayPreservesOriginalPositionAndDoesNotStarveUnderContinuousSight()
        {
            var k=Make(); k.Sample(0,Frames(Seen(x:3))); k.Sample(.2,Frames(Seen(x:9)));
            Assert.That(k.Read(1).Enemies,Is.Empty); Assert.That(k.Capture().Pending.Length,Is.EqualTo(1));
            k.Sample(.31,Frames(Seen(x:11)));
            var report=k.Read(1).Enemies.Single();Assert.That(report.Sighting.Position.x,Is.EqualTo(3));Assert.That(report.ObservedAt,Is.Zero);Assert.That(report.Source,Is.Zero);Assert.That(report.Visible,Is.False);
            Assert.That(k.Capture().Pending.Length,Is.EqualTo(1));
        }
        [Test] public void StaleReportCannotOverwriteFreshDirectSightingOrNewObservedLife()
        {
            var k=Make();k.Sample(0,Frames(Seen()));var frames=Frames();frames[1].Direct=new[]{Seen(life:2,x:12)};k.Sample(.2,frames);
            k.Sample(.31,Frames());var entry=k.Read(1).Enemies.Single();Assert.That(entry.Sighting.Life,Is.EqualTo(2));Assert.That(entry.Sighting.Position.x,Is.EqualTo(12));
        }
        [Test] public void ReportsAreNotRebroadcastAndFfaNeverShares()
        {
            var k=Make();k.Sample(0,Frames(Seen()));k.Sample(.31,Frames());Assert.That(k.Capture().Pending,Is.Empty);
            var ffa=Make(false);ffa.Sample(0,Frames(Seen()));ffa.Sample(1,Frames());Assert.That(ffa.Read(1).Enemies,Is.Empty);Assert.That(ffa.Capture().Pending,Is.Empty);
        }
        [Test] public void ExpiredReportDoesNotResurrectMemory()
        {
            var p=ProvingProfile.CreateBotPerceptionDefault();p.Set("bots.normal.memorySeconds",.5f);p.Set("bots.cooperation.communicationSeconds",1);
            var k=Make(profile:p);k.Sample(0,Frames(Seen()));k.Sample(1,Frames());Assert.That(k.Read(1).Enemies,Is.Empty);
        }
        [Test] public void OwnDeathAndRespawnDiscardIncomingReportsAndKnowledge()
        {
            var k=Make();k.Sample(0,Frames(Seen()));var dead=Frames();dead[1].Alive=false;k.Sample(.1,dead);
            Assert.That(k.Capture().Pending,Is.Empty);var respawn=Frames();respawn[1].OwnLife=2;k.Sample(.4,respawn);Assert.That(k.Read(1).Enemies,Is.Empty);
            k.Sample(.5,Frames(Seen()));var changed=Frames();changed[0].OwnLife=2;k.Sample(.6,changed);Assert.That(k.Read(0).Enemies,Is.Empty);
        }
        [Test] public void SnapshotRoundTripContinuesPendingDeliveryWithoutAliasing()
        {
            var a=Make();a.Sample(0,Frames(Seen()));var b=Make();var snap=JsonUtility.FromJson<NativeBotPerceptionSnapshot>(Json(a));b.Restore(snap);
            snap.Pending[0].Sighting.Position=Vector3.one*999;snap.Observers[0].Enemies[0].Sighting.Position=Vector3.one*999;
            Assert.That(Json(a),Is.EqualTo(Json(b)));a.Sample(.31,Frames());b.Sample(.31,Frames());Assert.That(Json(a),Is.EqualTo(Json(b)));
            string paused=Json(b);b.Read(0);b.Capture();Assert.That(Json(b),Is.EqualTo(paused));
        }
        [Test] public void InvalidFramesAndSnapshotsFailWithoutMutation()
        {
            var k=Make();k.Sample(0,Frames(Seen()));string initial=Json(k);
            foreach(double t in new[]{0d,-1,double.NaN,double.PositiveInfinity}) Assert.Throws<ArgumentException>(()=>k.Sample(t,Frames()));
            Assert.Throws<ArgumentException>(()=>k.Sample(1,Frames(Seen(target:1))));
            Assert.Throws<ArgumentException>(()=>k.Sample(1,Frames(Seen(x:float.NaN))));
            Assert.That(Json(k),Is.EqualTo(initial));
            Action<NativeBotPerceptionSnapshot>[] corruptions = {
                s=>s.Version++, s=>s.Configuration="other",s=>s.Time=double.NaN,
                s=>s.Observers[0].Enemies[0].ObservedAt=1,s=>s.Observers[0].Enemies[0].Source=2,
                s=>s.Pending[0].Source=2,s=>s.Pending[0].DeliverAt=-1,s=>s.Pending[0].ReceiverLife=99,
                s=>s.Pending=s.Pending.Concat(s.Pending).ToArray() };
            foreach(var corrupt in corruptions){var snap=k.Capture();corrupt(snap);Assert.Throws<ArgumentException>(()=>k.Restore(snap));Assert.That(Json(k),Is.EqualTo(initial));}
        }
    }
}
