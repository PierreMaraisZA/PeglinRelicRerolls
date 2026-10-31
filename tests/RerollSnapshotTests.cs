using PeglinRelicRerolls.Core;
using Xunit;

namespace PeglinRelicRerolls.Tests
{
    public class RerollSnapshotTests
    {
        [Fact]
        public void SerializeThenParse_RoundTrips()
        {
            RerollSnapshot original = new RerollSnapshot("ABC123", 3, 7);

            bool parsed = RerollSnapshot.TryParse(original.Serialize(), out RerollSnapshot result);

            Assert.True(parsed);
            Assert.Equal("ABC123", result.Seed);
            Assert.Equal(3, result.Count);
            Assert.Equal(7, result.Used);
        }

        [Fact]
        public void TryParse_SeedContainingSeparator_StillParses()
        {
            RerollSnapshot original = new RerollSnapshot("a|b", 2, 1);

            bool parsed = RerollSnapshot.TryParse(original.Serialize(), out RerollSnapshot result);

            Assert.True(parsed);
            Assert.Equal("a|b", result.Seed);
            Assert.Equal(2, result.Count);
            Assert.Equal(1, result.Used);
        }

        [Fact]
        public void TryParse_EmptySeed_Parses()
        {
            bool parsed = RerollSnapshot.TryParse("|4|0", out RerollSnapshot result);

            Assert.True(parsed);
            Assert.Equal(string.Empty, result.Seed);
            Assert.Equal(4, result.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("2")]              // v1 format: count only
        [InlineData("SEED|2")]         // missing used
        [InlineData("SEED|-1|0")]      // negative count
        [InlineData("SEED|2|-1")]      // negative used
        [InlineData("SEED|two|0")]     // not a number
        [InlineData("SEED|2|0x")]
        public void TryParse_InvalidText_Fails(string text)
        {
            Assert.False(RerollSnapshot.TryParse(text, out _));
        }

        [Fact]
        public void ResolveOnContinue_MatchingSeed_UsesSavedValues()
        {
            RerollSnapshot result = RerollSnapshot.ResolveOnContinue("SEED|1|4", "SEED", startingCount: 5);

            Assert.Equal(1, result.Count);
            Assert.Equal(4, result.Used);
        }

        [Fact]
        public void ResolveOnContinue_DifferentSeed_FallsBackToStartingCount()
        {
            // A snapshot from another run must never carry over
            RerollSnapshot result = RerollSnapshot.ResolveOnContinue("OTHER|9|0", "SEED", startingCount: 3);

            Assert.Equal("SEED", result.Seed);
            Assert.Equal(3, result.Count);
            Assert.Equal(0, result.Used);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("garbage")]
        [InlineData("2")]
        public void ResolveOnContinue_MissingOrCorruptFile_FallsBackToStartingCount(string saved)
        {
            RerollSnapshot result = RerollSnapshot.ResolveOnContinue(saved, "SEED", startingCount: 4);

            Assert.Equal(4, result.Count);
            Assert.Equal(0, result.Used);
        }

        [Fact]
        public void ResolveOnContinue_NegativeStartingCount_ClampsToZero()
        {
            RerollSnapshot result = RerollSnapshot.ResolveOnContinue(null, "SEED", startingCount: -2);

            Assert.Equal(0, result.Count);
        }
    }
}
