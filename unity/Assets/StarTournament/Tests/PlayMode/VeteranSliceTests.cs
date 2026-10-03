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
            // A projectile above the former 2.2 m lintel must now traverse both openings,
            // while the retained upper lintel still supplies actual physical structure.
            foreach(int direction in new[]{-1,1})
            {
                var lintel=arena.Definition.Solids.Single(s=>s.Id==(direction>0?"hall-north-lintel":"hall-south-lintel"));
                Assert.That(lintel.Position.y-lintel.Size.y*.5f-4,Is.EqualTo(3.5f).Within(.001f));
                Assert.That(Physics.Raycast(new Vector3(0,7,6*direction),Vector3.forward*direction,3,mask),Is.False);
                Assert.That(Physics.Raycast(new Vector3(0,7.75f,6*direction),Vector3.forward*direction,out var lintelHit,3,mask),Is.True);
                Assert.That(lintelHit.collider.name,Is.EqualTo(lintel.Id));
                motor.Initialize(movement,new Vector3(0,4,-5*direction));
                for(int i=0;i<75;i++){motor.Tick(new LocalAction{Move=Vector2.down*direction},1f/60);Physics.SyncTransforms();}
                Assert.That(-direction*motor.State.Position.z,Is.GreaterThan(10),"South portal traversal");
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
