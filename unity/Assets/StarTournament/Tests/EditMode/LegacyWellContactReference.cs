// Frozen sample routine from main50fa41f3; correctness oracle, never runtime.
using UnityEngine;
using System.Collections.Generic;
namespace StarTournament.ProvingGround.Tests
{
    static class LegacyWellContactReference
    {
        const int Samples=12;const float Reach=2.4f;
        public static float Visibility(Vector3 p,Vector3 normal,Vector3 tangent,Vector3 bitangent,IReadOnlyList<Bounds> boxes)
        {
            float occlusion=0;var origin=p+normal*.025f;
            for(int i=0;i<Samples;i++)
            {
                float z=Mathf.Sqrt((i+.5f)/Samples),radius=Mathf.Sqrt(1-z*z),angle=i*2.39996323f;
                var direction=normal*z+(tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle))*radius;var ray=new Ray(origin,direction);float nearest=Reach;
                foreach(var box in boxes)if(box.SqrDistance(origin)<Reach*Reach&&box.IntersectRay(ray,out float distance)&&distance>=0&&distance<nearest)nearest=distance;
                float blocked=1-nearest/Reach;occlusion+=blocked*blocked;
            }
            return Mathf.Clamp01(1-occlusion/Samples);
        }
    }
}
