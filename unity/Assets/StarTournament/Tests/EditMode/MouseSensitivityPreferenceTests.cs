using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class MouseSensitivityPreferenceTests
    {
        static float Resolve(ProvingProfile profile,System.Func<string,bool> hasKey,System.Func<string,float,float> read)
            =>(float)typeof(MouseSensitivityPreference).GetMethod("Resolve",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,
                null,new[]{typeof(ProvingProfile),typeof(System.Func<string,bool>),typeof(System.Func<string,float,float>)},null).Invoke(null,new object[]{profile,hasKey,read});
        [Test] public void MissingPersonalPreferenceUsesSelectedLabValueInsteadOfFactoryDefault()
        {
            var p=ProvingProfile.CreateDefault();p.Set("input.mouseDegreesPerPixel",.19f);
            Assert.That(Resolve(p,_=>false,(key,fallback)=>throw new System.Exception("Absent preference must not be read")),Is.EqualTo(.19f));
            Assert.That(p.Descriptor("input.mouseDegreesPerPixel").DefaultValue,Is.Not.EqualTo(.19f),"Lab tuning differs from factory metadata");
            Assert.That(p.Descriptor("input.mouseDegreesPerPixel").IsPreferenceDefault,Is.True);
            Assert.That(p.Descriptor("camera.eyeHeight").IsPreferenceDefault,Is.False);
        }
        [TestCase(.21f,.21f)][TestCase(float.NaN,.19f)][TestCase(99f,.19f)]
        public void ValidPersonalValueWinsAndInvalidValueFallsBackToLab(float personal,float expected)
        {
            var p=ProvingProfile.CreateDefault();p.Set("input.mouseDegreesPerPixel",.19f);string before=JsonUtility.ToJson(p);
            Assert.That(Resolve(p,key=>key==MouseSensitivityPreference.Key,(key,fallback)=>{Assert.That(fallback,Is.EqualTo(.19f));return personal;}),Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(p),Is.EqualTo(before),"Preference resolution never modifies Lab values or metadata");
        }
        [Test]
        public void PreferenceFallsBackValidatesAndStepsWithinDescriptor()
        {
            var profile=ProvingProfile.CreateDefault();var descriptor=MouseSensitivityPreference.Descriptor(profile);
            bool had=PlayerPrefs.HasKey(MouseSensitivityPreference.Key);float old=PlayerPrefs.GetFloat(MouseSensitivityPreference.Key);
            try
            {
                PlayerPrefs.DeleteKey(MouseSensitivityPreference.Key);
                Assert.That(MouseSensitivityPreference.Resolve(profile),Is.EqualTo(descriptor.DefaultValue));
                PlayerPrefs.SetFloat(MouseSensitivityPreference.Key,descriptor.Maximum+1);PlayerPrefs.Save();
                Assert.That(MouseSensitivityPreference.Resolve(profile),Is.EqualTo(descriptor.DefaultValue));
                Assert.That(MouseSensitivityPreference.Set(profile,descriptor.Minimum-1),Is.EqualTo(descriptor.Minimum));
                Assert.That(MouseSensitivityPreference.Step(profile,1),Is.EqualTo(descriptor.Minimum+descriptor.Step).Within(.0001f));
                Assert.That(MouseSensitivityPreference.Set(profile,descriptor.Maximum+1),Is.EqualTo(descriptor.Maximum));
            }
            finally { if(had)PlayerPrefs.SetFloat(MouseSensitivityPreference.Key,old);else PlayerPrefs.DeleteKey(MouseSensitivityPreference.Key);PlayerPrefs.Save(); }
        }
    }
}
