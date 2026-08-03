using NUnit.Framework;

namespace ND.UI.Loading.Tests
{
    public sealed class LoadingProgressTests
    {
        [TestCase(0f, 0.2f)]
        [TestCase(0.5f, 0.6f)]
        [TestCase(1f, 1f)]
        [TestCase(-1f, 0.2f)]
        [TestCase(2f, 1f)]
        public void MapSceneProgress_MapsAndClamps(float input, float expected)
        {
            Assert.That(LoadingProgress.MapSceneProgress(input), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void MapSceneProgress_NonFiniteFallsBackToPreparationBoundary()
        {
            Assert.That(LoadingProgress.MapSceneProgress(float.NaN), Is.EqualTo(0.2f));
            Assert.That(LoadingProgress.MapSceneProgress(float.PositiveInfinity), Is.EqualTo(0.2f));
        }

        [Test]
        public void MoveDisplayedProgress_ClampsAndNeverDecreases()
        {
            Assert.That(LoadingProgress.MoveDisplayedProgress(0.7f, 0.2f, 1f), Is.EqualTo(0.7f));
            Assert.That(LoadingProgress.MoveDisplayedProgress(0.2f, 2f, 5f), Is.EqualTo(1f));
            Assert.That(LoadingProgress.MoveDisplayedProgress(float.NaN, 0.5f, 0.1f), Is.EqualTo(0.1f));
            Assert.That(LoadingProgress.MoveDisplayedProgress(0.2f, 0.5f, float.NaN), Is.EqualTo(0.2f));
        }
    }
}
