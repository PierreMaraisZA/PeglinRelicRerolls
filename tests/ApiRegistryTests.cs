using System;
using System.Collections.Generic;
using PeglinRelicRerolls.Core;
using Xunit;

namespace PeglinRelicRerolls.Tests
{
    public class ApiRegistryTests
    {
        private readonly List<string> _warnings = new List<string>();
        private readonly ApiRegistry _registry;

        public ApiRegistryTests()
        {
            _registry = new ApiRegistry(_warnings.Add);
        }

        // ---- Nothing registered ----

        [Fact]
        public void NothingRegistered_NoOpinionAnywhere()
        {
            var ints = _registry.Create<Func<int?>>("ints");
            var texts = _registry.Create<Func<string>>("texts");
            var filters = _registry.Create<Func<bool>>("filters");

            Assert.Null(ints.FirstNonNull(f => f()));
            Assert.Null(ints.SmallestLimit(f => f()));
            Assert.Null(texts.FirstNonEmpty(f => f()));
            Assert.True(filters.AllAllow(f => f()));
            Assert.Equal(0, ints.Count);
            Assert.Empty(_warnings);
        }

        // ---- Registration ----

        [Fact]
        public void Set_ReplacesSameGuid_AndKeepsItsPlaceInTheOrder()
        {
            var ints = _registry.Create<Func<int?>>("ints");
            ints.Set("a", () => 1);
            ints.Set("b", () => 2);
            ints.Set("a", () => 10);

            Assert.Equal(2, ints.Count);
            Assert.Equal(10, ints.FirstNonNull(f => f()));   // "a" is still first, with its new answer
        }

        [Fact]
        public void Set_NullCallback_RemovesThatGuid()
        {
            var ints = _registry.Create<Func<int?>>("ints");
            ints.Set("a", () => 1);
            ints.Set("b", () => 2);

            Assert.True(ints.Set("a", null));

            Assert.Equal(1, ints.Count);
            Assert.Equal(2, ints.FirstNonNull(f => f()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Set_WithoutAGuid_IsIgnoredWithAWarning(string guid)
        {
            var ints = _registry.Create<Func<int?>>("ints");

            Assert.False(ints.Set(guid, () => 1));

            Assert.Equal(0, ints.Count);
            Assert.Single(_warnings);
        }

        [Fact]
        public void RemoveAll_RemovesOnlyThatCaller_AcrossEverySet()
        {
            var ints = _registry.Create<Func<int?>>("ints");
            var filters = _registry.Create<Func<bool>>("filters");
            ints.Set("a", () => 1);
            ints.Set("b", () => 2);
            filters.Set("a", () => false);
            filters.Set("b", () => true);

            _registry.RemoveAll("a");

            Assert.Equal(1, ints.Count);
            Assert.Equal(2, ints.FirstNonNull(f => f()));
            Assert.Equal(1, filters.Count);
            Assert.True(filters.AllAllow(f => f()));
        }

        [Fact]
        public void RemoveAll_UnknownGuid_DoesNothing()
        {
            var ints = _registry.Create<Func<int?>>("ints");
            ints.Set("a", () => 1);

            _registry.RemoveAll("nobody");

            Assert.Equal(1, ints.Count);
        }

        // ---- First non-null ----

        [Fact]
        public void FirstNonNull_SkipsNoOpinion_AndFirstRegisteredWins()
        {
            var ints = _registry.Create<Func<int?>>("ints");
            ints.Set("a", () => null);
            ints.Set("b", () => 5);
            ints.Set("c", () => 9);

            Assert.Equal(5, ints.FirstNonNull(f => f()));
        }

        [Fact]
        public void FirstNonNull_ZeroIsAnOpinion()
        {
            var ints = _registry.Create<Func<int?>>("ints");
            ints.Set("a", () => 0);
            ints.Set("b", () => 4);

            Assert.Equal(0, ints.FirstNonNull(f => f()));
        }

        [Fact]
        public void FirstNonEmpty_SkipsNullAndEmptyText()
        {
            var texts = _registry.Create<Func<string>>("texts");
            texts.Set("a", () => null);
            texts.Set("b", () => "");
            texts.Set("c", () => "slot");
            texts.Set("d", () => "other");

            Assert.Equal("slot", texts.FirstNonEmpty(f => f()));
        }

        [Fact]
        public void FirstNonEmpty_TellsAThrowingProviderApartFromNoOpinion()
        {
            var texts = _registry.Create<Func<string>>("texts");
            texts.Set("quiet", () => null);
            Assert.Null(texts.FirstNonEmpty(f => f(), out bool failed));
            Assert.False(failed);

            texts.Set("broken", () => throw new InvalidOperationException("boom"));
            Assert.Null(texts.FirstNonEmpty(f => f(), out failed));
            Assert.True(failed);   // the slot is unknown, not "the default slot"

            texts.Set("later", () => "slot");
            Assert.Equal("slot", texts.FirstNonEmpty(f => f(), out _));   // a later answer still wins
        }

        // ---- AND filters ----

        [Fact]
        public void NotifyAll_TellsEveryListenerInOrder()
        {
            var heard = new List<string>();
            var listeners = _registry.Create<Action<string>>("listeners");
            listeners.Set("a", where => heard.Add("a:" + where));
            listeners.Set("b", where => heard.Add("b:" + where));

            listeners.NotifyAll(l => l("shop"));

            Assert.Equal(new[] { "a:shop", "b:shop" }, heard);
        }

        [Fact]
        public void NotifyAll_AThrowingListenerIsSkippedAndLoggedOnce()
        {
            var heard = new List<string>();
            var listeners = _registry.Create<Action<string>>("listeners");
            listeners.Set("bad", _ => throw new InvalidOperationException("boom"));
            listeners.Set("good", where => heard.Add(where));

            listeners.NotifyAll(l => l("chest"));
            listeners.NotifyAll(l => l("relic_screen"));

            Assert.Equal(new[] { "chest", "relic_screen" }, heard);
            Assert.Single(_warnings);
        }

        [Fact]
        public void NotifyAll_NobodyRegistered_DoesNothing()
        {
            var listeners = _registry.Create<Action<string>>("listeners");
            listeners.NotifyAll(l => l("shop"));
            Assert.Empty(_warnings);
        }

        [Fact]
        public void NotifyAll_AListenerCanRemoveItselfWhileBeingTold()
        {
            var heard = new List<string>();
            var listeners = _registry.Create<Action<string>>("listeners");
            listeners.Set("a", _ => { heard.Add("a"); listeners.Remove("a"); });
            listeners.Set("b", _ => heard.Add("b"));

            listeners.NotifyAll(l => l("shop"));
            listeners.NotifyAll(l => l("shop"));

            Assert.Equal(new[] { "a", "b", "b" }, heard);
        }

        [Fact]
        public void AllAllow_OneFalseWins()
        {
            var filters = _registry.Create<Func<bool>>("filters");
            filters.Set("a", () => true);
            filters.Set("b", () => false);
            filters.Set("c", () => true);

            Assert.False(filters.AllAllow(f => f()));
        }

        [Fact]
        public void AllAllow_AllTrue_IsTrue()
        {
            var filters = _registry.Create<Func<bool>>("filters");
            filters.Set("a", () => true);
            filters.Set("b", () => true);

            Assert.True(filters.AllAllow(f => f()));
        }

        // ---- Smallest limit ----

        [Fact]
        public void SmallestLimit_SmallestNonNullWins()
        {
            var limits = _registry.Create<Func<int?>>("limits");
            limits.Set("a", () => 3);
            limits.Set("b", () => null);
            limits.Set("c", () => 1);
            limits.Set("d", () => 2);

            Assert.Equal(1, limits.SmallestLimit(f => f()));
        }

        [Fact]
        public void SmallestLimit_ZeroBeatsEverything()
        {
            var limits = _registry.Create<Func<int?>>("limits");
            limits.Set("a", () => 3);
            limits.Set("b", () => 0);
            limits.Set("c", () => -1);

            Assert.Equal(0, limits.SmallestLimit(f => f()));
        }

        [Fact]
        public void SmallestLimit_UnlimitedLosesToAnyNumber()
        {
            var limits = _registry.Create<Func<int?>>("limits");
            limits.Set("a", () => -1);
            limits.Set("b", () => 4);

            Assert.Equal(4, limits.SmallestLimit(f => f()));
        }

        [Fact]
        public void SmallestLimit_OnlyUnlimited_ReturnsMinusOne_EvenForOtherNegatives()
        {
            var limits = _registry.Create<Func<int?>>("limits");
            limits.Set("a", () => -7);
            limits.Set("b", () => -1);

            Assert.Equal(-1, limits.SmallestLimit(f => f()));
        }

        [Fact]
        public void SmallestLimit_AllNoOpinion_IsNull()
        {
            var limits = _registry.Create<Func<int?>>("limits");
            limits.Set("a", () => null);

            Assert.Null(limits.SmallestLimit(f => f()));
        }

        // ---- Callbacks that misbehave ----

        [Fact]
        public void ThrowingCallback_IsIgnored_AndTheOthersStillAnswer()
        {
            var ints = _registry.Create<Func<int?>>("ints");
            ints.Set("bad", () => throw new InvalidOperationException("boom"));
            ints.Set("good", () => 6);
            var filters = _registry.Create<Func<bool>>("filters");
            filters.Set("bad", () => throw new InvalidOperationException("boom"));
            var limits = _registry.Create<Func<int?>>("limits");
            limits.Set("bad", () => throw new InvalidOperationException("boom"));
            limits.Set("good", () => 3);

            Assert.Equal(6, ints.FirstNonNull(f => f()));     // no opinion: the next one answers
            Assert.True(filters.AllAllow(f => f()));          // counts as allowed
            Assert.Equal(3, limits.SmallestLimit(f => f()));  // skipped
        }

        [Fact]
        public void ThrowingCallbacks_AreLoggedOncePerCallerAndMember_NotOnEveryCall()
        {
            var a = _registry.Create<Func<bool>>("a");
            var b = _registry.Create<Func<bool>>("b");
            a.Set("one", () => throw new InvalidOperationException("boom"));
            a.Set("two", () => throw new Exception("x"));
            b.Set("one", () => throw new Exception("x"));

            for (int i = 0; i < 5; i++)
            {
                a.AllAllow(f => f());
                b.AllAllow(f => f());
            }

            Assert.Equal(3, _warnings.Count);
            Assert.Contains("one", _warnings[0]);
            Assert.Contains("a", _warnings[0]);
            Assert.Contains("boom", _warnings[0]);
        }

        [Fact]
        public void ThrowingCallback_LogsAgainAfterTheCallerRegistersAgain()
        {
            var filters = _registry.Create<Func<bool>>("filters");
            filters.Set("bad", () => throw new Exception("first"));
            filters.AllAllow(f => f());
            filters.Set("bad", () => throw new Exception("second"));
            filters.AllAllow(f => f());

            Assert.Equal(2, _warnings.Count);
        }

        [Fact]
        public void BrokenOrMissingLogger_DoesNotBreakTheCall()
        {
            foreach (Action<string> logger in new Action<string>[] { _ => throw new InvalidOperationException("logger down"), null })
            {
                var filters = new ApiRegistry(logger).Create<Func<bool>>("filters");
                filters.Set("bad", () => throw new Exception("x"));
                filters.Set("worse", () => false);

                Assert.False(filters.AllAllow(f => f()));
            }
        }

        // ---- Changing registrations from inside a callback ----

        [Fact]
        public void RemoveAll_DuringIteration_DoesNotBreakTheLoop()
        {
            var filters = _registry.Create<Func<bool>>("filters");
            int calledB = 0;
            filters.Set("a", () =>
            {
                _registry.RemoveAll("a");
                _registry.RemoveAll("b");
                return true;
            });
            filters.Set("b", () => { calledB++; return true; });

            Assert.True(filters.AllAllow(f => f()));   // does not throw
            // The loop works from a snapshot, so b still answered this round but is gone for the next one.
            Assert.Equal(1, calledB);
            Assert.Equal(0, filters.Count);
            filters.AllAllow(f => f());
            Assert.Equal(1, calledB);
        }

        [Fact]
        public void Set_DuringIteration_DoesNotBreakTheLoop()
        {
            var ints = _registry.Create<Func<int?>>("ints");
            ints.Set("a", () =>
            {
                ints.Set("a", () => 99);
                ints.Set("late", () => 1);
                return null;
            });
            ints.Set("b", () => 5);

            Assert.Equal(5, ints.FirstNonNull(f => f()));
            Assert.Equal(99, ints.FirstNonNull(f => f()));   // the replacement answers next time
        }

        // ---- TryCall ----

        [Fact]
        public void TryCall_ReturnsTheResult_OrFalseAndDefaultWhenItThrows()
        {
            Assert.True(_registry.TryCall("a", "m", () => 4, out int result));
            Assert.Equal(4, result);

            Assert.False(_registry.TryCall<int>("a", "m", () => throw new Exception("x"), out int failed));
            Assert.Equal(0, failed);
        }
    }
}
