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
            var frozen=CombatBowlCatalog.Freeze(ProvingProfile.CreateDefault());var before=JsonUtility.ToJson(frozen.Definition);
            arena.Build(frozen,ProvingProfile.CreateDefault());yield return null;
            foreach(var s in frozen.Definition.Solids)
            {
                var t=arena.transform.Find(s.Id);var collider=t.GetComponent<BoxCollider>();
                Assert.That(collider,Is.Not.Null,s.Id);Assert.That(t.position,Is.EqualTo(s.Position));Assert.That(t.localScale,Is.EqualTo(s.Size));Assert.That(Quaternion.Angle(t.rotation,s.Rotation),Is.LessThan(.01f));
            }
            var art=arena.GetComponentInChildren<OrbitalLeaguePresentation>();
            Assert.That(art.ProfileIdentity,Is.EqualTo("orbital-league-ring-v1@1"));
            Assert.That(art.GetComponentsInChildren<Transform>().Count(t=>t.name=="Slow orbital emblem"),Is.EqualTo(5));
            Assert.That(art.GetComponentsInChildren<Transform>().Count(t=>t.name=="Enclosed slow fan"),Is.EqualTo(2));
            Assert.That(art.GetComponentsInChildren<Collider>(),Is.Empty);
            Assert.That(arena.GetComponentsInChildren<Collider>().Length,Is.EqualTo(frozen.Definition.Solids.Length));
            Assert.That(art.LocalLightCount,Is.EqualTo(6));Assert.That(art.ShadowLightCount,Is.EqualTo(4));
            Assert.That(art.GetComponentsInChildren<Light>().Count(l=>l.shadows==LightShadows.Soft),Is.EqualTo(4));
            Assert.That(art.GetComponentsInChildren<Light>().All(l=>l.type==LightType.Spot),Is.True);
            Assert.That(art.GetComponentsInChildren<Light>().All(l=>Mathf.Approximately(l.spotAngle,100)),Is.True);
            int renderers=arena.GetComponentsInChildren<Renderer>().Length;
            arena.Build(frozen,ProvingProfile.CreateDefault());yield return null;
            Assert.That(arena.GetComponentsInChildren<OrbitalLeaguePresentation>().Length,Is.EqualTo(1));
            Assert.That(arena.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(renderers));
            Assert.That(arena.GetComponentsInChildren<Light>().Length,Is.EqualTo(6));
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
        [Test] public void SerializedV2LightingUpgradesValuesAndRangeTogether()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateOrbitalLeagueDefault()).Replace("\"version\":5","\"version\":2");
            Assert.That(json,Does.Contain("\"version\":2"));
            var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("surface.floorValue",.18f);
            old.Set("light.intensity",1.4f);old.Descriptor("light.intensity").Maximum=4;
            old.Set("light.range",7);old.Set("light.emission",1.5f);
            old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Version,Is.EqualTo(5));
            Assert.That(old.Get("surface.floorValue"),Is.EqualTo(.28f));
            Assert.That(old.Get("light.intensity"),Is.EqualTo(11));
            Assert.That(old.Descriptor("light.intensity").Maximum,Is.EqualTo(12));
            Assert.That(old.Validate(),Is.Empty);
        }
        [Test] public void SerializedV3LightingKeepsOverridesAndUpgradesOldDefaults()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateOrbitalLeagueDefault()).Replace("\"version\":5","\"version\":3");
            var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("light.fill",.65f);old.Set("light.intensity",8);old.Set("light.studioKey",.65f);
            old.Set("light.range",10);old.Set("light.shadowStrength",.65f);old.Descriptor("light.shadowStrength").Maximum=.7f;
            old.Set("light.shadowNormalBias",.4f);old.Set("light.shadowBias",.05f);
            old.Set("light.emission",1.8f); // Designer override, not the former default.
            old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Version,Is.EqualTo(5));
            Assert.That(old.Get("light.fill"),Is.EqualTo(.45f));
            Assert.That(old.Get("light.intensity"),Is.EqualTo(11));
            Assert.That(old.Get("light.shadowStrength"),Is.EqualTo(1));
            Assert.That(old.Get("light.emission"),Is.EqualTo(1.8f));
            Assert.That(old.Descriptor("light.shadowStrength").Maximum,Is.EqualTo(1));
            Assert.That(old.Validate(),Is.Empty);
        }
        [Test] public void SerializedV4FloorKeepsDesignerOverrides()
        {
            var json=JsonUtility.ToJson(ProvingProfile.CreateOrbitalLeagueDefault()).Replace("\"version\":5","\"version\":4");
            var old=JsonUtility.FromJson<ProvingProfile>(json);
            old.Set("surface.floorValue",.24f);
            old.EnsureOrbitalLeagueDescriptors();
            Assert.That(old.Version,Is.EqualTo(5));
            Assert.That(old.Get("surface.floorValue"),Is.EqualTo(.24f));
            Assert.That(old.Validate(),Is.Empty);
        }
    }
}
