using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
 public sealed class ForgeReactorProfileTests
 {
  [Test]public void HistoricalV2KeepsBytesAndCustomCombatWhileAddingNamedReactorControls()
  {
   string path=Path.Combine(Path.GetTempPath(),"forge-reactor-history-"+Guid.NewGuid()+".json");
   try
   {
    var current=ProvingProfile.CreatePlasmaDefault();
    var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeForgeReactor",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(current,null);
    Assert.That(old.Version,Is.EqualTo(2));Assert.That(old.Descriptors.Any(d=>d.Path.StartsWith("plasma.reactor")),Is.False);
    var history=new DesignLabHistory(path,new LabBundle{Profiles={old}});history.Create("Custom FORGE v2");
    var draft=history.Selected.Snapshot;draft.Set("plasma.damage",33);draft.Set("plasma.viewScale",.6f);history.Save(draft);
    string hash=history.Selected.Hash,bytes=JsonUtility.ToJson(history.Selected.Snapshot);
    var upgraded=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(upgraded.StorageError,Is.Null);
    Assert.That(upgraded.Selected.Snapshot.Get("plasma.damage"),Is.EqualTo(33));Assert.That(upgraded.Selected.Snapshot.Get("plasma.viewScale"),Is.EqualTo(.6f));
    Assert.That(JsonUtility.ToJson(upgraded.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
    foreach(var d in current.Descriptors.Where(d=>d.Path.StartsWith("plasma.reactor")))
    {Assert.That(d.Validate(out _),Is.True);Assert.That(upgraded.Selected.Snapshot.Get(d.Path),Is.EqualTo(current.Get(d.Path)));}
    Assert.That(current.Validate(),Is.Empty);
   }
   finally{if(File.Exists(path))File.Delete(path);}
  }
 }
}
