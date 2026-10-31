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
                .Where(relic => relic != null && !excluded.Contains(relic) && !Owns(relicManager, relic))
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
                if (relic != null && !excluded.Contains(relic))
                {
                    return relic;
                }
            }
            return null;
        }

        /// <summary>The rarity this relic has for the current class (class overrides included).</summary>
        public static RelicRarity RarityOf(RelicManager relicManager, Relic relic)
        {
            object selectedClass = Traverse.Create(relicManager).Field("_selectedClass").GetValue();
            foreach (RelicRarity rarity in new[] { RelicRarity.COMMON, RelicRarity.RARE, RelicRarity.BOSS })
            {
                if (PossibleRelicsOfRarity == null || !(PossibleRelicsOfRarity.Invoke(relicManager, new object[] { rarity, false }) is IEnumerable entries))
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
            return relic != null ? relic.globalRarity : RelicRarity.COMMON;
        }

        /// <summary>The given relics plus every shop relic, ready to pass to the game as its "can't offer" list.</summary>
        public static List<Relic> Exclusions(RelicManager relicManager, IEnumerable<Relic> relics)
        {
            List<Relic> excluded = relics.Where(relic => relic != null).ToList();
            excluded.AddRange(ShopRelics(relicManager).Where(relic => !excluded.Contains(relic)));
            return excluded;
        }

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
