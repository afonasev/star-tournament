using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ArmorPickupTests
    {
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
