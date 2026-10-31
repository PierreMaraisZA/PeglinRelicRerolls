using System.Linq;
using PeglinRelicRerolls.Core;
using Xunit;

namespace PeglinRelicRerolls.Tests
{
    public class RerollRulesTests
    {
        private static readonly int[] DefaultLevels = { 5, 10, 15 };

        [Theory]
        [InlineData(10.0, 9.9, false)]    // 100ms after: an accidental double press, ignored
        [InlineData(10.0, 9.81, false)]   // 190ms after: still inside the window
        [InlineData(10.0, 9.8, true)]     // exactly 200ms after: allowed
        [InlineData(10.0, 9.0, true)]     // a second later: allowed
        public void DebouncePassed_Ignores_PressesWithin200ms(double now, double lastReroll, bool expected)
        {
            Assert.Equal(expected, RerollRules.DebouncePassed(now, lastReroll));
        }

        [Fact]
        public void DebouncePassed_FirstRerollIsAlwaysAllowed()
        {
            Assert.True(RerollRules.DebouncePassed(0.0, double.NegativeInfinity));
        }

        [Theory]
        [InlineData(0, 3, 3)]
        [InlineData(2, 3, 1)]
        [InlineData(3, 3, 0)]   // fourth shop reroll is blocked
        [InlineData(5, 3, 0)]   // never negative
        [InlineData(0, 0, 0)]   // limit 0 disables shop rerolls
        public void RemainingUnderLimit_CapsShopRerolls(int used, int limit, int expected)
        {
            Assert.Equal(expected, RerollRules.RemainingUnderLimit(used, limit));
        }

        [Fact]
        public void RemainingUnderLimit_NegativeLimitMeansUnlimited()
        {
            Assert.Equal(int.MaxValue, RerollRules.RemainingUnderLimit(100, -1));
        }

        [Fact]
        public void DieSlot_NeverTakesTheShopOnlyRelicTheGameAddsLast()
        {
            // The game builds shops as: rare (if rolled), commons, then the shop-only relic last
            ShopSlotKind[] shop = { ShopSlotKind.Rare, ShopSlotKind.Common, ShopSlotKind.Common, ShopSlotKind.ShopOnly };

            Assert.Equal(2, RerollRules.DieSlot(shop));
        }

        [Fact]
        public void DieSlot_PrefersACommonOverTheRare()
        {
            ShopSlotKind[] shop = { ShopSlotKind.Rare, ShopSlotKind.Common };

            Assert.Equal(1, RerollRules.DieSlot(shop));
        }

        [Fact]
        public void DieSlot_FallsBackToTheRareWhenThereAreNoCommons()
        {
            ShopSlotKind[] shop = { ShopSlotKind.Rare, ShopSlotKind.ShopOnly };

            Assert.Equal(0, RerollRules.DieSlot(shop));
        }

        [Theory]
        [InlineData(new ShopSlotKind[0])]
        [InlineData(new[] { ShopSlotKind.ShopOnly })]
        [InlineData(new[] { ShopSlotKind.ShopOnly, ShopSlotKind.Other })]
        public void DieSlot_NoSlotToGiveUp_ReturnsMinusOne(ShopSlotKind[] shop)
        {
            Assert.Equal(-1, RerollRules.DieSlot(shop));
        }

        [Theory]
        [InlineData(false, false, 20)]
        [InlineData(true, false, 40)]   // Wand of Greed
        [InlineData(false, true, 40)]   // act 4
        [InlineData(true, true, 80)]    // both, same as the game's relic prices
        public void ShopRerollCost_DoublesLikeRelicPrices(bool wandOfGreed, bool act4, int expected)
        {
            Assert.Equal(expected, RerollRules.ShopRerollCost(20, wandOfGreed, act4));
        }

        [Fact]
        public void ShopRerollCost_NegativeBaseIsFree()
        {
            Assert.Equal(0, RerollRules.ShopRerollCost(-5, true, true));
        }

        [Fact]
        public void ParsePenaltyLevels_DefaultConfig()
        {
            Assert.Equal(new[] { 5, 10, 15 }, RerollRules.ParsePenaltyLevels("5,10,15"));
        }

        [Fact]
        public void ParsePenaltyLevels_IgnoresJunkBlanksDuplicatesAndNonPositive()
        {
            Assert.Equal(new[] { 5, 10 }, RerollRules.ParsePenaltyLevels(" 5, x ,, -3, 0, 10, 10 "));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void ParsePenaltyLevels_Empty_ReturnsNoLevels(string text)
        {
            Assert.Empty(RerollRules.ParsePenaltyLevels(text));
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(4, 5)]
        [InlineData(5, 4)]
        [InlineData(9, 4)]
        [InlineData(10, 3)]
        [InlineData(14, 3)]
        [InlineData(15, 2)]
        [InlineData(20, 2)]
        public void StartingCount_PenaltiesStackWithCruciballLevel(int cruciballLevel, int expected)
        {
            Assert.Equal(expected, RerollRules.StartingCount(5, 1, DefaultLevels, cruciballLevel));
        }

        [Fact]
        public void StartingCount_PenaltyDisabled_KeepsBaseCount()
        {
            Assert.Equal(5, RerollRules.StartingCount(5, 0, DefaultLevels, 20));
        }

        [Fact]
        public void StartingCount_NegativePenalty_IsTreatedAsZero()
        {
            // A misconfigured negative penalty must not grant extra rerolls
            Assert.Equal(5, RerollRules.StartingCount(5, -2, DefaultLevels, 20));
        }

        [Fact]
        public void StartingCount_NeverBelowZero()
        {
            Assert.Equal(0, RerollRules.StartingCount(1, 1, DefaultLevels, 15));
        }

        [Theory]
        [InlineData(5, true)]
        [InlineData(10, true)]
        [InlineData(15, true)]
        [InlineData(4, false)]
        [InlineData(11, false)]
        public void IsPenaltyLevel_OnlyExactLevels(int level, bool expected)
        {
            Assert.Equal(expected, RerollRules.IsPenaltyLevel(1, DefaultLevels, level));
        }

        [Fact]
        public void IsPenaltyLevel_PenaltyDisabled_IsFalse()
        {
            Assert.False(RerollRules.IsPenaltyLevel(0, DefaultLevels, 5));
        }

        [Fact]
        public void UpgradeRoll_SameInputs_SameResult()
        {
            Assert.Equal(RerollRules.UpgradeRoll("SEED", 3), RerollRules.UpgradeRoll("SEED", 3));
        }

        [Fact]
        public void UpgradeRoll_DifferentRerollNumber_DifferentResult()
        {
            Assert.NotEqual(RerollRules.UpgradeRoll("SEED", 1), RerollRules.UpgradeRoll("SEED", 2));
        }

        [Fact]
        public void UpgradeRoll_NullSeed_DoesNotThrow()
        {
            double roll = RerollRules.UpgradeRoll(null, 0);

            Assert.InRange(roll, 0.0, 0.9999999999);
        }

        [Fact]
        public void UpgradeRoll_IsInRangeAndRoughlyUniform()
        {
            // Over many seeds the roll should behave like a fair [0, 1) random number,
            // so a 15% upgrade chance really triggers about 15% of the time.
            double[] rolls = Enumerable.Range(0, 20000)
                .Select(i => RerollRules.UpgradeRoll("SEED" + (i / 10), i % 10))
                .ToArray();

            Assert.All(rolls, roll => Assert.InRange(roll, 0.0, 0.9999999999));
            Assert.InRange(rolls.Average(), 0.48, 0.52);
            Assert.InRange(rolls.Count(roll => roll < 0.15) / (double)rolls.Length, 0.14, 0.16);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(5, 0)]   // never negative
        public void RemainingOnScreen_BossRewardIsCapped(int usedOnScreen, int expected)
        {
            Assert.Equal(expected, RerollRules.RemainingOnScreen(isBossReward: true, usedOnScreen, bossLimit: 1));
        }

        [Fact]
        public void RemainingOnScreen_NonBossScreensAreUncapped()
        {
            Assert.Equal(int.MaxValue, RerollRules.RemainingOnScreen(isBossReward: false, usedOnScreen: 50, bossLimit: 1));
        }

        [Fact]
        public void RemainingOnScreen_NegativeLimitMeansUncapped()
        {
            Assert.Equal(int.MaxValue, RerollRules.RemainingOnScreen(isBossReward: true, usedOnScreen: 3, bossLimit: -1));
        }

        [Fact]
        public void RemainingOnScreen_ZeroLimitBlocksBossRerolls()
        {
            Assert.Equal(0, RerollRules.RemainingOnScreen(isBossReward: true, usedOnScreen: 0, bossLimit: 0));
        }

        [Theory]
        [InlineData(3, 1, 5, true)]
        [InlineData(0, 1, 5, false)]                  // no rerolls left
        [InlineData(3, 0, 5, false)]                  // boss screen cap reached
        [InlineData(3, int.MaxValue, 0, false)]       // screen's pool exhausted
        [InlineData(3, int.MaxValue, 1, true)]        // one unseen relic is enough
        public void CanReroll_NeedsRerollsRoomAndUnseenRelics(int rerollsLeft, int remainingOnScreen, int unseen, bool expected)
        {
            Assert.Equal(expected, RerollRules.CanReroll(rerollsLeft, remainingOnScreen, unseen));
        }

        [Theory]
        [InlineData(4, 6, 4)]   // unseen relics left: use them
        [InlineData(0, 3, 3)]   // all seen but others exist: recycle
        [InlineData(0, 0, 0)]   // pool truly empty (owns everything): disabled
        [InlineData(0, -1, 0)]
        public void AvailableToReroll_RecyclesBeforeGivingUp(int unseen, int notOnScreen, int expected)
        {
            Assert.Equal(expected, RerollRules.AvailableToReroll(unseen, notOnScreen));
        }

        [Theory]
        [InlineData(0, 3, true)]
        [InlineData(2, 3, false)]   // still unseen relics, keep the list
        [InlineData(0, 0, false)]   // nothing to recycle into
        public void ShouldRecycleSeen_OnlyWhenUnseenRunOutButOthersRemain(int unseen, int notOnScreen, bool expected)
        {
            Assert.Equal(expected, RerollRules.ShouldRecycleSeen(unseen, notOnScreen));
        }

        [Fact]
        public void AvailableTier_UpgradeWithEmptyPool_StaysAtChestTier()
        {
            // A collector who owns every boss relic can't be handed one by a chest upgrade
            Assert.Equal(ChestTier.Rare, RerollRules.AvailableTier(ChestTier.Rare, ChestTier.Boss, unseenInRolledPool: 0));
        }

        [Fact]
        public void AvailableTier_UpgradeWithRelicsLeft_Upgrades()
        {
            Assert.Equal(ChestTier.Boss, RerollRules.AvailableTier(ChestTier.Rare, ChestTier.Boss, unseenInRolledPool: 2));
        }

        [Fact]
        public void AvailableTier_NoUpgrade_IsUnchanged()
        {
            Assert.Equal(ChestTier.Common, RerollRules.AvailableTier(ChestTier.Common, ChestTier.Common, unseenInRolledPool: 0));
        }

        [Theory]
        [InlineData(ChestTier.Common, 0.10, ChestTier.Rare)]
        [InlineData(ChestTier.Common, 0.15, ChestTier.Common)] // boundary: roll must be below the chance
        [InlineData(ChestTier.Common, 0.90, ChestTier.Common)]
        [InlineData(ChestTier.Rare, 0.04, ChestTier.Boss)]
        [InlineData(ChestTier.Rare, 0.05, ChestTier.Rare)]
        [InlineData(ChestTier.Rare, 0.10, ChestTier.Rare)]     // the rare-chance doesn't apply to rare chests
        [InlineData(ChestTier.Boss, 0.00, ChestTier.Boss)]     // nothing above boss
        public void ChestRerollTier_UpgradesAtMostOneTier(ChestTier chest, double roll, ChestTier expected)
        {
            Assert.Equal(expected, RerollRules.ChestRerollTier(chest, roll, rareChance: 0.15, bossChance: 0.05));
        }

        [Fact]
        public void ChestRerollTier_CommonNeverJumpsToBoss()
        {
            Assert.Equal(ChestTier.Rare, RerollRules.ChestRerollTier(ChestTier.Common, 0.0, 1.0, 1.0));
        }
    }
}
