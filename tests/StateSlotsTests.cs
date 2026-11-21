using System.Text.RegularExpressions;
using PeglinRelicRerolls.Core;
using Xunit;

namespace PeglinRelicRerolls.Tests
{
    public class StateSlotsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NoSlot_IsTheDefaultFile(string slot)
        {
            Assert.Equal("PeglinRelicRerolls.state", StateSlots.FileName(slot));
        }

        [Theory]
        [InlineData(true, false, true)]    // default slot, new file missing: read the pre-rename file
        [InlineData(true, true, false)]    // default slot, new file there: use it
        [InlineData(false, false, false)]  // another slot never touches the legacy file
        [InlineData(false, true, false)]
        public void ReadLegacyFile_OnlyForTheDefaultSlotWhenItsFileIsMissing(bool isDefault, bool primaryExists, bool expected)
        {
            Assert.Equal(expected, StateSlots.ReadLegacyFile(isDefault, primaryExists));
        }

        [Fact]
        public void DefaultFile_IsTheNameVersion101Used()
        {
            // Existing players have this exact file; renaming it would lose their state.
            Assert.Equal("PeglinRelicRerolls.state", StateSlots.DefaultFileName);
        }

        [Theory]
        [InlineData("bossrush", "PeglinRelicRerolls.bossrush.state")]
        [InlineData("boss-rush_2", "PeglinRelicRerolls.boss-rush_2.state")]
        public void PlainSlot_GetsItsOwnFile(string slot, string expected)
        {
            Assert.Equal(expected, StateSlots.FileName(slot));
        }

        [Theory]
        [InlineData("..\\evil")]
        [InlineData("../evil")]
        [InlineData("a/b")]
        [InlineData("C:\\x")]
        [InlineData("x.y")]
        [InlineData("na\0me")]
        [InlineData("Boss Rush")]
        [InlineData("émoji✓")]
        [InlineData("CON")]
        public void AnySlot_ProducesOnlySafeCharacters(string slot)
        {
            string safe = StateSlots.Sanitise(slot);

            Assert.Matches("^[a-z0-9_-]+$", safe);
            Assert.DoesNotContain("..", StateSlots.FileName(slot).Replace("PeglinRelicRerolls.", "").Replace(".state", ""));
            Assert.Matches(@"^PeglinRelicRerolls\.[a-z0-9_-]+\.state$", StateSlots.FileName(slot));
        }

        [Fact]
        public void NamesThatSanitiseToTheSameText_StillGetDifferentFiles()
        {
            Assert.NotEqual(StateSlots.FileName("a b"), StateSlots.FileName("a_b"));
            Assert.NotEqual(StateSlots.FileName("a.b"), StateSlots.FileName("a/b"));
            Assert.NotEqual(StateSlots.FileName("a b"), StateSlots.FileName("a?b"));
        }

        [Fact]
        public void NamesThatDifferOnlyByCase_GetDifferentFiles()
        {
            // Windows file names are case-insensitive.
            Assert.NotEqual(StateSlots.FileName("Run"), StateSlots.FileName("run"));
            Assert.NotEqual(StateSlots.FileName("Run"), StateSlots.FileName("RUN"));
        }

        [Fact]
        public void ChangedName_KeepsItsReadableStart()
        {
            Assert.StartsWith("boss_rush-", StateSlots.Sanitise("Boss Rush"));
        }

        [Fact]
        public void LongName_IsCut_AndStillUnique()
        {
            string a = new string('a', 200) + "1";
            string b = new string('a', 200) + "2";

            Assert.True(StateSlots.Sanitise(a).Length <= 64 + 1 + 8);
            Assert.NotEqual(StateSlots.FileName(a), StateSlots.FileName(b));
        }

        [Fact]
        public void SameName_AlwaysGivesTheSameFile()
        {
            Assert.Equal(StateSlots.FileName("Boss Rush"), StateSlots.FileName("Boss Rush"));
        }

        [Fact]
        public void OnlyIllegalCharacters_StillGetsAFile_NotTheDefault()
        {
            Assert.NotEqual(StateSlots.DefaultFileName, StateSlots.FileName("!!!"));
            Assert.NotEqual(StateSlots.FileName("!!!"), StateSlots.FileName("???"));
        }

        [Fact]
        public void NoSlotName_CanEverProduceTheDefaultFile()
        {
            foreach (string slot in new[] { "state", "default", "x", "-", "_" })
            {
                Assert.NotEqual(StateSlots.DefaultFileName, StateSlots.FileName(slot));
            }
        }
    }
}
