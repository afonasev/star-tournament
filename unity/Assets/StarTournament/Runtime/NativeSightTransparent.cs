using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Canonical arena surface flag, independent of material/presentation. Shots still hit WorldLayer.</summary>
    public sealed class NativeSightTransparent:MonoBehaviour {}
    public enum NativeVisibilityResult {Clear,Occluded,Indeterminate}
    public sealed class NativeVisibilityQuery
    {
        readonly PhysicsScene physics;readonly RaycastHit[] hits;
        // Bounded query storage is a technical cap. Saturation never proves a hidden respawn.
        public NativeVisibilityQuery(PhysicsScene physics,int capacity=32)
        {if(capacity<1)throw new System.ArgumentOutOfRangeException(nameof(capacity));this.physics=physics;hits=new RaycastHit[capacity];}
        public NativeVisibilityResult Query(Vector3 origin,Vector3 delta)
        {
            if(delta.sqrMagnitude<=Mathf.Epsilon)return NativeVisibilityResult.Clear;
            int n=physics.Raycast(origin,delta.normalized,hits,delta.magnitude,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)if(!hits[i].collider.GetComponent<NativeSightTransparent>())return NativeVisibilityResult.Occluded;
            return n==hits.Length?NativeVisibilityResult.Indeterminate:NativeVisibilityResult.Clear;
        }
    }
}
