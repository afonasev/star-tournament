using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class FpsAverageTests
    {
        [Test] public void UsesFramesOverElapsedTimeAndIncludesSlowFrames()
        {
            var average = new FpsAverage();
            Assert.That(average.Add(0.1), Is.False);
            Assert.That(average.Add(0.4), Is.True);
            Assert.That(average.Value, Is.EqualTo(4).Within(0.0001));
            Assert.That(average.Add(1), Is.True);
            Assert.That(average.Value, Is.EqualTo(1));
        }
        [Test] public void RejectsInvalidIntervalsWithoutContaminatingWindow()
        {
            var average = new FpsAverage();
            foreach(var dt in new[]{0d, -1d, double.NaN, double.PositiveInfinity})
                Assert.That(average.Add(dt), Is.False);
            Assert.That(average.Add(0.5), Is.True);
            Assert.That(average.Value, Is.EqualTo(2));
        }
    }
}
