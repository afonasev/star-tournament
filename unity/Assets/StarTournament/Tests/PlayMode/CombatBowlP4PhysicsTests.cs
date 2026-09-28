using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class CombatBowlP4PhysicsTests
    {
        GameObject root;ProvingArena arena;ProvingProfile movement,navigation;NativeNavigationProvider provider;
        [UnitySetUp] public IEnumerator Setup()
        {
            root=new GameObject("P4-physics");arena=root.AddComponent<ProvingArena>();movement=ProvingProfile.CreateDefault();navigation=ProvingProfile.CreateNavigationDefault();
            arena.Build(CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault()),movement);provider=new NativeNavigationProvider(arena,movement,navigation);yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown(){Object.Destroy(root);yield return null;}
        CharacterMotor Motor(Vector3 feet)
        {var go=new GameObject("motor");go.layer=ProvingArena.ParticipantLayer;go.transform.SetParent(root.transform);go.AddComponent<CharacterController>();var m=go.AddComponent<CharacterMotor>();m.Initialize(movement,feet);return m;}
        [UnityTest] public IEnumerator EveryRiseAndStairTraversesThreeLanesBothDirections()
        {
            foreach(var t in arena.ReadNavigationTransitions())for(int direction=-1;direction<=1;direction+=2)for(int lane=-1;lane<=1;lane++)
            {
                var lower=t.OrderedFeet[0];var upper=t.OrderedFeet.Last();var forward=upper-lower;forward.y=0;forward.Normalize();var lateral=Vector3.Cross(Vector3.up,forward)*lane*movement.Get("player.capsule.radius");
                var bottom=lower-forward*.5f+lateral;var top=upper+forward*.5f+lateral;var start=direction>0?bottom:top;var goal=direction>0?top:bottom;
                var motor=Motor(start);var bot=new NativeBotNavigation(provider,navigation);bot.SetStaticGoal(goal);int recoveries=0;
                for(int tick=0;tick<900&&bot.Status!=NativeNavigationStatus.Arrived;tick++)
                {var action=bot.Tick(tick/60d,motor.State,new NativeBotKnowledge{Alive=true,OwnLife=1});motor.Tick(action,1f/60);Physics.SyncTransforms();recoveries=Mathf.Max(recoveries,bot.Capture().Recoveries);}
                if(bot.Status!=NativeNavigationStatus.Arrived)Debug.Log("P4_NAV_FAILURE "+JsonUtility.ToJson(bot.Capture())+" pose "+JsonUtility.ToJson(motor.State));
                Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived),t.Id+" direction="+direction+" lane="+lane+" at="+motor.State.Position+" "+bot.Capture().Failure);
                Assert.That(recoveries,Is.Zero,t.Id);Assert.That(motor.State.Position.y,Is.EqualTo(goal.y).Within(navigation.Get("bots.navigation.heightTolerance")));Object.DestroyImmediate(motor.gameObject);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SpawnRoutesAndBasementSupportCannotShortcutThroughSlab()
        {
            foreach(var spawn in arena.Spawns)
            {
                Assert.That(provider.TryPhysicalSupport(spawn,out var support),Is.True);Assert.That(support.Support,Is.EqualTo(arena.Definition.SpawnRegions.Single(r=>r.Center==spawn).Support));
                Assert.That(provider.TryRoute(spawn,arena.Definition.Pickups.Single(x=>x.Kind==ArenaPickupKind.FullHeal).Anchor,out _,out var error),Is.True,spawn+" to B "+error);
                Assert.That(provider.TryRoute(spawn,arena.Definition.DamagePickupAnchor,out _,out error),Is.True,spawn+" to A "+error);
            }
            Assert.That(provider.TryPhysicalSupport(new Vector3(0,-1.2f,0),out var b),Is.True);Assert.That(b.Support,Is.EqualTo("support:armor-corridor"));
            Assert.That(provider.TryPhysicalSupport(new Vector3(0,4,0),out var a),Is.True);Assert.That(a.Support,Is.EqualTo("support:central"));
            Assert.That(Physics.Raycast(new Vector3(0,2,0),Vector3.up,out var hit,3,1<<ProvingArena.WorldLayer),Is.True);Assert.That(hit.collider.name,Is.EqualTo("central-hall"));
            yield return null;
        }
        [UnityTest] public IEnumerator BonusEffectsSurviveSnapshotAndDeathClearsBoost()
        {
            var m0=Motor(new Vector3(-4,4,0));var m1=Motor(new Vector3(4,4,0));var life=ProvingProfile.CreateCombatDefault();
            var session=new NativeCombatSession(new[]{m0,m1},arena,Physics.defaultPhysicsScene,movement,life,ProvingProfile.CreateNativeCombatDefault());var actions=new LocalAction[2];
            for(int i=0;i<901;i++)session.Tick(actions,1f/60);
            Assert.That(session.DamagePickup.Available,Is.True);m0.Initialize(movement,new Vector3(0,4,0));session.Tick(actions,1f/60);
            Assert.That(session.DamageBoostRemaining(0),Is.GreaterThan(9));Assert.That(session.ArmorPickup.Available,Is.True,"Upper participant must not collect B");
            var snapshot=session.Capture();session.Tick(actions,1f/60);session.Restore(snapshot);Assert.That(session.DamageBoostRemaining(0),Is.EqualTo(snapshot.DamageRemaining[0]));
            session.ApplyDamage(0,session.Life(0).Life,10000,1,session.Life(1).Life);Assert.That(session.DamageBoostRemaining(0),Is.Zero);
            yield return null;
        }
    }
}
