using System;
using System.Collections.Generic;
using System.Linq;

namespace PeglinRelicRerolls.Core
{
    /// <summary>Mirrors the game's relic rarities that matter for chest rerolls.</summary>
    public enum ChestTier
    {
        Common,
        Rare,
        Boss,
    }

    /// <summary>What a shop relic slot holds, for choosing which one the reroll die replaces.</summary>
    public enum ShopSlotKind
    {
        Common,
        Rare,
        Boss,
        ShopOnly,
        Other,
    }

    /// <summary>Game-rule maths for rerolls. Pure C# (no Unity/BepInEx) so it can be unit tested.</summary>
    public static class RerollRules
    {
        /// <summary>
        /// The shop slot the reroll die takes: the last common relic, otherwise the last rare/boss relic. Never the
        /// class's shop-only relic (those can only be found in shops). -1 means the shop has no slot to give up.
        /// </summary>
        public static int DieSlot(IReadOnlyList<ShopSlotKind> slots)
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i] == ShopSlotKind.Common)
                {
                    return i;
                }
            }
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                if (slots[i] == ShopSlotKind.Rare || slots[i] == ShopSlotKind.Boss)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The die's price, doubled by the same things that double shop relic prices (Wand of Greed, act 4).</summary>
        public static int ShopRerollCost(int baseCost, bool wandOfGreed, bool act4)
        {
            return Math.Max(0, baseCost) * (wandOfGreed ? 2 : 1) * (act4 ? 2 : 1);
        }

        /// <summary>Parses "5,10,15" into penalty levels, ignoring blanks, junk and non-positive values.</summary>
        public static int[] ParsePenaltyLevels(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new int[0];
            }
            return text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => int.TryParse(part.Trim(), out int level) ? level : 0)
                .Where(level => level > 0)
                .Distinct()
                .ToArray();
        }

        /// <summary>Cruciball levels are cumulative, so a level 12 run includes the level 5 and 10 penalties.</summary>
        public static int StartingCount(int baseCount, int penaltyPerLevel, int[] penaltyLevels, int cruciballLevel)
        {
            int levelsReached = penaltyLevels.Count(level => level <= cruciballLevel);
            return Math.Max(0, baseCount - levelsReached * Math.Max(0, penaltyPerLevel));
        }

        public static bool IsPenaltyLevel(int penaltyPerLevel, int[] penaltyLevels, int cruciballLevel)
        {
            return penaltyPerLevel > 0 && penaltyLevels.Contains(cruciballLevel);
        }

        /// <summary>
        /// Deterministic roll in [0, 1) for a run seed and reroll number, so quitting and continuing gives
        /// the same chest result. Uses FNV-1a + SplitMix64 rather than System.Random or string.GetHashCode,
        /// whose output can differ between .NET runtimes.
        /// </summary>
        public static double UpgradeRoll(string seed, int rerollNumber)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char c in seed ?? string.Empty)
                {
                    hash = (hash ^ c) * 1099511628211UL;
                }
                hash = (hash ^ (ulong)rerollNumber) * 1099511628211UL;

                ulong z = hash + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (z >> 11) * (1.0 / (1UL << 53));
            }
        }

        /// <summary>
        /// Rerolls still allowed on the current relic screen. Only the floor boss reward is capped;
        /// a negative limit means uncapped.
        /// </summary>
        public static int RemainingOnScreen(bool isBossReward, int usedOnScreen, int bossLimit)
        {
            return isBossReward ? RemainingUnderLimit(usedOnScreen, bossLimit) : int.MaxValue;
        }

        /// <summary>Minimum time between two rerolls, so a double tap or key bounce can't spend two at once.</summary>
        public const double RerollDebounceSeconds = 0.2;

        /// <summary>True if a reroll at <paramref name="now"/> is far enough after the last one to go through.</summary>
        public static bool DebouncePassed(double now, double lastReroll)
        {
            // Tiny tolerance so exactly 200ms counts despite floating-point rounding (10.0 - 9.8 is 0.19999...).
            return now - lastReroll >= RerollDebounceSeconds - 1e-9;
        }

        /// <summary>Rerolls left under a per-screen limit (e.g. 3 per shop). A negative limit means no limit.</summary>
        public static int RemainingUnderLimit(int used, int limit)
        {
            return limit < 0 ? int.MaxValue : Math.Max(0, limit - used);
        }

        /// <summary>
        /// A reroll needs a reroll to spend, room under the screen's cap, and at least one relic left that
        /// hasn't been seen on this screen in the screen's own rarity. The last rule stops a player who has
        /// exhausted a pool from rerolling a screen purely to fish for chest upgrades.
        /// </summary>
        public static bool CanReroll(int rerollsLeft, int remainingOnScreen, int unseenInScreenPool)
        {
            return rerollsLeft > 0 && remainingOnScreen > 0 && unseenInScreenPool > 0;
        }

        /// <summary>
        /// Relics a reroll could still draw from on this screen. Unseen relics first; once they're all used up,
        /// the do-not-offer list is recycled, so anything not currently on screen counts. Zero means the pool
        /// itself is empty (e.g. the player owns everything) and rerolling is disabled.
        /// </summary>
        public static int AvailableToReroll(int unseenInPool, int notOnScreenInPool)
        {
            return unseenInPool > 0 ? unseenInPool : Math.Max(0, notOnScreenInPool);
        }

        /// <summary>Recycle the do-not-offer list when nothing unseen is left but other relics still exist.</summary>
        public static bool ShouldRecycleSeen(int unseenInPool, int notOnScreenInPool)
        {
            return unseenInPool <= 0 && notOnScreenInPool > 0;
        }

        /// <summary>A chest upgrade only applies if the upgraded pool still has an unseen relic.</summary>
        public static ChestTier AvailableTier(ChestTier chest, ChestTier rolled, int unseenInRolledPool)
        {
            return rolled != chest && unseenInRolledPool <= 0 ? chest : rolled;
        }

        /// <summary>A chest reroll may upgrade one tier: common to rare, or rare to boss.</summary>
        public static ChestTier ChestRerollTier(ChestTier chest, double roll, double rareChance, double bossChance)
        {
            if (chest == ChestTier.Common && roll < rareChance)
            {
                return ChestTier.Rare;
            }
            if (chest == ChestTier.Rare && roll < bossChance)
            {
                return ChestTier.Boss;
            }
            return chest;
        }
    }
}
