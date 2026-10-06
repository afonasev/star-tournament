using System;
using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class DevelopmentNavigationReviewHandoffTests
    {
        [Test] public void ParsesCompleteAbsoluteHandoff()
        {
            Assert.That(DevelopmentNavigationReviewHandoff.TryParse("/private/tmp/arena-evidence", null, out var family), Is.True);
            Assert.That(family, Is.EqualTo(0));
        }

        [Test] public void LeavesAnAbsentHandoffInactive()
        {
            Assert.That(DevelopmentNavigationReviewHandoff.TryParse(null, null, out var family), Is.False);
            Assert.That(family, Is.Zero);
        }

        [TestCase("relative-evidence", "0")]
        [TestCase("/private/tmp/evidence", "0")]
        [TestCase("/private/tmp/evidence", "five")]
        [TestCase("/private/tmp/evidence", "-1")]
        [TestCase("/private/tmp/evidence", "5")]
        public void RejectsIncompleteOrInvalidHandoff(string directory, string family)
        {
            Assert.Throws<ArgumentException>(() => DevelopmentNavigationReviewHandoff.TryParse(directory, family, out _));
        }
    }
}
