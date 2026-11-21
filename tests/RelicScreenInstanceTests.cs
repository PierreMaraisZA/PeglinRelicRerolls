using PeglinRelicRerolls.Core;
using Xunit;

namespace PeglinRelicRerolls.Tests
{
    public class RelicScreenInstanceTests
    {
        private static readonly object ChestNode = new object();
        private static readonly object NextNode = new object();

        [Fact]
        public void MarkSeen_RemembersEveryOfferOnTheScreen()
        {
            // Given a chest that first offered "Boots"
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(ChestNode);
            screen.MarkSeen(new[] { "Boots" });

            // When the player rerolls into "Ring", then "Wand"
            screen.MarkSeen(new[] { "Ring" });
            screen.MarkSeen(new[] { "Wand" });

            // Then all three are excluded from later rerolls, not just the one on screen
            Assert.Equal(new[] { "Boots", "Ring", "Wand" }, screen.Seen);
            Assert.True(screen.HasSeen("Boots"));
        }

        [Fact]
        public void MarkSeen_IgnoresDuplicatesAndNulls()
        {
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(ChestNode);

            screen.MarkSeen(new[] { "Boots", null, "Boots" });

            Assert.Equal(new[] { "Boots" }, screen.Seen);
        }

        [Fact]
        public void Enter_SameNodeAgain_KeepsHistoryAndRerollCount()
        {
            // Given rerolls already used on this node's screen
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(ChestNode);
            screen.MarkSeen(new[] { "Boots" });
            screen.RecordReroll();

            // When the same screen is set up again on the same node (e.g. by another mod)
            screen.Enter(ChestNode);

            // Then nothing resets, so the boss limit and seen list can't be bypassed
            Assert.True(screen.HasSeen("Boots"));
            Assert.Equal(1, screen.RerollsUsed);
        }

        [Fact]
        public void Enter_DifferentNode_StartsFresh()
        {
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(ChestNode);
            screen.MarkSeen(new[] { "Boots" });
            screen.RecordReroll();

            screen.Enter(NextNode);

            Assert.Empty(screen.Seen);
            Assert.Equal(0, screen.RerollsUsed);
        }

        [Fact]
        public void NewScreen_WithoutVisitingTheMap_DoesNotInheritTheBossCap()
        {
            // Given rerolls used on a chest screen (e.g. a bonus chest, or a dev jump, that never passes the map)
            object chestScreen = new object();
            object bossScreen = new object();
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(chestScreen);
            screen.MarkSeen(new[] { "Boots" });
            screen.RecordReroll();

            // When the next relic screen opens
            screen.Enter(bossScreen);

            // Then it starts with no rerolls used, so the floor boss cap isn't already hit
            Assert.Equal(0, screen.RerollsUsed);
            Assert.Equal(1, RerollRules.RemainingOnScreen(isBossReward: true, screen.RerollsUsed, bossLimit: 1));
            Assert.Empty(screen.Seen);
        }

        private const int Common = 1;
        private const int Boss = 3;

        [Fact]
        public void ScreenKey_DifferentRarityOnSamePanel_StartsFresh()
        {
            // Given a reroll used on a common draft opened on the reward panel (Boss Rush's relic draft)
            object panel = new object();
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(new ScreenKey(panel, Common, isChest: false));
            screen.MarkSeen(new[] { "Boots" });
            screen.RecordReroll();

            // When the same panel is set up for the floor boss pick
            screen.Enter(new ScreenKey(panel, Boss, isChest: false));

            // Then the boss pick has its own reroll and an empty do-not-offer list
            Assert.Equal(0, screen.RerollsUsed);
            Assert.Equal(1, RerollRules.RemainingOnScreen(isBossReward: true, screen.RerollsUsed, bossLimit: 1));
            Assert.Empty(screen.Seen);
        }

        [Fact]
        public void ScreenKey_SameKeyAgain_KeepsHistoryAndRerollCount()
        {
            // Given a reroll used on the floor boss pick
            object panel = new object();
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(new ScreenKey(panel, Boss, isChest: false));
            screen.MarkSeen(new[] { "Crown" });
            screen.RecordReroll();

            // When the same pick is set up again (a fresh but equal key)
            screen.Enter(new ScreenKey(panel, Boss, isChest: false));

            // Then the boss limit can't be bypassed
            Assert.Equal(1, screen.RerollsUsed);
            Assert.True(screen.HasSeen("Crown"));
        }

        [Fact]
        public void ScreenKey_ChestAndBattleRewardOnSamePanel_AreDifferentScreens()
        {
            object panel = new object();
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(new ScreenKey(panel, Common, isChest: true));
            screen.MarkSeen(new[] { "Boots" });
            screen.RecordReroll();

            screen.Enter(new ScreenKey(panel, Common, isChest: false));

            Assert.Equal(0, screen.RerollsUsed);
            Assert.Empty(screen.Seen);
        }

        [Fact]
        public void ScreenKey_DifferentPanelSameSetup_AreDifferentScreens()
        {
            Assert.NotEqual(new ScreenKey(new object(), Boss, false), new ScreenKey(new object(), Boss, false));
        }

        [Fact]
        public void ScreenKey_NullPanel_EqualsItselfWithoutThrowing()
        {
            ScreenKey a = new ScreenKey(null, Common, false);
            ScreenKey b = new ScreenKey(null, Common, false);

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Clear_AfterMovingOn_StartsFreshEvenOnSameNodeKey()
        {
            // Given a screen on a node with no identity (null key, e.g. a dev-loaded scene)
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(null);
            screen.MarkSeen(new[] { "Boots" });

            // When the player moves on to the map and the next screen opens
            screen.Clear();
            screen.Enter(null);

            // Then the old relics can be offered again
            Assert.False(screen.HasSeen("Boots"));
        }

        [Fact]
        public void RecycleSeen_KeepsOnlyWhatIsOnScreenAndTheRerollCount()
        {
            // Given a screen that has cycled through every relic left
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(ChestNode);
            screen.MarkSeen(new[] { "Boots", "Ring", "Wand" });
            screen.RecordReroll();
            screen.RecordReroll();

            // When the do-not-offer list is recycled while "Wand" is showing
            screen.RecycleSeen(new[] { "Wand" });

            // Then earlier relics can come back, the one on screen can't, and the boss-limit count survives
            Assert.Equal(new[] { "Wand" }, screen.Seen);
            Assert.False(screen.HasSeen("Boots"));
            Assert.Equal(2, screen.RerollsUsed);
        }

        [Fact]
        public void OffersSomethingNew_OnlyWhenAnUnseenRelicIsOffered()
        {
            RelicScreenInstance<string> screen = new RelicScreenInstance<string>();
            screen.Enter(ChestNode);
            screen.MarkSeen(new[] { "Boots", "Ring" });

            Assert.False(screen.OffersSomethingNew(new[] { "Boots", "Ring" }));
            Assert.False(screen.OffersSomethingNew(new string[0]));
            Assert.True(screen.OffersSomethingNew(new[] { "Boots", "Wand" }));
        }
    }
}
