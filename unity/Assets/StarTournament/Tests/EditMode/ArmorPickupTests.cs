using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ArmorPickupTests
    {
        [Test] public void CrystalRunePickupAssetsAreUprightTwoSidedAndCollisionFree()
        {
            foreach(var name in new[]{"armor","speed","damage","heal"})
            {
                var path="Assets/StarTournament/Art/"+name+"-pickup-crystal-runes-lod0.glb";
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(asset,Is.Not.Null,path);
                var instance=Object.Instantiate(asset);
                try
                {
                    Assert.That(instance.GetComponentsInChildren<Collider>().Length,Is.Zero,name);
                    var renderers=instance.GetComponentsInChildren<Renderer>();
                    Assert.That(renderers.Length,Is.GreaterThan(2),name);
                    var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    Assert.That(bounds.size.y,Is.InRange(.75f,1.1f),name+" must stand upright without dominating the view");
                    Assert.That(bounds.size.z,Is.InRange(.09f,.18f),name+" must be slim but still two-sided");
                    var names=System.Array.ConvertAll(instance.GetComponentsInChildren<Transform>(),t=>t.name);
                    Assert.That(System.Array.Exists(names,n=>n.EndsWith("-front")),Is.True,name);
                    Assert.That(System.Array.Exists(names,n=>n.EndsWith("-rear")),Is.True,name);
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }
        [Test] public void FullArmorConsumesPickupAndItReturnsOnExactCooldown()
        {
            var profile=ProvingProfile.CreateCombatDefault();var life=new CombatLife("p1",profile);life.GrantArmor(100);
            var pickup=new ArmorPickup(Vector3.zero,profile);Assert.That(pickup.TryCollect(Vector3.zero,life),Is.True);Assert.That(life.Read().Armor,Is.EqualTo(100));Assert.That(pickup.Read().Available,Is.False);
            pickup.Advance(29);Assert.That(pickup.Read().Available,Is.False);pickup.Advance(1);Assert.That(pickup.Read().Available,Is.True);
        }
        [Test] public void SnapshotJsonRestoresCooldownWithoutAliasing()
        {
            var profile=ProvingProfile.CreateCombatDefault();var pickup=new ArmorPickup(Vector3.zero,profile);var life=new CombatLife("p1",profile);
            pickup.TryCollect(Vector3.zero,life);pickup.Advance(5);var snapshot=JsonUtility.FromJson<ArmorPickupState>(JsonUtility.ToJson(pickup.Read()));pickup.Advance(25);
            var restored=new ArmorPickup(Vector3.zero,profile);restored.Restore(snapshot);Assert.That(restored.Read().Available,Is.False);restored.Advance(25);Assert.That(restored.Read().Available,Is.True);
        }
    }
}
