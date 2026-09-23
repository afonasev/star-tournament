using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class OrbitalLeaguePresentationTests
    {
        GameObject root;
        [UnityTearDown] public IEnumerator Cleanup(){if(root)Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator ArtPreservesAllCollisionAndRepeatOwnsItsResources()
        {
            root=new GameObject("orbital-test");var arena=root.AddComponent<ProvingArena>();
            var frozen=CombatBowlCatalog.Freeze(ProvingProfile.CreateArenaFamiliesDefault());var before=JsonUtility.ToJson(frozen.Definition);
            arena.Build(frozen,ProvingProfile.CreateDefault());yield return null;
            foreach(var s in frozen.Definition.Solids)
            {
                var t=arena.transform.Find(s.Id);var collider=t.GetComponent<BoxCollider>();
                Assert.That(collider,Is.Not.Null,s.Id);Assert.That(t.position,Is.EqualTo(s.Position));Assert.That(t.localScale,Is.EqualTo(s.Size));Assert.That(Quaternion.Angle(t.rotation,s.Rotation),Is.LessThan(.01f));
            }
            var art=arena.GetComponentInChildren<OrbitalLeaguePresentation>();
            Assert.That(art.ProfileIdentity,Is.EqualTo("orbital-league-v1@2"));
            Assert.That(art.GetComponentsInChildren<Transform>().Count(t=>t.name=="Slow orbital emblem"),Is.EqualTo(5));
            Assert.That(art.GetComponentsInChildren<Transform>().Count(t=>t.name=="Enclosed slow fan"),Is.EqualTo(2));
            Assert.That(art.GetComponentsInChildren<Collider>(),Is.Empty);
            Assert.That(arena.GetComponentsInChildren<Collider>().Length,Is.EqualTo(frozen.Definition.Solids.Length));
            Assert.That(art.LocalLightCount,Is.EqualTo(6));Assert.That(art.GetComponentsInChildren<Light>().All(l=>l.shadows==LightShadows.None),Is.True);
            int renderers=arena.GetComponentsInChildren<Renderer>().Length;
            arena.Build(frozen,ProvingProfile.CreateDefault());yield return null;
            Assert.That(arena.GetComponentsInChildren<OrbitalLeaguePresentation>().Length,Is.EqualTo(1));
            Assert.That(arena.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(renderers));
            Assert.That(JsonUtility.ToJson(frozen.Definition),Is.EqualTo(before));
            Object.Destroy(root);yield return null;
        }
        [Test] public void ArtProfileRejectsOutOfRangeAndPublishesCompleteMetadata()
        {
            var p=ProvingProfile.CreateOrbitalLeagueDefault();Assert.That(p.Validate(),Is.Empty);
            Assert.That(p.Descriptors.All(d=>d.Validate(out _)),Is.True);
            Assert.That(p.Descriptor("broadcast.orbitSpeed").Group,Is.EqualTo("broadcast"));
            p.Set("broadcast.orbitSpeed",1000);Assert.That(p.Validate(),Is.Not.Empty);
        }
    }
}
