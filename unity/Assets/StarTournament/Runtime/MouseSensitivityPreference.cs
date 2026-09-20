using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Device preference, deliberately outside frozen match/profile state.</summary>
    public static class MouseSensitivityPreference
    {
        public const string Key = "StarTournament.Settings.MouseDegreesPerPixel";
        const string Path = "input.mouseDegreesPerPixel";
        public static NumericDescriptor Descriptor(ProvingProfile profile) => profile.Descriptor(Path);
        public static float Resolve(ProvingProfile profile)
        {
            var descriptor=Descriptor(profile);
            if(descriptor==null) throw new System.ArgumentException("Mouse sensitivity descriptor is required.");
            if(!PlayerPrefs.HasKey(Key)) return descriptor.DefaultValue;
            float value=PlayerPrefs.GetFloat(Key,descriptor.DefaultValue);
            return descriptor.Contains(value) ? value : descriptor.DefaultValue;
        }
        public static float Set(ProvingProfile profile,float value)
        {
            var descriptor=Descriptor(profile);
            if(descriptor==null) throw new System.ArgumentException("Mouse sensitivity descriptor is required.");
            value=Mathf.Clamp(value,descriptor.Minimum,descriptor.Maximum);
            value=Mathf.Round((value-descriptor.Minimum)/descriptor.Step)*descriptor.Step+descriptor.Minimum;
            value=Mathf.Clamp(value,descriptor.Minimum,descriptor.Maximum);
            PlayerPrefs.SetFloat(Key,value); PlayerPrefs.Save();
            return value;
        }
        public static float Step(ProvingProfile profile,int direction) => Set(profile,Resolve(profile)+Descriptor(profile).Step*direction);
    }
}
