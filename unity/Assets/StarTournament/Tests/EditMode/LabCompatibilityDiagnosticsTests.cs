using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class LabCompatibilityDiagnosticsTests
    {
        string directory,path;LabBundle baseline;
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-lab-compat-errors-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");baseline=new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault()}};}
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}
        void Seed(){var h=new DesignLabHistory(path,baseline);h.Create("Experiment");}
        DesignLabHistory Reject(LabBundle current,params string[] expected)
        {
            var bytes=File.ReadAllBytes(path);var h=new DesignLabHistory(path,current,releases:LabReleaseCatalog.Factory(current),persistMigration:false);
            Assert.That(h.Writable,Is.False);foreach(var text in expected)Assert.That(h.StorageError,Does.Contain(text));
            Assert.Throws<IOException>(()=>h.Create("must not replace source"));Assert.Throws<InvalidOperationException>(()=>h.Save(h.Selected.Snapshot));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));Assert.That(h.SelectedProfileId,Is.EqualTo(DesignLabHistory.ReleaseId));
            Assert.That(h.Selected.Snapshot.Hash(),Is.EqualTo(current.Hash()));return h;
        }
        void Write(LabParameterFile file)
        {
            file.Integrity=(string)typeof(DesignLabHistory).GetMethod("StorageIntegrity",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{file});
            File.WriteAllText(path,JsonUtility.ToJson(file));
        }
        [Test]public void ChangedRangeReportsSavedIdentityAndPathWithoutClampingOrWriting()
        {
            var h=new DesignLabHistory(path,baseline);h.Create("Range");var draft=h.Selected.Snapshot;
            var descriptor=draft.Profile("rifle.damage").Descriptor("rifle.damage");draft.Set("rifle.damage",descriptor.Maximum);h.Save(draft);
            var future=baseline.Clone();var changed=future.Profile("rifle.damage").Descriptor("rifle.damage");changed.Maximum=changed.DefaultValue+1;
            Assert.That(changed.Maximum,Is.LessThan(descriptor.Maximum));
            Reject(future,h.SelectedProfileId,"v"+h.Selected.Number,"rifle.damage","outside");
        }
        [TestCase(false)][TestCase(true)]
        public void LegacyOwnValuesOutsideNewConstraintsAreRefusedWithoutClamping(bool well)
        {
            baseline.Profiles.Add(well?ProvingProfile.CreateWellDefault():ProvingProfile.CreateTrooperDefault());
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var rebalance=typeof(ProvingProfile).GetMethod("BeforeWeaponRebalance",flags);
            var method=typeof(ProvingProfile).GetMethod(well?"BeforeWellDetail":"BeforeLowWeaponSwitch",flags);
            var previous=new LabBundle{Profiles=baseline.Profiles.Select(p=>(ProvingProfile)method.Invoke(well?p:rebalance.Invoke(p,null),null)).ToList()};
            var old=new DesignLabHistory(path,previous);old.Create("Saved values");
            string field=well?"well.fill":"view.switchShoulderDegrees";
            Assert.That(old.Selected.Snapshot.Get(field),Is.EqualTo(well?.8f:145));
            // Constrain the future grid explicitly: the current Well grid now accepts .8 exactly.
            if(well)baseline.Profile(field).Descriptor(field).Step=.05f;
            Reject(baseline,old.SelectedProfileId,field,well?"шагу":"outside");
        }
        [TestCase(0)][TestCase(2)]public void UnrecognizedSchemaVersionReportsProfileIdentityAndPreservesSource(int index)
        {
            baseline.Profiles.Add(ProvingProfile.CreateTrooperDefault());
            Seed();var file=JsonUtility.FromJson<LabParameterFile>(File.ReadAllText(path));var shape=file.Schemas[0].Profiles[index];shape.Version=999;Write(file);
            Reject(baseline,"схема Lab",shape.Id+"@999");
        }
        [Test]public void UnavailableAuthoredDefinitionReportsResourceIdentityAndPreservesSource()
        {
            Seed();var file=JsonUtility.FromJson<LabParameterFile>(File.ReadAllText(path));var shape=file.Schemas[0].Profiles[0];
            shape.Id="well-authoring-v1";shape.Version=999;shape.DefinitionHash=new string('1',64);shape.Paths.Clear();Write(file);
            Reject(baseline,"определение ресурса","well-authoring-v1@999");
        }
        [Test]public void UnknownFieldReportsItsPathAndPreservesLegacySource()
        {
            Seed();var file=DesignLabHistoryTests.ReadFixture(path,baseline);var revision=file.Profiles.Last().Revisions.Last();var profile=revision.Snapshot.Profiles.First();
            ((List<NumericDescriptor>)profile.Descriptors).Add(new NumericDescriptor{Path="retired.setting",Group="legacy",Label="Retired",Description="Unknown old parameter",Unit="ratio",Minimum=0,Maximum=1,Step=1,DefaultValue=1});
            ((List<ProvingProfileValue>)typeof(ProvingProfile).GetField("values",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(profile)).Add(new ProvingProfileValue{Path="retired.setting",Value=1});
            revision.Snapshot=JsonUtility.FromJson<LabBundle>(JsonUtility.ToJson(revision.Snapshot));
            revision.Hash=revision.Snapshot.Hash();File.WriteAllText(path,JsonUtility.ToJson(file));
            Reject(baseline,"Неподдерживаемая схема Lab","retired.setting");
        }
    }
}
