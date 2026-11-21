using System.Collections.Generic;
using PeglinRelicRerolls.Core;
using Xunit;

namespace PeglinRelicRerolls.Tests
{
    /// <summary>A relic screen whose slots show different rarities rerolls each slot at its own rarity.</summary>
    public class SlotRarityTests
    {
        private const int Common = 1;
        private const int Rare = 2;

        private const int Boss = 3;

        [Fact]
        public void SlotKinds_ARarerRelicKeepsItsRarity()
        {
            Assert.Equal(new[] { Common, Rare, Common }, RerollRules.SlotKinds(new int?[] { Common, Rare, Common }, 3, Common));
        }

        [Fact]
        public void SlotKinds_TheGamesLowerRarityFallbackRerollsAtTheScreensRarity()
        {
            // A boss screen whose boss pool ran dry shows rares; a rare screen whose rare pool ran dry shows commons.
            Assert.Equal(new[] { Boss, Boss, Boss }, RerollRules.SlotKinds(new int?[] { Boss, Rare, Rare }, 3, Boss));
            Assert.Equal(new[] { Rare, Rare }, RerollRules.SlotKinds(new int?[] { Common, Rare }, 2, Rare));
        }

        [Fact]
        public void SlotKinds_EmptyOrUnknownSlots_UseTheScreensRarity()
        {
            Assert.Equal(new[] { Rare, Common, Common }, RerollRules.SlotKinds(new int?[] { Rare, null }, 3, Common));
            Assert.Equal(new[] { Common, Common }, RerollRules.SlotKinds(null, 2, Common));
            Assert.Empty(RerollRules.SlotKinds(new int?[] { Rare }, 0, Common));
            Assert.Empty(RerollRules.SlotKinds(new int?[] { Rare }, -1, Common));
        }

        [Fact]
        public void GroupSlots_CountsEachKind_InOrderOfFirstAppearance()
        {
            List<KeyValuePair<int, int>> groups = RerollRules.GroupSlots(new[] { Rare, Common, Rare, Common, Common });
            Assert.Equal(new[] { new KeyValuePair<int, int>(Rare, 2), new KeyValuePair<int, int>(Common, 3) }, groups);
        }

        [Fact]
        public void GroupSlots_SingleRarityScreen_IsOneGroup()
        {
            Assert.Single(RerollRules.GroupSlots(new[] { Common, Common, Common }));
            Assert.Empty(RerollRules.GroupSlots(new int[0]));
            Assert.Empty(RerollRules.GroupSlots(null));
        }

        [Fact]
        public void InSlotOrder_PutsEachDrawBackInItsSlot()
        {
            var drawn = new Dictionary<int, IList<string>> { { Common, new[] { "c1", "c2" } }, { Rare, new[] { "r1" } } };
            Assert.Equal(new[] { "c1", "r1", "c2" }, RerollRules.InSlotOrder(new[] { Common, Rare, Common }, drawn));
        }

        [Fact]
        public void InSlotOrder_ShortDraw_LeavesThoseSlotsOut()
        {
            var drawn = new Dictionary<int, IList<string>> { { Common, new[] { "c1" } }, { Rare, new string[0] } };
            Assert.Equal(new[] { "c1" }, RerollRules.InSlotOrder(new[] { Common, Rare, Common }, drawn));
            Assert.Empty(RerollRules.InSlotOrder(new[] { Rare }, new Dictionary<int, IList<string>>()));
            Assert.Empty(RerollRules.InSlotOrder<string>(new[] { Rare }, null));
        }

        [Fact]
        public void SafeSum_NeverOverflows_AndIgnoresNegatives()
        {
            Assert.Equal(5, RerollRules.SafeSum(new[] { 2, 3 }));
            Assert.Equal(int.MaxValue, RerollRules.SafeSum(new[] { int.MaxValue, 4 }));
            Assert.Equal(3, RerollRules.SafeSum(new[] { 3, -2 }));
            Assert.Equal(0, RerollRules.SafeSum(null));
        }
    }
}
