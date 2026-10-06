using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public struct ArmorPickupState { public string InstanceId; public Vector3 Anchor; public bool Available; public double RespawnRemaining; }

    /// <summary>Pure authoritative lifecycle for each authored arena armor bonus.</summary>
    public sealed class ArmorPickup
    {
        readonly float amount, radius, heightTolerance;
        readonly double respawnSeconds;
        ArmorPickupState state;
        public ArmorPickup(Vector3 anchor, ProvingProfile profile,string instanceId="fixture")
        {
            if(profile==null) throw new ArgumentException("Valid lifecycle profile required",nameof(profile));
            profile=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile));
            profile.EnsureCombatDescriptors();
            if(profile==null||profile.Validate().Count!=0)throw new ArgumentException("Valid lifecycle profile required",nameof(profile));
            heightTolerance=profile.Get("pickup.maximumHeightDifference");amount=profile.Get("armor.pickupAmount");radius=profile.Get("armor.pickupRadius");respawnSeconds=profile.Get("armor.respawnSeconds");
            state=new ArmorPickupState{InstanceId=instanceId,Anchor=anchor,Available=true};
        }
        public ArmorPickupState Read()=>state;
        public void Restore(ArmorPickupState snapshot)
        {
            if(snapshot.InstanceId!=state.InstanceId||snapshot.Anchor!=state.Anchor||double.IsNaN(snapshot.RespawnRemaining)||double.IsInfinity(snapshot.RespawnRemaining)||snapshot.RespawnRemaining<0)throw new ArgumentException("Invalid armor pickup snapshot",nameof(snapshot));
            state=snapshot;
        }
        public bool InRange(Vector3 feet)
        {
            var delta=feet-state.Anchor;if(Mathf.Abs(delta.y)>heightTolerance)return false;delta.y=0;return delta.sqrMagnitude<=radius*radius;
        }
        public void Advance(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(!state.Available){state.RespawnRemaining=Math.Max(0,state.RespawnRemaining-seconds);if(state.RespawnRemaining==0)state.Available=true;}
        }
        public bool TryCollect(Vector3 feet,CombatLife life)
        {
            if(!state.Available||life==null||life.Read().Dead)return false;
            if(!InRange(feet))return false;
            life.GrantArmor(amount);state.Available=false;state.RespawnRemaining=respawnSeconds;return true;
        }
    }
}
