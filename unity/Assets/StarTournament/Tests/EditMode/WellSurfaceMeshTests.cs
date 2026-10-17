using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellSurfaceMeshTests
    {
        [Test] public void JoinedFittingTrianglesRemoveOnlyCoplanarOverlapAndPreserveAttributes()
        {
            var a=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.zero,new Vector3(4,1,4))});
            var b=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.right,new Vector3(4,1,2))});
            var combined=new Mesh();combined.CombineMeshes(new[]{new CombineInstance{mesh=a,transform=Matrix4x4.identity},new CombineInstance{mesh=b,transform=Matrix4x4.identity}});
            var joined=WellDetailMesh.JoinFlatFaces(combined);var untouched=WellDetailMesh.JoinFlatFaces(a);
            try
            {
                Assert.That(Area(combined),Is.EqualTo(76).Within(.001f));Assert.That(Area(joined),Is.EqualTo(64).Within(.001f));
                Assert.That(Area(untouched),Is.EqualTo(Area(a)).Within(.001f));Assert.That(joined.bounds,Is.EqualTo(combined.bounds));
                Assert.That(joined.normals.All(n=>Mathf.Abs(n.magnitude-1)<.0001f),Is.True);
                Assert.That(joined.uv.Length,Is.EqualTo(joined.vertexCount));Assert.That(joined.tangents.Length,Is.EqualTo(joined.vertexCount));
                Assert.That(joined.vertices.All(p=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude)),Is.True);
            }
            finally{Object.DestroyImmediate(a);Object.DestroyImmediate(b);Object.DestroyImmediate(combined);Object.DestroyImmediate(joined);Object.DestroyImmediate(untouched);}
        }
        [Test] public void FlushDoorFittingsRemoveBuriedTrianglesAndKeepVisibleEnvelope()
        {
            var source=WellDetailMesh.Bevel(new Vector3(3.6f,.2f,.095f));var clipped=WellDetailMesh.ClipBack(source,-.003f);
            try
            {
                Assert.That(clipped.bounds.max,Is.EqualTo(source.bounds.max));
                Assert.That(clipped.bounds.min.x,Is.EqualTo(source.bounds.min.x));Assert.That(clipped.bounds.min.y,Is.EqualTo(source.bounds.min.y));
                Assert.That(clipped.vertices.All(p=>p.z>=-.00301f),Is.True);
                var vertices=clipped.vertices;var normals=clipped.normals;var indices=clipped.triangles;
                Assert.That(indices.Length,Is.GreaterThan(0));Assert.That(Area(clipped),Is.LessThan(Area(source)));
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=indices[i];var b=indices[i+1];var c=indices[i+2];
                    Assert.That(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normals[a]+normals[b]+normals[c]),Is.GreaterThan(0));
                }
                Assert.That(normals.All(n=>Mathf.Abs(n.magnitude-1)<.0001f),Is.True);
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(clipped);}
        }
        [Test] public void CoplanarMaterialsKeepExactlyOneExposedSurfaceWithoutHoles()
        {
            var a=new[]{new Bounds(Vector3.zero,new Vector3(4,1,4))};
            var b=new[]{new Bounds(Vector3.right,new Vector3(4,1,2))};
            var first=WellSurfaceMesh.Build(a,b);var second=WellSurfaceMesh.Build(b,a,a);
            var prior=WellSurfaceMesh.Build(b,a);
            try
            {
                // Union footprint = 18 m², perimeter = 18 m, height = 1 m.
                Assert.That(Area(first)+Area(second),Is.EqualTo(54).Within(.001f));
                Assert.That(Area(first)+Area(prior),Is.EqualTo(66).Within(.001f),"Previously duplicated the shared top and underside");
                Assert.That(second.bounds.max.x,Is.EqualTo(3));
            }
            finally{Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(prior);}
        }
        static float Area(Mesh mesh)
        {
            var v=mesh.vertices;var t=mesh.triangles;float a=0;
            for(int i=0;i<t.Length;i+=3)a+=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).magnitude/2;
            return a;
        }
        [Test] public void MaterialPartitionPreservesEveryTriangleAndVertexAttribute()
        {
            var source=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.zero,new Vector3(4,1,6))});
            var contact=WellContactMesh.Build(source,new Bounds[0]);
            try
            {
                foreach(var mesh in new[]{source,contact})
                {
                    var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var colours=mesh.colors;var tangents=mesh.tangents;
                    var before=mesh.triangles;var bounds=mesh.bounds;
                    WellSurfaceMesh.SplitHorizontalMaterials(mesh);
                    Assert.That(mesh.subMeshCount,Is.EqualTo(3));
                    Assert.That(mesh.vertices,Is.EqualTo(vertices));Assert.That(mesh.normals,Is.EqualTo(normals));
                    Assert.That(mesh.uv,Is.EqualTo(uv));Assert.That(mesh.colors,Is.EqualTo(colours));Assert.That(mesh.tangents,Is.EqualTo(tangents));Assert.That(mesh.bounds,Is.EqualTo(bounds));
                    var actual=new System.Collections.Generic.List<string>();var expected=new System.Collections.Generic.List<string>();
                    for(int i=0;i<before.Length;i+=3)expected.Add($"{before[i]},{before[i+1]},{before[i+2]}");
                    float[] areas=new float[3];
                    for(int slot=0;slot<3;slot++)
                    {
                        var t=mesh.GetTriangles(slot);Assert.That(t.Length,Is.GreaterThan(0));
                        for(int i=0;i<t.Length;i+=3)
                        {
                            actual.Add($"{t[i]},{t[i+1]},{t[i+2]}");
                            Assert.That(normals[t[i]].y,Is.EqualTo(slot==0?1:slot==2?-1:0));
                            areas[slot]+=Vector3.Cross(vertices[t[i+1]]-vertices[t[i]],vertices[t[i+2]]-vertices[t[i]]).magnitude/2;
                        }
                    }
                    Assert.That(actual,Is.EquivalentTo(expected));
                    Assert.That(areas[0],Is.EqualTo(24).Within(.001f));Assert.That(areas[1],Is.EqualTo(20).Within(.001f));Assert.That(areas[2],Is.EqualTo(24).Within(.001f));
                }
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(contact);}
        }
        [Test] public void MachinedFittingsHaveFiniteOutwardNormalsAndStayInsideTheirEnvelope()
        {
            var size=new Vector3(3.6f,.2f,.095f);var bevel=WellDetailMesh.Bevel(size);var cylinder=WellDetailMesh.Cylinder();
            try
            {
                foreach(var mesh in new[]{bevel,cylinder})
                {
                    var vertices=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        Assert.That(float.IsNaN(vertices[i].sqrMagnitude)||float.IsInfinity(vertices[i].sqrMagnitude),Is.False);
                        Assert.That(normals[i].magnitude,Is.EqualTo(1).Within(.0001f));
                        Assert.That(Vector3.Dot(vertices[i],normals[i]),Is.GreaterThan(0));
                    }
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var normal=Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]);
                        Assert.That(Vector3.Dot(normal,normals[indices[i]]),Is.GreaterThan(0),mesh.name+" triangle "+i);
                    }
                }
                Assert.That(bevel.bounds.size.x,Is.EqualTo(size.x).Within(.0001f));
                Assert.That(bevel.bounds.size.y,Is.EqualTo(size.y).Within(.0001f));
                Assert.That(bevel.bounds.size.z,Is.EqualTo(size.z).Within(.0001f));
            }
            finally{Object.DestroyImmediate(bevel);Object.DestroyImmediate(cylinder);}
        }
        [Test] public void ContactVisibilityPreservesExposedGeometryAndRespondsOnlyToNearbyObstruction()
        {
            var empty=new Bounds[0];var wall=new[]{new Bounds(new Vector3(.6f,1,0),new Vector3(1,2,4))};
            Assert.That(WellContactMesh.Visibility(Vector3.zero,Vector3.up,Vector3.right,Vector3.back,empty),Is.EqualTo(1));
            Assert.That(WellContactMesh.Visibility(Vector3.zero,Vector3.up,Vector3.right,Vector3.back,wall),Is.LessThan(.9f));
            var original=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.zero,new Vector3(4,2,1))});
            var shaded=WellContactMesh.Build(original,empty);
            try
            {
                Assert.That(Area(shaded),Is.EqualTo(Area(original)).Within(.001f));Assert.That(shaded.bounds,Is.EqualTo(original.bounds));
                Assert.That(shaded.colors.All(c=>c.r==1&&c.g==1&&c.b==1),Is.True);
                Assert.That(shaded.vertexCount,Is.GreaterThan(original.vertexCount));
            }
            finally{Object.DestroyImmediate(original);Object.DestroyImmediate(shaded);}
        }
        [Test] public void JoinedSurfacesUseContinuousWorldMetreUvs()
        {
            var mesh=WellSurfaceMesh.Build(new[]{new Bounds(new Vector3(3,2,1),new Vector3(4,3,2)),new Bounds(new Vector3(5,2,1),new Vector3(2,2,2))});
            try
            {
                Assert.That(mesh.uv.Length,Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.tangents.Length,Is.EqualTo(mesh.vertexCount),"Normal maps require a tangent on every exposed surface");
                for(int i=0;i<mesh.vertexCount;i++)
                {
                    var tangent=(Vector3)mesh.tangents[i];
                    Assert.That(tangent.magnitude,Is.EqualTo(1).Within(.001f));
                    Assert.That(Vector3.Dot(tangent,mesh.normals[i]),Is.EqualTo(0).Within(.001f));
                }
                for(int i=0;i<mesh.vertexCount;i++)
                {
                    var p=mesh.vertices[i];var n=mesh.normals[i];
                    var expected=Mathf.Abs(n.y)>.5f?new Vector2(p.x,p.z):new Vector2(Mathf.Abs(n.x)>.5f?p.z:p.x,p.y);
                    Assert.That(mesh.uv[i],Is.EqualTo(expected));
                }
            }
            finally{Object.DestroyImmediate(mesh);}
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
