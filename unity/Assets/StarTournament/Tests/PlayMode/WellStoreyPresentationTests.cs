using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellStoreyPresentationTests
    {
        [UnityTest] public IEnumerator PreserveAuthoringAndCaptureComparableStoreys()
        {
            var stage=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_REVIEW_STAGE")??"departments";
            Assert.That(new[]{"materials","navigation","departments"},Does.Contain(stage));
            var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-storeys",stage));Directory.CreateDirectory(directory);
            var root=new GameObject("Well storey review");var movement=ProvingProfile.CreateDefault();var arenaGo=new GameObject("Arena");arenaGo.transform.SetParent(root.transform);var arena=arenaGo.AddComponent<ProvingArena>();
            var cameraGo=new GameObject("Review camera");cameraGo.transform.SetParent(root.transform);var camera=cameraGo.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
            var lightGo=new GameObject("Review daylight");lightGo.transform.SetParent(root.transform);WellArenaPresentation.ConfigureDaylight(lightGo.AddComponent<Light>(),ProvingProfile.CreateWellDefault());
            var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement);yield return null;yield return null;
                var skin=arena.GetComponentInChildren<WellArenaPresentation>();var renderers=skin.GetComponentsInChildren<MeshRenderer>();var filters=skin.GetComponentsInChildren<MeshFilter>();
                Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())),"Exact authored map stays unchanged");
                Assert.That(skin.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(skin.GetComponentsInChildren<Light>().Length,Is.EqualTo(24));
                int vertices=filters.Sum(f=>f.sharedMesh.vertexCount);Assert.That(vertices,Is.LessThan(650000));Assert.That(renderers.Length,Is.LessThan(64));
                Assert.That(arena.Definition.Solids.Count(WellFurniturePlan.IsFurniture),Is.EqualTo(62));
                var materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();var floorMaterials=materials.Where(m=>m.name.StartsWith("Well storey")&&m.name.EndsWith(" floor")).OrderBy(m=>m.name).ToArray();
                Assert.That(floorMaterials.Length,Is.EqualTo(3));Assert.That(floorMaterials.Select(m=>m.GetTexture("_BaseMap")).Distinct().Count(),Is.EqualTo(3));
                for(int f=0;f<3;f++)
                {
                    Assert.That(materials.Any(m=>m.name=="Well storey "+f+" upper wall"),Is.True);Assert.That(materials.Any(m=>m.name=="Well storey "+f+" lower panel"),Is.True);
                    foreach(var filter in filters.Where(x=>x.name=="Well storey "+f+" floor"))
                    {
                        var mesh=filter.sharedMesh;var points=mesh.vertices;
                        foreach(int i in mesh.GetTriangles(0))Assert.That(points[i].y,Is.EqualTo(f*WellArenaCatalog.Storey).Within(.0001f),"Top material cannot leak to another floor or a ceiling");
                    }
                }
                if(stage!="materials")
                {
                    var numbers=skin.GetComponentsInChildren<TextMesh>().Where(t=>Mathf.Abs(t.transform.forward.y)>.9f).ToArray();
                    Assert.That(numbers.Length,Is.EqualTo(6));
                    for(int f=0;f<3;f++)Assert.That(numbers.Count(t=>t.text=="0"+(f+1)),Is.EqualTo(2));
                    int routeFaces=0;
                    foreach(var filter in filters.Where(t=>t.name=="01 bio green"||t.name=="02 physics blue"||t.name=="03 electronics yellow"))
                    {
                        var mesh=filter.sharedMesh;var points=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];if(normals[a].y<.99f)continue;
                            float height=points[a].y;float local=height-Mathf.Round(height/WellArenaCatalog.Storey)*WellArenaCatalog.Storey;
                            if(Mathf.Abs(local-.012f)>.0001f)continue;routeFaces++;
                            for(int u=0;u<=8;u++)for(int v=0;v<=8-u;v++)
                            {
                                var point=points[a]*(u/8f)+points[b]*(v/8f)+points[c]*(1-(u+v)/8f);
                                Assert.That(arena.Definition.Solids.Any(s=>s.Material=="floor"&&new Bounds(s.Position,s.Size).Contains(point-Vector3.up*.03f)),Is.True,"Route must stay on an opaque floor: "+point);
                                Assert.That(arena.Definition.Solids.Any(s=>s.Material!="floor"&&new Bounds(s.Position,s.Size).Contains(point+Vector3.up*.05f)),Is.False,"Route must not enter walls or furniture: "+point);
                            }
                        }
                    }
                    Assert.That(routeFaces,Is.GreaterThanOrEqualTo(48));
                }
                if(stage=="departments")
                {
                    var diagnostics=materials.Where(m=>m.name.EndsWith(" diagnostic display")).ToArray();
                    Assert.That(diagnostics.Length,Is.EqualTo(3));Assert.That(diagnostics.Select(m=>m.GetTexture("_BaseMap")).Distinct().Count(),Is.EqualTo(3));
                    var names=new[]{"biology","physics","electronics"};
                    for(int f=0;f<3;f++)
                    {
                        string materialName="Well "+names[f]+" diagnostic display";
                        Assert.That(diagnostics.Any(m=>m.name==materialName),Is.True);
                        foreach(var filter in filters.Where(m=>m.GetComponent<MeshRenderer>().sharedMaterials.Any(a=>a.name==materialName)))
                        foreach(var point in filter.sharedMesh.vertices)
                        {
                            float height=filter.transform.TransformPoint(point).y;
                            Assert.That(height,Is.InRange(f*WellArenaCatalog.Storey+.01f,f*WellArenaCatalog.Storey+WellArenaCatalog.ClearHeight-.01f),materialName+" must not leak onto a neighbouring storey");
                        }
                    }
                }
                foreach(var material in materials)Assert.That(material.shader.isSupported,Is.True,material.name);
                var report=new List<string>{"stage="+stage,"map="+arena.Definition.Identity,"renderers="+renderers.Length,"vertices="+vertices,"lights=24","cosmetic_colliders=0","furniture=62"};
                foreach(var m in floorMaterials)report.Add(m.name+" colour="+m.color+" texture="+m.GetTexture("_BaseMap").name);
                File.WriteAllLines(Path.Combine(directory,"audit.txt"),report);
                for(int f=0;f<3;f++)
                {
                    float y=f*WellArenaCatalog.Storey;int side=f==1?-1:1,end=f==1?-1:1;float benchX=side*(f==1?22:12.7f);
                    var eyes=new[]{new Vector3(17.5f,y+1.7f,20.5f),new Vector3(6,y+1.7f,15),new Vector3(benchX-side*3.4f,y+1.7f,-end*1)};
                    var targets=new[]{new Vector3(19.3f,y+1.4f,23.85f),new Vector3(0,y+1.5f,23.5f),new Vector3(benchX,y+.8f,-end*6.4f)};
                    var names=new[]{"room","lift","corridor"};
                    for(int i=0;i<eyes.Length;i++)
                    {
                        cameraGo.transform.position=eyes[i];cameraGo.transform.LookAt(targets[i]);camera.Render();yield return null;
                        var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                        try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,names[i]+"-floor-"+f+".png"),image.EncodeToPNG());}
                        finally{RenderTexture.active=previous;Object.Destroy(image);}
                    }
                }
            }
            finally{camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(root);}yield return null;
        }
    }
}
