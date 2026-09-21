using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class SpeedPickupTests
    {
        [Test] public void InitialDelayThenCollectionUsesPostCollectionCooldown()
        {
            var profile=ProvingProfile.CreateCombatDefault();var pickup=new SpeedPickup(Vector3.zero,profile);
            Assert.That(pickup.Read().Available,Is.False);pickup.Advance(10);Assert.That(pickup.Read().Available,Is.True);
            Assert.That(pickup.TryCollect(Vector3.zero),Is.True);pickup.Advance(19.9);Assert.That(pickup.Read().Available,Is.False);pickup.Advance(.1);Assert.That(pickup.Read().Available,Is.True);
        }
        [Test] public void SnapshotRestoresDelayedStateAndEffectValues()
        {
            var profile=ProvingProfile.CreateCombatDefault();var pickup=new SpeedPickup(new Vector3(2,0,3),profile);pickup.Advance(3);
            var snapshot=JsonUtility.FromJson<SpeedPickupState>(JsonUtility.ToJson(pickup.Read()));var restored=new SpeedPickup(new Vector3(2,0,3),profile);restored.Restore(snapshot);
            Assert.That(restored.Read().Remaining,Is.EqualTo(7).Within(.0001));Assert.That(restored.Duration,Is.EqualTo(10));Assert.That(restored.Multiplier,Is.EqualTo(1.5f));
        }
    }
}
