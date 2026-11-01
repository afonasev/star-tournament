using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class QuarryArenaPhysicsTests
    {
        GameObject root;ProvingArena arena;ProvingProfile movement;
        [SetUp] public void Setup(){root=new GameObject("Quarry physics");movement=ProvingProfile.CreateDefault();arena=root.AddComponent<ProvingArena>();arena.Build(AuthoredArenaCatalog.Freeze(QuarryArenaCatalog.Id,movement),movement);}
        [UnityTearDown] public IEnumerator Cleanup(){Object.Destroy(root);yield return null;}
        CharacterMotor Motor(Vector3 p){var go=new GameObject("quarry-capsule");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var m=go.AddComponent<CharacterMotor>();m.Initialize(movement,p,arena);return m;}
        [UnityTest] public IEnumerator SpawnAllocationClearanceAndAllSupplyRoutes()
        {
            yield return null;Physics.SyncTransforms();
            var selector=new SafeSpawnSelector(Physics.defaultPhysicsScene,arena,movement,ProvingProfile.CreateNativeCombatDefault());
            string details=string.Join(";",arena.Spawns.Select((p,i)=>i+":"+selector.Valid(p,out _)))+" visible=";
            for(int a=0;a<8;a++)for(int b=a+1;b<8;b++)if(selector.BodyVisible(arena.Spawns[a],arena.Spawns[b])||selector.BodyVisible(arena.Spawns[b],arena.Spawns[a]))details+=a+"/"+b+" ";
            Assert.That(selector.TryInitial(NativeMatchRoster.Ffa(8),ProvingProfile.CreateTeamDefault().Get("spawn.initialOpponentSeparation"),out var selected),Is.True,selector.InitialFailure+" "+details);
            Assert.That(selected.Length,Is.EqualTo(8));
            var routes=new NativeNavigationProvider(arena,movement,ProvingProfile.CreateNavigationDefault());
            foreach(var transition in arena.Definition.Transitions)
                for(int i=0;i<transition.OrderedFeet.Length;i++)
                {
                    Assert.That(routes.TryLocate(transition.OrderedFeet[i],out var located),Is.True,transition.Id+" foot "+i);
                    var expected=i==0?transition.LowerSupport:i==transition.OrderedFeet.Length-1?transition.UpperSupport:transition.Id;
                    Assert.That(located.Support,Is.EqualTo(expected),transition.Id+" foot "+i);
                }
            var failures=new System.Collections.Generic.List<string>();
            foreach(var spawn in arena.Spawns)foreach(var item in arena.Definition.Pickups)
                if(!routes.TryRoute(spawn,item.Anchor,out _,out var failure))failures.Add(spawn+" -> "+item.Id+": "+failure);
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
            foreach(var pos in arena.Spawns.Concat(arena.Definition.Pickups.Select(p=>p.Anchor)))
            {
                var motor=Motor(pos);for(int tick=0;tick<10;tick++)motor.Tick(default,1/60f);
                Assert.That(Vector3.Distance(motor.State.Position,pos),Is.LessThan(.15f),"clear physical anchor "+pos);Object.DestroyImmediate(motor.gameObject);
            }
        }
        [UnityTest] public IEnumerator BothRampsCarryMotorInBothDirectionsAndTunnelBlocksThroughShot()
        {
            yield return null;
            foreach(var t in arena.Definition.Transitions)foreach(bool reverse in new[]{false,true})
            {
                var points=reverse?t.OrderedFeet.Reverse().ToArray():t.OrderedFeet;var motor=Motor(points[0]);for(int i=0;i<5;i++)motor.Tick(default,1/60f);
                int next=1;
                for(int tick=0;tick<600&&next<points.Length;tick++)
                {
                    var delta=points[next]-motor.State.Position;delta.y=0;if(delta.magnitude<.22f){next++;continue;}
                    var local=Quaternion.Euler(0,-motor.State.Yaw,0)*delta.normalized;motor.Tick(new LocalAction{Move=new Vector2(local.x,local.z)},1/60f);Physics.SyncTransforms();
                }
                Assert.That(next,Is.EqualTo(points.Length),t.Id+" reverse="+reverse+" at "+motor.State.Position);Assert.That(motor.State.Position.y,Is.EqualTo(points.Last().y).Within(.3f));Object.DestroyImmediate(motor.gameObject);
            }
            Assert.That(Physics.Linecast(new Vector3(-24,1.7f,-5),new Vector3(21,1.7f,-5),1<<ProvingArena.WorldLayer),Is.True,"Dog-leg rock blocks shots between entrances");
            Assert.That(Physics.Raycast(new Vector3(-2,1,3),Vector3.up,3,1<<ProvingArena.WorldLayer),Is.True,"Tunnel has a real roof");
        }
        [UnityTest] public IEnumerator RealSessionCollectsEveryPickupAtItsSupport()
        {
            yield return null;
            foreach(var pickup in arena.Definition.Pickups)
            {
                var motor=Motor(arena.Spawns[0]);for(int i=0;i<5;i++)motor.Tick(default,1/60f);
                var session=new NativeCombatSession(new[]{motor},arena,Physics.defaultPhysicsScene,movement,ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault());
                if(pickup.Kind==ArenaPickupKind.Damage)session.Tick(new LocalAction[1],(float)session.DamagePickup.Remaining+1/60f);
                if(pickup.Kind==ArenaPickupKind.Speed)session.Tick(new LocalAction[1],(float)session.SpeedPickups.Single(p=>p.InstanceId==pickup.Id).Remaining+1/60f);
                if(pickup.Kind==ArenaPickupKind.FullHeal)session.Tick(new LocalAction[1],(float)session.HealPickups.Single(p=>p.InstanceId==pickup.Id).Remaining+1/60f);
                if(pickup.Kind==ArenaPickupKind.Cutter)
                {
                    var below=motor.State;below.Position=new Vector3(pickup.Anchor.x,0,pickup.Anchor.z);below.Velocity=Vector3.zero;motor.RestoreState(below);
                    session.Tick(new LocalAction[1],1/60f);Assert.That(session.WeaponPickups.Single(p=>p.InstanceId==pickup.Id).Available,Is.True,"No collection through terrace slab");
                }
                var pose=motor.State;pose.Position=pickup.Anchor;pose.Velocity=Vector3.zero;motor.RestoreState(pose);
                if(pickup.Kind==ArenaPickupKind.FullHeal)session.ApplyDamage(0,session.Life(0).Life,25);
                bool collected=false;session.PickupCollected+=(seat,id,kind)=>{if(id==pickup.Id)collected=true;};session.Tick(new LocalAction[1],1/60f);
                Assert.That(collected,Is.True,pickup.Id+" physical pickup");session.Stop();Object.DestroyImmediate(motor.gameObject);
            }
        }
    }
}
