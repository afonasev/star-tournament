using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WellWorkZonePlayTests
    {
        GameObject root;ProvingArena arena;ProvingProfile movement;
        static string Output=>Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_ZONES_OUTPUT");
        void Build(ArenaDefinition definition)
        {
            root=new GameObject("Well work zone QA");movement=ProvingProfile.CreateDefault();
            var go=new GameObject("Arena");go.transform.SetParent(root.transform);arena=go.AddComponent<ProvingArena>();arena.Build(ArenaFreezeSnapshot.Create(definition,movement),movement);
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(root)Object.Destroy(root);yield return null;}
        void Walk(Vector3[] feet,string label)
        {
            var go=new GameObject("Work zone capsule");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();
            var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,feet[0],arena);
            try
            {
                for(int i=0;i<5;i++)motor.Tick(default,1/60f);
                for(int next=1;next<feet.Length;next++)
                {
                    for(int tick=0;tick<600;tick++)
                    {
                        var delta=feet[next]-motor.State.Position;delta.y=0;if(delta.magnitude<.16f)break;
                        var local=Quaternion.Euler(0,-motor.State.Yaw,0)*delta.normalized;
                        motor.Tick(new LocalAction{Move=new Vector2(local.x,local.z)},1/60f);Physics.SyncTransforms();
                    }
                    Assert.That(Vector3.Distance(motor.State.Position,feet[next]),Is.LessThan(.25f),label+" at "+motor.State.Position+" goal "+feet[next]);
                }
            }
            finally{Object.DestroyImmediate(go);}
        }
        [UnityTest]public IEnumerator BothSidesOfIslandAndPostAndArchiveBypassArePhysicallyWalkable()
        {
            Build(WellArenaCatalog.Build());yield return null;Physics.SyncTransforms();
            float y=WellArenaCatalog.Storey;
            var routes=new NativeNavigationProvider(arena,movement,ProvingProfile.CreateNavigationDefault());
            var paths=new[]{
                new[]{new Vector3(15.3f,0,18),new Vector3(15.3f,0,23.2f),new Vector3(19.5f,0,23.2f),new Vector3(19.5f,0,18)},
                new[]{new Vector3(-20,y,4),new Vector3(-20,y,8.8f),new Vector3(-16.9f,y,8.8f),new Vector3(-16.9f,y,4)},
                new[]{new Vector3(-21,2*y,21),new Vector3(-19.6f,2*y,20),new Vector3(-12.6f,2*y,20),new Vector3(-12.6f,2*y,23.5f)}
            };
            foreach(var path in paths)
            {
                foreach(bool reverse in new[]{false,true})Walk(reverse?path.Reverse().ToArray():path,"work zone reverse="+reverse);
                Assert.That(routes.TryRoute(path.First(),path.Last(),out _,out var failure),Is.True,failure);
            }
            foreach(var id in new[]{"furniture-f0-corner-bench-1-1","furniture-f1-corridor-bench","furniture-f2-cabinet--1-1"})
            {
                var solid=arena.Definition.Solids.Single(s=>s.Id==id);float floor=solid.Position.y-solid.Size.y/2;
                var start=new Vector3(solid.Position.x-solid.Size.x/2-.6f,floor+.7f,solid.Position.z);
                Assert.That(Physics.Raycast(start,Vector3.right,out var hit,solid.Size.x+1.2f,ProvingArena.ShotMask),Is.True,id);
                Assert.That(hit.collider.name,Is.EqualTo(id));
            }
        }
        static void Capture(Camera camera,string file)
        {
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.DefaultHDR);var active=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,file),image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=active;Object.Destroy(image);Object.Destroy(rt);}
        }
        [UnityTest]public IEnumerator ComparableViewsAndAssetInventoryReuseExistingFurniture()
        {
            Assert.That(Output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(Output);
            var records=new List<string>();var inventories=new List<long[]>();
            foreach(bool before in new[]{true,false})
            {
                Build(AuthoredArenaCatalog.Resolve(WellArenaCatalog.Id,before?"7":"8"));
                var light=new GameObject("Review daylight");light.transform.SetParent(root.transform);WellArenaPresentation.ConfigureDaylight(light.AddComponent<Light>(),ProvingProfile.CreateWellDefault());
                var go=new GameObject("Work zone observer");go.transform.SetParent(root.transform);var camera=go.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
                yield return null;yield return null;
                var skin=arena.GetComponentInChildren<WellArenaPresentation>();Assert.That(skin.GetComponentsInChildren<Collider>(),Is.Empty);
                var renderers=arena.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
                var meshes=arena.GetComponentsInChildren<MeshFilter>().Where(m=>m.GetComponent<Renderer>()?.enabled==true).Select(m=>m.sharedMesh).Distinct().ToArray();
                var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
                var textures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(m.GetTexture)).Where(t=>t).Distinct().ToArray();
                long triangles=meshes.Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3));
                var inventory=new long[]{renderers.Length,materials.Length,triangles,textures.Length,textures.Sum(t=>(long)t.width*t.height),skin.GetComponentsInChildren<Light>().Length,arena.GetComponentsInChildren<Collider>().Length,arena.GetComponentsInChildren<LODGroup>().Length,renderers.Count(r=>r.shadowCastingMode!=ShadowCastingMode.Off)};
                inventories.Add(inventory);string tag=before?"before":"after";
                records.Add(tag+" map="+arena.Definition.Identity+" renderers,materials,triangles,textures,texturePixels,localLights,colliders,lodGroups,shadowCasters="+string.Join(",",inventory));
                Assert.That(inventory[5],Is.EqualTo(24));
                var eyes=new[]{new Vector3(13.8f,1.9f,18.1f),new Vector3(-22,9.1f,2.7f),new Vector3(-19.5f,16.3f,18.2f)};
                var targets=new[]{new Vector3(17.3f,.8f,20.7f),new Vector3(-18.2f,8.1f,6.4f),new Vector3(-15.5f,15.7f,22.9f)};
                for(int i=0;i<3;i++){camera.transform.position=eyes[i];camera.transform.LookAt(targets[i]);for(int warm=0;warm<3;warm++){camera.Render();yield return null;}Capture(camera,"zone-"+(i+1)+"-"+tag+".png");}
                File.WriteAllText(Path.Combine(Output,tag+"-arena.json"),JsonUtility.ToJson(arena.Definition,true));
                Object.Destroy(root);yield return null;yield return null;
            }
            File.WriteAllLines(Path.Combine(Output,"asset-metrics.txt"),records);
            // Relocation uses the same assets; materials, textures, lights, collider and LOD counts cannot grow.
            foreach(int index in new[]{1,3,4,5,6,7})Assert.That(inventories[1][index],Is.EqualTo(inventories[0][index]),"asset inventory column "+index);
        }
    }
}
