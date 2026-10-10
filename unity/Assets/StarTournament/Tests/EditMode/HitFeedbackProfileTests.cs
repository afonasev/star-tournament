using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class HitFeedbackProfileTests
    {
        [Test] public void ContactControlsAreEditablePresentationAndHaveValidMetadata()
        {
            var profile=ProvingProfile.CreateHitFeedbackDefault();var bundle=new LabBundle{Profiles={profile}};
            Assert.That(bundle.Validate(),Is.Empty);
            foreach(var descriptor in profile.Descriptors)
            {
                Assert.That(descriptor.Validate(out var reason),Is.True,reason);
                Assert.That(bundle.IsEditable(descriptor.Path),Is.True);Assert.That(bundle.Domain(descriptor.Path),Is.EqualTo("Presentation"));
            }
        }
        [Test] public void AddingContactRegistryPreservesHistoricalHashAndCustomValues()
        {
            string path=Path.Combine(Path.GetTempPath(),"hit-feedback-history-"+Guid.NewGuid()+".json");
            try
            {
                var current=DesignLabHistoryTests.Shipped();var old=current.Clone();old.Profiles.RemoveAll(p=>p.Id==ProvingProfile.HitFeedbackPresentationId);
                var history=new DesignLabHistory(path,old);history.Create("Мои эффекты");var draft=history.Selected.Snapshot;draft.Set("blood.markSeconds",42);history.Save(draft);
                var selected=history.Selected;string id=history.SelectedProfileId;
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                Assert.That(upgraded.Selected.Snapshot.Get("blood.markSeconds"),Is.EqualTo(42));
                Assert.That(upgraded.Selected.Snapshot.Get("hit.degrees"),Is.EqualTo(12));
                var preserved=upgraded.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Number==selected.Number);
                Assert.That(preserved.Hash,Is.EqualTo(selected.Hash));Assert.That(preserved.Snapshot.Profiles.Any(p=>p.Id==ProvingProfile.HitFeedbackPresentationId),Is.False);
                Assert.That(new DesignLabHistory(path,current).StorageError,Is.Null);
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
