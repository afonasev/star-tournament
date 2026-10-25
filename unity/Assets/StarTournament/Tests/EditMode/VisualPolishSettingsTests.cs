using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class VisualPolishSettingsTests
    {
        [Test] public void AntialiasingPreferencePersistsAValidSharedLevelAndRepairsInvalidValues()
        {
            string key=NativeVisualPreferences.AntialiasingKey;bool existed=PlayerPrefs.HasKey(key);int old=PlayerPrefs.GetInt(key);
            try
            {
                PlayerPrefs.DeleteKey(key);Assert.That(NativeVisualPreferences.AntialiasingLevel,Is.EqualTo(NativeVisualPreferences.DefaultAntialiasing));
                foreach(int level in new[]{NativeVisualPreferences.AntialiasingOff,NativeVisualPreferences.AntialiasingLow,NativeVisualPreferences.AntialiasingMedium,NativeVisualPreferences.AntialiasingHigh})
                {NativeVisualPreferences.SetAntialiasingLevel(level);Assert.That(NativeVisualPreferences.AntialiasingLevel,Is.EqualTo(level));Assert.That(PlayerPrefs.GetInt(key),Is.EqualTo(level));}
                PlayerPrefs.SetInt(key,99);Assert.That(NativeVisualPreferences.AntialiasingLevel,Is.EqualTo(NativeVisualPreferences.DefaultAntialiasing));
                NativeVisualPreferences.SetAntialiasingLevel(99);Assert.That(PlayerPrefs.GetInt(key),Is.EqualTo(NativeVisualPreferences.DefaultAntialiasing));
            }
            finally{if(existed)PlayerPrefs.SetInt(key,old);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
        [Test] public void VisualRegistryUpgradeRetainsImmutableHistoryHashes()
        {
            string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"st-visual-history-"+System.Guid.NewGuid());System.IO.Directory.CreateDirectory(directory);
            string path=System.IO.Path.Combine(directory,"history.json");var current=DesignLabHistoryTests.Shipped();
            var old=new LabBundle{Profiles=System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(current.Profiles,p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeVisualPolish",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)))};
            try
            {
                var original=new DesignLabHistory(path,old);original.Create("Visual predecessor");var hash=original.Selected.Hash;
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                Assert.That(System.Linq.Enumerable.Any(upgraded.SelectedProfile.Revisions,r=>r.Hash==hash&&r.Snapshot.Hash()==hash),Is.True);
                var latest=upgraded.Selected.Snapshot;Assert.That(latest.Get(NativeVisualPreferences.Path(VisualEffect.Lighting)),Is.EqualTo(50));
            }
            finally{System.IO.Directory.Delete(directory,true);}
        }
        [Test] public void ArenaHarmonizationUpgradePreservesHistoryAndCustomTuning()
        {
            string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"st-arena-light-history-"+System.Guid.NewGuid());System.IO.Directory.CreateDirectory(directory);
            string path=System.IO.Path.Combine(directory,"history.json");var current=DesignLabHistoryTests.Shipped();
            var old=new LabBundle{Profiles=System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(current.Profiles,p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeArenaHarmonization",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)))};
            try
            {
                var original=new DesignLabHistory(path,old);original.Create("Arena custom predecessor");
                var custom=original.Selected.Snapshot.Clone();custom.Set("visual.fillScale",.6f);custom.Set("visual.metalSmoothness",.9f);custom.Set("visual.LightingPercent",0);
                original.Save(custom);var hash=original.Selected.Hash;int revisions=original.SelectedProfile.Revisions.Count;
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                Assert.That(System.Linq.Enumerable.Any(upgraded.SelectedProfile.Revisions,r=>r.Hash==hash&&r.Snapshot.Hash()==hash),Is.True);
                Assert.That(upgraded.Selected.Snapshot.Get("visual.fillScale"),Is.EqualTo(.6f));
                Assert.That(upgraded.Selected.Snapshot.Get("visual.metalSmoothness"),Is.EqualTo(.9f));
                Assert.That(upgraded.Selected.Snapshot.Get("visual.LightingPercent"),Is.Zero);
                Assert.That(upgraded.Selected.Snapshot.Get("visual.wallSmoothness"),Is.EqualTo(old.Get("visual.wallSmoothness")));
                Assert.That(upgraded.Selected.Snapshot.Get("visual.bowlLocalLightScale"),Is.EqualTo(.8f));
                Assert.That(upgraded.Selected.Snapshot.Get("visual.ambientLift"),Is.EqualTo(.18f));
                int count=upgraded.SelectedProfile.Revisions.Count;Assert.That(count,Is.GreaterThan(revisions));
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.SelectedProfile.Revisions.Count,Is.EqualTo(count));
                var direct=old.Profiles[0];direct.Set("visual.fillScale",.6f);direct.EnsureDefaultDescriptors();
                Assert.That(direct.Version,Is.EqualTo(19));Assert.That(direct.Get("visual.fillScale"),Is.EqualTo(.6f));Assert.That(direct.Get("visual.wallSmoothness"),Is.EqualTo(.08f));
                foreach(var d in direct.Descriptors)Assert.That(d.Validate(out var reason),Is.True,reason);
            }
            finally{System.IO.Directory.Delete(directory,true);}
        }
        [Test] public void NeutralCurveIsSmoothMonotoneAndIdentityAtZero()
        {
            foreach(float strength in new[]{0f,.5f,1f})
            using(var curve=NativeVisualPresentation.NeutralCurve(strength,.12f,.15f))
            {
                float previous=-1;
                for(int i=0;i<=256;i++)
                {float x=i/256f,y=curve.Evaluate(x);Assert.That(y,Is.InRange(0f,1f));Assert.That(y,Is.GreaterThanOrEqualTo(previous));if(strength==0)Assert.That(y,Is.EqualTo(x).Within(.00001f));previous=y;}
                Assert.That(curve.Evaluate(0),Is.Zero);Assert.That(curve.Evaluate(1),Is.EqualTo(1-strength*.15f).Within(.00001f));
                if(strength>0)Assert.That(curve.Evaluate(.15f),Is.GreaterThan(.15f));
            }
        }
        [Test] public void NeutralRegistryUpgradeKeepsVersion16Snapshot()
        {
            string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"st-color-history-"+System.Guid.NewGuid());System.IO.Directory.CreateDirectory(directory);
            string path=System.IO.Path.Combine(directory,"history.json");var current=DesignLabHistoryTests.Shipped();
            var old=new LabBundle{Profiles=System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(current.Profiles,p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeNeutralColorGrading",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)))};
            try
            {
                var original=new DesignLabHistory(path,old);original.Create("Neutral predecessor");var hash=original.Selected.Hash;
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                Assert.That(System.Linq.Enumerable.Any(upgraded.SelectedProfile.Revisions,r=>r.Hash==hash&&r.Snapshot.Hash()==hash),Is.True);
                Assert.That(upgraded.Selected.Snapshot.Get(NativeVisualPreferences.Path(VisualEffect.ColorGrading)),Is.EqualTo(50));
                Assert.That(upgraded.Selected.Snapshot.Get(NativeVisualPreferences.ExposurePath),Is.Zero);
            }
            finally{System.IO.Directory.Delete(directory,true);}
        }
        [Test] public void ColorAdjustmentsArePersistedClampedAndStepped()
        {
            var profile=ProvingProfile.CreateDefault();
            foreach(string path in NativeVisualPreferences.ColorAdjustmentPaths)
            {
                string key="StarTournament.Graphics."+path;bool existed=PlayerPrefs.HasKey(key);float old=PlayerPrefs.GetFloat(key);var d=profile.Descriptor(path);
                try
                {
                    Assert.That(d.Validate(out var reason),Is.True,reason);
                    NativeVisualPreferences.SetAdjustment(profile,path,d.Maximum+10);Assert.That(NativeVisualPreferences.Adjustment(profile,path),Is.EqualTo(d.Maximum));
                    NativeVisualPreferences.SetAdjustment(profile,path,d.Minimum-10);Assert.That(NativeVisualPreferences.Adjustment(profile,path),Is.EqualTo(d.Minimum));
                    NativeVisualPreferences.SetAdjustment(profile,path,d.Minimum+1.4f*d.Step);Assert.That(NativeVisualPreferences.Adjustment(profile,path),Is.EqualTo(d.Minimum+d.Step).Within(.00001f));
                    PlayerPrefs.SetFloat(key,float.PositiveInfinity);Assert.That(NativeVisualPreferences.Adjustment(profile,path),Is.EqualTo(profile.Get(path)));
                }
                finally{if(existed)PlayerPrefs.SetFloat(key,old);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
            }
        }
        [Test] public void DescriptorsDefineValidSteppedPreferences()
        {
            var profile=ProvingProfile.CreateDefault();
            foreach(VisualEffect effect in System.Enum.GetValues(typeof(VisualEffect)))
            {var d=profile.Descriptor(NativeVisualPreferences.Path(effect));Assert.That(d.Validate(out var reason),Is.True,reason);Assert.That(d.Minimum,Is.Zero);Assert.That(d.Maximum,Is.EqualTo(100));Assert.That(d.Step,Is.EqualTo(effect==VisualEffect.AmbientOcclusion?25:5));}
        }
        [Test] public void DisabledKeepsStrengthAndInvalidValuesUseProfileDefault()
        {
            var profile=ProvingProfile.CreateDefault();var effect=VisualEffect.AmbientOcclusion;string key=NativeVisualPreferences.Key(effect);
            bool hasStrength=PlayerPrefs.HasKey(key+".Strength"),hasEnabled=PlayerPrefs.HasKey(key+".Enabled");float oldStrength=PlayerPrefs.GetFloat(key+".Strength");int oldEnabled=PlayerPrefs.GetInt(key+".Enabled");
            try
            {
                NativeVisualPreferences.SetStrength(profile,effect,74);NativeVisualPreferences.SetEnabled(effect,false);
                Assert.That(NativeVisualPreferences.Strength(profile,effect),Is.EqualTo(75));Assert.That(NativeVisualPreferences.Effective(profile,effect),Is.Zero);
                NativeVisualPreferences.SetEnabled(effect,true);Assert.That(NativeVisualPreferences.Effective(profile,effect),Is.EqualTo(.75f));
                PlayerPrefs.SetFloat(key+".Strength",float.NaN);Assert.That(NativeVisualPreferences.Strength(profile,effect),Is.EqualTo(50));
                NativeVisualPreferences.SetStrength(profile,effect,150);Assert.That(NativeVisualPreferences.Strength(profile,effect),Is.EqualTo(100));
            }
            finally
            {if(hasStrength)PlayerPrefs.SetFloat(key+".Strength",oldStrength);else PlayerPrefs.DeleteKey(key+".Strength");if(hasEnabled)PlayerPrefs.SetInt(key+".Enabled",oldEnabled);else PlayerPrefs.DeleteKey(key+".Enabled");PlayerPrefs.Save();}
        }
    }
}
