using UnityEngine;

namespace StarTournament.ProvingGround
{
    public struct GamepadLookSettings
    {
        public const string HorizontalPath="input.gamepadHorizontalDegreesPerSecond";
        public const string DelayPath="input.gamepadReturnDelay";
        public const string VerticalPath="input.gamepadVerticalDegreesPerSecond";
        const string Prefix="StarTournament.Settings.Gamepad.";
        public float Horizontal,Vertical,ReturnDelay;
        public float Deadzone;
        public bool AutoLevel,InvertX,InvertY;
        public static GamepadLookSettings Default(ProvingProfile profile) => new GamepadLookSettings {
            Horizontal=profile.Descriptor(HorizontalPath)!=null?profile.Get(HorizontalPath):profile.Get("input.gamepadDegreesPerSecond"),
            Vertical=profile.Descriptor(VerticalPath)!=null?profile.Get(VerticalPath):profile.Get("input.gamepadDegreesPerSecond"),AutoLevel=true,ReturnDelay=profile.Get(DelayPath),Deadzone=profile.Get("input.deadzone") };
        public static float Snap(ProvingProfile profile,string path,float value)
        {
            var d=profile.Descriptor(path);
            if(!d.Contains(value))value=float.IsNaN(value)||float.IsInfinity(value)?d.DefaultValue:Mathf.Clamp(value,d.Minimum,d.Maximum);
            return Mathf.Clamp(d.Minimum+Mathf.Round((value-d.Minimum)/d.Step)*d.Step,d.Minimum,d.Maximum);
        }
        public static GamepadLookSettings Resolve(ProvingProfile profile,PlayerProfileRecord record)
        {
            if(record==null||record.GamepadLookVersion==0)return Default(profile);
            return new GamepadLookSettings { Horizontal=Valid(profile,HorizontalPath,record.GamepadHorizontal),
                Vertical=Valid(profile,VerticalPath,record.GamepadVertical),AutoLevel=record.GamepadAutoLevel,
                ReturnDelay=record.GamepadLookVersion>=2?Valid(profile,DelayPath,record.GamepadReturnDelay):profile.Get(DelayPath),
                Deadzone=record.GamepadLookVersion>=3?Valid(profile,"input.deadzone",record.GamepadDeadzone):profile.Get("input.deadzone"),
                InvertX=record.GamepadLookVersion>=3&&record.LookInvertX,InvertY=record.GamepadLookVersion>=3&&record.LookInvertY };
        }
        static float Valid(ProvingProfile p,string path,float v)=>p.Descriptor(path).Contains(v)?v:p.Get(path);
        public static GamepadLookSettings General(ProvingProfile p)
        {
            var s=Default(p);
            s.Horizontal=Valid(p,HorizontalPath,PlayerPrefs.GetFloat(Prefix+"X",s.Horizontal));
            s.Vertical=Valid(p,VerticalPath,PlayerPrefs.GetFloat(Prefix+"Y",s.Vertical));
            s.ReturnDelay=Valid(p,DelayPath,PlayerPrefs.GetFloat(Prefix+"Delay",s.ReturnDelay));
            s.AutoLevel=PlayerPrefs.GetInt(Prefix+"AutoLevel",1)!=0;
            s.Deadzone=Valid(p,"input.deadzone",PlayerPrefs.GetFloat(Prefix+"Deadzone",s.Deadzone));
            s.InvertX=PlayerPrefs.GetInt(Prefix+"InvertX",0)!=0;s.InvertY=PlayerPrefs.GetInt(Prefix+"InvertY",0)!=0;return s;
        }
        public static void SaveGeneral(GamepadLookSettings s)
        {
            PlayerPrefs.SetFloat(Prefix+"X",s.Horizontal);PlayerPrefs.SetFloat(Prefix+"Y",s.Vertical);
            PlayerPrefs.SetFloat(Prefix+"Delay",s.ReturnDelay);
            PlayerPrefs.SetFloat(Prefix+"Deadzone",s.Deadzone);
            PlayerPrefs.SetInt(Prefix+"InvertX",s.InvertX?1:0);PlayerPrefs.SetInt(Prefix+"InvertY",s.InvertY?1:0);
            PlayerPrefs.SetInt(Prefix+"AutoLevel",s.AutoLevel?1:0);PlayerPrefs.Save();
        }
        public static void ResetGeneral(System.Action<string> delete=null){delete=delete??PlayerPrefs.DeleteKey;foreach(var key in new[]{"X","Y","Delay","AutoLevel","Deadzone","InvertX","InvertY"})delete(Prefix+key);}
    }
}
