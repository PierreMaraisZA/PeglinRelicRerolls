using System;
using PeglinRelicRerolls.Core;
using Xunit;

namespace PeglinRelicRerolls.Tests
{
    public class RerollCounterTests
    {
        [Fact]
        public void Reset_SetsStartingCountAndClearsUsed()
        {
            // Given a counter that has been used
            RerollCounter counter = new RerollCounter();
            counter.Reset(5);
            counter.TrySpend();

            // When a new run starts
            counter.Reset(4);

            // Then the count is the new starting value and nothing is marked used
            Assert.Equal(4, counter.Count);
            Assert.Equal(0, counter.Used);
        }

        [Fact]
        public void Add_AndRefund_NeverWrapPastTheLargestCount()
        {
            var counter = new RerollCounter();
            counter.Reset(int.MaxValue);   // another mod can give a huge starting count
            counter.Add(1);
            Assert.Equal(int.MaxValue, counter.Count);

            Assert.True(counter.TrySpend());
            counter.Add(5);
            counter.Refund();
            Assert.Equal(int.MaxValue, counter.Count);
            Assert.Equal(0, counter.Used);
        }

        [Fact]
        public void Reset_ClampsNegativeStartToZero()
        {
            RerollCounter counter = new RerollCounter();

            counter.Reset(-3);

            Assert.Equal(0, counter.Count);
        }

        [Fact]
        public void TrySpend_DecrementsCountAndTracksUsed()
        {
            RerollCounter counter = new RerollCounter();
            counter.Reset(2);

            bool spent = counter.TrySpend();

            Assert.True(spent);
            Assert.Equal(1, counter.Count);
            Assert.Equal(1, counter.Used);
        }

        [Fact]
        public void TrySpend_AtZero_FailsWithoutGoingNegative()
        {
            // Given a counter with no rerolls left
            RerollCounter counter = new RerollCounter();
            counter.Reset(1);
            counter.TrySpend();

            // When the player tries to reroll again
            bool spent = counter.TrySpend();

            // Then nothing is spent and the count stays at zero
            Assert.False(spent);
            Assert.Equal(0, counter.Count);
            Assert.Equal(1, counter.Used);
        }

        [Fact]
        public void Add_IncreasesCount()
        {
            RerollCounter counter = new RerollCounter();
            counter.Reset(0);

            counter.Add(1);

            Assert.Equal(1, counter.Count);
        }

        [Fact]
        public void Add_Negative_Throws()
        {
            RerollCounter counter = new RerollCounter();

            Assert.Throws<ArgumentOutOfRangeException>(() => counter.Add(-1));
        }

        [Theory]
        [InlineData(3, 2, 5)]
        [InlineData(3, -2, 1)]
        [InlineData(3, -3, 0)]
        [InlineData(3, -10, 0)]            // never below 0
        [InlineData(0, 0, 0)]
        [InlineData(int.MaxValue, 5, int.MaxValue)]   // no overflow
        [InlineData(0, int.MinValue, 0)]
        public void Adjust_AddsOrRemoves_NeverBelowZero(int start, int amount, int expected)
        {
            RerollCounter counter = new RerollCounter();
            counter.Reset(start);

            counter.Adjust(amount);

            Assert.Equal(expected, counter.Count);
        }

        [Fact]
        public void Adjust_DoesNotChangeUsed()
        {
            RerollCounter counter = new RerollCounter();
            counter.Reset(3);
            counter.TrySpend();

            counter.Adjust(-1);
            counter.Adjust(4);

            Assert.Equal(1, counter.Used);
            Assert.Equal(5, counter.Count);
        }

        [Fact]
        public void Refund_ReturnsTheSpentReroll()
        {
            // Given a reroll that turned up nothing new
            RerollCounter counter = new RerollCounter();
            counter.Reset(3);
            counter.TrySpend();

            // When it is refunded
            counter.Refund();

            // Then the player is back where they started
            Assert.Equal(3, counter.Count);
            Assert.Equal(0, counter.Used);
        }

        [Fact]
        public void Refund_WithoutASpend_DoesNothing()
        {
            // A stray refund must never create rerolls out of thin air
            RerollCounter counter = new RerollCounter();
            counter.Reset(3);

            counter.Refund();

            Assert.Equal(3, counter.Count);
            Assert.Equal(0, counter.Used);
        }

        [Fact]
        public void ShopPurchase_ThenQuitAndContinue_RollsBackToLastSave()
        {
            // Given the game saved the run on the map with 5 rerolls
            RerollCounter counter = new RerollCounter();
            counter.Reset(5);
            RerollSnapshot lastSave = counter.Snapshot("SEED");

            // When the player buys a reroll in the shop, then quits before the game saves again
            counter.Add(1);
            RerollSnapshot onDisk = RerollSnapshot.ResolveOnContinue(lastSave.Serialize(), "SEED", startingCount: 5);
            counter.Restore(onDisk);

            // Then continuing restores the saved count, so the purchase can't be repeated for free
            Assert.Equal(5, counter.Count);
        }

        [Fact]
        public void RerollThenQuitAndContinue_RestoresUsedSoTheChestRollRepeats()
        {
            // Given a save taken before a chest
            RerollCounter counter = new RerollCounter();
            counter.Reset(5);
            RerollSnapshot lastSave = counter.Snapshot("SEED");
            counter.TrySpend();
            double firstRoll = RerollRules.UpgradeRoll("SEED", counter.Used);

            // When the player quits, continues and rerolls the same chest again
            counter.Restore(lastSave);
            counter.TrySpend();
            double secondRoll = RerollRules.UpgradeRoll("SEED", counter.Used);

            // Then the upgrade roll is identical, so retrying can't fish for a better rarity
            Assert.Equal(firstRoll, secondRoll);
        }
    }
}
