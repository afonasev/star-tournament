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
        [Test] public void AuthoringDefaultMatchesRuntimeWithoutChangingHistoricalOrNamedProfiles()
        {
            string directory=Path.Combine(Path.GetTempPath(),"author-circles-"+Guid.NewGuid());Directory.CreateDirectory(directory);string path=Path.Combine(directory,"history.json");
            try
            {
                var current=DesignLabHistoryTests.Shipped();var old=current.Clone();old.Profiles=old.Profiles.Select(Before).ToList();var catalog=LabReleaseCatalog.Factory(old);
                var history=new DesignLabHistory(path,old,releases:catalog,authoring:true);var original=history.Selected;string originalJson=JsonUtility.ToJson(original.Snapshot);
                history.Create("Custom rings");string customId=history.SelectedProfileId;var draft=history.Selected.Snapshot;draft.Set("tunnels.pickupRingRadius",1.12f);history.Save(draft);var custom=history.Selected;
                var next=new DesignLabHistory(path,current,releases:catalog,authoring:true,persistMigration:false);
                Assert.That(next.StorageError,Is.Null);Assert.That(next.ReleaseError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("detail.ringRadius"),Is.EqualTo(1.65f));Assert.That(next.Selected.Snapshot.Get("tunnels.pickupRingRadius"),Is.EqualTo(1.12f));
                var savedCustom=next.Profiles.Single(p=>p.Id==customId).Revisions.Single(r=>r.Number==custom.Number);Assert.That(savedCustom.Hash,Is.EqualTo(custom.Hash));Assert.That(savedCustom.Snapshot.Hash(),Is.EqualTo(custom.Hash));
                var factory=next.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId);var saved=factory.Revisions.Single(r=>r.Number==original.Number);
                Assert.That(saved.Hash,Is.EqualTo(original.Hash));Assert.That(JsonUtility.ToJson(saved.Snapshot),Is.EqualTo(originalJson));
                var effective=factory.Revisions.OrderByDescending(r=>r.Number).First().Snapshot;
                foreach(var key in new[]{"detail.ringRadius","tunnels.pickupRingRadius","lunar.pickupRingRadius"})Assert.That(effective.Get(key),Is.EqualTo(1.35f),key);
                var output=next.BuildReleaseCatalog();Assert.That(output.Entries.Single().Hash,Is.EqualTo(catalog.Entries.Single().Hash));
                Assert.That(DesignLabHistory.ResolvePublishedDefault(current,output).Selected.Hash,Is.EqualTo(effective.Hash()));
            }
            finally{Directory.Delete(directory,true);}
        }
        [TestCase(false)][TestCase(true)] public void AuthoringWellDefaultUsesCurrentLightingAndPreservesSourceAndCustomProfile(bool beforeIdleDeparture)
        {
            string directory=Path.Combine(Path.GetTempPath(),"author-well-"+Guid.NewGuid());Directory.CreateDirectory(directory);string path=Path.Combine(directory,"history.json");
            try
            {
                var current=DesignLabHistoryTests.Shipped();var before=typeof(ProvingProfile).GetMethod("BeforeWellDaylight",BindingFlags.Instance|BindingFlags.NonPublic);
                var old=current.Clone();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)before.Invoke(p,null)).ToList();
                if(beforeIdleDeparture)
                {
                    var older=typeof(ProvingProfile).GetMethod("BeforeWellIdleDeparture",BindingFlags.Instance|BindingFlags.NonPublic);
                    old.Profiles=old.Profiles.Select(p=>(ProvingProfile)older.Invoke(p,null)).ToList();
                }
                var catalog=LabReleaseCatalog.Factory(old);
                var history=new DesignLabHistory(path,old,releases:catalog,authoring:true);var original=history.Selected;string originalJson=JsonUtility.ToJson(original.Snapshot);
                history.Create("Custom Well");var draft=history.Selected.Snapshot;draft.Set("well.lampRange",15);history.Save(draft);var custom=history.Selected;
                var next=new DesignLabHistory(path,current,releases:catalog,authoring:true,persistMigration:false);
                Assert.That(next.StorageError,Is.Null);Assert.That(next.Selected.Snapshot.Get("well.daylight"),Is.EqualTo(.8f));Assert.That(next.Selected.Snapshot.Get("well.lampRange"),Is.EqualTo(15));
                Assert.That(next.SelectedProfile.Revisions.Single(r=>r.Number==custom.Number).Hash,Is.EqualTo(custom.Hash));
                var factory=next.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId);Assert.That(JsonUtility.ToJson(factory.Revisions.Single(r=>r.Number==original.Number).Snapshot),Is.EqualTo(originalJson));
                var effective=factory.Revisions.OrderByDescending(r=>r.Number).First().Snapshot;
                foreach(var key in new[]{"well.daylight","well.innerAngle","well.fill","well.lampRange","well.daylightShadow"})Assert.That(effective.Get(key),Is.EqualTo(current.Get(key)),key);
                var output=next.BuildReleaseCatalog();Assert.That(output.Entries.Single().Hash,Is.EqualTo(catalog.Entries.Single().Hash));
                Assert.That(DesignLabHistory.ResolvePublishedDefault(current,output).Selected.Hash,Is.EqualTo(effective.Hash()));
            }
            finally{Directory.Delete(directory,true);}
        }
        [Test] public void AuthoringDefaultKeepsIntegratedSteelRiflePresentation()
        {
            string directory=Path.Combine(Path.GetTempPath(),"author-steel-"+Guid.NewGuid());Directory.CreateDirectory(directory);string path=Path.Combine(directory,"history.json");
            try
            {
                var current=DesignLabHistoryTests.Shipped();var before=typeof(ProvingProfile).GetMethod("BeforeRifleSteelPresentation",BindingFlags.Instance|BindingFlags.NonPublic);
                var old=current.Clone();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)before.Invoke(p,null)).ToList();var catalog=LabReleaseCatalog.Factory(old);
                var history=new DesignLabHistory(path,old,releases:catalog,authoring:true);var original=history.Selected;history.Create("Custom old rifle");var custom=history.Selected;
                var next=new DesignLabHistory(path,current,releases:catalog,authoring:true,persistMigration:false);
                Assert.That(next.StorageError,Is.Null);Assert.That(next.Selected.Snapshot.Get("presentation.rifleTracerLength"),Is.EqualTo(3));
                Assert.That(next.SelectedProfile.Revisions.Single(r=>r.Number==custom.Number).Hash,Is.EqualTo(custom.Hash));
                var factory=next.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId);Assert.That(factory.Revisions.Single(r=>r.Number==original.Number).Hash,Is.EqualTo(original.Hash));
                var effective=factory.Revisions.OrderByDescending(r=>r.Number).First().Snapshot;
                foreach(var key in new[]{"presentation.rifleTracerLength","presentation.rifleTracerRed","presentation.rifleTracerGreen","presentation.rifleTracerBlue"})Assert.That(effective.Get(key),Is.EqualTo(current.Get(key)),key);
                var output=next.BuildReleaseCatalog();Assert.That(output.Entries.Single().Hash,Is.EqualTo(catalog.Entries.Single().Hash));
                Assert.That(DesignLabHistory.ResolvePublishedDefault(current,output).Selected.Hash,Is.EqualTo(effective.Hash()));
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
