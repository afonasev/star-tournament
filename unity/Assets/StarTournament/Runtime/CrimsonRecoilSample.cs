using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Absolute presentation sample; session time owns pause, replay and cadence.</summary>
    public readonly struct CrimsonRecoilSample
    {
        public readonly float Weight,BackMeters,PitchDegrees,RollDegrees;
        CrimsonRecoilSample(float weight,float back,float pitch,float roll)
        {Weight=weight;BackMeters=back*weight;PitchDegrees=pitch*weight;RollDegrees=roll*weight;}

        public static CrimsonRecoilSample Read(ProvingProfile profile,WeaponId weapon,double age)
        {
            if(weapon!=WeaponId.RocketLauncher||profile.Version<33||profile.Get("grip.crimson.recoil.enabled")<=0||
                age<0||double.IsNaN(age)||double.IsInfinity(age))return default;
            float kick=profile.Get("grip.crimson.recoil.kickSeconds"),recovery=profile.Get("grip.crimson.recoil.recoverySeconds");
            if(age>=kick+recovery)return default;
            float weight;
            if(age<kick){float remaining=1-(float)age/kick;weight=1-remaining*remaining*remaining;}
            else {float phase=(float)(age-kick)/recovery;weight=1-phase*phase*(3-2*phase);}
            // Cubic ease-out and smoothstep are normalized interpolation, not hidden art knobs.
            return new CrimsonRecoilSample(weight,profile.Get("grip.crimson.recoil.backMeters"),
                profile.Get("grip.crimson.recoil.pitchDegrees"),profile.Get("grip.crimson.recoil.rollDegrees"));
        }
    }
}
