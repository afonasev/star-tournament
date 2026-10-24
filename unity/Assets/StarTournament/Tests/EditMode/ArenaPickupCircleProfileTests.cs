using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ArenaPickupCircleProfileTests
    {
        static ProvingProfile Before(ProvingProfile p)=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeArenaPickupMarkings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
        [TestCase(false)][TestCase(true)] public void MetadataIsAdditiveAndFitsExistingSurfaces(bool lunar)
        {
            var current=lunar?ProvingProfile.CreateLunarPresentation():ProvingProfile.CreateIndustrialTunnelsPresentation();var old=Before(current);string prefix=lunar?"lunar.":"tunnels.";
            Assert.That(current.Version,Is.EqualTo(lunar?6:4));Assert.That(old.Version,Is.EqualTo(lunar?4:2));Assert.That(current.Validate(),Is.Empty);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)),d.Path);Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);}
            foreach(var key in new[]{"pickupRingRadius","pickupRingWidth","pickupRingHeight"}){var descriptor=current.Descriptors.Single(d=>d.Path==prefix+key);Assert.That(descriptor.Validate(out var reason),Is.True,reason);Assert.That(descriptor.Group,Is.EqualTo("pickup-markings"));}
            Assert.That(current.Get(prefix+"pickupRingWidth"),Is.EqualTo(ProvingProfile.CreateOrbitalLeagueDefault().Get("detail.lineWidth")));
            if(lunar)Assert.That(current.Get("lunar.pickupRingHeight"),Is.GreaterThan(.008f),"Paint clears existing label plate top");
            if(lunar)Assert.That(current.Get(prefix+"pickupRingRadius")+current.Get(prefix+"pickupRingWidth")/2,Is.LessThan(1.5f),"Balcony half width and H/V half separation");
        }
        [TestCase(false)][TestCase(true)] public void HistoricalHashesAndCustomValuesSurvive(bool compact)
        {
            string directory=Path.Combine(Path.GetTempPath(),"all-arena-circles-"+Guid.NewGuid());Directory.CreateDirectory(directory);string path=Path.Combine(directory,"history.json");
            try
            {
                var current=DesignLabHistoryTests.Shipped();var old=current.Clone();old.Profiles=old.Profiles.Select(Before).ToList();
                var history=new DesignLabHistory(path,old);history.Create("До белых кругов");var draft=history.Selected.Snapshot.Clone();draft.Set("lunar.officeLamp",19);draft.Set("tunnels.light.warm",11);history.Save(draft);
                string hash=history.Selected.Hash;int number=history.Selected.Number;
                if(!compact){var file=typeof(DesignLabHistory).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(history);File.WriteAllText(path,JsonUtility.ToJson(file));}
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                var preserved=upgraded.SelectedProfile.Revisions.Single(r=>r.Number==number);Assert.That(preserved.Hash,Is.EqualTo(hash));Assert.That(preserved.Snapshot.Hash(),Is.EqualTo(hash));
                Assert.That(upgraded.Selected.Snapshot.Get("lunar.officeLamp"),Is.EqualTo(19));Assert.That(upgraded.Selected.Snapshot.Get("tunnels.light.warm"),Is.EqualTo(11));
                Assert.That(upgraded.Selected.Snapshot.Profiles.Single(p=>p.Id==ProvingProfile.TunnelsArtId).Version,Is.EqualTo(4));Assert.That(upgraded.Selected.Snapshot.Profiles.Single(p=>p.Id=="lunar-laboratory-presentation-v1").Version,Is.EqualTo(6));
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(upgraded.Selected.Hash));Assert.That(reopened.SelectedProfile.Revisions.Single(r=>r.Number==number).Hash,Is.EqualTo(hash));
            }
            finally{Directory.Delete(directory,true);}
        }
    }
}
