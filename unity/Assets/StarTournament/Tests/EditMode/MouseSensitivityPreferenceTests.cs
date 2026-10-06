using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class MouseSensitivityPreferenceTests
    {
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
