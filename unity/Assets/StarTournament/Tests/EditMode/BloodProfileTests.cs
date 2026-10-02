using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class BloodProfileTests
    {
        [Test] public void BloodFieldsAreEditablePresentationWithValidMetadataAndSteps()
        {
            var p=ProvingProfile.CreateBloodDefault();var bundle=new LabBundle{Profiles={p}};
            Assert.That(bundle.Validate(),Is.Empty);Assert.That(p.Descriptors.All(d=>d.Path.StartsWith("blood.")),Is.True);
            foreach(var d in p.Descriptors){Assert.That(d.Validate(out var reason),Is.True,reason);Assert.That(bundle.IsEditable(d.Path),Is.True);Assert.That(bundle.Domain(d.Path),Is.EqualTo("Presentation"));}
            p.Set("blood.markSeconds",0);Assert.That(p.Validate(),Is.Not.Empty);
        }
        [TestCase(false)][TestCase(true)] public void PreBloodHistoryKeepsItsImmutableHashAndMigratesToEditableBloodFields(bool beforeBonusAlerts)
        {
            string path=Path.Combine(Path.GetTempPath(),"blood-history-"+Guid.NewGuid()+".json");
            try
            {
                var old=DesignLabHistoryTests.Shipped();old.Profiles.RemoveAll(p=>p.Id==ProvingProfile.BloodPresentationId);if(beforeBonusAlerts)old.Profiles=old.Profiles.Select(p=>p.BeforeDamageBonusAlerts()).ToList();var history=new DesignLabHistory(path,old);history.Create("Blood test");
                string id=history.SelectedProfileId;var draft=history.Selected.Snapshot;draft.Set("rifle.damage",31);history.Save(draft);string hash=history.Selected.Hash;int revision=history.Selected.Number;
                var current=DesignLabHistoryTests.Shipped();var upgraded=new DesignLabHistory(path,current);
                Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.Selected.Snapshot.Get("blood.markSeconds"),Is.EqualTo(30));
                Assert.That(upgraded.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Number==revision).Hash,Is.EqualTo(hash));
                var tuned=upgraded.Selected.Snapshot;tuned.Set("blood.markSeconds",12);upgraded.Save(tuned);
                Assert.That(new DesignLabHistory(path,current).Selected.Snapshot.Get("blood.markSeconds"),Is.EqualTo(12));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
