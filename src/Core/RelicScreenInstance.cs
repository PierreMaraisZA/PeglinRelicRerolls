using System.Collections.Generic;

namespace PeglinRelicRerolls.Core
{
    /// <summary>
    /// Everything shown on one relic screen "instance" (a single post-battle reward or a single chest),
    /// so rerolls never offer a relic the player has already seen there. The instance is keyed by the
    /// relic screen: it carries over if the same screen is set up again, and resets for a new screen or
    /// once the player moves on. Generic so it can be unit tested without Unity's Relic type.
    /// </summary>
    public class RelicScreenInstance<T>
    {
        private readonly HashSet<T> _seen = new HashSet<T>();
        private readonly List<T> _seenInOrder = new List<T>();
        private object _screenKey;
        private bool _started;

        public int RerollsUsed { get; private set; }

        public IReadOnlyList<T> Seen => _seenInOrder;

        /// <summary>Starts tracking for a screen, clearing what was tracked for a different screen.</summary>
        public void Enter(object screenKey)
        {
            if (_started && Equals(_screenKey, screenKey))
            {
                return;
            }
            Clear();
            _screenKey = screenKey;
            _started = true;
        }

        /// <summary>Called when the player moves on (the game saves on the map) or a new run starts.</summary>
        public void Clear()
        {
            _seen.Clear();
            _seenInOrder.Clear();
            RerollsUsed = 0;
            _screenKey = null;
            _started = false;
        }

        public void MarkSeen(IEnumerable<T> items)
        {
            foreach (T item in items)
            {
                if (item != null && _seen.Add(item))
                {
                    _seenInOrder.Add(item);
                }
            }
        }

        public bool HasSeen(T item) => _seen.Contains(item);

        /// <summary>
        /// Empties the do-not-offer list once every remaining relic has been seen, keeping only what is on
        /// screen right now so the next reroll still changes something. The reroll count is kept, so the
        /// boss limit isn't reset.
        /// </summary>
        public void RecycleSeen(IEnumerable<T> keep)
        {
            _seen.Clear();
            _seenInOrder.Clear();
            MarkSeen(keep);
        }

        /// <summary>True if the offer contains at least one relic not already seen on this screen.</summary>
        public bool OffersSomethingNew(IEnumerable<T> offer)
        {
            foreach (T item in offer)
            {
                if (item != null && !_seen.Contains(item))
                {
                    return true;
                }
            }
            return false;
        }

        public void RecordReroll()
        {
            RerollsUsed++;
        }
    }
}
