using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class PlasmaSessionTests
    {
        Scene scene;GameObject owner;CharacterMotor[] motors;ProvingProfile move;NativeCombatSession session;
        void Setup(float speed=30,bool teams=false)
        {
            scene=SceneManager.CreateScene("plasma-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));owner=new GameObject("plasma owner");SceneManager.MoveGameObjectToScene(owner,scene);
            move=ProvingProfile.CreateDefault();var plasma=ProvingProfile.CreatePlasmaDefault();plasma.Set("plasma.speed",speed);
            var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);motors=new CharacterMotor[4];
            for(int i=0;i<4;i++){var go=new GameObject("p"+i);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(move,new Vector3(-8+i*3,0,0));}
            motors[1].Initialize(move,new Vector3(-8,0,6));NativeMatchState match=null;
            if(teams){var p=ProvingProfile.CreateMatchDefault();match=new NativeMatchState(new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}),NativeMatchConfiguration.Default(p),p,50);}
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault(),match,plasmaProfile:plasma);
            var s=session.Capture();s.Lives[0].PlasmaOwned=true;s.Lives[0].PlasmaAmmo=100;s.Lives[0].SelectedWeapon=WeaponId.Plasma;s.Lives[0].Ammo=100;session.Restore(s);
        }
        void Fire(){var a=new LocalAction[4];a[0].FireHeld=true;session.Tick(a,.02f);}
        void Ticks(int n){for(int i=0;i<n;i++)session.Tick(new LocalAction[4],.02f);}
        void Wall(){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(owner.transform);go.layer=ProvingArena.WorldLayer;go.transform.position=new Vector3(-8,1,3);go.transform.localScale=new Vector3(2,4,.01f);Physics.SyncTransforms();}
        [UnityTearDown]public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest]public IEnumerator SphereGrazesTargetAndWallEdgeWithoutSplash()
        {
            Setup();yield return null;motors[1].Initialize(move,new Vector3(-7.71f,0,6));Fire();Ticks(20);
            Assert.That(session.Life(1).Health,Is.EqualTo(76),"Visible sphere edge must hit where its center ray misses");
            session.ApplyDamage(1,1,100); // Clear the participant lane before checking an offset thin world obstacle.
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(owner.transform);wall.layer=ProvingArena.WorldLayer;
            wall.transform.position=new Vector3(-7.86f,1.6f,3);wall.transform.localScale=new Vector3(.1f,.2f,.01f);Physics.SyncTransforms();
            int contacts=0;session.PlasmaBoltHit+=c=>{contacts++;Assert.That(c.Contact.Contact,Is.EqualTo(PelletContact.World));};
            Fire();Ticks(8);Assert.That(contacts,Is.EqualTo(1),"Sphere must stop at a thin wall missed by center ray");Assert.That(session.PlasmaBolts,Is.Empty);
        }
        [UnityTest]public IEnumerator FiniteFlightDirectDamageAndRenderCannotChangeAuthority()
        {
            Setup();yield return null;int contacts=0;session.PlasmaBoltHit+=e=>{contacts++;Assert.That(e.AppliedDamage,Is.EqualTo(24));};
            using(var visual=new PlasmaPresentation(session,owner.transform))
            {
                Fire();Assert.That(session.Life(1).Health,Is.EqualTo(100));Assert.That(session.Life(0).PlasmaAmmo,Is.EqualTo(99));Ticks(1);
                Assert.That(session.PlasmaBolts.Single().Distance,Is.EqualTo(.6f).Within(.001));string before=JsonUtility.ToJson(session.Capture());visual.Render();Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(before));
                Ticks(9);visual.Render();Assert.That(session.Life(1).Health,Is.EqualTo(76));Assert.That(contacts,Is.EqualTo(1));Assert.That(session.PlasmaBolts,Is.Empty);Ticks(15);visual.Render();Assert.That(visual.ActiveCount,Is.Zero);
            }
        }
        [UnityTest]public IEnumerator DodgingAvoidsDamageAndFirstParticipantStopsTheOrb()
        {
            Setup(10);yield return null;Fire();Ticks(10);motors[1].Initialize(move,new Vector3(-4,0,6));motors[2].Initialize(move,new Vector3(-8,0,6));motors[3].Initialize(move,new Vector3(-8,0,8));Ticks(35);
            Assert.That(session.Life(1).Health,Is.EqualTo(100));Assert.That(session.Life(2).Health,Is.EqualTo(76));Assert.That(session.Life(3).Health,Is.EqualTo(100));
        }
        [UnityTest]public IEnumerator ThinWallHasNoSplashAndAllyReceivesSharedFriendlyDamage()
        {
            Setup(200,true);yield return null;Fire();Ticks(2);Assert.That(session.Life(1).Health,Is.EqualTo(88));Ticks(10);Wall();Fire();Ticks(2);
            Assert.That(session.Life(1).Health,Is.EqualTo(88));Assert.That(session.PlasmaBolts,Is.Empty);
        }
        [UnityTest]public IEnumerator RestorePauseOwnerDeathAndOldSnapshotRemainCompatible()
        {
            Setup(10);yield return null;Fire();Ticks(10);var saved=session.Capture();double time=session.Time;
            yield return new WaitForSecondsRealtime(.05f);Assert.That(session.Time,Is.EqualTo(time));
            session.ApplyDamage(0,1,100);Ticks(25);Assert.That(session.Life(1).Health,Is.EqualTo(76));session.Restore(JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(saved)));Ticks(25);Assert.That(session.Life(1).Health,Is.EqualTo(76));
            var old=session.Capture();old.Version=9;old.PlasmaBolts=null;session.Restore(old);Assert.That(session.PlasmaBolts,Is.Empty);
        }
        [UnityTest]public IEnumerator PresentationFreezesWithSessionAndClearsEveryFilamentOnRestore()
        {
            Setup();yield return null;
            using(var visual=new PlasmaPresentation(session,owner.transform,seat=>new Vector3(-8,1.4f,0)))
            {
                session.ShotResolved+=shot=>{if(shot.Weapon==WeaponId.Plasma)visual.Launch(shot.Sequence,shot.Shooter);};
                Fire();visual.Render();Assert.That(visual.ActiveCount,Is.GreaterThan(1),"Flight and muzzle corona both exist");
                var lines=owner.GetComponentsInChildren<LineRenderer>();
                var before=lines.SelectMany(line=>Enumerable.Range(0,line.positionCount).Select(line.GetPosition)).ToArray();
                string state=JsonUtility.ToJson(session.Capture());yield return new WaitForSecondsRealtime(.05f);visual.Render();
                Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(state));
                CollectionAssert.AreEqual(before,lines.SelectMany(line=>Enumerable.Range(0,line.positionCount).Select(line.GetPosition)).ToArray());
                session.Restore(session.Capture());Assert.That(visual.ActiveCount,Is.Zero);
                Assert.That(owner.GetComponentsInChildren<LineRenderer>().Length,Is.Zero,"Restore hides all pooled effect renderers");
                visual.Render();Assert.That(visual.ActiveCount,Is.EqualTo(session.PlasmaBolts.Length),"Restore rebuilds flights, never old muzzle flashes");
            }
        }
        [UnityTest]public IEnumerator InvalidBoltSetIsRejectedBeforeStateMutation()
        {
            Setup();yield return null;Fire();var saved=session.Capture();string json=JsonUtility.ToJson(saved);saved.PlasmaBolts=new[]{saved.PlasmaBolts[0],saved.PlasmaBolts[0]};
            Assert.Throws<ArgumentException>(()=>session.Restore(saved));Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(json));
        }
    }
}
