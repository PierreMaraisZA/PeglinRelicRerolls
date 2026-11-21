using System;
using System.Linq;

namespace PeglinRelicRerolls.Core
{
    /// <summary>Removes an object's handlers from an event delegate, for objects the mod throws away or sets up again.</summary>
    public static class DelegateCleanup
    {
        /// <summary>
        /// The delegate without any handler whose target is <paramref name="target"/> (every copy, if it was added
        /// more than once). Returns null when nothing is left, like removing the last handler from an event.
        /// </summary>
        public static T RemoveTarget<T>(T handlers, object target) where T : Delegate
        {
            if (handlers == null || target == null)
            {
                return handlers;
            }
            Delegate[] kept = handlers.GetInvocationList().Where(handler => !ReferenceEquals(handler.Target, target)).ToArray();
            return (T)Delegate.Combine(kept);
        }
    }
}
