using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Device preference, deliberately outside frozen match/profile state.</summary>
    public static class MouseSensitivityPreference
    {
        public const string Key = "StarTournament.Settings.MouseDegreesPerPixel";
        const string Path = "input.mouseDegreesPerPixel";
        public static NumericDescriptor Descriptor(ProvingProfile profile) => profile.Descriptor(Path);
        public static float Resolve(ProvingProfile profile)=>Resolve(profile,PlayerPrefs.HasKey,PlayerPrefs.GetFloat);
        internal static float Resolve(ProvingProfile profile,System.Func<string,bool> hasKey,System.Func<string,float,float> read)
        {
            var descriptor=Descriptor(profile);
            if(descriptor==null) throw new System.ArgumentException("Mouse sensitivity descriptor is required.");
            float fallback=profile.Get(Path);
            if(!descriptor.Contains(fallback))fallback=descriptor.DefaultValue;
            if(!hasKey(Key))return fallback;
            float value=read(Key,fallback);
            return descriptor.Contains(value)?value:fallback;
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
