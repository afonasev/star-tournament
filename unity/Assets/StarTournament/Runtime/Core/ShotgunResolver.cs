using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public enum HitZone { Head, Torso, Limb }
    public readonly struct CombatTarget
    {
        public readonly int Seat, Life;
        public readonly ParticipantState Pose;
        public CombatTarget(int seat, int life, ParticipantState pose) { Seat = seat; Life = life; Pose = pose; }
    }
    public readonly struct PelletHit
    {
        public readonly int TargetIndex;
        public readonly HitZone Zone;
        public readonly float Distance;
        public PelletHit(int targetIndex, HitZone zone, float distance) { TargetIndex = targetIndex; Zone = zone; Distance = distance; }
    }
    /// <summary>Analytic hit volumes depend on gameplay poses, never on animated GLB bones.</summary>
    public sealed class ShotgunResolver
    {
        readonly ProvingProfile profile;
        readonly float headY,headRadius,torsoY,torsoX,torsoHeight,torsoZ;
        readonly float armX,armBottom,armTop,armRadius,legX,legBottom,legTop,legRadius;
        public int PelletCount => (int)profile.Get("shot.pellets");
        public float Range => profile.Get("shot.range");
        public int ProjectileCount(WeaponId weapon) => weapon==WeaponId.Rifle?1:PelletCount;
        public float RangeFor(WeaponId weapon) => weapon==WeaponId.Rifle?float.PositiveInfinity:profile.Get("shot.range");
        public ShotgunResolver(ProvingProfile profile, float height)
        {
            if (profile.Validate().Count != 0 || profile.Id != "unity-native-combat-v1") throw new ArgumentException("Invalid native combat profile");
            this.profile = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile)); this.profile.EnsureNativeCombatDescriptors();
            // Dimensions come from this resolver's private frozen profile. Resolve never changes
            // them; calculate the same products once instead of allocating zone keys per target/ray.
            float Z(string key)=>this.profile.Get("zone."+key)*height;
            headY=Z("headY");headRadius=Z("headRadius");
            torsoY=Z("torsoY");torsoX=Z("torsoX");torsoHeight=Z("torsoHeight");torsoZ=Z("torsoZ");
            armX=Z("armX");armBottom=Z("armBottom");armTop=Z("armTop");armRadius=Z("armRadius");
            legX=Z("legX");legBottom=Z("legBottom");legTop=Z("legTop");legRadius=Z("legRadius");
        }
        public float FullDamage(HitZone zone) => profile.Get("shot.damage");
        public float FullDamage(HitZone zone,WeaponId weapon) => profile.Get(weapon==WeaponId.Rifle?"rifle.damage":"shot.damage");
        public Vector3 Direction(Quaternion aim, int pellet, uint shotSeed)
            => Direction(aim,pellet,shotSeed,WeaponId.Shotgun);
        public Vector3 Direction(Quaternion aim, int pellet, uint shotSeed,WeaponId weapon)
        {
            // Integer mixing constants define the sampling algorithm, not gameplay tuning.
            uint seed = shotSeed ^ ((uint)pellet + 1u) * 0x9e3779b9u;
            float Next() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return seed / (float)uint.MaxValue; }
            float radius = Mathf.Sqrt(Next()), angle = Next() * 2 * Mathf.PI;
            float spread = Mathf.Tan(profile.Get(weapon==WeaponId.Rifle?"rifle.spread":"shot.spread") * Mathf.Deg2Rad);
            return aim * new Vector3(Mathf.Cos(angle) * radius * spread, Mathf.Sin(angle) * radius * spread, 1).normalized;
        }
        public PelletHit Resolve(Vector3 origin, Vector3 direction, CombatTarget[] targets, int shooter, float worldDistance)
            => Resolve(origin,direction,targets,shooter,worldDistance,WeaponId.Shotgun);
        public PelletHit Resolve(Vector3 origin, Vector3 direction, CombatTarget[] targets, int shooter, float worldDistance,WeaponId weapon)
        {
            return ResolveSegment(origin,direction,targets,shooter,0,Mathf.Min(RangeFor(weapon),worldDistance));
        }
        /// <summary>Swept point bullet against current authoritative zones; no distance cutoff.</summary>
        public PelletHit ResolveSegment(Vector3 origin,Vector3 direction,CombatTarget[] targets,int shooter,int shooterLife,float limit)
        {
            float nearest = limit;
            int targetIndex = -1; HitZone selected = default;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i].Seat == shooter && (shooterLife==0 || targets[i].Life==shooterLife)) continue;
                var hit=ResolveTarget(origin,direction,targets[i],nearest);
                if(hit.TargetIndex>=0){nearest=hit.Distance;targetIndex=i;selected=hit.Zone;}

            }
            return new PelletHit(targetIndex, selected, nearest);
        }
        /// <summary>Same head/torso/limb intersection for every weapon; callers decide participant blocking.</summary>
        public PelletHit ResolveTarget(Vector3 origin,Vector3 direction,CombatTarget target,float limit)
        {
            float nearest=limit;bool found=false;HitZone selected=default;
            var inverse = Quaternion.Euler(0, -target.Pose.Yaw, 0);
            Vector3 o = inverse * (origin - target.Pose.Position), d = inverse * direction;
            void Consider(float distance, HitZone zone)
            {
                // Strict comparison makes world occlusion win exact ties; roster order breaks target ties.
                if (distance >= 0 && distance < nearest) { nearest = distance; found = true; selected = zone; }
            }
            Consider(Sphere(o, d, Vector3.up * headY, headRadius), HitZone.Head);
            var bounds = new Bounds(Vector3.up * torsoY, new Vector3(torsoX, torsoHeight, torsoZ) * 2);
            if(bounds.Contains(o))Consider(0,HitZone.Torso);
            else if (bounds.IntersectRay(new Ray(o, d), out float torso)) Consider(torso, HitZone.Torso);
            for (int side = -1; side <= 1; side += 2)
            {
                Consider(Capsule(o, d, side * armX, armBottom, armTop, armRadius), HitZone.Limb);
                Consider(Capsule(o, d, side * legX, legBottom, legTop, legRadius), HitZone.Limb);
            }
            return new PelletHit(found?0:-1,selected,nearest);
        }
        static float Sphere(Vector3 o, Vector3 d, Vector3 center, float radius)
        {
            Vector3 offset = o - center;
            float b = Vector3.Dot(offset, d), c = offset.sqrMagnitude - radius * radius;
            if (c <= 0) return 0;
            float discriminant = b * b - c;
            if (discriminant < 0) return -1;
            float t = -b - Mathf.Sqrt(discriminant);
            return t >= 0 ? t : -1;
        }
        static float Capsule(Vector3 o, Vector3 d, float x, float bottom, float top, float radius)
        {
            float best = float.PositiveInfinity;
            void Add(float t) { if (t >= 0) best = Mathf.Min(best, t); }
            Add(Sphere(o, d, new Vector3(x, bottom, 0), radius));
            Add(Sphere(o, d, new Vector3(x, top, 0), radius));
            float ox = o.x - x, a = d.x*d.x + d.z*d.z;
            float c = ox*ox + o.z*o.z - radius*radius;
            if(c<=0 && o.y>=bottom && o.y<=top)return 0;
            // Numeric epsilon only avoids division by zero for axis-parallel rays.
            if (a > 1e-8f)
            {
                float b = ox*d.x + o.z*d.z;
                float disc = b*b - a*c;
                if (disc >= 0)
                {
                    float t = (-b - Mathf.Sqrt(disc)) / a;
                    float y = o.y + t*d.y;
                    if (y >= bottom && y <= top) Add(t);
                }
            }
            return float.IsInfinity(best) ? -1 : best;
        }
    }
}
