using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class LabParameterTypeTests
    {
        string directory,path;LabBundle baseline;
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-lab-types-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");baseline=new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateBloodDefault(),ProvingProfile.CreateHitFeedbackDefault()}};}
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}
        [Test]public void ActualBooleanDeclarationsHaveExactDomainAndValidDefaults()
        {
            var descriptors=baseline.Descriptors.Where(d=>d.Unit=="boolean").ToArray();
            Assert.That(descriptors.Select(d=>d.Path),Is.EquivalentTo(new[]{"blood.enabled","hit.enabled"}));
            foreach(var d in descriptors){Assert.That(d.Validate(out var reason),Is.True,reason);Assert.That(d.Minimum,Is.Zero);Assert.That(d.Maximum,Is.EqualTo(1));Assert.That(d.Step,Is.EqualTo(1));}
            Assert.That(baseline.Validate(),Is.Empty);
        }
        [TestCase("blood.enabled",.5f)][TestCase("hit.enabled",.25f)][TestCase("blood.enabled",-1f)][TestCase("hit.enabled",2f)]
        [TestCase("blood.enabled",float.NaN)][TestCase("hit.enabled",float.PositiveInfinity)]
        public void InvalidBooleanDraftIsRejectedWithoutCoercionOrSourceWrite(string key,float value)
        {
            var h=new DesignLabHistory(path,baseline);h.Create("Types");var hash=h.Selected.Hash;var bytes=File.ReadAllBytes(path);
            var draft=h.Selected.Snapshot;draft.Set(key,value);
            Assert.That(draft.Validate().Any(i=>i.Path==key&&i.Message.Contains("0 или 1")),Is.True);
            Assert.Throws<ArgumentException>(()=>h.Save(draft));Assert.That(h.Selected.Hash,Is.EqualTo(hash));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            if(float.IsNaN(value))Assert.That(float.IsNaN(draft.Get(key)),Is.True);else Assert.That(draft.Get(key),Is.EqualTo(value));
        }
        [TestCase("blood.enabled")][TestCase("hit.enabled")]
        public void CallerMetadataCannotBypassBooleanStorageValidation(string key)
        {
            var h=new DesignLabHistory(path,baseline);h.Create("Metadata spoof");var bytes=File.ReadAllBytes(path);string hash=h.Selected.Hash;
            var draft=h.Selected.Snapshot;draft.Profile(key).Descriptor(key).Unit="ratio";draft.Profile(key).Descriptor(key).Step=.5f;draft.Set(key,.5f);
            Assert.That(draft.Validate(),Is.Empty,"Spoofed caller metadata alone would hide the invalid boolean");
            Assert.Throws<InvalidDataException>(()=>h.Save(draft));Assert.That(h.Selected.Hash,Is.EqualTo(hash));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [TestCase("blood.enabled")][TestCase("hit.enabled")]
        public void BothBooleanStatesSaveReloadAndRetainOriginalHistory(string key)
        {
            var h=new DesignLabHistory(path,baseline);h.Create("Boolean");string original=h.Selected.Hash;
            var draft=h.Selected.Snapshot;draft.Set(key,0);h.Save(draft);string zero=h.Selected.Hash;
            var read=new DesignLabHistory(path,baseline);Assert.That(read.StorageError,Is.Null);Assert.That(read.Selected.Snapshot.Get(key),Is.Zero);Assert.That(read.Selected.Hash,Is.EqualTo(zero));
            draft=read.Selected.Snapshot;draft.Set(key,1);read.Save(draft);var again=new DesignLabHistory(path,baseline);
            Assert.That(again.StorageError,Is.Null);Assert.That(again.Selected.Snapshot.Get(key),Is.EqualTo(1));Assert.That(again.SelectedProfile.Revisions.Count,Is.EqualTo(3));
            Assert.That(again.SelectedProfile.Revisions[0].Hash,Is.EqualTo(original));Assert.That(again.SelectedProfile.Revisions[0].Snapshot.Get(key),Is.EqualTo(1));
        }
        [TestCase("minimum")][TestCase("maximum")][TestCase("step")][TestCase("default")]
        public void BooleanMetadataCannotDeclareFractionalOrWiderDomain(string field)
        {
            var d=JsonUtility.FromJson<NumericDescriptor>(JsonUtility.ToJson(baseline.Profile("blood.enabled").Descriptor("blood.enabled")));
            if(field=="minimum")d.Minimum=-1;if(field=="maximum")d.Maximum=2;if(field=="step")d.Step=.5f;if(field=="default")d.DefaultValue=.5f;
            Assert.That(d.Validate(out var reason),Is.False);Assert.That(reason,Does.Contain("Логический параметр"));
        }
        [TestCase("blood.enabled")][TestCase("hit.enabled")]
        public void HistoricalFractionRemainsUntouchedAndReportsTheTypedProblem(string key)
        {
            var h=new DesignLabHistory(path,baseline);h.Create("Old numeric switch");
            var file=DesignLabHistoryTests.ReadFixture(path,baseline);var revision=file.Profiles.Last().Revisions.Single();revision.Snapshot.Set(key,.5f);revision.Hash=revision.Snapshot.Hash();
            File.WriteAllText(path,JsonUtility.ToJson(file));var bytes=File.ReadAllBytes(path);
            var invalid=new DesignLabHistory(path,baseline,releases:LabReleaseCatalog.Factory(baseline),persistMigration:false);
            Assert.That(invalid.Writable,Is.False);Assert.That(invalid.StorageError,Does.Contain(key).And.Contain("0 или 1"));
            Assert.Throws<IOException>(()=>invalid.CompactStorage());CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
    }
}
