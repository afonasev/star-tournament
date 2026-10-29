using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class CrimsonRecoilTests
    {
        [Test] public void ImpulseIsFastBoundedAndReturnsExactlyToRest()
        {
            var p=ProvingProfile.CreateTrooperDefault();double kick=p.Get("grip.crimson.recoil.kickSeconds"),returnTime=p.Get("grip.crimson.recoil.recoverySeconds");
            Assert.That(returnTime,Is.GreaterThan(kick*5));
            Assert.That(CrimsonRecoilSample.Read(p,WeaponId.RocketLauncher,0).Weight,Is.Zero);
            var peak=CrimsonRecoilSample.Read(p,WeaponId.RocketLauncher,kick);
            Assert.That(peak.Weight,Is.EqualTo(1).Within(.00001f));Assert.That(peak.BackMeters,Is.GreaterThan(.03f));Assert.That(peak.PitchDegrees,Is.GreaterThan(4));
            Assert.That(CrimsonRecoilSample.Read(p,WeaponId.RocketLauncher,kick*.5).Weight,Is.GreaterThan(.7f));
            float previous=1;
            for(int i=1;i<=100;i++)
            {
                var sample=CrimsonRecoilSample.Read(p,WeaponId.RocketLauncher,kick+returnTime*i/100);
                Assert.That(sample.Weight,Is.InRange(0,previous+.00001f));previous=sample.Weight;
            }
            Assert.That(CrimsonRecoilSample.Read(p,WeaponId.RocketLauncher,kick+returnTime+.001).Weight,Is.Zero);
        }
        [Test] public void WeaponGateDisableAndInvalidTimeCannotCarryAnImpulse()
        {
            var p=ProvingProfile.CreateTrooperDefault();
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))if(weapon!=WeaponId.RocketLauncher)Assert.That(CrimsonRecoilSample.Read(p,weapon,.04).Weight,Is.Zero);
            foreach(double age in new[]{-1d,double.NegativeInfinity,double.PositiveInfinity,double.NaN})Assert.That(CrimsonRecoilSample.Read(p,WeaponId.RocketLauncher,age).Weight,Is.Zero);
            var first=CrimsonRecoilSample.Read(p,WeaponId.RocketLauncher,.13);var again=CrimsonRecoilSample.Read(p,WeaponId.RocketLauncher,.13);
            Assert.That(again.BackMeters,Is.EqualTo(first.BackMeters));Assert.That(again.PitchDegrees,Is.EqualTo(first.PitchDegrees));
            p.Set("grip.crimson.recoil.enabled",0);Assert.That(CrimsonRecoilSample.Read(p,WeaponId.RocketLauncher,.04).Weight,Is.Zero);
        }
        [TestCase(false)] [TestCase(true)] public void Version32HistoryAndCustomGripSurvive(bool custom)
        {
            var now=ProvingProfile.CreateTrooperDefault();var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCrimsonRecoil",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(now,null);
            Assert.That(now.Version,Is.EqualTo(34));Assert.That(old.Version,Is.EqualTo(32));Assert.That(old.Descriptors.Any(d=>d.Path.StartsWith("grip.crimson.recoil.")),Is.False);
            Assert.That(now.Validate(),Is.Empty);
            var path=Path.Combine(Path.GetTempPath(),"crimson-recoil-"+Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{old}});
                if(custom)history.Create("Authored Crimson grip");else history.Rename("Default");
                var saved=history.Selected.Snapshot;
                if(custom){saved.Set("grip.crimson.detail.scale",1.3f);history.Save(saved);}
                string hash=history.Selected.Hash,bytes=JsonUtility.ToJson(history.Selected.Snapshot);
                var migrated=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{now}});Assert.That(migrated.StorageError,Is.Null);
                foreach(var d in old.Descriptors)Assert.That(migrated.Selected.Snapshot.Get(d.Path),Is.EqualTo(saved.Get(d.Path)),d.Path);
                foreach(var r in migrated.Profiles.SelectMany(p=>p.Revisions).Where(r=>r.Hash==hash))Assert.That(JsonUtility.ToJson(r.Snapshot),Is.EqualTo(bytes));
                Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==hash),Is.True);
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
