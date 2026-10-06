using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable]
    public struct RocketState
    {
        public uint Id;
        public int Owner, OwnerLife;
        public Vector3 Position, Direction;
        public float Distance, DamageMultiplier;
        public bool AccuracyTracked;
    }
    public readonly struct RocketExplosion
    {
        public readonly RocketState Rocket;
        public readonly Vector3 Position;
        public readonly double Time;
        public readonly int DirectSeat;
        public RocketExplosion(RocketState rocket,Vector3 position,double time,int directSeat)
        { Rocket=rocket;Position=position;Time=time;DirectSeat=directSeat; }
    }
    /// <summary>Gameplay geometry only. Neither animated bones nor renderer bounds affect rockets.</summary>
    public sealed class RocketResolver
    {
        readonly float height,capsuleRadius;
        public float Speed {get;} public float Radius {get;} public float MaximumDamage {get;} public float Range {get;}
        public RocketResolver(ProvingProfile combat,ProvingProfile movement)
        {
            if(combat.Validate().Count!=0||movement.Validate().Count!=0)throw new ArgumentException("Invalid rocket profile");
            Speed=combat.Get("rocket.speed");Radius=combat.Get("rocket.radius");MaximumDamage=combat.Get("rocket.maximumDamage");Range=combat.Get("rocket.range");
            height=movement.Get("player.capsule.height");capsuleRadius=movement.Get("player.capsule.radius");
        }
        public Vector3 ClosestPoint(Vector3 point,Vector3 feet)
        {
            var axis=feet+Vector3.up*Mathf.Clamp(point.y-feet.y,capsuleRadius,height-capsuleRadius);
            var delta=point-axis;
            return delta.sqrMagnitude<=capsuleRadius*capsuleRadius?point:axis+delta.normalized*capsuleRadius;
        }
        public float Damage(float distance,bool direct,float multiplier)
            => MaximumDamage*(direct?1:Mathf.Clamp01(1-distance/Radius))*multiplier;
        public PelletHit Contact(Vector3 origin,Vector3 direction,float limit,CombatTarget[] targets,int owner,int ownerLife)
        {
            int hit=-1;
            for(int i=0;i<targets.Length;i++)
            {
                // A rocket leaves its own launch life; a later respawn is a distinct contact target.
                if(targets[i].Seat==owner&&targets[i].Life==ownerLife)continue;
                float t=Capsule(origin-targets[i].Pose.Position,direction,capsuleRadius,height-capsuleRadius,capsuleRadius);
                if(t>=0&&t<limit){limit=t;hit=i;}
            }
            return new PelletHit(hit,HitZone.Torso,limit);
        }
        static float Sphere(Vector3 origin,Vector3 direction,Vector3 center,float radius)
        {
            var o=origin-center;float b=Vector3.Dot(o,direction),c=o.sqrMagnitude-radius*radius;
            if(c<=0)return 0;
            float d=b*b-c;if(d<0)return -1;
            float t=-b-Mathf.Sqrt(d);return t>=0?t:-1;
        }
        internal static float Capsule(Vector3 o,Vector3 d,float bottom,float top,float radius)
        {
            float best=float.PositiveInfinity;
            void Add(float t){if(t>=0)best=Mathf.Min(best,t);}
            Add(Sphere(o,d,Vector3.up*bottom,radius));Add(Sphere(o,d,Vector3.up*top,radius));
            float a=d.x*d.x+d.z*d.z,b=o.x*d.x+o.z*d.z,c=o.x*o.x+o.z*o.z-radius*radius;
            if(c<=0&&o.y>=bottom&&o.y<=top)return 0;
            // Numeric epsilon guards division by zero; it is not gameplay tuning.
            if(a>1e-8f)
            {
                float disc=b*b-a*c;
                if(disc>=0){float t=(-b-Mathf.Sqrt(disc))/a;float y=o.y+t*d.y;if(y>=bottom&&y<=top)Add(t);}
            }
            return float.IsInfinity(best)?-1:best;
        }
        public void Validate(RocketState rocket,int participants,uint sequence)
        {
            if(rocket.Id==0||rocket.Id>sequence||rocket.Owner<0||rocket.Owner>=participants||rocket.OwnerLife<1||!Finite(rocket.Position)||!Finite(rocket.Direction)||Mathf.Abs(rocket.Direction.sqrMagnitude-1)>1e-4f||!Finite(rocket.Distance)||rocket.Distance<0||rocket.Distance>=Range||!Finite(rocket.DamageMultiplier)||rocket.DamageMultiplier<1)
                throw new ArgumentException("Invalid rocket snapshot");
        }
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
    }
}
