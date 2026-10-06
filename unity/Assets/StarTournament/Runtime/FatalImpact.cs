using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Lethal source captured before presentation callbacks; never inferred from a later slot/event.</summary>
    public readonly struct FatalImpact
    {
        public readonly bool Valid;
        public readonly WeaponId Weapon;
        public readonly uint Sequence;
        public readonly Vector3 Direction, Point;
        public readonly float ExplosionFraction;
        public FatalImpact(WeaponId weapon,uint sequence,Vector3 direction,Vector3 point,float explosionFraction=0)
        { Valid=true;Weapon=weapon;Sequence=sequence;Direction=direction.normalized;Point=point;ExplosionFraction=explosionFraction; }
        public static FatalImpact FromShot(ShotNotice shot,int target,int life)
        {
            Vector3 direction=Vector3.zero,point=Vector3.zero;int count=0;
            foreach(var pellet in shot.Pellets)
                if(pellet.Contact==PelletContact.Participant&&pellet.TargetSeat==target&&pellet.TargetLife==life)
                {direction+=pellet.Direction;point+=pellet.Endpoint;count++;}
            return count==0?default:new FatalImpact(shot.Weapon,shot.Sequence,direction,point/count);
        }
        public Vector3 Velocity(ProvingProfile profile)
        {
            if(!Valid)return Vector3.zero;
            string path=Weapon==WeaponId.Shotgun?"shotgunSpeed":Weapon==WeaponId.Rifle?"rifleSpeed":Weapon==WeaponId.Cutter?"cutterSpeed":"rocketSpeed";
            float speed=profile.Get("corpse."+path);
            if(Weapon==WeaponId.RocketLauncher)speed*=Mathf.Pow(1-Mathf.Clamp01(ExplosionFraction),profile.Get("corpse.rocketFalloff"));
            return Direction*speed;
        }
    }
}
