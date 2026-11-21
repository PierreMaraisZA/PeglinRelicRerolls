using System;
using System.Collections.Generic;

namespace PeglinRelicRerolls.Core
{
    /// <summary>
    /// What other mods have registered with the public API, grouped by member. Generic and free of game types so it
    /// can be unit tested. Every registration belongs to a caller's plugin GUID; registering again replaces it, and
    /// <see cref="RemoveAll"/> drops everything a caller registered.
    ///
    /// Callbacks come from other mods, so every call is guarded: one that throws is logged once and counts as
    /// "no opinion" instead of breaking the mod.
    /// </summary>
    internal sealed class ApiRegistry
    {
        private readonly Action<string> _warn;
        private readonly List<ICallbackSet> _sets = new List<ICallbackSet>();
        private readonly HashSet<string> _reported = new HashSet<string>();

        /// <param name="warn">Where warnings go (the BepInEx log in the plugin). May be null.</param>
        public ApiRegistry(Action<string> warn)
        {
            _warn = warn;
        }

        /// <summary>Creates the set of callbacks for one API member.</summary>
        public CallbackSet<T> Create<T>(string member) where T : Delegate
        {
            CallbackSet<T> set = new CallbackSet<T>(this, member);
            _sets.Add(set);
            return set;
        }

        /// <summary>Removes every registration the given caller made, in every set.</summary>
        public void RemoveAll(string guid)
        {
            foreach (ICallbackSet set in _sets)
            {
                set.Remove(guid);
            }
        }

        /// <summary>
        /// Runs a callback from another mod. Returns false (and a default result) if it threw; the failure is
        /// logged once per caller and member until that caller registers again.
        /// </summary>
        public bool TryCall<T>(string guid, string member, Func<T> call, out T result)
        {
            try
            {
                result = call();
                return true;
            }
            catch (Exception e)
            {
                result = default;
                if (_reported.Add(ReportKey(guid, member)))
                {
                    Warn($"{guid}'s {member} callback threw {e.GetType().Name}: {e.Message}. Ignoring it (this is logged once).");
                }
                return false;
            }
        }

        internal void Warn(string message)
        {
            try
            {
                _warn?.Invoke(message);
            }
            catch
            {
                // A broken logger must never break the mod.
            }
        }

        internal void ForgetFailures(string guid, string member)
        {
            _reported.Remove(ReportKey(guid, member));
        }

        private static string ReportKey(string guid, string member) => guid + "\n" + member;

        internal interface ICallbackSet
        {
            void Remove(string guid);
        }

        /// <summary>The callbacks registered for one API member, in registration order.</summary>
        public sealed class CallbackSet<T> : ICallbackSet where T : Delegate
        {
            private readonly ApiRegistry _owner;
            private readonly string _member;
            private readonly List<Entry> _entries = new List<Entry>();

            internal CallbackSet(ApiRegistry owner, string member)
            {
                _owner = owner;
                _member = member;
            }

            public int Count => _entries.Count;

            /// <summary>
            /// Registers or replaces a caller's callback (a replaced one keeps its place in the order). A null
            /// callback removes it. Returns false, and warns, if the GUID is missing.
            /// </summary>
            public bool Set(string guid, T callback)
            {
                if (string.IsNullOrWhiteSpace(guid))
                {
                    _owner.Warn($"{_member} ignored: a plugin GUID is required.");
                    return false;
                }
                if (callback == null)
                {
                    Remove(guid);
                    return true;
                }
                _owner.ForgetFailures(guid, _member);
                int index = _entries.FindIndex(entry => entry.Guid == guid);
                Entry added = new Entry(guid, callback);
                if (index >= 0)
                {
                    _entries[index] = added;
                }
                else
                {
                    _entries.Add(added);
                }
                return true;
            }

            public void Remove(string guid)
            {
                _entries.RemoveAll(entry => entry.Guid == guid);
            }

            /// <summary>The first non-null answer, in registration order, or null if nobody has an opinion.</summary>
            public TResult? FirstNonNull<TResult>(Func<T, TResult?> call) where TResult : struct
            {
                foreach (Entry entry in Snapshot())
                {
                    if (_owner.TryCall(entry.Guid, _member, () => call(entry.Callback), out TResult? answer) && answer.HasValue)
                    {
                        return answer;
                    }
                }
                return null;
            }

            /// <summary>The first non-empty text, in registration order, or null if nobody has an opinion.</summary>
            public string FirstNonEmpty(Func<T, string> call) => FirstNonEmpty(call, out bool _);

            /// <summary>
            /// As above; <paramref name="anyFailed"/> is true when a callback asked before the answer threw, so the
            /// caller can tell "nobody has an opinion" from "someone's opinion is unknown".
            /// </summary>
            public string FirstNonEmpty(Func<T, string> call, out bool anyFailed)
            {
                anyFailed = false;
                foreach (Entry entry in Snapshot())
                {
                    if (!_owner.TryCall(entry.Guid, _member, () => call(entry.Callback), out string answer))
                    {
                        anyFailed = true;
                        continue;
                    }
                    if (!string.IsNullOrEmpty(answer))
                    {
                        return answer;
                    }
                }
                return null;
            }

            /// <summary>
            /// AND of every answer: false as soon as any caller says false. Nobody registered, or a callback that
            /// threw, counts as allowed.
            /// </summary>
            public bool AllAllow(Func<T, bool> call)
            {
                foreach (Entry entry in Snapshot())
                {
                    if (_owner.TryCall(entry.Guid, _member, () => call(entry.Callback), out bool allowed) && !allowed)
                    {
                        return false;
                    }
                }
                return true;
            }

            /// <summary>
            /// The smallest limit given, where 0 is "none" and any negative number is "unlimited" (returned as -1).
            /// Null if nobody gave one.
            /// </summary>
            public int? SmallestLimit(Func<T, int?> call)
            {
                int? smallest = null;
                foreach (Entry entry in Snapshot())
                {
                    if (!_owner.TryCall(entry.Guid, _member, () => call(entry.Callback), out int? answer) || !answer.HasValue)
                    {
                        continue;
                    }
                    int limit = answer.Value < 0 ? -1 : answer.Value;
                    smallest = smallest.HasValue ? SmallerLimit(smallest.Value, limit) : limit;
                }
                return smallest;
            }

            /// <summary>
            /// Tells every registered listener, in registration order. One that throws is logged once and skipped; the
            /// others are still told.
            /// </summary>
            public void NotifyAll(Action<T> call)
            {
                foreach (Entry entry in Snapshot())
                {
                    _owner.TryCall(entry.Guid, _member, () =>
                    {
                        call(entry.Callback);
                        return true;
                    }, out bool _);
                }
            }

            private static int SmallerLimit(int a, int b)
            {
                if (a < 0)
                {
                    return b;
                }
                if (b < 0)
                {
                    return a;
                }
                return Math.Min(a, b);
            }

            // Iterate a copy so a callback can register, replace or remove registrations without breaking the loop.
            private Entry[] Snapshot() => _entries.ToArray();

            private readonly struct Entry
            {
                public Entry(string guid, T callback)
                {
                    Guid = guid;
                    Callback = callback;
                }

                public string Guid { get; }

                public T Callback { get; }
            }
        }
    }
}
