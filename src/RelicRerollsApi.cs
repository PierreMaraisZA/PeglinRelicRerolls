using System;
using PeglinRelicRerolls.Core;
using Relics;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Public API for other mods (API version 1). Find this class by its simple name in this plugin's assembly, check
    /// <see cref="ApiVersion"/>, then register by your own plugin GUID. Registering again with the same GUID replaces
    /// the earlier registration; <see cref="RemoveAll"/> removes everything you registered.
    ///
    /// A callback that throws is logged once and counts as "no opinion". With nothing registered the mod behaves
    /// exactly as it does without this API. Members are only ever added, never renamed or changed in meaning.
    /// </summary>
    public static class RelicRerollsApi
    {
        private static readonly ApiRegistry Registry = new ApiRegistry(message => Plugin.Log?.LogWarning(message));

        private static readonly ApiRegistry.CallbackSet<Func<int?>> StartingCountProviders =
            Registry.Create<Func<int?>>(nameof(SetStartingCountProvider));

        private static readonly ApiRegistry.CallbackSet<Func<RelicRarity, bool, int?>> RelicScreenFilters =
            Registry.Create<Func<RelicRarity, bool, int?>>(nameof(SetRelicScreenFilter));

        private static readonly ApiRegistry.CallbackSet<Func<bool>> ShopFilters =
            Registry.Create<Func<bool>>(nameof(SetShopFilter));

        private static readonly ApiRegistry.CallbackSet<Func<Relic, bool>> RelicFilters =
            Registry.Create<Func<Relic, bool>>(nameof(SetRelicFilter));

        private static readonly ApiRegistry.CallbackSet<Func<string>> StateSlotProviders =
            Registry.Create<Func<string>>(nameof(SetStateSlotProvider));

        private static readonly ApiRegistry.CallbackSet<Func<bool>> AutoSaveFilters =
            Registry.Create<Func<bool>>(nameof(SetAutoSaveFilter));

        private static readonly ApiRegistry.CallbackSet<Action<string, Relic[]>> RerollListeners =
            Registry.Create<Action<string, Relic[]>>(nameof(SetRerollListener));

        /// <summary>The version of this API. A property, not a constant, so callers read it at run time.</summary>
        public static int ApiVersion => 1;

        /// <summary>Rerolls left in the current run.</summary>
        public static int RerollCount => RerollState.Count;

        /// <summary>Adds rerolls to the current run, or removes them if negative (never below 0).</summary>
        public static void AddRerolls(int amount)
        {
            RerollState.Adjust(amount);
        }

        /// <summary>
        /// Starting rerolls for a new run. Return null for no opinion (the normal rule, including the Cruciball
        /// penalty). Read only when a new run resets the count, never when a run is continued.
        /// </summary>
        public static void SetStartingCountProvider(string guid, Func<int?> provider)
        {
            StartingCountProviders.Set(guid, provider);
        }

        /// <summary>
        /// Decides, per relic screen, how many rerolls are allowed. Called with the screen's rarity and whether it is
        /// a chest. Return null for no opinion, 0 for no reroll button, -1 for unlimited, or n for at most n rerolls
        /// on that screen. The smallest answer wins, and it replaces the floor boss limit setting.
        /// </summary>
        public static void SetRelicScreenFilter(string guid, Func<RelicRarity, bool, int?> limit)
        {
            RelicScreenFilters.Set(guid, limit);
        }

        /// <summary>Returning false, from any caller, hides the reroll die in shops and shop relic rerolls.</summary>
        public static void SetShopFilter(string guid, Func<bool> allow)
        {
            ShopFilters.Set(guid, allow);
        }

        /// <summary>Returning false, from any caller, keeps that relic out of every reroll result.</summary>
        public static void SetRelicFilter(string guid, Func<Relic, bool> allow)
        {
            RelicFilters.Set(guid, allow);
        }

        /// <summary>
        /// The name of the state slot for the current run. Null or empty means the default slot (the same file
        /// earlier versions used); anything else is its own file, so a mode's rerolls never mix with a normal run's.
        /// </summary>
        public static void SetStateSlotProvider(string guid, Func<string> slot)
        {
            StateSlotProviders.Set(guid, slot);
        }

        /// <summary>
        /// Returning false, from any caller, stops the mod saving and loading the count by itself when the game saves
        /// or continues a run. For modes with their own save flow: they call <see cref="Commit"/> and
        /// <see cref="Restore"/> at their own save and load points instead. A new run still resets the count.
        /// </summary>
        public static void SetAutoSaveFilter(string guid, Func<bool> allow)
        {
            AutoSaveFilters.Set(guid, allow);
        }

        /// <summary>
        /// Called after every reroll that changed what is offered (never for one that was refunded). The first argument is
        /// where it happened: "relic_screen", "chest" or "shop" (more names may be added later; ignore ones you don't
        /// know). The second is every relic now offered there, in slot order; each listener gets its own copy. Read
        /// <see cref="RerollCount"/> for the rerolls left.
        /// </summary>
        public static void SetRerollListener(string guid, Action<string, Relic[]> listener)
        {
            RerollListeners.Set(guid, listener);
        }

        /// <summary>Writes the current count to the current slot now. For modes with their own save flow.</summary>
        public static void Commit()
        {
            try
            {
                RerollState.Commit();
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"Commit failed: {e.Message}");
            }
        }

        /// <summary>Loads the count for the current run from the current slot now. For modes with their own save flow.</summary>
        public static void Restore()
        {
            try
            {
                RerollState.Restore();
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"Restore failed: {e.Message}");
            }
        }

        /// <summary>Removes everything the given plugin registered.</summary>
        public static void RemoveAll(string guid)
        {
            Registry.RemoveAll(guid);
        }

        // What the rest of the mod reads. Each returns the "nothing registered" answer when no one has an opinion.

        internal static int? StartingCount() => StartingCountProviders.FirstNonNull(provider => provider());

        internal static int? RelicScreenLimit(RelicRarity rarity, bool isChest) =>
            RelicScreenFilters.SmallestLimit(limit => limit(rarity, isChest));

        internal static bool ShopAllowed() => ShopFilters.AllAllow(allow => allow());

        internal static bool AutoSaveAllowed() => AutoSaveFilters.AllAllow(allow => allow());

        /// <summary>Tells the reroll listeners; each gets its own copy of the offer, so none can change another's.</summary>
        internal static void RerollHappened(string where, Relic[] offered)
        {
            if (RerollListeners.Count == 0)
            {
                return;
            }
            Relic[] offer = offered ?? new Relic[0];
            RerollListeners.NotifyAll(listener => listener(where, (Relic[])offer.Clone()));
        }

        internal static bool HasRelicFilter => RelicFilters.Count > 0;

        internal static bool RelicAllowed(Relic relic) => relic == null || RelicFilters.AllAllow(allow => allow(relic));

        private static bool _resolvingSlot;

        /// <summary>
        /// The slot name from the providers (null for the default slot). Returns false when the slot can't be known:
        /// a provider threw before one answered, or a provider called Commit or Restore while being asked (which would
        /// loop back here). The caller then saves or loads nothing, so one mode's count never lands in another's file.
        /// </summary>
        internal static bool TryCurrentSlot(out string slot)
        {
            slot = null;
            if (_resolvingSlot)
            {
                Plugin.Log?.LogWarning("A state slot provider called Commit or Restore while being asked for its slot; nothing was saved or loaded.");
                return false;
            }
            _resolvingSlot = true;
            try
            {
                slot = StateSlotProviders.FirstNonEmpty(provider => provider(), out bool anyFailed);
                if (slot == null && anyFailed)
                {
                    Plugin.Log?.LogWarning("A state slot provider failed, so the reroll slot is unknown; nothing was saved or loaded.");
                    return false;
                }
                return true;
            }
            finally
            {
                _resolvingSlot = false;
            }
        }
    }
}
