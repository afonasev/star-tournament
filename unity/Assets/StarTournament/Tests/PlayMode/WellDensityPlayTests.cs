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
    public sealed class WellDensityPlayTests
    {
        GameObject root;ProvingArena arena;ProvingProfile movement;
        static string Output=>Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_DENSITY_OUTPUT");
        void Build(ArenaDefinition definition)
        {
            root=new GameObject("Well density QA");movement=ProvingProfile.CreateDefault();var go=new GameObject("Arena");go.transform.SetParent(root.transform);
            arena=go.AddComponent<ProvingArena>();arena.Build(ArenaFreezeSnapshot.Create(definition,movement),movement);
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(root)Object.Destroy(root);yield return null;}
        void Walk(Vector3[] feet,string label)
        {
            var go=new GameObject("Density capsule");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,feet[0],arena);
            try
            {
                for(int i=0;i<5;i++)motor.Tick(default,1/60f);
                for(int next=1;next<feet.Length;next++)
                {
                    for(int tick=0;tick<600;tick++)
                    {
                        var delta=feet[next]-motor.State.Position;delta.y=0;if(delta.magnitude<.16f)break;
                        var local=Quaternion.Euler(0,-motor.State.Yaw,0)*delta.normalized;motor.Tick(new LocalAction{Move=new Vector2(local.x,local.z)},1/60f);Physics.SyncTransforms();
                    }
                    Assert.That(Vector3.Distance(motor.State.Position,feet[next]),Is.LessThan(.25f),label+" at "+motor.State.Position+" goal "+feet[next]);
                }
            }
            finally{Object.DestroyImmediate(go);}
        }
        [UnityTest]public IEnumerator AllSixBodiesHaveRealTwoWayMotorBypassesNavigationAndShotObstruction()
        {
            Build(WellArenaCatalog.Build());yield return null;Physics.SyncTransforms();var routes=new NativeNavigationProvider(arena,movement,ProvingProfile.CreateNavigationDefault());
            foreach(var solid in arena.Definition.Solids.Where(s=>s.Id.Contains("-density-")))
            {
                float y=solid.Position.y-solid.Size.y/2;float x=solid.Position.x,z=solid.Position.z,w=solid.Size.x/2+.8f,depth=solid.Size.z/2+.8f;
                Vector3[] points;
                if(solid.Surface.EndsWith("bench"))points=new[]{new Vector3(x-w,y,z-depth),new Vector3(x-w,y,z+depth),new Vector3(x+w,y,z+depth),new Vector3(x+w,y,z-depth),new Vector3(x-w,y,z-depth)};
                else
                {
                    float front=z-Mathf.Sign(z)*depth;
                    points=new[]{new Vector3(x-w,y,z),new Vector3(x-w,y,front),new Vector3(x+w,y,front),new Vector3(x+w,y,z)};
                }
                foreach(bool reverse in new[]{false,true})Walk(reverse?points.Reverse().ToArray():points,solid.Id+" reverse="+reverse);
                var start=new Vector3(x-w,y,z);var goal=new Vector3(x+w,y,z);
                Assert.That(routes.TryRoute(start,goal,out var route,out var failure),Is.True,solid.Id+" "+failure);
                Walk(new[]{start}.Concat(route.Select(r=>r.Position)).Concat(new[]{goal}).ToArray(),solid.Id+" navigation");
                Assert.That(Physics.Raycast(start+Vector3.up*.7f,Vector3.right,out var hit,w*2,ProvingArena.ShotMask),Is.True,solid.Id);Assert.That(hit.collider.name,Is.EqualTo(solid.Id));
            }
            foreach(var from in arena.Definition.Spawns)foreach(var to in arena.Definition.Pickups.Select(p=>p.Anchor))Assert.That(routes.TryRoute(from,to,out _,out var failure),Is.True,from+" -> "+to+" "+failure);
        }
        [UnityTest]public IEnumerator CornerDoorThresholdsAndPickupRingFootprintsRemainFree()
        {
            Build(WellArenaCatalog.Build());yield return null;Physics.SyncTransforms();float radius=movement.Get("player.capsule.radius")*.95f,height=movement.Get("player.capsule.height");
            for(int floor=0;floor<3;floor++)foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})foreach(var pos in new[]{new Vector3(x*14.4f,floor*WellArenaCatalog.Storey,z*16),new Vector3(x*9.6f,floor*WellArenaCatalog.Storey,z*18.4f)})
                Assert.That(Physics.CheckCapsule(pos+Vector3.up*(radius+.1f),pos+Vector3.up*(height-radius),radius,(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer)),Is.False,"door "+pos);
            var settings=ProvingProfile.CreateWellDefault();float ring=settings.Get("well.pickupRingRadius")+settings.Get("well.pickupRingWidth")/2;
            foreach(var pickup in arena.Definition.Pickups)for(int i=0;i<64;i++)
            {
                var point=pickup.Anchor+new Vector3(Mathf.Cos(i*Mathf.PI/32)*ring,.1f,Mathf.Sin(i*Mathf.PI/32)*ring);
                foreach(var body in arena.Definition.Solids.Where(s=>s.Id.Contains("-density-")))Assert.That(new Bounds(body.Position,body.Size).Contains(point),Is.False,pickup.Id+" ring under "+body.Id);
            }
        }
        static void Capture(Camera camera,string file)
        {
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.DefaultHDR);var active=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,file),image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=active;Object.Destroy(image);Object.Destroy(rt);}
        }
        [UnityTest]public IEnumerator ComparableRoomAndGalleryViewsAndAssetInventory()
        {
            Assert.That(Output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(Output);var metrics=new List<string>();var inventories=new List<long[]>();
            foreach(bool before in new[]{true,false})
            {
                Build(before?AuthoredArenaCatalog.Resolve(WellArenaCatalog.Id,"8"):WellArenaCatalog.Build());
                var light=new GameObject("Density daylight");light.transform.SetParent(root.transform);WellArenaPresentation.ConfigureDaylight(light.AddComponent<Light>(),ProvingProfile.CreateWellDefault());
                var go=new GameObject("Density observer");go.transform.SetParent(root.transform);var camera=go.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
                yield return null;yield return null;
                var skin=arena.GetComponentInChildren<WellArenaPresentation>();Assert.That(skin.GetComponentsInChildren<Collider>(),Is.Empty);
                var renderers=arena.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var meshes=arena.GetComponentsInChildren<MeshFilter>().Where(m=>m.GetComponent<Renderer>()?.enabled==true).Select(m=>m.sharedMesh).Distinct().ToArray();
                var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();var textures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(m.GetTexture)).Where(t=>t).Distinct().ToArray();
                long triangles=meshes.Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3));
                var inventory=new long[]{renderers.Length,materials.Length,triangles,textures.Length,textures.Sum(t=>(long)t.width*t.height),skin.GetComponentsInChildren<Light>().Length,arena.GetComponentsInChildren<Collider>().Length,arena.GetComponentsInChildren<LODGroup>().Length,renderers.Count(r=>r.shadowCastingMode!=ShadowCastingMode.Off)};
                inventories.Add(inventory);string tag=before?"before":"after";metrics.Add(tag+" map="+arena.Definition.Identity+" renderers,materials,triangles,textures,texturePixels,localLights,colliders,lodGroups,shadowCasters="+string.Join(",",inventory));
                var eyes=new[]{new Vector3(-13,1.9f,-17.5f),new Vector3(12.5f,9.1f,18.3f),new Vector3(13.3f,16.3f,-17.6f)};
                var targets=new[]{new Vector3(-17,.8f,-20.8f),new Vector3(15.3f,8,21.2f),new Vector3(17.3f,15.2f,-20.2f)};
                for(int floor=0;floor<3;floor++)
                {
                    for(int view=0;view<2;view++)
                    {
                        int side=floor==1?1:-1;var anchor=new Vector3(side*11,floor*WellArenaCatalog.Storey,-side*15.25f);
                        camera.transform.position=view==0?eyes[floor]:anchor+new Vector3(-side*2.7f,1.8f,side*3.5f);
                        camera.transform.LookAt(view==0?targets[floor]:anchor+Vector3.up*.9f);
                        for(int warm=0;warm<3;warm++){camera.Render();yield return null;}
                        Capture(camera,(view==0?"room":"gallery")+"-"+(floor+1)+"-"+tag+".png");
                    }
                }
                File.WriteAllText(Path.Combine(Output,tag+"-arena.json"),JsonUtility.ToJson(arena.Definition,true));Object.Destroy(root);yield return null;yield return null;
            }
            File.WriteAllLines(Path.Combine(Output,"asset-metrics.txt"),metrics);
            foreach(int index in new[]{1,3,4,5,7})Assert.That(inventories[1][index],Is.EqualTo(inventories[0][index]),"no new material/texture/light/LOD resources column "+index);
            Assert.That(inventories[1][6],Is.EqualTo(inventories[0][6]+6),"six authoritative colliders only");
        }
    }
}
