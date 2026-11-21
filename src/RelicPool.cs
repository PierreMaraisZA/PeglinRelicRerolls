using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Relics;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Shared helpers for what a reroll can draw from, used by both relic screens and the shop. Built on the
    /// game's own relic queues and validity checks, so owned relics and class rarities are handled exactly as
    /// the game does.
    /// </summary>
    internal static class RelicPool
    {
        // The game's own pool helpers are private; call them so "anything left?" uses its exact validity rules.
        private static readonly System.Reflection.MethodInfo PossibleRelicsOfRarity =
            AccessTools.Method(typeof(RelicManager), "GetAllPosibleRelicsOfRarityList");
        private static readonly System.Reflection.MethodInfo IsRelicValid =
            AccessTools.Method(typeof(RelicManager), "CheckIfRelicValid");

        /// <summary>
        /// How many relics of this rarity the game could still offer, leaving out the given relics and every
        /// shop relic. Returns int.MaxValue if the game's helpers can't be reached (so rerolls aren't blocked).
        /// </summary>
        public static int CountAvailable(RelicManager relicManager, RelicRarity rarity, IEnumerable<Relic> exclude)
        {
            if (relicManager == null || PossibleRelicsOfRarity == null || IsRelicValid == null)
            {
                return int.MaxValue;
            }
            try
            {
                IEnumerable candidates = (IEnumerable)PossibleRelicsOfRarity.Invoke(relicManager, new object[] { rarity, false });
                List<Relic> excluded = Exclusions(relicManager, exclude);
                int count = 0;
                foreach (object candidate in candidates)
                {
                    if ((bool)IsRelicValid.Invoke(relicManager, new object[] { candidate, rarity, excluded, false }))
                    {
                        count++;
                    }
                }
                return count;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't count {rarity} relics: {e.Message}");
                return int.MaxValue;
            }
        }

        /// <summary>
        /// Draws up to <paramref name="count"/> new relics of exactly this rarity off the game's queue, leaving out the
        /// given relics and every shop relic. No fallback to a lower rarity; may return fewer (or none).
        /// </summary>
        public static Relic[] Draw(RelicManager relicManager, RelicRarity rarity, int count, IEnumerable<Relic> exclude)
        {
            Relic[] queued = relicManager.GetMultipleRelicsOffOfQueue(count, rarity);
            RelicRarity[] rarities = Enumerable.Repeat(rarity, count).ToArray();
            return relicManager.CheckAndUpdateSeededRelicOffers(queued, rarities, Exclusions(relicManager, exclude).ToArray(), fallback: false)
                .Where(relic => relic != null && relic != relicManager.consolationPrize)
                .Distinct()
                .ToArray();
        }

        /// <summary>Shop-only relics for the current class that aren't owned and aren't in the given list.</summary>
        public static List<Relic> AvailableShopOnly(RelicManager relicManager, IEnumerable<Relic> exclude)
        {
            HashSet<Relic> excluded = new HashSet<Relic>(exclude.Where(relic => relic != null));
            return (relicManager.ShopRelicPool ?? new List<Relic>())
                .Where(relic => relic != null && !excluded.Contains(relic) && !Owns(relicManager, relic) && RelicRerollsApi.RelicAllowed(relic))
                .Distinct()
                .ToList();
        }

        /// <summary>The next shop-only relic off the game's own shop queue that isn't in the given list, or null.</summary>
        public static Relic DrawShopOnly(RelicManager relicManager, IEnumerable<Relic> exclude)
        {
            HashSet<Relic> excluded = new HashSet<Relic>(exclude.Where(relic => relic != null));
            int queueSize = relicManager.ShopRelicPool?.Count ?? 0;
            for (int i = 0; i < queueSize; i++)
            {
                Relic relic = relicManager.GetShopOnlyRelicOffOfQueue();
                if (relic != null && !excluded.Contains(relic) && RelicRerollsApi.RelicAllowed(relic))
                {
                    return relic;
                }
            }
            return null;
        }

        /// <summary>The rarity this relic has for the current class (class overrides included).</summary>
        public static RelicRarity RarityOf(RelicManager relicManager, Relic relic)
        {
            // Not in any known list: report the relic's own rarity, or NONE. Never assume Common.
            return ListedRarityOf(relicManager, relic) ?? (relic != null ? relic.globalRarity : RelicRarity.NONE);
        }

        /// <summary>
        /// The relic's rarity for the current class as the game's own rarity lists give it (a secret seed's custom pools
        /// first, then the normal ones it falls back to), or null when no list has it (the Consolation Prize, say).
        /// </summary>
        public static RelicRarity? ListedRarityOf(RelicManager relicManager, Relic relic)
        {
            if (relicManager == null || relic == null || PossibleRelicsOfRarity == null)
            {
                return null;
            }
            object selectedClass = Traverse.Create(relicManager).Field("_selectedClass").GetValue();
            foreach (bool forceNonSecret in new[] { false, true })
            {
                foreach (RelicRarity rarity in KnownRarities)
                {
                    if (!(PossibleRelicsOfRarity.Invoke(relicManager, new object[] { rarity, forceNonSecret }) is IEnumerable entries))
                    {
                        continue;
                    }
                    foreach (object entry in entries)
                    {
                        Traverse entryTraverse = Traverse.Create(entry);
                        if (entryTraverse.Field("relic").GetValue<Relic>() != relic)
                        {
                            continue;
                        }
                        if (entryTraverse.Field("classRarities").GetValue() is IDictionary classRarities
                            && selectedClass != null && classRarities.Contains(selectedClass))
                        {
                            return (RelicRarity)classRarities[selectedClass];
                        }
                        return rarity;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// The given relics, every shop relic, and any relic another mod's filter rules out, ready to pass to the game
        /// as its "can't offer" list. The filter lives here so counting what's left and drawing agree.
        /// </summary>
        public static List<Relic> Exclusions(RelicManager relicManager, IEnumerable<Relic> relics)
        {
            List<Relic> excluded = relics.Where(relic => relic != null).ToList();
            excluded.AddRange(ShopRelics(relicManager).Where(relic => !excluded.Contains(relic)));
            if (RelicRerollsApi.HasRelicFilter)
            {
                excluded.AddRange(FilteredOut(relicManager).Where(relic => !excluded.Contains(relic)));
            }
            return excluded;
        }

        /// <summary>Every relic the game could offer in any rarity that a registered relic filter says no to.</summary>
        private static IEnumerable<Relic> FilteredOut(RelicManager relicManager)
        {
            HashSet<Relic> rejected = new HashSet<Relic>();
            if (PossibleRelicsOfRarity == null)
            {
                return rejected;
            }
            try
            {
                // Both views of the pool: with a custom secret seed the game can fall back from the secret pool to the
                // normal one, so a relic the filter rejects must be excluded from either.
                foreach (bool forceNonSecret in new[] { false, true })
                {
                    foreach (RelicRarity rarity in KnownRarities)
                    {
                        if (!(PossibleRelicsOfRarity.Invoke(relicManager, new object[] { rarity, forceNonSecret }) is IEnumerable entries))
                        {
                            continue;
                        }
                        foreach (object entry in entries)
                        {
                            Relic relic = Traverse.Create(entry).Field("relic").GetValue<Relic>();
                            if (relic != null && !rejected.Contains(relic) && !RelicRerollsApi.RelicAllowed(relic))
                            {
                                rejected.Add(relic);
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't apply another mod's relic filter: {e.Message}");
            }
            return rejected;
        }

        private static readonly RelicRarity[] KnownRarities = { RelicRarity.COMMON, RelicRarity.RARE, RelicRarity.BOSS };

        /// <summary>True for the rarities the mod knows how to reroll; anything else is left alone, never treated as Common.</summary>
        public static bool IsRerollable(RelicRarity rarity) => System.Array.IndexOf(KnownRarities, rarity) >= 0;

        /// <summary>
        /// Shop-only relics for every class, not just the current one. The game already skips the current class's
        /// when offering relics; this makes sure a relic that's shop-only for any class never comes up in a reroll.
        /// </summary>
        public static IEnumerable<Relic> ShopRelics(RelicManager relicManager)
        {
            HashSet<Relic> shop = new HashSet<Relic>();
            if (relicManager.ShopRelicPool != null)
            {
                shop.UnionWith(relicManager.ShopRelicPool.Where(relic => relic != null));
            }
            if (Traverse.Create(relicManager).Field("_classLoadouts").GetValue() is IEnumerable loadouts)
            {
                foreach (object pair in loadouts)
                {
                    List<Relic> classShopRelics = Traverse.Create(pair)
                        .Field("Loadout").Field("ShopRelicOverrides").Field("relics").GetValue<List<Relic>>();
                    if (classShopRelics != null)
                    {
                        shop.UnionWith(classShopRelics.Where(relic => relic != null));
                    }
                }
            }
            return shop;
        }

        public static string DisplayName(Relic relic)
        {
            return relic == null ? "(none)" : string.IsNullOrEmpty(relic.englishDisplayName) ? relic.name : relic.englishDisplayName;
        }

        private static bool Owns(RelicManager relicManager, Relic relic)
        {
            return Traverse.Create(relicManager).Field("_ownedRelics").GetValue() is IDictionary owned && owned.Contains(relic.effect);
        }
    }
}
