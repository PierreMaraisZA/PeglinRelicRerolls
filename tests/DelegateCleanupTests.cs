using System;
using System.Collections.Generic;
using PeglinRelicRerolls.Core;
using Xunit;

namespace PeglinRelicRerolls.Tests
{
    public class DelegateCleanupTests
    {
        private class Listener
        {
            public readonly List<float> Calls = new List<float>();

            public void OnHealthChanged(float amount) => Calls.Add(amount);
        }

        [Fact]
        public void RemoveTarget_RemovesEveryCopyOfThatObjectsHandler()
        {
            // Given a shop item that was set up twice (a reroll), so it listens twice
            Listener item = new Listener();
            Action<float> handlers = item.OnHealthChanged;
            handlers += item.OnHealthChanged;

            // When it is cleaned up
            handlers = DelegateCleanup.RemoveTarget(handlers, item);

            // Then nothing is left listening
            Assert.Null(handlers);
        }

        [Fact]
        public void RemoveTarget_KeepsOtherObjectsHandlersInOrder()
        {
            Listener first = new Listener();
            Listener removed = new Listener();
            Listener last = new Listener();
            Action<float> handlers = first.OnHealthChanged;
            handlers += removed.OnHealthChanged;
            handlers += last.OnHealthChanged;

            handlers = DelegateCleanup.RemoveTarget(handlers, removed);
            handlers(5f);

            Assert.Equal(new[] { 5f }, first.Calls);
            Assert.Empty(removed.Calls);
            Assert.Equal(new[] { 5f }, last.Calls);
            Assert.Equal(2, handlers.GetInvocationList().Length);
        }

        [Fact]
        public void RemoveTarget_NoHandlers_StaysNull()
        {
            Action<float> handlers = null;

            Assert.Null(DelegateCleanup.RemoveTarget(handlers, new Listener()));
        }

        [Fact]
        public void RemoveTarget_ObjectNotListening_ChangesNothing()
        {
            Listener listening = new Listener();
            Action<float> handlers = listening.OnHealthChanged;

            handlers = DelegateCleanup.RemoveTarget(handlers, new Listener());

            Assert.Single(handlers.GetInvocationList());
        }

        [Fact]
        public void RemoveTarget_NullTarget_KeepsStaticHandlers()
        {
            // A static handler has no target; a null target must not remove it
            Action<float> handlers = StaticHandler;

            handlers = DelegateCleanup.RemoveTarget(handlers, null);

            Assert.Single(handlers.GetInvocationList());
        }

        private static void StaticHandler(float amount)
        {
        }
    }
}
