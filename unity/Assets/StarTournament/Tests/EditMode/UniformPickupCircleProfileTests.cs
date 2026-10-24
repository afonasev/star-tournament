using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class UniformPickupCircleProfileTests
    {
        static ProvingProfile Before(ProvingProfile p)=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeUniformPickupRadii",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
        [Test] public void AllDefaultsAgreeAndOnlyRadiusMetadataChanges()
        {
            foreach(var p in new[]{ProvingProfile.CreateOrbitalLeagueDefault(),ProvingProfile.CreateCombatBowlRingPresentationDefault(),ProvingProfile.CreateIndustrialTunnelsPresentation(),ProvingProfile.CreateLunarPresentation()})
            {
                string path=p.Id.StartsWith("orbital-league")?"detail.ringRadius":p.Id==ProvingProfile.TunnelsArtId?"tunnels.pickupRingRadius":"lunar.pickupRingRadius";
                Assert.That(p.Validate(),Is.Empty);Assert.That(p.Get(path),Is.EqualTo(1.35f));var old=Before(p);
                foreach(var d in old.Descriptors.Where(d=>d.Path!=path)){Assert.That(JsonUtility.ToJson(p.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)),d.Path);Assert.That(p.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);}
                if(path.StartsWith("lunar.")){var r=p.Descriptors.Single(d=>d.Path==path);var w=p.Descriptors.Single(d=>d.Path=="lunar.pickupRingWidth");Assert.That(r.Maximum+w.Maximum/2,Is.LessThan(1.5f),"H/V separation and balcony half-width");}
            }
            Assert.That(ProvingProfile.CreateWellDefault().Get("well.pickupRingRadius"),Is.EqualTo(1.35f));Assert.That(ProvingProfile.CreateIndustrialTunnelsPresentation().Get("tunnels.wayfinding.ringRadius"),Is.EqualTo(2.2f),"Yellow circle unchanged");
        }
        [TestCase(false)][TestCase(true)] public void FactoryRadiusUpgradesButHistoryAndCustomValuesRemainExact(bool compact)
        {
            string directory=Path.Combine(Path.GetTempPath(),"uniform-circles-"+Guid.NewGuid());Directory.CreateDirectory(directory);string path=Path.Combine(directory,"history.json");
            try
            {
                var current=DesignLabHistoryTests.Shipped();var old=current.Clone();old.Profiles=old.Profiles.Select(Before).ToList();var history=new DesignLabHistory(path,old);string factoryHash=history.Selected.Hash;
                history.Create("Custom before unified circles");var draft=history.Selected.Snapshot.Clone();draft.Set("tunnels.pickupRingRadius",1.12f);draft.Set("lunar.pickupRingRadius",.91f);draft.Set("lunar.officeLamp",19);history.Save(draft);string hash=history.Selected.Hash;int number=history.Selected.Number;
                if(!compact){var file=typeof(DesignLabHistory).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(history);File.WriteAllText(path,JsonUtility.ToJson(file));}
                var next=new DesignLabHistory(path,current);Assert.That(next.StorageError,Is.Null);var original=next.SelectedProfile.Revisions.Single(r=>r.Number==number);Assert.That(original.Hash,Is.EqualTo(hash));Assert.That(original.Snapshot.Hash(),Is.EqualTo(hash));
                Assert.That(next.Selected.Snapshot.Get("detail.ringRadius"),Is.EqualTo(1.65f));Assert.That(next.Selected.Snapshot.Get("tunnels.pickupRingRadius"),Is.EqualTo(1.12f));Assert.That(next.Selected.Snapshot.Get("lunar.pickupRingRadius"),Is.EqualTo(.91f));Assert.That(next.Selected.Snapshot.Get("lunar.officeLamp"),Is.EqualTo(19));
                var factory=next.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId);Assert.That(factory.Revisions.Single(r=>r.Number==1).Hash,Is.EqualTo(factoryHash));var latest=factory.Revisions.OrderByDescending(r=>r.Number).First().Snapshot;
                foreach(var key in new[]{"detail.ringRadius","tunnels.pickupRingRadius","lunar.pickupRingRadius"})Assert.That(latest.Get(key),Is.EqualTo(1.35f),key);
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(next.Selected.Hash));
            }
            finally{Directory.Delete(directory,true);}
        }
        [Test] public void SerializedBowlProfileAdoptsDefaultAndKeepsCustomRadius()
        {
            var old=Before(ProvingProfile.CreateCombatBowlRingPresentationDefault());old.EnsureOrbitalLeagueDescriptors();Assert.That(old.Version,Is.EqualTo(11));Assert.That(old.Get("detail.ringRadius"),Is.EqualTo(1.35f));
            old=Before(ProvingProfile.CreateCombatBowlRingPresentationDefault());old.Set("detail.ringRadius",1.5f);old.EnsureOrbitalLeagueDescriptors();Assert.That(old.Get("detail.ringRadius"),Is.EqualTo(1.5f));
        }
    }
}
