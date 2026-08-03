using System;
using NUnit.Framework;

namespace ND.UI.Loading.Tests
{
    public sealed class LoadingTipSelectorTests
    {
        [Test]
        public void EmptyOrNullTips_ReturnFallback()
        {
            Assert.That(new LoadingTipSelector(null, "fallback").GetNextTip(), Is.EqualTo("fallback"));
            Assert.That(new LoadingTipSelector(Array.Empty<string>(), "fallback").GetNextTip(), Is.EqualTo("fallback"));
        }

        [Test]
        public void Normalization_RemovesWhitespaceAndOrdinalDuplicates()
        {
            var selector = new LoadingTipSelector(
                new[] { null, "", " ", "same", "same" },
                "fallback",
                new Random(1));

            for (var index = 0; index < 5; index++)
            {
                Assert.That(selector.GetNextTip(), Is.EqualTo("same"));
            }
        }

        [Test]
        public void MultipleTips_NeverRepeatImmediately()
        {
            var selector = new LoadingTipSelector(
                new[] { "one", "two", "three" },
                "fallback",
                new Random(7));
            var previous = selector.GetNextTip();

            for (var index = 0; index < 100; index++)
            {
                var current = selector.GetNextTip();
                Assert.That(current, Is.Not.EqualTo(previous));
                previous = current;
            }
        }

        [Test]
        public void InjectedRandom_DeterminesSelectionInsteadOfRegistrationCycle()
        {
            var selector = new LoadingTipSelector(
                new[] { "first", "second", "third" },
                "fallback",
                new FixedRandom(2, 0));

            Assert.That(selector.GetNextTip(), Is.EqualTo("third"));
            Assert.That(selector.GetNextTip(), Is.EqualTo("first"));
        }

        private sealed class FixedRandom : Random
        {
            private readonly int[] values;
            private int index;

            public FixedRandom(params int[] values)
            {
                this.values = values;
            }

            public override int Next(int maxValue)
            {
                return values[index++ % values.Length] % maxValue;
            }
        }
    }
}
