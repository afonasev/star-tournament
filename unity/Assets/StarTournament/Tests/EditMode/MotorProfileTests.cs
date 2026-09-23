using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class MotorProfileTests
    {
        [Test]
        public void DefaultProfile_HasCompleteValidDescriptorRegistry()
        {
            var profile = ProvingProfile.CreateDefault();

            Assert.That(profile.Descriptors.Count, Is.GreaterThan(20));
            Assert.That(profile.Validate(), Is.Empty);
            Assert.That(profile.Get("player.movement.maximumGroundSpeed"), Is.EqualTo(9f));
            Assert.That(profile.Get("camera.eyeHeight"), Is.EqualTo(1.55f));
            Assert.That(profile.Get("world.minimumSupportHeight"), Is.EqualTo(-10f));
        }

        [Test]
        public void Descriptor_RejectsInvalidStep()
        {
            var descriptor = new NumericDescriptor
            {
                Path = "test.value", Group = "test", Label = "Test value", Description = "Test.", Unit = "count",
                Minimum = 0f, Maximum = 1f, Step = 0f, DefaultValue = .5f,
            };

            Assert.That(descriptor.Validate(out var reason), Is.False);
            Assert.That(reason, Does.Contain("Step"));
        }

        [Test]
        public void Profile_ReportsOutOfRangeValueAtItsPath()
        {
            var profile = ProvingProfile.CreateDefault();
            profile.Set("player.movement.jumpSpeed", 31f);

            var hasJumpSpeedIssue = false;
            foreach (var issue in profile.Validate())
                hasJumpSpeedIssue |= issue.Path == "player.movement.jumpSpeed";
            Assert.That(hasJumpSpeedIssue, Is.True);
        }

        [Test]
        public void Profile_RoundTripsThroughUnityJson()
        {
            var profile = ProvingProfile.CreateDefault();
            var restored = JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile));

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Validate(), Is.Empty);
            Assert.That(restored.Get("weapon.probeCooldown"), Is.EqualTo(.7f));
        }
    }
}
