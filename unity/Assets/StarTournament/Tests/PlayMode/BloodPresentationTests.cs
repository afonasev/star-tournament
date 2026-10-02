using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class BloodPresentationTests
    {
        Scene scene;GameObject root;ProvingArena arena;NativeCombatSession session;ProvingProfile tuning;BloodPresentation blood;
        void Setup()
        {
            scene=SceneManager.CreateScene("blood-test-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            root=new GameObject("blood-test");SceneManager.MoveGameObjectToScene(root,scene);
            var movement=ProvingProfile.CreateDefault();var go=new GameObject("arena");go.transform.SetParent(root.transform);arena=go.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),movement);
            var motors=new CharacterMotor[2];for(int i=0;i<2;i++){go=new GameObject("player-"+i);go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(movement,new Vector3(0,AuthoredPhysicsFixture.Value("fixture.floorHeight"),-3+i*6));}
            var combat=ProvingProfile.CreateNativeCombatDefault();combat.Set("shot.spread",0);session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),movement,ProvingProfile.CreateCombatDefault(),combat);EquippedCombatFixture.Equip(session);
            tuning=ProvingProfile.CreateBloodDefault();blood=new BloodPresentation(session,tuning,root.transform,scene.GetPhysicsScene());
        }
        [UnityTearDown] public IEnumerator Cleanup(){blood?.Dispose();if(root)Object.Destroy(root);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        DamageNotice Notice(float health=1,float armor=0,WeaponId weapon=WeaponId.Rifle)=>new DamageNotice(1,session.Life(1).Life,health,armor,session.Time,new FatalImpact(weapon,1,Vector3.forward,new Vector3(0,.9f,0)));
        [UnityTest] public IEnumerator ActualRifleDamageUsesAuthoritativeImpactAndArmorOnlyHitDoesNotEmit()
        {
            Setup();yield return null;var saved=session.Capture();saved.Lives[1].Armor=100;session.Restore(saved);
            DamageNotice received=default;session.Damaged+=n=>received=n;
            var actions=new LocalAction[2];actions[0].Fire=true;session.Tick(actions,.02f);for(int i=0;i<20;i++){session.Tick(new LocalAction[2],.02f);blood.Render();}
            Assert.That(received.Impact.Valid,Is.True);Assert.That(received.Impact.Weapon,Is.EqualTo(WeaponId.Rifle));Assert.That(received.HealthLost,Is.Zero);Assert.That(blood.Bursts,Is.Zero);
            saved=session.Capture();saved.Lives[1].Armor=0;session.Restore(saved);received=default;int shots=session.ShotCount;actions[0].Fire=true;session.Tick(actions,.02f);Assert.That(session.ShotCount,Is.EqualTo(shots+1));for(int i=0;i<20;i++){session.Tick(new LocalAction[2],.02f);blood.Render();}
            Assert.That(received.HealthLost,Is.GreaterThan(0));Assert.That(blood.Bursts,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator CapsFreezeRestoreAndExpiryArePresentationOnly()
        {
            Setup();yield return null;
            string snapshot=JsonUtility.ToJson(session.Capture());blood.Observe(Notice(0,10));Assert.That(blood.ActiveDrops,Is.Zero);
            for(int i=0;i<30;i++)blood.Observe(Notice());Assert.That(blood.ActiveDrops,Is.EqualTo((int)tuning.Get("blood.maxDrops")));
            Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(snapshot));blood.Render();int count=blood.ActiveDrops;yield return new WaitForSecondsRealtime(.1f);blood.Render();Assert.That(blood.ActiveDrops,Is.EqualTo(count));
            session.Restore(session.Capture());Assert.That(blood.ActiveDrops+blood.ActiveMarks,Is.Zero);
            blood.Observe(Notice(1,0,WeaponId.Cutter));blood.Observe(Notice(1,0,WeaponId.Cutter));Assert.That(blood.ActiveDrops,Is.EqualTo((int)tuning.Get("blood.drops")));
            for(int i=0;i<100;i++){session.Tick(new LocalAction[2],.02f);blood.Render();}Assert.That(blood.ActiveDrops,Is.Zero);
            Assert.That(root.GetComponentsInChildren<Collider>().Where(c=>c.name.StartsWith("blood-")).Count(),Is.Zero);
            session.Restore(session.Capture());Assert.That(blood.ActiveMarks,Is.Zero);
        }
        [UnityTest] public IEnumerator SurfaceMeshStaysOnActualWallAndClipsAtItsEdge()
        {
            Setup();var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform);go.layer=ProvingArena.WorldLayer;go.transform.position=new Vector3(40,1,0);go.transform.localScale=new Vector3(4,4,.1f);yield return null;Physics.SyncTransforms();
            Assert.That(scene.GetPhysicsScene().Raycast(new Vector3(41.99f,1,-1),Vector3.forward,out var hit,2,1<<ProvingArena.WorldLayer),Is.True);
            var mesh=BloodPresentation.BuildSurfaceMark(hit,.3f,27,Vector3.right);Assert.That(mesh,Is.Not.Null);Assert.That(mesh.vertices.All(v=>v.x<=42.0001f&&v.x>=37.9999f),Is.True);
            Assert.That(mesh.vertices.All(v=>Mathf.Abs(v.z-(-.052f))<.001f),Is.True);Assert.That(scene.GetPhysicsScene().Raycast(new Vector3(40,1,-1),Vector3.forward,out var centre,2,1<<ProvingArena.WorldLayer),Is.True);
            var full=BloodPresentation.BuildSurfaceMark(centre,.3f,27,Vector3.right);Assert.That(mesh.triangles.Length,Is.LessThan(full.triangles.Length));Object.Destroy(mesh);Object.Destroy(full);
        }
        [UnityTest] public IEnumerator MarksHaveVariedSilhouettesAndTailsFollowSurfaceVelocity()
        {
            Setup();var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform);go.layer=ProvingArena.WorldLayer;go.transform.position=new Vector3(40,1,0);go.transform.localScale=new Vector3(4,4,.1f);yield return null;Physics.SyncTransforms();
            Assert.That(scene.GetPhysicsScene().Raycast(new Vector3(40,1,-1),Vector3.forward,out var hit,2,1<<ProvingArena.WorldLayer),Is.True);
            var shapes=new System.Collections.Generic.HashSet<string>();var topology=new System.Collections.Generic.HashSet<int>();
            for(uint seed=1;seed<=12;seed++)
            {
                var mesh=BloodPresentation.BuildSurfaceMark(hit,.3f,seed,Vector3.right,.45f);Assert.That(mesh,Is.Not.Null);
                Assert.That(mesh.vertices.All(v=>Mathf.Abs(v.z+.052f)<.001f),Is.True);
                Assert.That(mesh.bounds.max.x-hit.point.x,Is.GreaterThan(hit.point.x-mesh.bounds.min.x));
                shapes.Add(string.Join(";",mesh.vertices.Select(v=>v.ToString("F4"))));topology.Add(mesh.triangles.Length);Object.Destroy(mesh);
            }
            Assert.That(shapes.Count,Is.EqualTo(12),"Seed varies the silhouette, not just stamp orientation");Assert.That(topology.Count,Is.GreaterThan(1),"Different tail/satellite patterns");
        }
    }
}
