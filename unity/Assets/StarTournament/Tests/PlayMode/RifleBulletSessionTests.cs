using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class RifleBulletSessionTests
    {
        Scene scene;GameObject owner;ProvingArena arena;CharacterMotor[] motors;
        ProvingProfile move,combat,life;NativeCombatSession session;
        void Setup(float speed=100,bool teams=false)
        {
            scene=SceneManager.CreateScene("rifle-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            owner=new GameObject("rifle owner");SceneManager.MoveGameObjectToScene(owner,scene);
            move=ProvingProfile.CreateDefault();combat=ProvingProfile.CreateNativeCombatDefault();life=ProvingProfile.CreateCombatDefault();
            combat.Set("rifle.spread",0);combat.Set("rifle.speed",speed);
            combat.Set("rifle.damage",20);
            arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
            motors=new CharacterMotor[4];
            for(int i=0;i<4;i++){var go=new GameObject("p"+i);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(move,new Vector3(-8+i*3,0,0));}
            motors[1].Initialize(move,new Vector3(-8,0,6));
            NativeMatchState match=null;
            if(teams){var p=ProvingProfile.CreateMatchDefault();match=new NativeMatchState(new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}),NativeMatchConfiguration.Default(p),p,50);}
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,match);
        }
        void Fire(){var a=new LocalAction[4];a[0].Fire=true;session.Tick(a,.02f);}
        void Ticks(int n){for(int i=0;i<n;i++)session.Tick(new LocalAction[4],.02f);}
        GameObject Wall(Vector3 center,Vector3 size)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(owner.transform);go.layer=ProvingArena.WorldLayer;go.transform.position=center;go.transform.localScale=size;Physics.SyncTransforms();return go;}
        [UnityTearDown]public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest]public IEnumerator FirstVisibleFlightUsesStrafedMuzzleThenFreezesLaunchOffset()
        {
            foreach(int side in new[]{-1,1})
            foreach(int firstTicks in new[]{1,4})
            {
                Setup(speed:50);yield return null;
                var muzzle=new Vector3(-7.8f,1.5f,.4f);
                using(var visual=new RifleBulletPresentation(session,move,owner.transform,_=>muzzle))
                {
                    session.ShotResolved+=shot=>visual.Launch(shot.Sequence,shot.Shooter);
                    Fire();visual.Render();
                    var line=owner.GetComponentsInChildren<LineRenderer>().Single();
                    Assert.That(Vector3.Distance(line.GetPosition(0),line.GetPosition(1)),Is.EqualTo(0));
                    muzzle+=Vector3.right*(side*.25f);Ticks(firstTicks);visual.Render();
                    var bullet=session.RifleBullets.Single();
                    float length=move.Get("presentation.rifleTracerLength");
                    float tail=Mathf.Min(bullet.Distance,Mathf.Max(move.Get("presentation.shotMuzzleClearance"),bullet.Distance-length));
                    var launch=line.GetPosition(1)-(line.GetPosition(0)-line.GetPosition(1))*tail/(bullet.Distance-tail);
                    Assert.That(Vector3.Distance(launch,muzzle),Is.LessThan(.0001f),"First visible line missed the strafed muzzle after "+firstTicks+" ticks");
                    float blendDistance=Mathf.Max(length,bullet.Distance);
                    var offset=muzzle-(bullet.Position-bullet.Direction*bullet.Distance);
                    muzzle+=Vector3.right*(side*.25f);Ticks(1);visual.Render();bullet=session.RifleBullets.Single();
                    var expected=bullet.Position+offset*(1-Mathf.Clamp01(bullet.Distance/blendDistance));
                    Assert.That(Vector3.Distance(line.GetPosition(0),expected),Is.LessThan(.0001f),"Already visible flight followed a later muzzle");
                }
                yield return SceneManager.UnloadSceneAsync(scene);scene=default;
            }
        }
        [UnityTest]public IEnumerator LaunchDoesNoDamageThenContactAndVisualAreOnceAndSynchronous()
        {
            Setup();yield return null;int launch=0,contacts=0;double damageTime=-1,contactTime=-2;
            session.ShotResolved+=s=>{launch++;Assert.That(s.Pellets,Is.Empty);};
            session.Damaged+=d=>damageTime=d.Time;
            session.RifleBulletHit+=e=>{contacts++;contactTime=e.Time;Assert.That(e.AppliedDamage,Is.EqualTo(20));};
            using(var visual=new RifleBulletPresentation(session,move,owner.transform))
            {
                Fire();Assert.That(launch,Is.EqualTo(1));Assert.That(session.Life(0).RifleAmmo,Is.EqualTo(199));Assert.That(session.Life(1).Health,Is.EqualTo(100));
                Ticks(1);string before=JsonUtility.ToJson(session.Capture());visual.Render();Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(before));
                var trace=owner.GetComponentsInChildren<LineRenderer>().Single();Assert.That(trace.GetPosition(0).z,Is.EqualTo(2).Within(.001));Assert.That(trace.GetComponents<Collider>(),Is.Empty);
                Ticks(1);Assert.That(session.Life(1).Health,Is.EqualTo(100));Ticks(1);visual.Render();
                Assert.That(session.Life(1).Health,Is.EqualTo(80));Assert.That(contactTime,Is.EqualTo(damageTime));Assert.That(contacts,Is.EqualTo(1));
                Assert.That(session.RifleBullets,Is.Empty);Assert.That(owner.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("Rifle contact ")),Is.True);
                Ticks(10);visual.Render();Assert.That(contacts,Is.EqualTo(1));
            }
        }
        [UnityTest]public IEnumerator TargetCanLeaveAndAnotherTargetCanEnterFlightPath()
        {
            Setup(10);yield return null;Fire();Ticks(10);motors[1].Initialize(move,new Vector3(-4,0,6));
            motors[2].Initialize(move,new Vector3(-8,0,6));Ticks(25);
            Assert.That(session.Life(1).Health,Is.EqualTo(100));Assert.That(session.Life(2).Health,Is.EqualTo(80));
        }
        [UnityTest]public IEnumerator ThinWallAndAllyStopHighSpeedBulletWithHalfFriendlyDamage()
        {
            Setup(1000,true);yield return null;motors[2].Initialize(move,new Vector3(-8,0,8));
            int contacts=0;session.RifleBulletHit+=e=>{contacts++;Assert.That(e.AppliedDamage,Is.EqualTo(contacts==1?10:0));};
            Fire();Ticks(1);Assert.That(contacts,Is.EqualTo(1));Assert.That(session.Life(1).Health,Is.EqualTo(90));Assert.That(session.Life(2).Health,Is.EqualTo(100));
            Ticks(10);Wall(new Vector3(-8,1,3),new Vector3(2,4,.01f));Fire();Ticks(1);
            Assert.That(contacts,Is.EqualTo(2));Assert.That(session.RifleBullets,Is.Empty);Assert.That(session.Life(2).Health,Is.EqualTo(100));
        }
        [UnityTest]public IEnumerator NoDistanceOrLifetimeCutoffAndCanHitBeyondOldRange()
        {
            Setup();yield return null;
            foreach(var collider in owner.GetComponentsInChildren<Collider>())if(collider.gameObject.layer==ProvingArena.WorldLayer)collider.enabled=false;
            Wall(new Vector3(0,-.5f,0),new Vector3(500,1,500));motors[1].Initialize(move,new Vector3(-8,0,100));
            Fire();Ticks(45);Assert.That(session.RifleBullets.Single().Distance,Is.GreaterThan(80));Assert.That(session.Life(1).Health,Is.EqualTo(100));
            Ticks(5);Assert.That(session.Life(1).Health,Is.EqualTo(80));
            motors[1].Initialize(move,new Vector3(-4,0,100));Ticks(10);Fire();Ticks(200);
            Assert.That(session.RifleBullets.Single().Distance,Is.GreaterThan(350));
        }
        [UnityTest]public IEnumerator PauseRestoreOwnerDeathAndCallbackSnapshotCannotDuplicateDamage()
        {
            Setup(10);yield return null;Fire();Ticks(10);var saved=session.Capture();
            double time=session.Time;Vector3 position=session.RifleBullets.Single().Position;
            yield return new WaitForSecondsRealtime(.05f);Assert.That(session.Time,Is.EqualTo(time));Assert.That(session.RifleBullets.Single().Position,Is.EqualTo(position));
            NativeCombatSessionSnapshot callback=null;session.Damaged+=d=>{if(d.Participant==1)callback=session.Capture();};
            session.ApplyDamage(0,1,100);Ticks(25);Assert.That(session.Life(1).Health,Is.EqualTo(80));Assert.That(callback.RifleBullets,Is.Empty);
            session.Restore(callback);Ticks(10);Assert.That(session.Life(1).Health,Is.EqualTo(80));
            session.Restore(JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(saved)));Ticks(25);Assert.That(session.Life(1).Health,Is.EqualTo(80));
            var repeated=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);Assert.That(repeated.RifleBullets,Is.Empty);
        }
        [UnityTest]public IEnumerator InvalidBulletSnapshotsAreRejectedBeforeMutation()
        {
            Setup();yield return null;Fire();var saved=session.Capture();
            var invalid=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(saved));invalid.RifleBullets=new[]{saved.RifleBullets[0],saved.RifleBullets[0]};
            Assert.Throws<ArgumentException>(()=>session.Restore(invalid));
            invalid=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(saved));invalid.RifleBullets[0].Direction=Vector3.zero;
            Assert.Throws<ArgumentException>(()=>session.Restore(invalid));invalid.Version=6;Assert.Throws<ArgumentException>(()=>session.Restore(invalid));
            Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(JsonUtility.ToJson(saved)));
        }
    }
}
