using System;
using System.Collections.Generic;

namespace ND.UI.Loading
{
    /// <summary>Selects normalized loading tips without consuming gameplay random state.</summary>
    public sealed class LoadingTipSelector
    {
        private readonly List<string> validTips = new List<string>();
        private readonly string fallbackTip;
        private readonly Random random;
        private int lastIndex = -1;

        public LoadingTipSelector(
            IReadOnlyList<string> tips,
            string fallbackTip,
            Random random = null)
        {
            this.fallbackTip = string.IsNullOrWhiteSpace(fallbackTip)
                ? "여행 준비를 하고 있습니다…"
                : fallbackTip;
            this.random = random ?? new Random();

            if (tips == null)
            {
                return;
            }

            var uniqueTips = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < tips.Count; index++)
            {
                var tip = tips[index];
                if (!string.IsNullOrWhiteSpace(tip) && uniqueTips.Add(tip))
                {
                    validTips.Add(tip);
                }
            }
        }

        /// <summary>Returns a tip, excluding the immediately previous distinct candidate when possible.</summary>
        public string GetNextTip()
        {
            if (validTips.Count == 0)
            {
                return fallbackTip;
            }

            if (validTips.Count == 1)
            {
                lastIndex = 0;
                return validTips[0];
            }

            if (lastIndex < 0)
            {
                lastIndex = random.Next(validTips.Count);
                return validTips[lastIndex];
            }

            var candidate = random.Next(validTips.Count - 1);
            if (candidate >= lastIndex)
            {
                candidate++;
            }
            lastIndex = candidate;
            return validTips[candidate];
        }
    }
}
