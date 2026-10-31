using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class PulseDisplayHistoryTests
    {
        static ProvingProfile Previous()=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforePulseDisplay",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateDefault(),null);
        [Test] public void AppendControlsPreservesEveryExistingValueAndCustomSpreadTrace()
        {
            var old=Previous();var current=ProvingProfile.CreateDefault();
            foreach(var d in old.Descriptors)Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            old.Set("presentation.rifleTracerLength",.8f);old.Set("presentation.rifleTracerRed",.3f);old.EnsureDefaultDescriptors();
            Assert.That(old.Get("presentation.rifleTracerLength"),Is.EqualTo(.8f));Assert.That(old.Get("presentation.rifleTracerRed"),Is.EqualTo(.3f));Assert.That(old.Validate(),Is.Empty);
            foreach(var d in current.Descriptors.Where(d=>d.Path.StartsWith("presentation.pulseDisplay.")))Assert.That(d.Validate(out _),Is.True,d.Path);
        }
        [Test] public void PriorPublishedAndCustomRevisionsKeepTheirExactBytes()
        {
            string path=Path.Combine(Path.GetTempPath(),"pulse-display-"+Guid.NewGuid()+".json");
            try
            {
                var old=new LabBundle{Profiles=new List<ProvingProfile>{Previous()}};
                var history=new DesignLabHistory(path,old);history.Create("Custom Pulse");var draft=history.Selected.Snapshot;draft.Set("presentation.rifleTracerLength",.8f);history.Save(draft);
                var revisions=history.Profiles.SelectMany(p=>p.Revisions).Select(r=>new {Hash=r.Hash,Bytes=JsonUtility.ToJson(r.Snapshot)}).ToArray();
                var current=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateDefault()}};
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);Assert.That(migrated.Selected.Snapshot.Get("presentation.rifleTracerLength"),Is.EqualTo(.8f));
                foreach(var prior in revisions)Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==prior.Hash&&JsonUtility.ToJson(r.Snapshot)==prior.Bytes),Is.True,prior.Hash);
                var tuning=migrated.Selected.Snapshot;tuning.Set("presentation.pulseDisplay.intensity",1.4f);migrated.Save(tuning);
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Snapshot.Get("presentation.pulseDisplay.intensity"),Is.EqualTo(1.4f));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
