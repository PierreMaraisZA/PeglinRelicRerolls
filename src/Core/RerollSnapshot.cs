using System;
using System.Globalization;

namespace PeglinRelicRerolls.Core
{
    /// <summary>The reroll state saved alongside a run: "seed|count|used".</summary>
    public readonly struct RerollSnapshot
    {
        public RerollSnapshot(string seed, int count, int used)
        {
            Seed = seed ?? string.Empty;
            Count = count;
            Used = used;
        }

        public string Seed { get; }

        public int Count { get; }

        public int Used { get; }

        public string Serialize()
        {
            return string.Join("|",
                Seed,
                Count.ToString(CultureInfo.InvariantCulture),
                Used.ToString(CultureInfo.InvariantCulture));
        }

        public static bool TryParse(string text, out RerollSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            // Read the numbers from the right so a seed that happens to contain '|' still parses.
            string trimmed = text.Trim();
            int usedSeparator = trimmed.LastIndexOf('|');
            int countSeparator = usedSeparator > 0 ? trimmed.LastIndexOf('|', usedSeparator - 1) : -1;
            if (countSeparator < 0
                || !int.TryParse(trimmed.Substring(countSeparator + 1, usedSeparator - countSeparator - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                || !int.TryParse(trimmed.Substring(usedSeparator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int used)
                || count < 0
                || used < 0)
            {
                return false;
            }
            snapshot = new RerollSnapshot(trimmed.Substring(0, countSeparator), count, used);
            return true;
        }

        /// <summary>
        /// Picks the count to use when a run is continued. A snapshot only applies to the run it was
        /// saved from; anything else (missing, corrupt, another run's seed) falls back to the starting count.
        /// </summary>
        public static RerollSnapshot ResolveOnContinue(string savedText, string currentSeed, int startingCount)
        {
            if (TryParse(savedText, out RerollSnapshot saved)
                && string.Equals(saved.Seed, currentSeed ?? string.Empty, StringComparison.Ordinal))
            {
                return saved;
            }
            return new RerollSnapshot(currentSeed, Math.Max(0, startingCount), 0);
        }
    }
}
