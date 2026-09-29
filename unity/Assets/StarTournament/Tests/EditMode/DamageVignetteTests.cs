using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class DamageVignetteTests
    {
        [Test] public void ActualLossChoosesColorAndClockFreezesAndFades()
        {
            var p=ProvingProfile.CreateDefault();var pulse=new DamageVignettePulse();
            pulse.Hit(new DamageNotice(0,1,0,12,10),p);Assert.That(pulse.HealthHit,Is.False);
            float peak=p.Get("ui.damageVignette.opacity");double top=10+p.Get("ui.damageVignette.attackSeconds");
            Assert.That(pulse.Sample(10),Is.Zero);Assert.That(pulse.Sample(top),Is.EqualTo(peak).Within(.00001));
            pulse.Hit(new DamageNotice(0,1,1,8,top),p);Assert.That(pulse.HealthHit,Is.True);
            Assert.That(pulse.Sample(top),Is.EqualTo(peak).Within(.00001),"Repeated hit does not flash back to zero");
            for(int i=0;i<100;i++){double time=top+i*.01;pulse.Hit(new DamageNotice(0,1,1,0,time),p);Assert.That(pulse.Sample(time+.01),Is.LessThanOrEqualTo(peak));}
            Assert.That(pulse.Sample(top+3),Is.Zero);pulse.Reset();Assert.That(pulse.Sample(top),Is.Zero);
        }
        [Test] public void NoLossCannotCreateOrRecolorPulse()
        {
            var p=ProvingProfile.CreateDefault();var pulse=new DamageVignettePulse();
            pulse.Hit(new DamageNotice(0,1,0,0,1),p);Assert.That(pulse.Sample(1.05),Is.Zero);
            pulse.Hit(new DamageNotice(0,1,2,0,2),p);pulse.Hit(new DamageNotice(0,1,0,0,2.02),p);
            Assert.That(pulse.HealthHit,Is.True);Assert.That(pulse.Sample(2.04),Is.GreaterThan(0));
        }
        [Test] public void LabUpgradePreservesPriorHashValuesAndProvidesValidatedFields()
        {
            string path=Path.Combine(Path.GetTempPath(),"vignette-lab-"+Guid.NewGuid()+".json");
            try
            {
                var shipped=new LabBundle{Profiles={ProvingProfile.CreateDefault()}};
                var legacy=new LabBundle{Profiles={shipped.Profiles[0].BeforeDamageVignette()}};
                var old=new DesignLabHistory(path,legacy);old.Create("Custom");var draft=old.Selected.Snapshot;
                draft.Set("camera.fieldOfViewDegrees",100);old.Save(draft);var before=old.Selected;
                var upgraded=new DesignLabHistory(path,shipped);
                Assert.That(upgraded.Selected.Snapshot.Get("camera.fieldOfViewDegrees"),Is.EqualTo(100));
                Assert.That(upgraded.SelectedProfile.Revisions.Single(x=>x.Hash==before.Hash).Snapshot.Hash(),Is.EqualTo(before.Hash));
                foreach(var d in shipped.Descriptors.Where(d=>d.Path.StartsWith("ui.damageVignette.")))
                {Assert.That(d.Validate(out _),Is.True);Assert.That(upgraded.Selected.Snapshot.IsEditable(d.Path),Is.True);Assert.That(shipped.Domain(d.Path),Is.EqualTo("Presentation"));shipped.Set(d.Path,d.Maximum+d.Step);Assert.That(shipped.Validate().Any(x=>x.Path==d.Path),Is.True);shipped.Set(d.Path,d.DefaultValue);}
                Assert.That(upgraded.Selected.Snapshot.Validate(),Is.Empty);
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
