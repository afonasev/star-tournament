using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class VisualPolishSettingsTests
    {
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
