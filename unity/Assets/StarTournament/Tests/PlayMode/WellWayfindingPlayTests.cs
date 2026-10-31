using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WellWayfindingPlayTests
    {
        GameObject root;ProvingArena arena;ProvingProfile movement;
        static string Output=>Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_WAYFINDING_OUTPUT");
        void Build(ProvingProfile art)
        {
            root=new GameObject("Wayfinding QA");movement=ProvingProfile.CreateDefault();var go=new GameObject("Arena");go.transform.SetParent(root.transform);arena=go.AddComponent<ProvingArena>();arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement,art);
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(root)Object.Destroy(root);yield return null;}
        static ProvingProfile Legacy(ProvingProfile current)=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWellWayfinding",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(current,null);
        [UnityTest]public IEnumerator PlacardRolesAlignmentDestinationsAndReadPointsMatchRealRoutes()
        {
            Build(ProvingProfile.CreateWellDefault());yield return null;Physics.SyncTransforms();
            var skin=arena.GetComponentInChildren<WellArenaPresentation>();var plates=skin.GetComponentsInChildren<WellWayfindingPlate>();var texts=skin.GetComponentsInChildren<TextMesh>();
            Assert.That(plates.Length,Is.EqualTo(42));Assert.That(texts.Length,Is.EqualTo(60));Assert.That(skin.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(skin.GetComponentsInChildren<Light>().Length,Is.EqualTo(24));
            Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())));Assert.That(arena.Definition.Solids.Count(WellFurniturePlan.IsFurniture),Is.EqualTo(62));
            var roles=new[]{"atrium-floor","lift-floor","lift-directions","lift-header","stair-header","gallery-direction"};
            for(int floor=0;floor<3;floor++)foreach(var role in roles)Assert.That(plates.Count(p=>p.Floor==floor&&p.Role==role),Is.EqualTo(role=="gallery-direction"?4:2),role);
            var settings=ProvingProfile.CreateWellDefault();var navigation=new NativeNavigationProvider(arena,movement,ProvingProfile.CreateNavigationDefault());var audit=new List<string>();
            foreach(var plate in plates)
            {
                float floor=plate.Floor*WellArenaCatalog.Storey;
                float expected=plate.Role=="atrium-floor"?settings.Get("well.signAtriumHeight"):plate.Role=="lift-floor"||plate.Role=="lift-directions"?settings.Get("well.signReaderHeight"):settings.Get("well.signHeaderHeight");
                Assert.That(plate.transform.position.y-floor,Is.EqualTo(expected).Within(.00001f),plate.name);
                if(plate.Role.EndsWith("header"))
                {
                    var wall=arena.Definition.Solids.Single(s=>s.Id==plate.WallId);int along=wall.Size.x>wall.Size.z?0:2;
                    Assert.That(plate.transform.position[along],Is.EqualTo(wall.Position[along]).Within(.00001f),"header centred on real portal");
                }
                foreach(var text in plate.GetComponentsInChildren<TextMesh>())
                {
                    var renderer=text.GetComponent<MeshRenderer>();Assert.That(renderer.sharedMaterial.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"),Is.False);Assert.That(renderer.sharedMaterial.shader.isSupported,Is.True);
                    var centre=renderer.bounds.center;var delta=centre-plate.ReadPoint;
                    var blockers=Physics.RaycastAll(plate.ReadPoint,delta.normalized,Mathf.Max(0,delta.magnitude-.03f),1<<ProvingArena.WorldLayer).Where(h=>!h.collider.GetComponent<NativeSightTransparent>()).ToArray();
                    Assert.That(blockers,Is.Empty,plate.name+" hidden from route read point by "+string.Join(",",blockers.Select(h=>h.collider.name)));
                }
                if(plate.Role=="gallery-direction")
                {
                    int end=plate.Destination=="lift-north"?1:-1;var target=new Vector3(0,floor,end*23);
                    var feet=plate.ReadPoint;feet.y=floor;
                    Assert.That(navigation.TryRoute(feet,target,out _,out var failure),Is.True,plate.name+" "+failure);
                    string text=plate.GetComponentInChildren<TextMesh>().text;bool right=Vector3.Dot(plate.transform.right,Vector3.forward*end)>0;
                    Assert.That(right?text.EndsWith("→"):text.StartsWith("←"),Is.True,plate.name+" direction in local wall axes");
                }
                if(plate.Role=="stair-header")
                {
                    string label=plate.GetComponentInChildren<TextMesh>().text;
                    Assert.That(label.Contains("↑"),Is.EqualTo(plate.Floor<2));Assert.That(label.Contains("↓"),Is.EqualTo(plate.Floor>0));
                }
                audit.Add(plate.Role+" floor="+plate.Floor+" wall="+plate.WallId+" face="+plate.transform.position+" reader="+plate.ReadPoint);
            }
            Assert.That(texts.Count(t=>Mathf.Abs(t.transform.forward.y)>.9f),Is.EqualTo(6),"floor markings retained");
            if(!string.IsNullOrEmpty(Output)){Directory.CreateDirectory(Output);File.WriteAllLines(Path.Combine(Output,"route-read-points.txt"),audit);}
        }
        static void Capture(Camera camera,string file)
        {
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.DefaultHDR);var active=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,file),image.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=active;Object.Destroy(image);Object.Destroy(rt);}
        }
        [UnityTest]public IEnumerator ComparableEyeLevelAndAtriumViewsWithAssetInventory()
        {
            Assert.That(Output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(Output);var records=new List<string>();string exact=null;Vector3[] floorPositions=null;
            foreach(bool before in new[]{true,false})
            {
                var art=ProvingProfile.CreateWellDefault();if(before)art=Legacy(art);Build(art);
                var light=new GameObject("Wayfinding daylight");light.transform.SetParent(root.transform);WellArenaPresentation.ConfigureDaylight(light.AddComponent<Light>(),art);
                var go=new GameObject("Wayfinding observer");go.transform.SetParent(root.transform);var camera=go.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
                yield return null;yield return null;
                var skin=arena.GetComponentInChildren<WellArenaPresentation>();var texts=skin.GetComponentsInChildren<TextMesh>();var positions=texts.Where(t=>Mathf.Abs(t.transform.forward.y)>.9f).Select(t=>t.transform.localPosition).ToArray();
                if(before){exact=JsonUtility.ToJson(arena.Definition);floorPositions=positions;}else{Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(exact));Assert.That(positions,Is.EqualTo(floorPositions));}
                var renderers=arena.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();var meshes=arena.GetComponentsInChildren<MeshFilter>().Where(m=>m.GetComponent<Renderer>()?.enabled==true).Select(m=>m.sharedMesh).Distinct().ToArray();
                var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();var textures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(m.GetTexture)).Where(t=>t).Distinct().ToArray();
                string tag=before?"before":"after";
                records.Add(tag+" map="+arena.Definition.Identity+" art="+art.Version+" renderers="+renderers.Length+" materials="+materials.Length+" triangles="+meshes.Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3))+" textures="+textures.Length+" texturePixels="+textures.Sum(t=>(long)t.width*t.height)+" shadowCasters="+renderers.Count(r=>r.shadowCastingMode!=ShadowCastingMode.Off)+" lights="+skin.GetComponentsInChildren<Light>().Length+" colliders="+arena.GetComponentsInChildren<Collider>().Length+" lod="+arena.GetComponentsInChildren<LODGroup>().Length+" wallPlates="+skin.GetComponentsInChildren<WellWayfindingPlate>().Length+" texts="+texts.Length);
                for(int floor=0;floor<3;floor++)
                {
                    float y=floor*WellArenaCatalog.Storey;var eyes=new[]{new Vector3(0,y+1.7f,18),new Vector3(20,y+1.7f,0),new Vector3(floor==1?23:15.8f,y+1.7f,0),new Vector3(0,y+1.7f,-13.5f)};
                    var targets=new[]{new Vector3(0,y+2.65f,24.65f),new Vector3(24.65f,y+3.5f,0),new Vector3(floor==1?16.15f:9.72f,y+3.95f,0),new Vector3(0,y+5.75f,9.48f)};
                    var names=new[]{"lift","stair","gallery","atrium"};
                    for(int i=0;i<eyes.Length;i++)
                    {
                        camera.transform.position=eyes[i];camera.transform.LookAt(targets[i]);for(int warm=0;warm<3;warm++){camera.Render();yield return null;}
                        Capture(camera,names[i]+"-"+(floor+1)+"-"+tag+".png");
                    }
                }
                Object.Destroy(root);yield return null;yield return null;
            }
            File.WriteAllLines(Path.Combine(Output,"asset-metrics.txt"),records);
        }
    }
}
