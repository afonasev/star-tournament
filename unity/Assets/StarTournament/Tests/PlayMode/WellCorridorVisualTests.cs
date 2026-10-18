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
    public sealed class WellCorridorVisualTests
    {
        sealed class Face
        {
            public Vector2[] Points;public Rect Rect;public string Owner;public Vector3 Centre;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static float Overlap(Vector2[] subject,Vector2[] clip)
        {
            var origin=clip[0];subject=subject.Select(p=>p-origin).ToArray();clip=clip.Select(p=>p-origin).ToArray();
            var polygon=subject.ToList();float winding=Mathf.Sign(Cross(clip[1]-clip[0],clip[2]-clip[0]));
            for(int edge=0;edge<3&&polygon.Count>0;edge++)
            {
                var a=clip[edge];var direction=clip[(edge+1)%3]-a;var output=new List<Vector2>();
                for(int i=0;i<polygon.Count;i++)
                {
                    var p=polygon[i];var q=polygon[(i+1)%polygon.Count];float dp=winding*Cross(direction,p-a),dq=winding*Cross(direction,q-a);
                    if(dp>=0)output.Add(p);
                    if((dp>=0)!=(dq>=0))output.Add(p+(q-p)*(dp/(dp-dq)));
                }
                polygon=output;
            }
            float area=0;for(int i=0;i<polygon.Count;i++)area+=Cross(polygon[i],polygon[(i+1)%polygon.Count]);return Mathf.Abs(area)*.5f;
        }
        static List<string> Conflicts(WellArenaPresentation skin)
        {
            var groups=new Dictionary<string,List<Face>>();
            foreach(var filter in skin.GetComponentsInChildren<MeshFilter>())
            {
                string owner=filter.name;
                // Architectural batches and doorway trims; furniture is reviewed separately.
                if(!owner.StartsWith("Well storey ")&&owner!="Well ceramic white"&&owner!="Well pale floor"&&!owner.StartsWith("0")&&owner!="Well details / Well brushed metal"&&owner!="Well details / Well satin aluminium")continue;
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)
                {
                    var a=filter.transform.TransformPoint(vertices[indices[i]]);var b=filter.transform.TransformPoint(vertices[indices[i+1]]);var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                    var n=Vector3.Cross(b-a,c-a).normalized;int axis=Mathf.Abs(n.x)>.99999f?0:Mathf.Abs(n.y)>.99999f?1:Mathf.Abs(n.z)>.99999f?2:-1;if(axis<0)continue;
                    if(Mathf.Abs(a[axis]-b[axis])>.00001f||Mathf.Abs(a[axis]-c[axis])>.00001f)continue;
                    var centre=(a+b+c)/3;
                    // Door/stair headers and wall junctions above furniture, plus every horizontal architectural face.
                    if(owner.StartsWith("Well details")&&centre.y%WellArenaCatalog.Storey<2.8f)continue;
                    int u=(axis+1)%3,v=(axis+2)%3;
                    var points=new[]{new Vector2(a[u],a[v]),new Vector2(b[u],b[v]),new Vector2(c[u],c[v])};
                    if(Mathf.Abs(Cross(points[1]-points[0],points[2]-points[0]))<.000001f)continue;
                    string key=axis+":"+Mathf.Sign(n[axis])+":"+Mathf.RoundToInt(a[axis]*10000);
                    if(!groups.TryGetValue(key,out var group))groups[key]=group=new List<Face>();
                    group.Add(new Face{Points=points,Rect=Rect.MinMaxRect(Mathf.Min(a[u],b[u],c[u]),Mathf.Min(a[v],b[v],c[v]),Mathf.Max(a[u],b[u],c[u]),Mathf.Max(a[v],b[v],c[v])),Owner=owner,Centre=centre});
                }
            }
            var conflicts=new List<string>();
            foreach(var group in groups.Values)
            {
                group.Sort((a,b)=>a.Rect.xMin.CompareTo(b.Rect.xMin));
                for(int i=0;i<group.Count;i++)for(int j=i+1;j<group.Count&&group[j].Rect.xMin<group[i].Rect.xMax-.00001f;j++)
                {
                    var a=group[i];var b=group[j];
                    if(b.Rect.yMax<=a.Rect.yMin+.00001f||b.Rect.yMin>=a.Rect.yMax-.00001f)continue;
                    float area=Overlap(a.Points,b.Points);
                    if(area>.00001f)
                    {conflicts.Add(a.Owner+" / "+b.Owner+" at "+a.Centre.ToString("F4")+" vs "+b.Centre.ToString("F4")+" area="+area);if(conflicts.Count>=20)return conflicts;}
                }
            }
            return conflicts;
        }
        [UnityTest] public IEnumerator CorridorZonesKeepClearStructureAndSaveAllFloors()
        {
            var root=new GameObject("Well joint stability QA");var movement=ProvingProfile.CreateDefault();var arenaObject=new GameObject("Arena");arenaObject.transform.SetParent(root.transform);var arena=arenaObject.AddComponent<ProvingArena>();
            var cameraObject=new GameObject("QA camera");cameraObject.transform.SetParent(root.transform);var camera=cameraObject.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.backgroundColor=new Color(.8f,.87f,.94f);camera.clearFlags=CameraClearFlags.SolidColor;
            var lightObject=new GameObject("QA daylight");lightObject.transform.SetParent(root.transform);WellArenaPresentation.ConfigureDaylight(lightObject.AddComponent<Light>(),ProvingProfile.CreateWellDefault());
            var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement);yield return null;
                var skin=arena.GetComponentInChildren<WellArenaPresentation>();
                Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())));
                Assert.That(skin.GetComponentsInChildren<Collider>(),Is.Empty);
                Assert.That(skin.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.vertexCount),Is.LessThan(650000),"Existing Well presentation geometry budget");
                Assert.That(skin.GetComponentsInChildren<MeshRenderer>().Length,Is.LessThan(64),"Existing material batch budget");
                Assert.That(skin.GetComponentsInChildren<Light>().Length,Is.EqualTo(24));
                var conflicts=Conflicts(skin);
                Assert.That(arena.Definition.Solids.Count(s=>s.Id.Contains("-corridor-")),Is.EqualTo(12));
                var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-storeys/architecture-audit"));Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,"surface-audit.txt"),conflicts.Count==0?"No overlapping opaque architectural/door-header triangles.\n":string.Join("\n",conflicts));
                var eyes=new List<Vector3>();var targets=new List<Vector3>();var names=new List<string>();
                for(int floor=0;floor<3;floor++)
                {
                    float y=floor*WellArenaCatalog.Storey;int side=floor==1?-1:1,end=floor==1?-1:1;
                    eyes.Add(new Vector3(side*3,y+1.7f,end*18));targets.Add(new Vector3(side*6.8f,y+.7f,end*22.8f));names.Add("storage-floor-"+floor);
                    float benchX=side*(floor==1?22:12.7f);
                    eyes.Add(new Vector3(benchX-side*3.4f,y+1.7f,-end*1));targets.Add(new Vector3(benchX,y+.8f,-end*6.4f));names.Add("bench-floor-"+floor);
                    eyes.Add(new Vector3(side*7,y+1.7f,-end*10));targets.Add(new Vector3(side*11,y+1,-end*15.25f));names.Add("cabinet-floor-"+floor);
                }
                for(int i=0;i<eyes.Count;i++)
                {
                    cameraObject.transform.position=eyes[i];cameraObject.transform.LookAt(targets[i]);camera.Render();yield return null;
                    var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    try{texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(directory,names[i]+".png"),texture.EncodeToPNG());}
                    finally{RenderTexture.active=previous;Object.Destroy(texture);}
                }
                Assert.That(conflicts,Is.Empty,string.Join("\n",conflicts));
            }
            finally{camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(root);}yield return null;
        }
    }
}
