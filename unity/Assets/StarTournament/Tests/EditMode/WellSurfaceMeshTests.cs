using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellSurfaceMeshTests
    {
        static float Area(Mesh mesh)
        {
            var v=mesh.vertices;var t=mesh.triangles;float a=0;
            for(int i=0;i<t.Length;i+=3)a+=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).magnitude/2;
            return a;
        }
        [Test] public void AdjacentOverlappingAndDuplicateBoxesHaveOnlyExteriorFaces()
        {
            foreach(float offset in new[]{0f,.5f,1f})
            {
                var mesh=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.zero,Vector3.one),new Bounds(Vector3.right*offset,Vector3.one)});
                try{Assert.That(Area(mesh),Is.EqualTo(6+4*offset).Within(.0001f));}finally{Object.DestroyImmediate(mesh);}
            }
        }
        [Test] public void PartialTJunctionRemovesOnlyBuriedPortion()
        {
            var mesh=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.zero,new Vector3(4,2,1)),new Bounds(new Vector3(0,0,1),new Vector3(1,2,1))});
            try{Assert.That(Area(mesh),Is.EqualTo(34).Within(.0001f));}finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void OtherMaterialsOccludeInternalFacesWithoutRemovingExposedSurface()
        {
            var mesh=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.zero,Vector3.one)},new[]{new Bounds(Vector3.right,Vector3.one)});
            try{Assert.That(Area(mesh),Is.EqualTo(5).Within(.0001f));}finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void WallSkinsCloseEveryPerpendicularEndpointAndShaftBottomWithoutMutatingAuthoring()
        {
            var arena=WellArenaCatalog.Build();string before=JsonUtility.ToJson(arena);int joints=0;
            foreach(var s in arena.Solids.Where(s=>s.Material=="wall"))
            {
                var skin=WellArenaPresentation.WallSkin(s,arena.Solids);
                if(s.Id.StartsWith("shaft-"))Assert.That(skin.min.y,Is.EqualTo(-.3f).Within(.0001f),s.Id);
                int axis=s.Size.x>s.Size.z?0:2,across=axis==0?2:0;if(s.Size[across]>.31f)continue;
                var original=new Bounds(s.Position,s.Size);
                foreach(var o in arena.Solids.Where(o=>o.Material=="wall"&&o.Size[axis]<=.31f&&o.Size[across]>.31f))
                {
                    var b=new Bounds(o.Position,o.Size);
                    if(b.max.y<=original.min.y+.0001f||b.min.y>=original.max.y-.0001f||s.Position[across]<b.min[across]-.0001f||s.Position[across]>b.max[across]+.0001f)continue;
                    if(Mathf.Abs(original.min[axis]-o.Position[axis])<.0001f){Assert.That(skin.min[axis],Is.LessThanOrEqualTo(b.min[axis]+.0001f),s.Id);joints++;}
                    if(Mathf.Abs(original.max[axis]-o.Position[axis])<.0001f){Assert.That(skin.max[axis],Is.GreaterThanOrEqualTo(b.max[axis]-.0001f),s.Id);joints++;}
                }
            }
            Assert.That(joints,Is.GreaterThan(100));Assert.That(JsonUtility.ToJson(arena),Is.EqualTo(before));
        }
    }
}
