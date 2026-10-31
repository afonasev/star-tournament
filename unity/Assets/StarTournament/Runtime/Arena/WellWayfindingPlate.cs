using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Read-only audit anchors; no collider or gameplay ownership.</summary>
    public sealed class WellWayfindingPlate:MonoBehaviour
    {
        public string Role,Destination,WallId;
        public int Floor;
        public Vector2 Size;
        public Vector3 ReadPoint;
        public Vector3 Normal=>-transform.forward;
    }
}
