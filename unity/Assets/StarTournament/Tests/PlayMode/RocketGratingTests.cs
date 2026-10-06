using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class RocketGratingTests
    {
        Scene scene;GameObject root;ProvingArena arena;CharacterMotor[] motors;
        ProvingProfile movement,combat;NativeCombatSession session;
        [UnitySetUp] public IEnumerator Setup()
        {
            scene=SceneManager.CreateScene("rocket-grating-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            root=new GameObject("rocket grating contract");SceneManager.MoveGameObjectToScene(root,scene);
            movement=ProvingProfile.CreateDefault();combat=ProvingProfile.CreateNativeCombatDefault();
            arena=root.AddComponent<ProvingArena>();arena.Build(CombatBowlCatalog.Freeze(movement),movement);
            motors=new CharacterMotor[2];
            for(int i=0;i<2;i++)
            {var go=new GameObject("participant "+i);go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(movement,new Vector3(-32+i*64,4,-28));}
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),movement,ProvingProfile.CreateCombatDefault(),combat);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        Vector3 Gap(ArenaSolid solid)
        {
            var art=ProvingProfile.CreateCombatBowlRingPresentationDefault();
            // Midpoint between presentation bars in both axes, inside the full canonical plane.
            float pitch=art.Get("ring.gratingPitch"),bar=art.Get("ring.gratingBar");
            // The middle gap avoids neighboring solid corner supports/walls.
            float Offset(float size)=>-size/2+bar/2+(Mathf.Floor((size/2-bar/2)/pitch)+.5f)*pitch;
            return solid.Position+new Vector3(Offset(solid.Size.x),0,Offset(solid.Size.z));
        }
        void Inject(Vector3 origin,Vector3 direction)
        {
            var snapshot=session.Capture();snapshot.ShotSequence++;
            snapshot.Rockets=new[]{new RocketState{Id=snapshot.ShotSequence,Owner=0,OwnerLife=session.Life(0).Life,Position=origin,Direction=direction,DamageMultiplier=1}};
            session.Restore(snapshot);
        }
        [UnityTest] public IEnumerator EveryBalconyAndBridgeGapStopsBothSidesWithinOneTickExactlyOnce()
        {
            int explosions=0;RocketExplosion last=default;session.RocketExploded+=e=>{explosions++;last=e;Assert.That(session.Rockets,Is.Empty,"Removed before callback");};
            var grates=arena.Definition.Solids.Where(s=>s.Surface=="grating").ToArray();Assert.That(grates.Length,Is.EqualTo(6));
            foreach(var grate in grates)foreach(int side in new[]{-1,1})
            {
                var gap=Gap(grate);var origin=gap+Vector3.up*side*(grate.Size.y/2+.1f);var direction=Vector3.down*side;
                Assert.That(scene.GetPhysicsScene().Raycast(origin,direction,out _,grate.Size.y+.2f,1<<ProvingArena.WorldLayer),Is.False,"Hitscan/Cutter/LOS/splash mask stays transparent: "+grate.Id);
                int before=explosions;Inject(origin,direction);session.Tick(new LocalAction[2],.02f);
                Assert.That(explosions,Is.EqualTo(before+1),grate.Id+" side "+side);Assert.That(last.DirectSeat,Is.EqualTo(-1));
                Assert.That(last.Position.y,Is.EqualTo(grate.Position.y+side*(grate.Size.y/2+movement.Get("player.capsule.skinWidth"))).Within(.001f));
                session.Tick(new LocalAction[2],.02f);Assert.That(explosions,Is.EqualTo(before+1));Assert.That(session.Rockets,Is.Empty);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator WorldAndParticipantBeforeGrateWinAndOtherMovementOnlySurfacePasses()
        {
            var grate=arena.Definition.Solids.Single(s=>s.Id=="bridge-north");var gap=Gap(grate);var direction=Vector3.up;
            var barrier=GameObject.CreatePrimitive(PrimitiveType.Cube);barrier.transform.SetParent(root.transform);barrier.transform.position=gap-Vector3.up*.7f;barrier.transform.localScale=new Vector3(.5f,.05f,.5f);barrier.layer=ProvingArena.MovementOnlyLayer;Physics.SyncTransforms();
            Assert.That(arena.RaycastRocketGrating(gap-Vector3.up, direction,2,out var hit),Is.True);Assert.That(hit.collider.name,Is.EqualTo(grate.Id));
            // A movement-only barrier outside the grating footprint is not a rocket surface.
            barrier.transform.position=new Vector3(10,2,12);Physics.SyncTransforms();Inject(new Vector3(10,1.5f,12),direction);
            int count=0;RocketExplosion last=default;session.RocketExploded+=e=>{count++;last=e;};session.Tick(new LocalAction[2],.02f);
            Assert.That(count,Is.Zero);Assert.That(session.Rockets,Has.Length.EqualTo(1));
            barrier.transform.position=gap-Vector3.up*.7f;barrier.layer=ProvingArena.WorldLayer;Physics.SyncTransforms();
            Inject(gap-Vector3.up,direction);session.Tick(new LocalAction[2],.02f);Assert.That(count,Is.EqualTo(1));Assert.That(last.Position.y,Is.LessThan(grate.Position.y-.6f));
            UnityEngine.Object.DestroyImmediate(barrier);
            motors[1].Initialize(movement,new Vector3(gap.x,0,gap.z));Physics.SyncTransforms();Inject(gap-Vector3.up*3,direction);session.Tick(new LocalAction[2],.02f);
            Assert.That(count,Is.EqualTo(2));Assert.That(last.DirectSeat,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator SplashCrossesGrateAndMotorKeepsSupport()
        {
            var grate=arena.Definition.Solids.Single(s=>s.Id=="balcony-west");var point=new Vector3(-25,grate.Position.y,0);
            motors[1].Initialize(movement,new Vector3(point.x,0,point.z));Inject(point+Vector3.up*(grate.Size.y/2+.1f),Vector3.down);
            session.Tick(new LocalAction[2],.02f);Assert.That(session.Life(1).Health,Is.LessThan(100),"Grating must not occlude splash below");
            motors[1].Initialize(movement,new Vector3(point.x,4,point.z));for(int i=0;i<120;i++)motors[1].Tick(default,1f/60);
            Assert.That(motors[1].State.Position.y,Is.EqualTo(4).Within(.1f));Assert.That(motors[1].State.Grounded,Is.True);yield return null;
        }
        [UnityTest] public IEnumerator RifleBulletCrossesGrateAndRebuildDropsOldGratingReferences()
        {
            var grate=arena.Definition.Solids.Single(s=>s.Id=="balcony-west");var point=new Vector3(-25,grate.Position.y,0);
            motors[1].Initialize(movement,new Vector3(-25,4,0));var snapshot=session.Capture();snapshot.ShotSequence++;
            snapshot.RifleBullets=new[]{new RifleBulletState{Id=snapshot.ShotSequence,Owner=0,OwnerLife=session.Life(0).Life,Position=point-Vector3.up*.5f,Direction=Vector3.up,DamageMultiplier=1}};session.Restore(snapshot);
            int contacts=0;session.RifleBulletHit+=_=>contacts++;for(int i=0;i<10;i++)session.Tick(new LocalAction[2],.02f);
            Assert.That(contacts,Is.EqualTo(1));Assert.That(session.Life(1).Health,Is.LessThan(100));Assert.That(session.RifleBullets,Is.Empty);
            arena.Build(AuthoredPhysicsFixture.Freeze(),movement);Assert.That(arena.RaycastRocketGrating(point-Vector3.up,Vector3.up,2,out _),Is.False);yield return null;
        }
    }
}
