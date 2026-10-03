using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeBotObservationTests
    {
        Scene scene; GameObject root; CharacterMotor[] motors;
        NativeCombatSession session; NativeBotObservationProvider provider; NativeBotPerception knowledge;
        ProvingProfile movement;
        void Setup(bool teams=false)
        {
            scene=SceneManager.CreateScene("perception-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            root=new GameObject("perception-test");SceneManager.MoveGameObjectToScene(root,scene);
            movement=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
            var arenaRoot=new GameObject("arena");arenaRoot.transform.SetParent(root.transform);var arena=arenaRoot.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),movement);
            motors=new CharacterMotor[3];
            for(int i=0;i<3;i++){var go=new GameObject("participant-"+i);go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(movement,new Vector3(-8+i*3,0,2));}
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB}):NativeMatchRoster.Ffa(3);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),movement,ProvingProfile.CreateCombatDefault(),combat);
            knowledge=new NativeBotPerception(roster,Enumerable.Repeat(NativeBotDifficulty.Easy,3).ToArray(),ProvingProfile.CreateBotPerceptionDefault());
            provider=new NativeBotObservationProvider(session,roster,scene.GetPhysicsScene(),movement,combat);
        }
        void Place(int i,Vector3 feet,float yaw=0){motors[i].Initialize(movement,feet);motors[i].Tick(new LocalAction{LookDegrees=new Vector2(yaw,0)},.02f);Physics.SyncTransforms();}
        void Sample(double t)=>knowledge.Sample(t,provider.Observe(knowledge));
        NativeBotMemoryEntry Enemy(int observer,int target)=>knowledge.Read(observer).Enemies.Single(e=>e.Sighting.Participant==target);
        GameObject Block(Vector3 position,Vector3 scale,int layer)
        {
            var go=new GameObject("test-occluder");go.transform.SetParent(root.transform);go.layer=layer;go.transform.position=position;go.AddComponent<BoxCollider>().size=scale;Physics.SyncTransforms();return go;
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(root)Object.Destroy(root);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}

        [UnityTest] public IEnumerator ArmoredGlassSeparatesSightFromShotAndKeepsTracking()
        {
            Setup();yield return null;Place(0,new Vector3(-8,0,2));Place(2,new Vector3(-8,0,6));
            var glass=Block(new Vector3(-8,1.5f,4),new Vector3(4,3,.12f),ProvingArena.WorldLayer);glass.AddComponent<NativeSightTransparent>();
            Sample(0);Assert.That(Enemy(0,2).Visible,Is.True);Assert.That(Enemy(0,2).Sighting.ShotBlocked,Is.True);
            Place(2,new Vector3(-7,0,6));Sample(.1);Assert.That(Enemy(0,2).Sighting.Position.x,Is.EqualTo(-7));
            var saved=JsonUtility.FromJson<NativeBotPerceptionSnapshot>(JsonUtility.ToJson(knowledge.Capture()));
            Assert.That(saved.Observers[0].Enemies.Single(e=>e.Sighting.Participant==2).Sighting.ShotBlocked,Is.True);
            glass.GetComponent<BoxCollider>().isTrigger=true;Physics.SyncTransforms();Sample(.2);Assert.That(Enemy(0,2).Sighting.ShotBlocked,Is.False);
            glass.GetComponent<BoxCollider>().isTrigger=false;glass.layer=ProvingArena.MovementOnlyLayer;Physics.SyncTransforms();Sample(.3);Assert.That(Enemy(0,2).Sighting.ShotBlocked,Is.False);
            Object.Destroy(glass);yield return null;Sample(.4);Assert.That(Enemy(0,2).Visible,Is.True);Assert.That(Enemy(0,2).Sighting.ShotBlocked,Is.False);
        }

        [UnityTest] public IEnumerator ActualWallHiddenMovementDeathAndExpiryDoNotLeak()
        {
            Setup();yield return null;Place(0,new Vector3(-8,0,2));Place(2,new Vector3(-8,0,6));Sample(0);
            var first=Enemy(0,2);Assert.That(first.Visible,Is.True);
            Block(new Vector3(-8,2,4),new Vector3(4,4,.3f),ProvingArena.WorldLayer);
            Place(2,new Vector3(-7,0,7));Sample(.1);Assert.That(Enemy(0,2).Sighting.Position,Is.EqualTo(first.Sighting.Position));Assert.That(Enemy(0,2).Visible,Is.False);
            session.ApplyDamage(2,session.Life(2).Life,500);Sample(.2);Assert.That(Enemy(0,2).Sighting.Life,Is.EqualTo(first.Sighting.Life));
            // Hidden physical respawn cannot reveal its new life or position to knowledge.
            for(int tick=0;tick<500 && session.Life(2).Dead;tick++)session.Tick(new LocalAction[3],.02f);
            Assert.That(session.Life(2).Dead,Is.False);Place(2,new Vector3(-7,0,7));Sample(.3);Assert.That(Enemy(0,2).Sighting.Life,Is.EqualTo(first.Sighting.Life));
            Sample(2);Assert.That(knowledge.Read(0).Enemies.Any(e=>e.Sighting.Participant==2),Is.False);
        }
        [UnityTest] public IEnumerator HorizontalFovReacquisitionAndWorldLayerOcclusion()
        {
            Setup();yield return null;Place(0,new Vector3(-8,0,2));Place(2,new Vector3(-8,0,6));
            var blocker=Block(new Vector3(-8,1,4),new Vector3(2,2,.3f),ProvingArena.MovementOnlyLayer);Sample(0);Assert.That(Enemy(0,2).Visible,Is.True);
            blocker.layer=ProvingArena.WorldLayer;Physics.SyncTransforms();Sample(.1);Assert.That(Enemy(0,2).Visible,Is.False);
            Object.Destroy(blocker);yield return null;Place(0,new Vector3(-8,0,2),180);Sample(.2);Assert.That(Enemy(0,2).Visible,Is.False);
            Place(0,new Vector3(-8,0,2));Sample(.3);Assert.That(Enemy(0,2).Visible,Is.True);Assert.That(Enemy(0,2).ObservedAt,Is.EqualTo(.3));
            Place(0,new Vector3(0,0,3));Place(2,new Vector3(0,AuthoredPhysicsFixture.Value("fixture.upperFloorHeight"),5));Sample(.4);
            Assert.That(Enemy(0,2).Visible,Is.False,"Actual fixture upper slab must occlude the other floor");
        }
        [UnityTest] public IEnumerator DifficultyFovBoundariesUseHorizontalAngle()
        {
            Setup();yield return null;
            var origin=new Vector3(-8,0,2);Place(0,origin);
            foreach(float angle in new[]{60f,70f})
            {
                Place(2,origin+Quaternion.Euler(0,angle,0)*Vector3.forward*3);
                foreach(NativeBotDifficulty difficulty in Enum.GetValues(typeof(NativeBotDifficulty)))
                {
                    knowledge=new NativeBotPerception(NativeMatchRoster.Ffa(3),Enumerable.Repeat(difficulty,3).ToArray(),ProvingProfile.CreateBotPerceptionDefault());
                    // Vertical looking direction does not narrow the established horizontal sensor FOV.
                    motors[0].Tick(new LocalAction{LookDegrees=new Vector2(0,50)},.02f);Physics.SyncTransforms();Sample(0);
                    bool expected=difficulty==NativeBotDifficulty.Hard || (angle==60 && difficulty==NativeBotDifficulty.Normal);
                    Assert.That(knowledge.Read(0).Enemies.Any(e=>e.Sighting.Participant==2),Is.EqualTo(expected),difficulty+" at "+angle);
                }
            }
        }
        [UnityTest] public IEnumerator TeamReportComesFromObservedPoseWhileReceiverLooksAway()
        {
            Setup(true);yield return null;Place(0,new Vector3(-8,0,2));Place(1,new Vector3(-5,0,2),180);Place(2,new Vector3(-8,0,6));Sample(0);
            Assert.That(knowledge.Read(0).Enemies.Select(e=>e.Sighting.Participant),Is.EqualTo(new[]{2}));Assert.That(knowledge.Read(1).Enemies,Is.Empty);
            var observed=Enemy(0,2).Sighting.Position;Place(2,new Vector3(-7,0,7));Sample(.2);Assert.That(knowledge.Read(1).Enemies,Is.Empty);
            Sample(.31);var reported=Enemy(1,2);Assert.That(reported.Visible,Is.False);Assert.That(reported.Sighting.Position,Is.EqualTo(observed));Assert.That(reported.ObservedAt,Is.Zero);
        }
    }
}
