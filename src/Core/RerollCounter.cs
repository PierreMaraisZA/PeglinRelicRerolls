using System;

namespace PeglinRelicRerolls.Core
{
    /// <summary>
    /// The reroll count for one run. Pure C# (no Unity/BepInEx) so it can be unit tested.
    /// </summary>
    public class RerollCounter
    {
        public int Count { get; private set; }

        /// <summary>Rerolls spent this run; seeds the chest upgrade roll so reloading can't re-roll it.</summary>
        public int Used { get; private set; }

        public void Reset(int startingCount)
        {
            Count = Math.Max(0, startingCount);
            Used = 0;
        }

        public void Add(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend to remove rerolls.");
            }
            Count += amount;
        }

        public bool TrySpend()
        {
            if (Count <= 0)
            {
                return false;
            }
            Count--;
            Used++;
            return true;
        }

        /// <summary>Gives back a reroll that was spent but produced nothing usable.</summary>
        public void Refund()
        {
            if (Used <= 0)
            {
                return;
            }
            Count++;
            Used--;
        }

        public RerollSnapshot Snapshot(string seed)
        {
            return new RerollSnapshot(seed, Count, Used);
        }

        public void Restore(RerollSnapshot snapshot)
        {
            Count = Math.Max(0, snapshot.Count);
            Used = Math.Max(0, snapshot.Used);
        }
    }
}
