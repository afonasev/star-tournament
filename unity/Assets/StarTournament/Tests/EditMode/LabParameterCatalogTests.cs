using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class LabParameterCatalogTests
    {
        string directory,path;LabBundle baseline;
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-lab-catalog-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");baseline=new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateBloodDefault()}};}
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}
        [TestCase("boolean",1,1,LabParameterKind.Boolean)]
        [TestCase("count",1,0,LabParameterKind.Integer)]
        [TestCase("count",2,1,LabParameterKind.Integer)]
        [TestCase("seconds",.1f,1,LabParameterKind.Decimal)]
        [TestCase("units",1,.5f,LabParameterKind.Decimal)]
        public void TypeFollowsDeclaredValueLatticeWithoutChangingStorage(string unit,float step,float anchor,LabParameterKind expected)
        {
            var d=new NumericDescriptor{Path="test",Group="test",Label="Test",Description="Test",Unit=unit,Minimum=0,Maximum=10,Step=step,DefaultValue=anchor};
            string serialized=JsonUtility.ToJson(d);Assert.That(d.Kind,Is.EqualTo(expected));Assert.That(d.Application,Is.EqualTo(LabParameterApplication.NextGame));
            Assert.That(JsonUtility.ToJson(d),Is.EqualTo(serialized));Assert.That(serialized,Does.Not.Contain("Kind").And.Not.Contain("Application"));
        }
        [TestCase(.34f,.35f,true)][TestCase(.35f,.35f,false)][TestCase(.40f,.35f,false)]
        public void KillNoticeAreaRequiresOrderedAnchorsAndInvalidDraftCannotPublish(float bottom,float top,bool valid)
        {
            var h=new DesignLabHistory(path,baseline);h.Create("Notice area");var bytes=File.ReadAllBytes(path);string selected=h.Selected.Hash;
            var draft=h.Selected.Snapshot;draft.Set("ui.killNoticeBottom",bottom);draft.Set("ui.killNoticeTop",top);
            if(valid){Assert.That(draft.Validate(),Is.Empty);h.Save(draft);Assert.That(h.Selected.Snapshot.Get("ui.killNoticeBottom"),Is.EqualTo(bottom));}
            else
            {
                Assert.That(draft.Validate().Any(i=>i.Path=="ui.killNoticeTop"),Is.True);Assert.Throws<ArgumentException>(()=>h.Save(draft));
                Assert.That(h.Selected.Hash,Is.EqualTo(selected));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            }
        }
        [Test]public void HistoricalInvalidNoticeAreaRemainsUntouchedAndCannotActivate()
        {
            var h=new DesignLabHistory(path,baseline);h.Create("Old notice area");var file=DesignLabHistoryTests.ReadFixture(path,baseline);
            var revision=file.Profiles.Single(p=>p.Id==file.SelectedId).Revisions.Single(r=>r.Number==file.SelectedRevision);
            revision.Snapshot.Set("ui.killNoticeBottom",.40f);revision.Snapshot.Set("ui.killNoticeTop",.35f);revision.Hash=revision.Snapshot.Hash();
            File.WriteAllText(path,JsonUtility.ToJson(file));var bytes=File.ReadAllBytes(path);
            var invalid=new DesignLabHistory(path,baseline,persistMigration:false);Assert.That(invalid.Writable,Is.False);
            Assert.That(invalid.StorageError,Does.Contain("ui.killNoticeTop"));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test]public void PackageDefaultUsesHighestSavedRevisionAndIsDetachedFromSelectedExperiment()
        {
            var catalog=LabReleaseCatalog.Factory(baseline);var tuned=baseline.Clone();tuned.Set("rifle.damage",31);tuned.Set("blood.enabled",0);
            catalog.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=5,Date="20261017",Hash=tuned.Hash(),Snapshot=tuned});
            var laterSequence=baseline.Clone();laterSequence.Set("rifle.damage",32);
            catalog.Entries.Add(new LabReleaseEntry{Sequence=3,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=2,Date="20261017",Hash=laterSequence.Hash(),Snapshot=laterSequence});
            var h=new DesignLabHistory(path,baseline,releases:catalog,resetToLatestDefault:true,persistMigration:false);h.Create("Experiment");var draft=h.Selected.Snapshot;draft.Set("rifle.damage",33);h.Save(draft);
            var bytes=File.ReadAllBytes(path);string selected=h.Selected.Hash;
            var defaults=h.ReleaseParameterDefaults();Assert.That(defaults.Get("rifle.damage"),Is.EqualTo(31));Assert.That(defaults.Get("blood.enabled"),Is.Zero);
            defaults.Set("rifle.damage",40);Assert.That(h.ReleaseParameterDefaults().Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(h.Selected.Hash,Is.EqualTo(selected));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test]public void ProjectDefaultTracksMarksWithoutSelectingOrSubstitutingFactoryValues()
        {
            var h=new DesignLabHistory(path,baseline,releases:LabReleaseCatalog.Factory(baseline),authoring:true,persistMigration:false);
            var draft=h.Selected.Snapshot;draft.Set("rifle.damage",31);h.Save(draft);int marked=h.Selected.Number;h.MarkSelectedForRelease(true);
            draft=h.Selected.Snapshot;draft.Set("rifle.damage",32);h.Save(draft);string selected=h.Selected.Hash;
            var bytes=File.ReadAllBytes(path);Assert.That(h.ReleaseParameterDefaults().Get("rifle.damage"),Is.EqualTo(31));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            h.MarkForRelease(DesignLabHistory.ReleaseId,marked,false);Assert.That(h.ReleaseParameterDefaults().Get("rifle.damage"),Is.EqualTo(baseline.Get("rifle.damage")));
            foreach(var r in h.SelectedProfile.Revisions.Where(r=>r.ReleaseCandidate))h.MarkForRelease(DesignLabHistory.ReleaseId,r.Number,false);
            bytes=File.ReadAllBytes(path);Assert.Throws<InvalidOperationException>(()=>h.ReleaseParameterDefaults());Assert.That(h.Selected.Hash,Is.EqualTo(selected));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
    }
}
