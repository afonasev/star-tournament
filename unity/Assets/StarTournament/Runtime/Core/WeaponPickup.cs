using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public struct WeaponPickupState
    {
        public string InstanceId, Support;
        public WeaponId Weapon;
        public Vector3 Anchor;
        public bool Available;
        public double Remaining;
    }
    /// <summary>One authoritative instance; renderer, physics and clocks are supplied by adapters.</summary>
    public sealed class WeaponPickup
    {
        readonly float radius, heightTolerance;
        readonly double respawnSeconds;
        WeaponPickupState state;
        public static bool IsWeapon(ArenaPickupKind kind)=>kind==ArenaPickupKind.Shotgun||kind==ArenaPickupKind.Pulse||kind==ArenaPickupKind.Cutter;
        public WeaponPickup(ArenaPickupDefinition definition,ProvingProfile profile)
        {
            if(definition==null||!IsWeapon(definition.Kind)||string.IsNullOrWhiteSpace(definition.Id)||string.IsNullOrWhiteSpace(definition.Support))throw new ArgumentException("Named weapon definition required");
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            profile=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile));profile.EnsureCombatDescriptors();
            if(profile.Validate().Count!=0)throw new ArgumentException("Invalid pickup profile");
            radius=profile.Get("weaponPickup.radius");heightTolerance=profile.Get("pickup.maximumHeightDifference");respawnSeconds=profile.Get("weaponPickup.respawnSeconds");
            state=new WeaponPickupState{InstanceId=definition.Id,Support=definition.Support,Anchor=definition.Anchor,Available=true,Weapon=definition.Kind==ArenaPickupKind.Shotgun?WeaponId.Shotgun:definition.Kind==ArenaPickupKind.Pulse?WeaponId.RocketLauncher:WeaponId.Cutter};
        }
        public WeaponPickupState Read()=>state;
        public void ValidateSnapshot(WeaponPickupState s)
        {
            if(s.InstanceId!=state.InstanceId||s.Anchor!=state.Anchor||s.Support!=state.Support||s.Weapon!=state.Weapon||double.IsNaN(s.Remaining)||double.IsInfinity(s.Remaining)||s.Remaining<0||s.Remaining>respawnSeconds||s.Available&&s.Remaining!=0||!s.Available&&s.Remaining==0)throw new ArgumentException("Invalid weapon pickup snapshot");
        }
        public void Restore(WeaponPickupState s){ValidateSnapshot(s);state=s;}
        public void Advance(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(!state.Available){state.Remaining=Math.Max(0,state.Remaining-seconds);if(state.Remaining==0)state.Available=true;}
        }
        public bool InRange(Vector3 feet)
        {var delta=feet-state.Anchor;if(Mathf.Abs(delta.y)>heightTolerance)return false;delta.y=0;return delta.sqrMagnitude<=radius*radius;}
        public bool TryCollect(Vector3 feet,string support,CombatLife life)
        {
            if(!state.Available||support!=state.Support||!InRange(feet)||life==null||!life.CollectWeapon(state.Weapon))return false;
            state.Available=false;state.Remaining=respawnSeconds;return true;
        }
    }
}
