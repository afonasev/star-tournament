using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class FullHealSessionTests
    {
        GameObject root;ProvingArena arena;ProvingProfile movement,life,combat;CharacterMotor[] motors;
        [UnitySetUp] public IEnumerator Setup()
        {
            root=new GameObject("full-heal-test");arena=root.AddComponent<ProvingArena>();movement=ProvingProfile.CreateDefault();life=ProvingProfile.CreateCombatDefault();combat=ProvingProfile.CreateNativeCombatDefault();
            arena.Build(CombatBowlCatalog.Freeze(movement),movement);
            motors=Enumerable.Range(0,2).Select(i=>{var go=new GameObject("motor-"+i);go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var m=go.AddComponent<CharacterMotor>();m.Initialize(movement,new Vector3(i*4-2,-1.2f,-4));return m;}).ToArray();yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown(){Object.Destroy(root);yield return null;}
        NativeCombatSession Session()=>new NativeCombatSession(motors,arena,Physics.defaultPhysicsScene,movement,life,combat);
        static void Advance(NativeCombatSession s,int ticks){for(int i=0;i<ticks;i++)s.Tick(new LocalAction[2],.02f);}
        [UnityTest] public IEnumerator DelayedCollectionUsesStableSeatOrderPreservesArmorAndRepeat()
        {
            var s=Session();s.ApplyDamage(0,1,50);s.ApplyDamage(1,1,99);
            var before=s.Capture();before.Lives[0].Armor=35;s.Restore(before);
            Advance(s,999);Assert.That(s.HealPickups.Single().Available,Is.False);
            // Pause is the owning adapter withholding Tick; elapsed rendering never calls Advance.
            var paused=JsonUtility.ToJson(s.Capture());yield return new WaitForSecondsRealtime(.1f);Assert.That(JsonUtility.ToJson(s.Capture()),Is.EqualTo(paused));
            motors[0].Initialize(movement,new Vector3(-.6f,-1.2f,0));motors[1].Initialize(movement,new Vector3(.6f,-1.2f,0));s.Tick(new LocalAction[2],.02f);
            Assert.That(s.Life(0).Health,Is.EqualTo(100));Assert.That(s.Life(0).Armor,Is.EqualTo(35));Assert.That(s.Life(1).Health,Is.EqualTo(1));
            Assert.That(s.HealPickups.Single().Remaining,Is.EqualTo(30));Assert.That(s.ShotCount,Is.Zero);
            motors[0].Initialize(movement,new Vector3(-2,-1.2f,-4));motors[1].Initialize(movement,new Vector3(2,-1.2f,-4));
            Advance(s,1450);var cooldown=s.Capture();Advance(s,50);Assert.That(s.HealPickups.Single().Available,Is.True);
            var restored=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(cooldown));s.Restore(restored);
            Assert.That(s.HealPickups.Single().Available,Is.False);Advance(s,50);Assert.That(s.HealPickups.Single().Available,Is.True);
            restored.Match=new NativeMatchSnapshot{TuningIdentity="foreign"};Assert.Throws<System.ArgumentException>(()=>s.Restore(restored),"A real match snapshot still requires a match reducer");
            var repeat=Session();Assert.That(repeat.HealPickups.Single().Remaining,Is.EqualTo(20));Assert.That(repeat.ArmorPickups.Length,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator UpperHallAndDeadParticipantDoNotCollectThroughSlab()
        {
            var s=Session();motors[0].Initialize(movement,new Vector3(0,4,0));s.ApplyDamage(0,1,50);s.ApplyDamage(1,1,100);
            // Keep death pending throughout the boundary without changing the base profile contract.
            var snapshot=s.Capture();snapshot.Lives[1].RespawnRemaining=30;s.Restore(snapshot);motors[1].Initialize(movement,new Vector3(0,-1.2f,0));
            Advance(s,1000);Assert.That(s.HealPickups.Single().Available,Is.True);Assert.That(s.Life(0).Health,Is.EqualTo(50));Assert.That(s.Life(1).Dead,Is.True);
            motors[0].Initialize(movement,new Vector3(0,-1.2f,0));s.Tick(new LocalAction[2],.02f);Assert.That(s.Life(0).Health,Is.EqualTo(100));Assert.That(s.HealPickups.Single().Available,Is.False);
            yield return null;
        }
    }
}
