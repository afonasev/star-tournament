using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class LunarLaboratoryPhysicsTests
    {
        [UnityTest] public IEnumerator RoutesStairsGlassAndSpawns()
        {
            var root=new GameObject("lunar-physics");var movement=ProvingProfile.CreateDefault();var nav=ProvingProfile.CreateNavigationDefault();var arena=root.AddComponent<ProvingArena>();
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(LunarLaboratoryCatalog.Id,movement),movement);yield return null;
                var selector=new SafeSpawnSelector(root.scene.GetPhysicsScene(),arena,movement,ProvingProfile.CreateNativeCombatDefault());
                for(int a=0;a<8;a++){Assert.That(selector.Valid(arena.Spawns[a],out _),Is.True,"invalid S"+(a+1));for(int b=a+1;b<8;b++)Assert.That(selector.BodyVisible(arena.Spawns[a],arena.Spawns[b])||selector.BodyVisible(arena.Spawns[b],arena.Spawns[a]),Is.False,"visible S"+(a+1)+" S"+(b+1));}
                Assert.That(selector.TryInitial(NativeMatchRoster.Ffa(8),ProvingProfile.CreateTeamDefault().Get("spawn.initialOpponentSeparation"),out _),Is.True,selector.InitialFailure);
                var provider=new NativeNavigationProvider(arena,movement,nav);
                foreach(var from in arena.Spawns)foreach(var to in arena.Definition.Pickups.Select(p=>p.Anchor).Concat(arena.Spawns))
                    Assert.That(provider.TryRoute(from,to,out _,out var failure),Is.True,from+" -> "+to+" "+failure);
                // Both portals of each downstairs storage room must remain real bot passages.
                foreach(int side in new[]{-1,1})foreach(bool across in new[]{false,true})
                {
                    var start=across?new Vector3(side*5,.02f,-side*8):new Vector3(side*7.25f,.02f,-side*5);
                    var end=across?new Vector3(side*7,.02f,-side*8):new Vector3(side*7.25f,.02f,-side*7.5f);
                    Assert.That(provider.TryRoute(start,end,out var route,out var failure),Is.True,"storage portal "+side+" "+across+" "+failure);
                    float length=0;var previous=start;foreach(var point in route){length+=Vector3.Distance(previous,point.Position);previous=point.Position;}
                    Assert.That(length,Is.LessThan(5),"portal must not detour through the other door");
                }
                foreach(var t in arena.Definition.Transitions)foreach(int dir in new[]{-1,1})foreach(int lane in new[]{-1,0,1})
                {
                    var lower=t.OrderedFeet.First();var upper=t.OrderedFeet.Last();var f=upper-lower;f.y=0;f.Normalize();var across=Vector3.Cross(Vector3.up,f)*lane*movement.Get("player.capsule.radius");
                    var start=(dir>0?lower-f*.65f:upper+f*.65f)+across;var goal=(dir>0?upper+f*.65f:lower-f*.65f)+across;
                    var go=new GameObject("walker");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,start,arena);
                    var bot=new NativeBotNavigation(provider,nav);bot.SetStaticGoal(goal);var own=new NativeBotKnowledge{Alive=true,OwnLife=1};
                    for(int tick=0;tick<1200&&bot.Status!=NativeNavigationStatus.Arrived&&bot.Status!=NativeNavigationStatus.Blocked;tick++)
                    {var action=bot.Tick(tick/60d,motor.State,own);motor.Tick(action,1/60f);Physics.SyncTransforms();}
                    Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived),t.Id+" "+dir+" "+lane+" "+bot.Capture().Failure+" "+motor.State.Position);Object.DestroyImmediate(go);
                }
                Assert.That(selector.BodyVisible(new Vector3(0,.02f,2),new Vector3(0,.02f,-2)),Is.True,"glass passes optical sight");
                Assert.That(Physics.Raycast(new Vector3(0,1.7f,2),Vector3.back,out var hit,4,ProvingArena.ShotMask),Is.True);Assert.That(hit.collider.name,Is.EqualTo("divider-glass"));
                Assert.That(Physics.Raycast(new Vector3(8,6.3f,11),Vector3.forward,2,ProvingArena.ShotMask),Is.True,"armored north window");
                Assert.That(Physics.Raycast(new Vector3(11,6.3f,7),Vector3.right,2,ProvingArena.ShotMask),Is.False,"authored broken east window");
                Assert.That(Physics.Raycast(new Vector3(-2,6.2f,10),Vector3.right,4,ProvingArena.ShotMask),Is.True,"S7/S8 opaque common wall");
                // Real controller jumps through the authored broken window and over the balcony guard.
                foreach(bool window in new[]{false,true})
                {
                    var go=new GameObject("jumper");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var m=go.AddComponent<CharacterMotor>();m.Initialize(movement,window?new Vector3(10,4.5f,-10.5f):new Vector3(0,4.5f,13.4f),arena);
                    var pose=m.State;pose.Yaw=window?90:0;m.RestoreState(pose);
                    for(int tick=0;tick<(window?65:150);tick++){m.Tick(new LocalAction{Move=tick>2?Vector2.up:Vector2.zero,Jump=tick==3},1/60f);Physics.SyncTransforms();}
                    Assert.That(window?m.State.Position.x:m.State.Position.z,Is.GreaterThan(window?12.5f:16),"jump passage "+window);
                    if(window)Assert.That(m.State.Position.y,Is.EqualTo(4.5f).Within(.05f),"window exits onto balcony");else Assert.That(m.State.Position.y,Is.LessThan(.2f),"balcony jump lands in courtyard");Object.DestroyImmediate(go);
                }
                var art=root.GetComponentInChildren<LunarLaboratoryPresentation>();Assert.That(art.LightCount,Is.GreaterThan(20));Assert.That(art.GetComponentsInChildren<Collider>(),Is.Empty);
                arena.Build(CombatBowlCatalog.Freeze(movement),movement);Assert.That(root.GetComponentInChildren<LunarLaboratoryPresentation>(),Is.Null);
            }
            finally {Object.Destroy(root);}yield return null;
        }
    }
}
