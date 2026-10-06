using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class IndustrialTunnelsPhysicsTests
    {
        [UnityTest] public IEnumerator AllAnchorsConnectedAndAllRampsWalkableBothWays()
        {
            var root=new GameObject("tunnels-physics");var movement=ProvingProfile.CreateDefault();var nav=ProvingProfile.CreateNavigationDefault();var arena=root.AddComponent<ProvingArena>();
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(IndustrialTunnelsCatalog.Id,movement),movement);yield return null;
                var selector=new SafeSpawnSelector(root.scene.GetPhysicsScene(),arena,movement,ProvingProfile.CreateNativeCombatDefault());
                Assert.That(selector.TryInitial(NativeMatchRoster.Ffa(4),ProvingProfile.CreateTeamDefault().Get("spawn.initialOpponentSeparation"),out _),Is.True,selector.InitialFailure);
                var provider=new NativeNavigationProvider(arena,movement,nav);
                foreach(var from in arena.Spawns)foreach(var to in arena.Definition.Pickups.Select(p=>p.Anchor).Concat(arena.Spawns))
                    Assert.That(provider.TryRoute(from,to,out _,out var failure),Is.True,from+" -> "+to+" "+failure);
                foreach(var t in arena.Definition.Transitions)foreach(int dir in new[]{-1,1})foreach(int lane in new[]{-1,0,1})
                {
                    var lower=t.OrderedFeet.First();var upper=t.OrderedFeet.Last();var f=upper-lower;f.y=0;f.Normalize();var across=Vector3.Cross(Vector3.up,f)*lane*movement.Get("player.capsule.radius");
                    var start=(dir>0?lower-f:upper+f)+across;var goal=(dir>0?upper+f:lower-f)+across;
                    var go=new GameObject("walker");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,start);
                    var bot=new NativeBotNavigation(provider,nav);bot.SetStaticGoal(goal);var own=new NativeBotKnowledge{Alive=true,OwnLife=1};
                    for(int tick=0;tick<900&&bot.Status!=NativeNavigationStatus.Arrived&&bot.Status!=NativeNavigationStatus.Blocked;tick++)
                    {var action=bot.Tick(tick/60d,motor.State,own);Assert.That(action.Jump,Is.False);motor.Tick(action,1/60f);Physics.SyncTransforms();}
                    Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived),t.Id+" "+dir+" "+lane+" "+bot.Capture().Failure+" "+motor.State.Position);
                    Assert.That(bot.Capture().Recoveries,Is.Zero);Object.DestroyImmediate(go);
                }
                var art=root.GetComponentInChildren<IndustrialTunnelsPresentation>();Assert.That(art.LightCount,Is.GreaterThan(20));Assert.That(art.PipeCount,Is.GreaterThan(30));Assert.That(art.GetComponentsInChildren<Collider>(),Is.Empty);
                arena.Build(CombatBowlCatalog.Freeze(movement),movement);Assert.That(root.GetComponentInChildren<IndustrialTunnelsPresentation>(),Is.Null);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
