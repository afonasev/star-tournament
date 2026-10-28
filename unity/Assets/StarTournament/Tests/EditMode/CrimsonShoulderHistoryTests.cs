using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class CrimsonShoulderHistoryTests
    {
        [Test] public void ShoulderParametersAreAdditiveAndHistoricalDefaultsRemainExact()
        {
            var current=ProvingProfile.CreateTrooperDefault();
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCrimsonShoulder",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(30));Assert.That(current.Version,Is.EqualTo(34));
            Assert.That(old.Descriptors.Any(d=>d.Path.StartsWith("grip.crimson.")),Is.False);
            foreach(var d in old.Descriptors)Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            Assert.That(current.Validate(),Is.Empty);
            current.Set("grip.crimson.shoulderY",.11f);current.EnsureTrooperDescriptors();
            Assert.That(current.Get("grip.crimson.shoulderY"),Is.EqualTo(.11f));
        }
        [TestCase(30,false)] [TestCase(30,true)] [TestCase(31,false)] [TestCase(31,true)]
        public void PriorSnapshotsAndCustomValuesSurviveUpgrade(int version,bool custom)
        {
            var path=Path.Combine(Path.GetTempPath(),"crimson-history-"+Guid.NewGuid()+".json");
            try
            {
                var now=ProvingProfile.CreateTrooperDefault();
                var old=(ProvingProfile)typeof(ProvingProfile).GetMethod(version==30?"BeforeCrimsonShoulder":"BeforeCrimsonDetail",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(now,null);
                var prior=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{old}});
                if(custom)prior.Create("Authored shoulder pose");
                var saved=prior.Selected.Snapshot;
                if(custom)saved.Set("grip.RocketLauncher.supportYaw",80);
                if(custom)prior.Save(saved);else prior.Rename("Default");var hash=prior.Selected.Hash;var bytes=JsonUtility.ToJson(prior.Selected.Snapshot);
                var current=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{now}});
                Assert.That(current.StorageError,Is.Null);
                foreach(var d in old.Descriptors)Assert.That(current.Selected.Snapshot.Get(d.Path),Is.EqualTo(saved.Get(d.Path)),d.Path);
                Assert.That(current.Selected.Snapshot.Get("grip.crimson.enabled"),Is.EqualTo(1));
                Assert.That(current.Selected.Snapshot.Get("grip.crimson.detail.scale"),Is.EqualTo(1.2f));
                var retained=current.Profiles.SelectMany(p=>p.Revisions).Where(r=>r.Hash==hash).ToArray();Assert.That(retained,Is.Not.Empty);
                foreach(var revision in retained)Assert.That(JsonUtility.ToJson(revision.Snapshot),Is.EqualTo(bytes));
                var reopened=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{now}});
                Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(current.Selected.Hash));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
