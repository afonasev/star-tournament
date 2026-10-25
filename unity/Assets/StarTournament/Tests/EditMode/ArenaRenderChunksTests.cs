using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class ArenaRenderChunksTests
    {
        [Test] public void ChunksPreserveEveryTriangleAttributeMaterialAndCollider()
        {
            var root=new GameObject("chunk contract");var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var other=new Material(material);var mesh=new Mesh();
            try
            {
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var tangents=new List<Vector4>();var colors=new List<Color>();var uv=new List<Vector4>();
                for(int i=0;i<25920;i++){vertices.Add(new Vector3(i%12*4,i%3,i/12*4));normals.Add(Vector3.up);tangents.Add(new Vector4(1,0,0,-1));colors.Add(new Color(i/25920f,.25f,.5f,1));uv.Add(new Vector4(i*.25f,i*.125f,i*.5f,i));}
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTangents(tangents);mesh.SetColors(colors);
                mesh.SetUVs(0,uv.Select(v=>new Vector2(v.x,v.y)).ToList());mesh.SetUVs(1,uv.Select(v=>new Vector3(v.x,v.y,v.z)).ToList());mesh.SetUVs(7,uv);
                mesh.subMeshCount=2;mesh.SetTriangles(Enumerable.Range(0,12960).ToArray(),0);mesh.SetTriangles(Enumerable.Range(12960,12960).ToArray(),1);mesh.RecalculateBounds();
                root.AddComponent<MeshFilter>().sharedMesh=mesh;var source=root.AddComponent<MeshRenderer>();source.sharedMaterials=new[]{material,other};
                source.shadowCastingMode=ShadowCastingMode.TwoSided;source.receiveShadows=false;source.renderingLayerMask=7;
                var collider=root.AddComponent<BoxCollider>();var beforeBounds=collider.bounds;
                var optimizer=root.AddComponent<ArenaRenderChunks>();optimizer.Build(root.transform, false);
                Assert.That(optimizer.SourceCount,Is.EqualTo(1));Assert.That(optimizer.ChunkCount,Is.GreaterThan(2));Assert.That(source.enabled,Is.False);
                Assert.That(root.GetComponent<BoxCollider>(),Is.SameAs(collider));Assert.That(collider.bounds,Is.EqualTo(beforeBounds));
                var originalLookup=vertices.Select((v,i)=>new{v,i}).ToDictionary(x=>x.v,x=>x.i);
                var remaining=new HashSet<int>(Enumerable.Range(0,25920));
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r!=source))
                {
                    Assert.That(renderer.shadowCastingMode,Is.EqualTo(source.shadowCastingMode));Assert.That(renderer.receiveShadows,Is.False);Assert.That(renderer.renderingLayerMask,Is.EqualTo(7));
                    var part=renderer.GetComponent<MeshFilter>().sharedMesh;var partUV0=new List<Vector2>();var partUV1=new List<Vector3>();var partUV7=new List<Vector4>();part.GetUVs(0,partUV0);part.GetUVs(1,partUV1);part.GetUVs(7,partUV7);
                    var pv=part.vertices;var pn=part.normals;var pt=part.tangents;var pc=part.colors;
                    foreach(int index in part.triangles)
                    {
                        int original=originalLookup[pv[index]];Assert.That(remaining.Remove(original),Is.True,"triangle vertex lost or duplicated");
                        Assert.That(renderer.sharedMaterial,Is.SameAs(original<12960?material:other));Assert.That(pn[index],Is.EqualTo(normals[original]));Assert.That(pt[index],Is.EqualTo(tangents[original]));Assert.That(pc[index],Is.EqualTo(colors[original]));
                        Assert.That(partUV0[index],Is.EqualTo(new Vector2(uv[original].x,uv[original].y)));Assert.That(partUV1[index],Is.EqualTo(new Vector3(uv[original].x,uv[original].y,uv[original].z)));Assert.That(partUV7[index],Is.EqualTo(uv[original]));
                    }
                }
                Assert.That(remaining,Is.Empty);optimizer.UseChunks(false);Assert.That(source.enabled,Is.True);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled),Is.EqualTo(1));
                optimizer.UseChunks(true);Assert.That(source.enabled,Is.False);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);Object.DestroyImmediate(other);}
        }
        [Test] public void SkippedStaticBatchNeverOwnsOrDestroysBorrowedPrimitiveMesh()
        {
            var root=new GameObject("skipped batch ownership");
            var source=GameObject.CreatePrimitive(PrimitiveType.Cube);source.name="single-solid";source.transform.SetParent(root.transform,false);
            var borrowed=source.GetComponent<MeshFilter>().sharedMesh;
            var material=new Material(Shader.Find("Hidden/StarTournament/NoStaticBatchingTest"));
            try
            {
                source.GetComponent<MeshRenderer>().sharedMaterial=material;
                var arena=root.AddComponent<ProvingArena>();
                // This fixture isolates projection ownership without constructing navigation or a match.
                typeof(ProvingArena).GetProperty("Definition").SetValue(arena,new ArenaDefinition{MapId="ownership-fixture",Solids=new[]{new ArenaSolid{Id=source.name}}});
                var optimizer=root.AddComponent<ArenaRenderChunks>();optimizer.Build(root.transform);
                Assert.That(optimizer.SourceCount,Is.EqualTo(1));
                var candidate=source.GetComponentsInChildren<MeshRenderer>().Single(r=>r!=source.GetComponent<MeshRenderer>());
                Assert.That(candidate.isPartOfStaticBatch,Is.False,"fixture must exercise skipped Combine");
                Assert.That(candidate.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(borrowed));
                Assert.That(optimizer.ReadOwnedMeshes().Contains(borrowed),Is.False);
                Object.DestroyImmediate(optimizer);Assert.That(borrowed,Is.Not.Null);Assert.That(borrowed.vertexCount,Is.GreaterThan(0));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(material);}
            Assert.That(borrowed,Is.Not.Null);Assert.That(borrowed.vertexCount,Is.GreaterThan(0));
        }
        [Test] public void TransparentAndPropertyBlockRenderersKeepOriginalSortingAndOwnership()
        {
            var root=GameObject.CreatePrimitive(PrimitiveType.Sphere);var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){renderQueue=3000};
            try
            {
                var mesh=Object.Instantiate(root.GetComponent<MeshFilter>().sharedMesh);var v=mesh.vertices;for(int i=0;i<v.Length;i++)v[i]*=100;mesh.vertices=v;mesh.RecalculateBounds();root.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;var optimizer=root.AddComponent<ArenaRenderChunks>();optimizer.Build(root.transform, false);
                Assert.That(optimizer.ChunkCount,Is.Zero);Assert.That(renderer.enabled,Is.True);
                material.renderQueue=2000;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",Color.red);renderer.SetPropertyBlock(block);optimizer.Build(root.transform, false);
                Assert.That(optimizer.ChunkCount,Is.Zero);Assert.That(renderer.enabled,Is.True);Object.DestroyImmediate(mesh);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(material);}
        }
    }
}
