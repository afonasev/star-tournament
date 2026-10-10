using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class DeathRagdollTests
    {
        Scene scene;ProvingGround ground;Gamepad pad;
        IEnumerator Load()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();ground.enabled=false;
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True,"Ragdoll QA must start a match rather than exercise setup preview");
        }
        void Render()=>ground.SendMessage("LateUpdate");
        void Advance(int count){for(int i=0;i<count;i++){ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);Render();}}
        TrooperVisual Corpse()=>ground.GetComponentsInChildren<TrooperVisual>().Single(v=>v.name.StartsWith("corpse-"));
        [UnityTearDown]public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);yield return null;}
        [UnityTest]public IEnumerator AirborneCorpseKeepsPoseMomentumAndFreezesAtMatchClock()
        {
            yield return Load();ground.PlaceCombatReviewSeat(0,new Vector3(-20,2,0),0);
            var running=new LocalAction[ground.Session.ParticipantCount];running[0].Move=Vector2.right;
            for(int i=0;i<6;i++)ground.Session.Tick(running,.02f);Render();
            var live=ground.GetComponentsInChildren<TrooperVisual>().Single(v=>v.name=="trooper-presentation"&&v.transform.parent.name=="player-1");
            var before=live.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hips").position;
            var expectedVelocity=ground.Session.Pose(0).Velocity;Assert.That(expectedVelocity.x,Is.GreaterThan(.1f));DeathNotice death=default;ground.Session.Died+=n=>death=n;
            ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,500);Render();var corpse=Corpse();
            Assert.That(death.Pose.Velocity,Is.EqualTo(expectedVelocity),"Motor disable must not erase corpse momentum");
            Assert.That(corpse.Ragdoll,Is.Not.Null);Assert.That(Vector3.Distance(corpse.Ragdoll.Center,before),Is.LessThan(.001));
            Assert.That(corpse.GraphValid,Is.False);Assert.That(corpse.GetComponentInChildren<Animator>().enabled,Is.False);
            Assert.That(corpse.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="weapon:joined").enabled,Is.False);
            Assert.That(corpse.GetComponentsInChildren<Collider>(),Is.Empty);
            Assert.That(corpse.Ragdoll.Colliders.All(c=>c.gameObject.scene!=scene),Is.True);
            Advance(20);var fallen=corpse.Ragdoll.Center;Assert.That(fallen.y,Is.LessThan(before.y-.1f),"clock="+ground.Session.Time+" sleeping="+corpse.Ragdoll.Sleeping+" speed="+corpse.Ragdoll.MaximumSegmentSpeed+" phase="+ground.Session.Match.Phase);
            yield return new WaitForSecondsRealtime(.15f);Render();Assert.That(corpse.Ragdoll.Center,Is.EqualTo(fallen));
            Advance(150);Assert.That(corpse.Ragdoll.Center.y,Is.GreaterThan(-1.5f),"World support prevents falling through arena");
            Assert.That(corpse.Ragdoll.MaximumSegmentSpeed,Is.LessThan(ground.DeathProfile.Get("corpse.maximumSpeed")));
            var left=corpse.GetComponentsInChildren<Transform>().Single(t=>t.name=="LeftHand");var right=corpse.GetComponentsInChildren<Transform>().Single(t=>t.name=="RightHand");
            var restLeft=left.position;Advance(15);Assert.That(Vector3.Distance(left.position,restLeft),Is.LessThan(.3f),"Final pose must settle");
            Assert.That(Vector3.Distance(left.position,right.position),Is.GreaterThan(.1f),"No two-hand weapon constraint");
            ground.StartCombatReview(new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);yield return null;
            Assert.That(ground.GetComponentsInChildren<TrooperVisual>().Any(v=>v.name.StartsWith("corpse-")),Is.False);
        }
        [UnityTest]public IEnumerator EveryRealWeaponReportsFatalContextBeforeLaterEffects()
        {
            // Isolate fatal attribution from UI roster/pickups and selection presentation.
            // Existing native combat fixtures supply the same physical arena and real resolvers.
            scene=SceneManager.CreateScene("fatal-context-"+System.Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("fatal-context");SceneManager.MoveGameObjectToScene(owner,scene);
            var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();combat.Set("shot.spread",0);combat.Set("rifle.spread",0);
            var life=ProvingProfile.CreateCombatDefault();var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
            var motors=new CharacterMotor[2];
            for(int i=0;i<2;i++){var go=new GameObject("p"+i);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();}
            foreach(var weapon in new[]{WeaponId.Rifle,WeaponId.Shotgun,WeaponId.RocketLauncher,WeaponId.Cutter})
            {
                motors[0].Initialize(move,Vector3.zero);motors[1].Initialize(move,new Vector3(0,0,4));
                var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);EquippedCombatFixture.Equip(session);
                var snapshot=session.Capture();snapshot.Lives[1].Health=1;snapshot.Lives[0].SelectedWeapon=weapon;snapshot.Lives[0].PendingWeapon=default;
                snapshot.Lives[0].Ammo=CombatLife.AmmoFor(snapshot.Lives[0],weapon);session.Restore(snapshot);
                DeathNotice notice=default;int deaths=0;session.Died+=n=>{if(n.Seat==1){notice=n;deaths++;}};
                var actions=new LocalAction[2];actions[0].Fire=true;actions[0].FireHeld=true;session.Tick(actions,.02f);
                for(int i=0;i<60&&deaths==0;i++){actions[0].Fire=false;session.Tick(actions,.02f);}
                Assert.That(session.ShotCount,Is.EqualTo(1),weapon+" must use the real firing path");
                Assert.That(deaths,Is.EqualTo(1),weapon+" "+JsonUtility.ToJson(session.Life(1)));Assert.That(notice.Impact.Valid,Is.True);Assert.That(notice.Impact.Weapon,Is.EqualTo(weapon));
                Assert.That(notice.Impact.Direction.z,Is.GreaterThan(0));session.Stop();
            }
            yield return null;
        }
    }
}
