using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public struct FullHealPickupState { public string InstanceId; public Vector3 Anchor; public bool Available; public double Remaining; }
    /// <summary>Pure authoritative lifecycle for a delayed, post-collection full heal pickup.</summary>
    public sealed class FullHealPickup
    {
        readonly float radius, target, respawn, heightTolerance;
        FullHealPickupState state;
        public FullHealPickup(Vector3 anchor, ProvingProfile profile,string instanceId="fixture")
        {
            if(profile==null) throw new ArgumentNullException(nameof(profile));
            profile=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile)); profile.EnsureCombatDescriptors();
            if(profile.Validate().Count!=0) throw new ArgumentException("Invalid lifecycle profile",nameof(profile));
            heightTolerance=profile.Get("pickup.maximumHeightDifference");radius=profile.Get("heal.pickupRadius"); target=profile.Get("heal.targetHealth"); respawn=profile.Get("heal.respawnSeconds");
            state=new FullHealPickupState { InstanceId=instanceId,Anchor=anchor, Available=profile.Get("heal.initialDelaySeconds")==0, Remaining=profile.Get("heal.initialDelaySeconds") };
        }
        public FullHealPickupState Read()=>state;
        public bool InRange(Vector3 feet) { var d=feet-state.Anchor;if(Mathf.Abs(d.y)>heightTolerance)return false;d.y=0;return d.sqrMagnitude<=radius*radius; }
        public void Advance(double seconds)
        {
            if(seconds<=0||double.IsNaN(seconds)||double.IsInfinity(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
            if(state.Available)return;
            // Technical tolerance compensates float fixed-step accumulation; far below one supported simulation tick.
            state.Remaining=Math.Max(0,state.Remaining-seconds);if(state.Remaining<=0.000001d){state.Remaining=0;state.Available=true;}
        }
        public bool TryCollect(Vector3 feet,CombatLife life)
        {
            if(!state.Available||life==null||life.Read().Dead||!InRange(feet))return false;
            life.HealTo(target);state.Available=false;state.Remaining=respawn;return true;
        }
        public void ValidateSnapshot(FullHealPickupState snapshot)
        {
            if(snapshot.InstanceId!=state.InstanceId||snapshot.Anchor!=state.Anchor||double.IsNaN(snapshot.Remaining)||double.IsInfinity(snapshot.Remaining)||snapshot.Remaining<0||(snapshot.Available&&snapshot.Remaining!=0)||(!snapshot.Available&&snapshot.Remaining==0)) throw new ArgumentException("Invalid full heal pickup snapshot",nameof(snapshot));
        }
        public void Restore(FullHealPickupState snapshot)
        {
            ValidateSnapshot(snapshot);state=snapshot;
        }
    }
}
