using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellGardenPresentationTests
    {
        [UnityTest] public IEnumerator EntranceLinkedMeadowPreservesArenaAndCapturesViews()
        {
            var directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_GARDEN_OUTPUT")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-garden/views"));Directory.CreateDirectory(directory);
            var root=new GameObject("Well garden review");var movement=ProvingProfile.CreateDefault();var art=ProvingProfile.CreateWellDefault();
            var arenaGo=new GameObject("Arena");arenaGo.transform.SetParent(root.transform);var arena=arenaGo.AddComponent<ProvingArena>();
            var cameraGo=new GameObject("Review camera");cameraGo.transform.SetParent(root.transform);var camera=cameraGo.AddComponent<Camera>();
            camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
            var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.DefaultHDR);camera.targetTexture=rt;
            var lightGo=new GameObject("Daylight");lightGo.transform.SetParent(root.transform);var light=lightGo.AddComponent<Light>();WellArenaPresentation.ConfigureDaylight(light,art);
            NativeVisualPresentation visual=null;
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement,art);yield return null;
                visual=root.AddComponent<NativeVisualPresentation>();visual.Bind(movement,arena.transform,light,new[]{camera});yield return null;yield return null;
                var skin=arena.GetComponentInChildren<WellArenaPresentation>();var filters=skin.GetComponentsInChildren<MeshFilter>();
                Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())));
                // Compare to durable pre-garden bytes, rather than just rebuilding the new authoring twice.
                var baseline=JsonUtility.FromJson<ArenaDefinition>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"StarTournament/Tests/EditMode/Fixtures/well-daylight-arena.json"))));
                Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(JsonUtility.ToJson(baseline)));
                Assert.That(skin.GetComponentsInChildren<Collider>(),Is.Empty);
                Assert.That(skin.GetComponentsInChildren<Light>().Length,Is.EqualTo(24));Assert.That(skin.GetComponentsInChildren<Light>().Count(l=>l.shadows!=LightShadows.None),Is.EqualTo(3));
                Assert.That(arena.Definition.Solids.Count(WellFurniturePlan.IsFurniture),Is.EqualTo(56));
                int vertices=filters.Sum(f=>f.sharedMesh.vertexCount),renderers=skin.GetComponentsInChildren<MeshRenderer>().Length;
                Assert.That(vertices,Is.LessThan(650000));Assert.That(renderers,Is.LessThan(64));
                var garden=filters.Single(f=>f.name=="Well natural garden");var mesh=garden.sharedMesh;
                Assert.That(mesh.subMeshCount,Is.EqualTo(6));
                var grassMaterial=garden.GetComponent<MeshRenderer>().sharedMaterials[2];
                Assert.That(grassMaterial.IsKeywordEnabled("_ALPHATEST_ON"),Is.True);
                Assert.That(grassMaterial.color,Is.EqualTo(Color.white));
                var grassTexture=(Texture2D)grassMaterial.GetTexture("_BaseMap");Assert.That(grassTexture,Is.Not.Null);
                var probeTarget=RenderTexture.GetTemporary(64,32,0,RenderTextureFormat.ARGB32);
                var probePixels=new Texture2D(64,32,TextureFormat.RGBA32,false);var previousTarget=RenderTexture.active;
                try
                {
                    Graphics.Blit(grassTexture,probeTarget);RenderTexture.active=probeTarget;probePixels.ReadPixels(new Rect(0,0,64,32),0,0);probePixels.Apply();
                    var visible=probePixels.GetPixels().Where(p=>p.a>.7f).ToArray();Assert.That(visible.Length,Is.GreaterThan(100));
                    Assert.That(visible.Average(p=>p.g),Is.GreaterThan(.15f),"Imported grass must retain RGB, not just its alpha silhouette");
                }
                finally{RenderTexture.active=previousTarget;RenderTexture.ReleaseTemporary(probeTarget);Object.Destroy(probePixels);}
                Assert.That(filters.Any(f=>f.sharedMesh.name=="Well curved leaves"),Is.False,"No mismatched large 3D foliage remains");
                var points=mesh.vertices;var triangles=mesh.GetTriangles(1);
                foreach(float side in new[]{-1f,1f})
                {
                    var bedPoints=mesh.GetTriangles(2).Select(index=>points[index]).Where(p=>p.y>.33f&&Mathf.Abs(p.x-side*6)<1.4f&&Mathf.Abs(p.z-side*6)<1.4f).ToArray();
                    Assert.That(bedPoints.Length,Is.EqualTo(1200),"Both beds use exactly 100 crossed meadow tufts in the same grass submesh/material");
                    foreach(var p in bedPoints)Assert.That(p.y,Is.InRange(.345f,.56f),"Same low tuft silhouette relative to raised soil");
                    Assert.That(mesh.GetTriangles(2).Select(index=>points[index]).Any(p=>p.y>.33f&&Mathf.Abs(p.x-side*6)<1.85f&&Mathf.Abs(p.z-side*6)<1.85f&&(Mathf.Abs(p.x-side*6)>1.4f||Mathf.Abs(p.z-side*6)>1.4f)),Is.False,"Bed grass cannot spill over its soil footprint");
                }
                Assert.That(triangles.Length,Is.GreaterThan(100));
                // Geometric coverage along the complete route, and across both actual thresholds.
                bool Paved(Vector2 p)
                {
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var a=points[triangles[i]];var b=points[triangles[i+1]];var c=points[triangles[i+2]];
                        if(Mathf.Abs(a.y-b.y)>.00001f||Mathf.Abs(a.y-c.y)>.00001f)continue;
                        var v0=new Vector2(b.x-a.x,b.z-a.z);var v1=new Vector2(c.x-a.x,c.z-a.z);var v2=p-new Vector2(a.x,a.z);
                        float det=v0.x*v1.y-v1.x*v0.y;if(Mathf.Abs(det)<.000001f)continue;
                        float u=(v2.x*v1.y-v1.x*v2.y)/det,v=(v0.x*v2.y-v2.x*v0.y)/det;
                        if(u>=0&&v>=0&&u+v<=1)return true;
                    }
                    return false;
                }
                for(int row=0;row<24;row++)
                {
                    float x=-9.2f+row*.8f;
                    Assert.That(Paved(new Vector2(x,WellLabDetail.GardenPathZ(x))),Is.True,"Route row "+row);
                }
                foreach(float side in new[]{-1f,1f})
                {
                    Assert.That(Paved(new Vector2(side*9.58f,0)),Is.True,"Pavers meet the real entrance");
                    for(float z=-1;z<=1;z+=.25f)
                    {
                        var p=new Vector3(side*9.6f,.1f,z);
                        Assert.That(arena.Definition.Solids.Any(s=>s.Material!="floor"&&new Bounds(s.Position,s.Size).Contains(p)),Is.False,"Entrance must actually be open");
                    }
                }
                foreach(int submesh in new[]{2,3,4,5})foreach(int index in mesh.GetTriangles(submesh))
                {
                    var p=points[index];Assert.That(Mathf.Abs(p.x),Is.LessThan(9.6f));Assert.That(Mathf.Abs(p.z),Is.LessThan(9.6f));
                    bool raisedBed=p.y>=.345f&&new[]{-1f,1f}.Any(side=>Mathf.Abs(p.x-side*6)<1.4f&&Mathf.Abs(p.z-side*6)<1.4f);
                    if(raisedBed)Assert.That(p.y,Is.InRange(.345f,.56f));
                    else Assert.That(p.y,Is.InRange(.008f,.43f));
                    Assert.That(new Vector2(p.x,p.z).magnitude,Is.GreaterThan(2.35f),"Clear central landing");
                    Assert.That(Mathf.Abs(p.z-WellLabDetail.GardenPathZ(p.x)),Is.GreaterThan(1.45f),"Clear walking route");
                }
                var names=new[]{"atrium-corner","west-entrance","east-entrance","garden-overview","garden-detail","landing"};
                var eyes=new[]{new Vector3(7,1.7f,-7),new Vector3(-11,1.7f,0),new Vector3(11,1.7f,0),new Vector3(6,6,-6),new Vector3(-4,1.7f,-3),new Vector3(0,1.7f,0)};
                var targets=new[]{new Vector3(9.6f,4,9.6f),new Vector3(0,.3f,0),new Vector3(0,.3f,0),Vector3.zero,new Vector3(-6,.3f,-6),new Vector3(9.6f,.4f,0)};
                for(int i=0;i<names.Length;i++)
                {
                    camera.transform.position=eyes[i];camera.transform.LookAt(targets[i]);camera.Render();yield return null;
                    var previous=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,names[i]+".png"),image.EncodeToPNG());}
                    finally{RenderTexture.active=previous;Object.Destroy(image);}
                }
                File.WriteAllText(Path.Combine(directory,"audit.json"),"{\"map\":\""+arena.Definition.Identity+"\",\"lights\":24,\"furniture\":56,\"cosmetic_colliders\":0,\"vertices\":"+vertices+",\"renderers\":"+renderers+",\"garden_vertices\":"+mesh.vertexCount+"}");
            }
            finally{if(visual)visual.ResetWorld();camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(root);}yield return null;
        }
    }
}
