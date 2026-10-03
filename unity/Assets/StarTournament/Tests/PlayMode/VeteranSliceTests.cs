using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class VeteranSliceTests
    {
        GameObject root;
        [UnityTearDown] public IEnumerator Cleanup(){if(root)Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator CasesBlockShotsAndMovementWhilePortalRemainsWalkableBothWays()
        {
            var movement=ProvingProfile.CreateDefault();root=new GameObject("Veteran physical acceptance");var arena=root.AddComponent<ProvingArena>();arena.Build(CombatBowlCatalog.Freeze(movement),movement);yield return null;
            int mask=1<<ProvingArena.WorldLayer;
            Assert.That(Physics.Raycast(new Vector3(4.85f,4.8f,4.5f),Vector3.forward,out var hit,3,mask),Is.True);
            Assert.That(hit.collider.name,Is.EqualTo("veteran-case-tall"));
            Assert.That(Physics.Raycast(new Vector3(-4.65f,5.35f,6.8f),Vector3.forward,out var cabinetHit,1.2f,mask),Is.True);
            Assert.That(cabinetHit.collider.name,Is.EqualTo("veteran-service-cabinet"));
            var walker=new GameObject("Veteran walker");walker.transform.SetParent(root.transform);walker.layer=ProvingArena.ParticipantLayer;walker.AddComponent<CharacterController>();var motor=walker.AddComponent<CharacterMotor>();
            motor.Initialize(movement,new Vector3(4.85f,4,4.5f));
            for(int i=0;i<120;i++){motor.Tick(new LocalAction{Move=Vector2.up},1f/60);Physics.SyncTransforms();}
            Assert.That(motor.State.Position.z,Is.LessThan(5.95f),"Physical case must stop capsule");
            foreach(float x in new[]{-1f,0,1f})foreach(int direction in new[]{-1,1})
            {
                motor.Initialize(movement,new Vector3(x,4,direction>0?5:11));
                for(int i=0;i<75;i++){motor.Tick(new LocalAction{Move=Vector2.up*direction},1f/60);Physics.SyncTransforms();}
                Assert.That(direction*motor.State.Position.z,Is.GreaterThan(direction>0?10:-6),"North portal lane "+x+" direction "+direction);
            }
            var art=arena.GetComponentInChildren<OrbitalLeaguePresentation>();Assert.That(art.GetComponentsInChildren<Collider>(),Is.Empty);
            Assert.That(art.GetComponentsInChildren<ParticleSystem>().Single().main.maxParticles,Is.EqualTo(8));
            Assert.That(art.GetComponentsInChildren<Light>().Single(l=>l.name=="Veteran service light").shadows,Is.EqualTo(LightShadows.None));
            var before=JsonUtility.ToJson(arena.Definition);int renderers=arena.GetComponentsInChildren<Renderer>().Length;
            arena.Build(CombatBowlCatalog.Freeze(movement),movement);yield return null;
            Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(before));Assert.That(arena.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(renderers));
        }
    }
}
