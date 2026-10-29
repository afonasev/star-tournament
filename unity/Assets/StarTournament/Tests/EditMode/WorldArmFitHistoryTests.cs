using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
 public sealed class WorldArmFitHistoryTests
 {
  [TestCase(false)] [TestCase(true)]
  public void Version33SnapshotsAndAuthoredValuesRemainExact(bool authored)
  {
   var now=ProvingProfile.CreateTrooperDefault();
   var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWorldArmFit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(now,null);
   Assert.That(now.Version,Is.EqualTo(34));Assert.That(old.Version,Is.EqualTo(33));
   Assert.That(old.Descriptors.Any(d=>d.Path.StartsWith("grip.world.")),Is.False);
   foreach(var d in old.Descriptors)Assert.That(now.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
   foreach(var d in now.Descriptors.Where(d=>d.Path.StartsWith("grip.world.")))
   {Assert.That(d.Validate(out _),Is.True,d.Path);Assert.That(d.Group,Is.EqualTo("trooper-world-grip"));Assert.That(d.Step,Is.GreaterThan(0));}
   var path=Path.Combine(Path.GetTempPath(),"world-arm-history-"+Guid.NewGuid()+".json");
   try
   {
    var prior=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{old}});
    if(authored)prior.Create("Authored grip");else prior.Rename("Default");
    var saved=prior.Selected.Snapshot;
    if(authored){saved.Set("grip.Rifle.supportYaw",23);prior.Save(saved);}
    string hash=prior.Selected.Hash,bytes=JsonUtility.ToJson(prior.Selected.Snapshot);
    var current=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{now}});
    Assert.That(current.StorageError,Is.Null);
    foreach(var d in old.Descriptors)Assert.That(current.Selected.Snapshot.Get(d.Path),Is.EqualTo(saved.Get(d.Path)),d.Path);
    Assert.That(current.Selected.Snapshot.Get("grip.world.enabled"),Is.EqualTo(1));
    var retained=current.Profiles.SelectMany(p=>p.Revisions).Where(r=>r.Hash==hash).ToArray();Assert.That(retained,Is.Not.Empty);
    foreach(var r in retained)Assert.That(JsonUtility.ToJson(r.Snapshot),Is.EqualTo(bytes));
    var reopened=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{now}});
    Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(current.Selected.Hash));
   }
   finally{if(File.Exists(path))File.Delete(path);}
  }
 }
}
