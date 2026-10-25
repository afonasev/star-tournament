using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class RifleSteelPresentationTests
    {
        static ProvingProfile Previous()=> (ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeRifleSteelPresentation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateDefault(),null);
        [Test] public void UpgradeChangesOldDefaultsAndRetainsCustomValues()
        {
            var p=Previous();p.Set("presentation.rifleTracerLength",2.2f);p.Set("presentation.rifleTracerRed",.3f);
            p.EnsureDefaultDescriptors();
            Assert.That(p.Version,Is.EqualTo(19));Assert.That(p.Validate(),Is.Empty);
            Assert.That(p.Get("presentation.rifleTracerLength"),Is.EqualTo(2.2f));
            Assert.That(p.Get("presentation.rifleTracerRed"),Is.EqualTo(.3f));
            Assert.That(p.Get("presentation.rifleTracerGreen"),Is.EqualTo(.55f));
            Assert.That(p.Get("presentation.rifleTracerBlue"),Is.EqualTo(.5f));
        }
        [Test] public void HistoryRetainsV18HashesAndCustomTuningOnRepeatedOpen()
        {
            var old=new LabBundle{Profiles=new List<ProvingProfile>{Previous()}};
            var current=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateDefault()}};
            string path=Path.Combine(Path.GetTempPath(),"rifle-steel-"+Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,old);string released=history.Selected.Hash;
                history.Create("Custom trace");var draft=history.Selected.Snapshot;
                draft.Set("presentation.rifleTracerLength",2.2f);draft.Set("presentation.rifleTracerRed",.3f);history.Save(draft);string custom=history.Selected.Hash;
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("presentation.rifleTracerLength"),Is.EqualTo(2.2f));
                Assert.That(migrated.Selected.Snapshot.Get("presentation.rifleTracerRed"),Is.EqualTo(.3f));
                var hashes=migrated.Profiles.SelectMany(p=>p.Revisions).Select(v=>v.Hash).ToArray();
                Assert.That(hashes,Does.Contain(released));Assert.That(hashes,Does.Contain(custom));
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                migrated.Select(DesignLabHistory.ReleaseId,migrated.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Last().Number);
                Assert.That(migrated.Selected.Snapshot.Get("presentation.rifleTracerLength"),Is.EqualTo(1.5f));
                Assert.That(migrated.Selected.Snapshot.Get("presentation.rifleTracerRed"),Is.EqualTo(.56f));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
