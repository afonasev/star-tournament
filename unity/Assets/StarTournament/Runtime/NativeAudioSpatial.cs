using UnityEngine;

namespace StarTournament.ProvingGround
{
    public readonly struct AudioSpatialMix
    {
        public readonly float Gain,Pan;
        public AudioSpatialMix(float gain,float pan){Gain=gain;Pan=pan;}
    }
    // Software spatialization for the single physical split-screen output.
    // Unity's one AudioListener remains 2D; each event is mixed only once.
    public static class NativeAudioSpatial
    {
        public static Vector3 ClosestPoint(Vector3 start,Vector3 end,Vector3 listener)
        {
            var delta=end-start;
            return delta.sqrMagnitude<=.000001f?start:start+delta*Mathf.Clamp01(Vector3.Dot(listener-start,delta)/delta.sqrMagnitude);
        }
        public static AudioSpatialMix ForListener(Vector3 source,Vector3 listener,float yaw,float range,float gain)
        {
            var delta=source-listener;float distance=delta.magnitude;
            var right=Quaternion.Euler(0,yaw,0)*Vector3.right;
            float pan=distance<=.0001f?0:Mathf.Clamp(Vector3.Dot(delta/distance,right),-1,1);
            return new AudioSpatialMix(gain*Mathf.Clamp01(1-distance/Mathf.Max(.001f,range)),pan);
        }
    }
}
