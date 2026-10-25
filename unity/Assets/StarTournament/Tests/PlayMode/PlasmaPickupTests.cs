using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class PlasmaPickupTests
    {
        Scene scene;
        [UnityTearDown]public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest]public IEnumerator EveryForgeAnchorHasRealSupportAndGrantsOneHundredCharges()
        {
            foreach(string map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
            {
                scene=SceneManager.CreateScene("forge-pickup-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                var owner=new GameObject("FORGE pickup fixture");SceneManager.MoveGameObjectToScene(owner,scene);var move=ProvingProfile.CreateDefault();
                var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredArenaCatalog.Freeze(map,move),move);
                var body=new GameObject("collector");body.transform.SetParent(owner.transform);body.layer=ProvingArena.ParticipantLayer;var motor=body.AddComponent<CharacterMotor>();
                yield return null;
                foreach(var anchor in arena.Definition.Pickups.Where(p=>p.Kind==ArenaPickupKind.Plasma))
                {
                    if(map!=IndustrialTunnelsCatalog.Id)
                    {
                        var other=anchor.Anchor;other.y=map==LunarLaboratoryCatalog.Id?LunarLaboratoryCatalog.Upper:0;
                        motor.Initialize(move,other);var wrong=new NativeCombatSession(new[]{motor},arena,scene.GetPhysicsScene(),move,ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault());
                        wrong.Tick(new LocalAction[1],.02f);Assert.That(wrong.Life(0).PlasmaOwned,Is.False,map+"/"+anchor.Id+" cannot collect through floors");
                    }
                    motor.Initialize(move,anchor.Anchor);var s=new NativeCombatSession(new[]{motor},arena,scene.GetPhysicsScene(),move,ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault());
                    s.Tick(new LocalAction[1],.02f);Assert.That(s.Life(0).PlasmaOwned,Is.True,map+"/"+anchor.Id+" physical support");Assert.That(s.Life(0).PlasmaAmmo,Is.EqualTo(100));
                    Assert.That(s.WeaponPickups.Single(p=>p.InstanceId==anchor.Id).Available,Is.False);
                }
                yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
