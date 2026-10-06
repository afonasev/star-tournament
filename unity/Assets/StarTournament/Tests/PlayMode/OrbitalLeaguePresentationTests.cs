using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class OrbitalLeaguePresentationTests
    {
        GameObject root;
        [UnityTearDown] public IEnumerator Cleanup(){if(root)Object.Destroy(root);yield return null;}
        static bool HasRenderedFace(Transform solid,Vector3 point,Vector3 outward)
        {
            var mesh=solid.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=solid.TransformPoint(vertices[triangles[i]]);var b=solid.TransformPoint(vertices[triangles[i+1]]);var c=solid.TransformPoint(vertices[triangles[i+2]]);
                var normal=Vector3.Cross(b-a,c-a);
                if(Vector3.Dot(normal.normalized,outward)<.99f||Mathf.Abs(Vector3.Dot(point-a,outward))>.001f)continue;
                var v0=b-a;var v1=c-a;var v2=point-a;
                float d00=Vector3.Dot(v0,v0),d01=Vector3.Dot(v0,v1),d11=Vector3.Dot(v1,v1);
                float d20=Vector3.Dot(v2,v0),d21=Vector3.Dot(v2,v1),denominator=d00*d11-d01*d01;
                if(Mathf.Abs(denominator)<.000001f)continue;
                float v=(d11*d20-d01*d21)/denominator,w=(d00*d21-d01*d20)/denominator;
                if(v>=-.0001f&&w>=-.0001f&&v+w<=1.0001f)return true;
            }
            return false;
        }
        [UnityTest] public IEnumerator OverlappingFloorAndHallWallHaveOneVisibleFace()
        {
            root=new GameObject("surface-ownership");var arena=root.AddComponent<ProvingArena>();
            var frozen=CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault());var original=JsonUtility.ToJson(frozen.Definition);
            arena.Build(frozen,ProvingProfile.CreateDefault());yield return null;
            var floorPoint=new Vector3(0,0,18);
            Assert.That(HasRenderedFace(arena.transform.Find("foundation-north"),floorPoint,Vector3.up),Is.False);
            Assert.That(HasRenderedFace(arena.transform.Find("north-flank"),floorPoint,Vector3.up),Is.True);
            var wallPoint=new Vector3(-6,6,-3);
            Assert.That(HasRenderedFace(arena.transform.Find("shell-west--1"),wallPoint,Vector3.right),Is.False);
            Assert.That(HasRenderedFace(arena.transform.Find("hall-west-south"),wallPoint,Vector3.right),Is.True);
            var diagonal=arena.transform.Find("northwest-diagonal");
            Assert.That(diagonal.GetComponent<MeshFilter>().sharedMesh.bounds.max.y,Is.GreaterThan(.5f),"Diagonal render deck must win its coplanar overlap");
            Assert.That(diagonal.GetComponent<BoxCollider>().center,Is.EqualTo(Vector3.zero),"Renderer lift must not move collision");
            Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(original));
            Assert.That(arena.GetComponentsInChildren<Collider>().Length,Is.EqualTo(frozen.Definition.Solids.Length));
        }
        [UnityTest] public IEnumerator ArtPreservesAllCollisionAndRepeatOwnsItsResources()
        {
            root=new GameObject("orbital-test");var arena=root.AddComponent<ProvingArena>();
            var frozen=CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault());var before=JsonUtility.ToJson(frozen.Definition);
            arena.Build(frozen,ProvingProfile.CreateDefault());yield return null;
            foreach(var s in frozen.Definition.Solids)
            {
                var t=arena.transform.Find(s.Id);var collider=t.GetComponent<BoxCollider>();
                Assert.That(collider,Is.Not.Null,s.Id);Assert.That(t.position,Is.EqualTo(s.Position));Assert.That(t.localScale,Is.EqualTo(s.Size));Assert.That(Quaternion.Angle(t.rotation,s.Rotation),Is.LessThan(.01f));
                if(s.Id.Contains("roof")||s.Id.Contains("ceiling"))
                    Assert.That(t.GetComponent<Renderer>().shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),s.Id);
            }
            var art=arena.GetComponentInChildren<OrbitalLeaguePresentation>();
            Assert.That(art.ProfileIdentity,Is.EqualTo("orbital-league-ring-v1@10"));
            Assert.That(art.GetComponentsInChildren<Transform>().Count(t=>t.name=="Slow orbital emblem"),Is.EqualTo(5));
            Assert.That(art.GetComponentsInChildren<Transform>().Count(t=>t.name=="Enclosed slow fan"),Is.EqualTo(3));
            Assert.That(art.GetComponentsInChildren<Collider>(),Is.Empty);
            Assert.That(arena.GetComponentsInChildren<Collider>().Length,Is.EqualTo(frozen.Definition.Solids.Length));
            Assert.That(art.FixtureCount,Is.GreaterThan(16));
            Assert.That(art.FixtureLightCount,Is.GreaterThan(0));
            Assert.That(art.LocalLightCount,Is.EqualTo(17+art.FixtureLightCount));
            Assert.That(art.LocalLightCount,Is.GreaterThanOrEqualTo(art.FixtureCount));
            Assert.That(art.ShadowLightCount,Is.EqualTo(2));
            Assert.That(art.GetComponentsInChildren<Light>().Count(l=>l.shadows==LightShadows.Soft),Is.EqualTo(2));
            Assert.That(art.GetComponentsInChildren<Light>().All(l=>l.type==LightType.Spot),Is.True);
            Assert.That(art.GetComponentsInChildren<Light>().All(l=>Mathf.Approximately(l.spotAngle,70)),Is.True);
            Assert.That(art.GetComponentsInChildren<Light>().All(l=>l.transform.forward.y<-.1f),Is.True,"Every luminaire must illuminate the room below its visible panel");
            var fixtureLights=art.GetComponentsInChildren<Light>().Where(l=>l.name.StartsWith("League fixture light /")).ToArray();
            Assert.That(fixtureLights.All(l=>l.range>=6.5f && l.range<=12 && l.shadows==LightShadows.None),Is.True);
            int surfaces=(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer);
            foreach(var light in fixtureLights)
                Assert.That(arena.gameObject.scene.GetPhysicsScene().Raycast(light.transform.position,Vector3.down,
                    out _,light.range,surfaces,QueryTriggerInteraction.Ignore),Is.True,light.name+" at "+light.transform.position);
            int renderers=arena.GetComponentsInChildren<Renderer>().Length;
            arena.Build(frozen,ProvingProfile.CreateDefault());yield return null;
            Assert.That(arena.GetComponentsInChildren<OrbitalLeaguePresentation>().Length,Is.EqualTo(1));
            Assert.That(arena.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(renderers));
            Assert.That(arena.GetComponentsInChildren<Light>().Length,Is.EqualTo(art.LocalLightCount));
            Assert.That(JsonUtility.ToJson(frozen.Definition),Is.EqualTo(before));
            Object.Destroy(root);yield return null;
        }
        [UnityTest] public IEnumerator WindowsTransmitSeparateDistantSpaceAndRemainSolid()
        {
            root=new GameObject("window-contract");var arena=root.AddComponent<ProvingArena>();arena.Build(CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault()),ProvingProfile.CreateDefault());yield return null;
            var windows=arena.Definition.Solids.Where(s=>s.Surface=="window").ToArray();Assert.That(windows.Length,Is.EqualTo(20));
            foreach(var window in windows)
            {
                var pane=arena.transform.Find(window.Id);var material=pane.GetComponent<Renderer>().sharedMaterial;
                Assert.That(material.shader.name,Is.EqualTo("StarTournament/OrbitalGlass"));Assert.That(material.renderQueue,Is.EqualTo(3000));
                Assert.That(material.GetColor("_Color").a,Is.LessThan(.1f));Assert.That(material.HasProperty("_BaseMap"),Is.False,"Space texture must not live on glass");
                Assert.That(window.Size.y,Is.LessThan(3));Assert.That(Mathf.Max(window.Size.x,window.Size.z),Is.LessThan(10));
                var inward=window.Size.x>window.Size.z?Vector3.back*Mathf.Sign(window.Position.z):Vector3.left*Mathf.Sign(window.Position.x);
                Assert.That(Physics.Raycast(window.Position+inward*2,-inward,out var hit,4,1<<ProvingArena.WorldLayer),Is.True);Assert.That(hit.collider.transform,Is.EqualTo(pane));
            }
            var shell=arena.Definition.Solids.Single(s=>s.Id=="interior-shell-roof");
            foreach(bool alongX in new[]{false,true})foreach(int side in new[]{-1,1})
            {
                float alongExtent=(alongX?shell.Size.x:shell.Size.z)/2;
                float crossExtent=(alongX?shell.Size.z:shell.Size.x)/2;
                for(float along=-alongExtent+.1f;along<alongExtent;along+=1.1f)for(float y=.1f;y<8.4f;y+=.6f)
                {
                    var normal=alongX?Vector3.forward*side:Vector3.right*side;
                    var point=alongX?new Vector3(along,y,side*(crossExtent-.1f)):new Vector3(side*(crossExtent-.1f),y,along);
                    Assert.That(Physics.Raycast(point,normal,.6f,1<<ProvingArena.WorldLayer),Is.True,"Hull leak "+point);
                }
            }
            var art=arena.GetComponentInChildren<OrbitalLeaguePresentation>();var faces=art.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Distant space cube / ")).ToArray();
            Assert.That(faces.Length,Is.EqualTo(6));Assert.That(faces.Count(r=>r.name.EndsWith("planet")),Is.EqualTo(2));Assert.That(faces.Count(r=>r.name.EndsWith("Sun")),Is.EqualTo(1));
            Assert.That(faces.All(r=>r.bounds.center.magnitude>100),Is.True);Assert.That(art.GetComponentsInChildren<Collider>(),Is.Empty);
        }
        [UnityTest] public IEnumerator WayfindingUsesPlanSidesAndPanelsStayOnOpaqueWalls()
        {
            root=new GameObject("wayfinding-contract");var arena=root.AddComponent<ProvingArena>();
            arena.Build(CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault()),ProvingProfile.CreateDefault());yield return null;
            var art=arena.GetComponentInChildren<OrbitalLeaguePresentation>();var labels=art.GetComponentsInChildren<TextMesh>();
            foreach(var label in labels.Where(l=>l.text.Contains("NORTH")))
                Assert.That(label.color.r,Is.GreaterThan(label.color.b*3),"North remains red on both levels");
            foreach(var label in labels.Where(l=>l.text.Contains("SOUTH")))
                Assert.That(label.color.g,Is.GreaterThan(label.color.b*3),"South remains yellow on both levels");
            foreach(var label in labels.Where(l=>l.text.Contains("WEST") && !l.text.Contains("RISE") || l.text.Contains("EAST") && !l.text.Contains("RISE")))
                Assert.That(label.color.b,Is.GreaterThan(label.color.g*2),"Side navigation is violet");
            var center=labels.Single(l=>l.text.Contains("ARMOR"));
            Assert.That(center.color.b,Is.GreaterThan(center.color.r*3),"Central corridor keeps blue text");
            var p=ProvingProfile.CreateCombatBowlRingPresentationDefault();
            foreach(var title in new[]{"WEST RISE / 01","EAST RISE / 02"})
            {
                var rise=labels.Single(l=>l.text==title);
                Assert.That(Physics.Raycast(rise.transform.position,rise.transform.forward,out var support,.1f,1<<ProvingArena.WorldLayer),Is.True,title+" must face an opaque wall");
                Assert.That(support.collider.name,Is.EqualTo(title.StartsWith("WEST")?"west-rise-south":"east-rise-north"));
                Assert.That(rise.GetComponent<MeshRenderer>().bounds.size.x,Is.LessThan(p.Get("wayfinding.panelWidth")),title+" must fit its backing");
            }
            for(int i=0;i<16;i++)
            {
                string key="wayfinding.sign-"+i;
                var pos=new Vector3(p.Get(key+".x"),p.Get(key+".y"),p.Get(key+".z"));
                var outward=Quaternion.Euler(0,p.Get(key+".yaw"),0)*Vector3.forward;
                // The panel corners and centre must be backed by opaque geometry, not glazing or a portal.
                foreach(float x in new[]{-.5f,0,.5f})foreach(float y in new[]{-.5f,0,.5f})
                {
                    var point=pos+Vector3.Cross(Vector3.up,outward)*(x*p.Get("wayfinding.panelWidth"))+Vector3.up*(y*p.Get("wayfinding.panelHeight"));
                    Assert.That(Physics.Raycast(point+outward*.1f,-outward,out var hit,.2f,1<<ProvingArena.WorldLayer),Is.True,key);
                    Assert.That(hit.collider.name,Does.Not.StartWith("window"),key);
                }
            }
            Assert.That(art.GetComponentsInChildren<Collider>(),Is.Empty);
        }
        [Test] public void RingV3GainsWayfindingWithoutResettingDesignerValues()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateCombatBowlRingPresentationDefault()).Replace("\"version\":10","\"version\":3");
            var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("light.intensity",9);old.Set("ring.glassOpacity",.08f);old.Set("color.north.r",.8f);
            old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Version,Is.EqualTo(10));Assert.That(old.Get("light.intensity"),Is.EqualTo(9));
            Assert.That(old.Get("ring.glassOpacity"),Is.EqualTo(.08f));Assert.That(old.Get("color.north.r"),Is.EqualTo(.8f));
            Assert.That(old.Descriptors.Where(d=>d.Group=="wayfinding").All(d=>d.Validate(out _)),Is.True);
            Assert.That(old.Validate(),Is.Empty);
        }
        [Test] public void ArtProfileRejectsOutOfRangeAndPublishesCompleteMetadata()
        {
            var p=ProvingProfile.CreateOrbitalLeagueDefault();Assert.That(p.Validate(),Is.Empty);
            Assert.That(p.Descriptors.All(d=>d.Validate(out _)),Is.True);
            Assert.That(p.Descriptor("broadcast.orbitSpeed").Group,Is.EqualTo("broadcast"));
            Assert.That(p.Descriptor("light.fixtureContribution").Group,Is.EqualTo("lighting"));
            Assert.That(p.Descriptor("light.fixtureRange").Validate(out _),Is.True);
            Assert.That(p.Descriptor("light.fixtureSurfaceReach").Validate(out _),Is.True);
            p.Set("broadcast.orbitSpeed",1000);Assert.That(p.Validate(),Is.Not.Empty);
        }
        [Test] public void SerializedV2LightingUpgradesValuesAndRangeTogether()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateOrbitalLeagueDefault()).Replace("\"version\":10","\"version\":2");
            Assert.That(json,Does.Contain("\"version\":2"));
            var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("surface.floorValue",.18f);
            old.Set("light.intensity",1.4f);old.Descriptor("light.intensity").Maximum=4;
            old.Set("light.range",7);old.Set("light.emission",1.5f);
            old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Version,Is.EqualTo(10));
            Assert.That(old.Get("surface.floorValue"),Is.EqualTo(.28f));
            Assert.That(old.Get("light.intensity"),Is.EqualTo(16));
            Assert.That(old.Descriptor("light.intensity").Maximum,Is.EqualTo(20));
            Assert.That(old.Validate(),Is.Empty);
        }
        [Test] public void SerializedV3LightingKeepsOverridesAndUpgradesOldDefaults()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateOrbitalLeagueDefault()).Replace("\"version\":10","\"version\":3");
            var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("light.fill",.65f);old.Set("light.intensity",8);old.Set("light.studioKey",.65f);
            old.Set("light.range",10);old.Set("light.shadowStrength",.65f);old.Descriptor("light.shadowStrength").Maximum=.7f;
            old.Set("light.shadowNormalBias",.4f);old.Set("light.shadowBias",.05f);
            old.Set("light.emission",1.8f); // Designer override, not the former default.
            old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Version,Is.EqualTo(10));
            Assert.That(old.Get("light.fill"),Is.EqualTo(.25f));
            Assert.That(old.Get("light.intensity"),Is.EqualTo(16));
            Assert.That(old.Get("light.shadowStrength"),Is.EqualTo(1));
            Assert.That(old.Get("light.emission"),Is.EqualTo(1.8f));
            Assert.That(old.Descriptor("light.shadowStrength").Maximum,Is.EqualTo(1));
            Assert.That(old.Validate(),Is.Empty);
        }
        [Test] public void SerializedV4FloorKeepsDesignerOverrides()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateOrbitalLeagueDefault()).Replace("\"version\":10","\"version\":4");
            var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("surface.floorValue",.24f);
            old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Version,Is.EqualTo(10));
            Assert.That(old.Get("surface.floorValue"),Is.EqualTo(.24f));
            Assert.That(old.Validate(),Is.Empty);
        }
        [Test] public void SerializedRingV2GainsWindowsWithoutResettingLightingOrCustomFrame()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateCombatBowlRingPresentationDefault()).Replace("\"version\":10","\"version\":2");
            Assert.That(json,Does.Contain("\"version\":2"));var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("light.intensity",9);old.Set("ring.windowFrameWidth",.4f);old.Set("ring.spawn-mark-1.z",17);old.Set("ring.skyValue",.08f);
            old.EnsureOrbitalLeagueDescriptors();Assert.That(old.Version,Is.EqualTo(10));
            Assert.That(old.Get("light.intensity"),Is.EqualTo(9));Assert.That(old.Get("ring.windowFrameWidth"),Is.EqualTo(.18f));Assert.That(old.Get("ring.spawn-mark-1.z"),Is.EqualTo(14));Assert.That(old.Get("ring.skyValue"),Is.EqualTo(.16f));
            var custom=JsonUtility.FromJson<ProvingProfile>(json);custom.Set("ring.windowFrameWidth",.8f);custom.EnsureOrbitalLeagueDescriptors();
            Assert.That(custom.Get("ring.windowFrameWidth"),Is.EqualTo(.8f));Assert.That(custom.Validate(),Is.Empty);
        }
        [Test] public void SerializedRingV1UpgradesLightingAndPreservesRingOverrides()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateCombatBowlRingPresentationDefault()).Replace("\"version\":10","\"version\":1");
            var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("light.fill",.65f);old.Set("light.intensity",8f);
            old.Set("light.shadowStrength",.65f);old.Descriptor("light.shadowStrength").Maximum=.7f;
            old.Set("surface.floorValue",.18f);old.Set("ring.gratingDepth",.08f);
            old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Version,Is.EqualTo(10));
            Assert.That(old.Get("light.intensity"),Is.EqualTo(16f));
            Assert.That(old.Get("light.shadowStrength"),Is.EqualTo(1f));
            Assert.That(old.Get("surface.floorValue"),Is.EqualTo(.28f));
            Assert.That(old.Get("ring.gratingDepth"),Is.EqualTo(.08f));
            old.Set("light.intensity",9f);old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Get("light.intensity"),Is.EqualTo(9f));
            Assert.That(old.Validate(),Is.Empty);
        }
        [Test] public void SerializedRingV4AddsDepthWithoutOverwritingDesignerLighting()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateCombatBowlRingPresentationDefault()).Replace("\"version\":10","\"version\":4");
            var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("light.fill",.45f);old.Set("light.intensity",11f);old.Set("light.studioKey",.4f);
            old.Set("light.range",12f);old.Set("light.spotAngle",100f);
            old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Version,Is.EqualTo(10));
            Assert.That(old.Get("light.fill"),Is.EqualTo(.25f));
            Assert.That(old.Get("light.intensity"),Is.EqualTo(16f));
            Assert.That(old.Get("light.studioKey"),Is.EqualTo(.6f));
            Assert.That(old.Get("light.range"),Is.EqualTo(10f));
            Assert.That(old.Get("light.spotAngle"),Is.EqualTo(70f));
            Assert.That(old.Get("layout.fill-1.z"),Is.EqualTo(-5f));
            Assert.That(old.Get("layout.fill-6.y"),Is.EqualTo(3f));
            Assert.That(old.Get("light.outerAimInset"),Is.EqualTo(2f));
            Assert.That(old.Get("light.keyShadowStrength"),Is.EqualTo(.85f));
            Assert.That(old.Descriptor("light.keyShadowStrength").Validate(out _),Is.True);
            Assert.That(old.Validate(),Is.Empty);
            var custom=JsonUtility.FromJson<ProvingProfile>(json);
            custom.Set("light.intensity",9f);custom.EnsureOrbitalLeagueDescriptors();
            Assert.That(custom.Get("light.intensity"),Is.EqualTo(9f));
        }
    }
}
