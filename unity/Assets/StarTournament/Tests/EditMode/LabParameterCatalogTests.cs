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
