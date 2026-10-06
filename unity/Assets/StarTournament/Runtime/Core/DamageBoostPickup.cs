using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public struct DamageBoostPickupState { public string InstanceId; public Vector3 Anchor; public bool Available; public double Remaining; }
    /// <summary>Pure authoritative lifecycle for a delayed, post-collection damage boost pickup.</summary>
    public sealed class DamageBoostPickup
    {
        readonly float radius, duration, multiplier, respawn, heightTolerance;
        DamageBoostPickupState state;
        public DamageBoostPickup(Vector3 anchor, ProvingProfile profile,string instanceId="fixture")
        {
            if(profile==null) throw new ArgumentNullException(nameof(profile));
            profile=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile)); profile.EnsureCombatDescriptors();
            if(profile.Validate().Count!=0) throw new ArgumentException("Invalid lifecycle profile",nameof(profile));
            heightTolerance=profile.Get("pickup.maximumHeightDifference");radius=profile.Get("damageBoost.pickupRadius"); duration=profile.Get("damageBoost.durationSeconds"); multiplier=profile.Get("damageBoost.multiplier"); respawn=profile.Get("damageBoost.respawnSeconds");
            state=new DamageBoostPickupState { InstanceId=instanceId,Anchor=anchor, Available=false, Remaining=profile.Get("damageBoost.initialDelaySeconds") };
        }
        public DamageBoostPickupState Read()=>state;
        public float Duration=>duration; public float Multiplier=>multiplier;
        public bool InRange(Vector3 feet) { var d=feet-state.Anchor;if(Mathf.Abs(d.y)>heightTolerance)return false;d.y=0;return d.sqrMagnitude<=radius*radius; }
        public void Advance(double seconds)
        {
            if(seconds<=0||double.IsNaN(seconds)||double.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
            if(state.Available)return;
            state.Remaining=Math.Max(0,state.Remaining-seconds);if(state.Remaining<=0.000001d){state.Remaining=0;state.Available=true;}
        }
        public bool TryCollect(Vector3 feet)
        {
            if(!state.Available||!InRange(feet))return false;
            state.Available=false;state.Remaining=respawn;return true;
        }
        public void Restore(DamageBoostPickupState snapshot)
        {
            if(snapshot.InstanceId!=state.InstanceId||snapshot.Anchor!=state.Anchor||double.IsNaN(snapshot.Remaining)||double.IsInfinity(snapshot.Remaining)||snapshot.Remaining<0||(!snapshot.Available&&snapshot.Remaining==0)) throw new ArgumentException("Invalid damage boost pickup snapshot",nameof(snapshot));
            state=snapshot;
        }
    }
}
