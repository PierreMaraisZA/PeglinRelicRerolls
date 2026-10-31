using System;
using System.Collections.Generic;
using System.Linq;
using Battle;
using HarmonyLib;
using Map;
using PeglinRelicRerolls.Core;
using PeglinUI;
using Relics;
using Scenarios.Shop;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PeglinRelicRerolls
{
    /// <summary>Shop item that grants one reroll when bought.</summary>
    internal class PurchasableReroll : IPurchasableItem
    {
        private readonly int _cost;

        public PurchasableReroll(int cost)
        {
            _cost = cost;
        }

        public void PurchaseItem()
        {
            RerollState.Add(1);
            ModLog.Info($"Bought a reroll. Rerolls: {RerollState.Count}");
        }

        public Sprite GetPurchasableImage() => DieSprite.Get();

        public Vector3 GetSpriteLocalScale() => Vector3.one;

        public Color GetSpriteColor() => Color.white;

        public int GetCost() => _cost;

        public void ShowTooltip(Vector3 position, Vector3 offset, bool followMousePosition = false)
        {
            TooltipManager.Instance.ShowTooltipRelic(RerollTooltip.Relic, position, offset, followMousePosition);
        }

        public bool GetUnlocked() => true;
    }

    /// <summary>
    /// Tracks the open shop so its unbought relics can be rerolled. Each reroll replaces every unbought relic with
    /// a new one of the same rarity (the shop-only relic with another shop-only relic), never repeating a relic
    /// already offered in this shop, and keeping any price increases the slot had built up.
    /// </summary>
    internal static class ShopRerollSession
    {
        private static readonly RelicScreenInstance<Relic> Instance = new RelicScreenInstance<Relic>();

        private static ShopManager _shop;

        /// <summary>Whether at least one unbought relic could change; refreshed on open, purchase and reroll.</summary>
        private static bool _anySlotCanChange;

        private static bool _hasUnboughtRelics;

        public static void Begin(ShopManager shop, Relic removedForDie)
        {
            _shop = shop;
            Instance.Enter(shop);
            Instance.MarkSeen(ShopRelics().Select(slot => slot.Relic));
            if (removedForDie != null)
            {
                Instance.MarkSeen(new[] { removedForDie });
            }
            Refresh();
            ModLog.Info($"Shop opened: {ShopRelics().Count} relic(s) can be rerolled.");
        }

        public static bool IsActive
        {
            get
            {
                if (!Plugin.EnableShopRelicReroll.Value || _shop == null || !ShopManager.isOpen || PauseMenu.Paused || GameBlockingWindow.windowOpen)
                {
                    return false;
                }
                // Hide while a shop item's details panel or the deck view is open over the shop. Checks the panels
                // themselves rather than the shop's "shopItemOpen" flag: the game never clears that flag after
                // buying something through a details panel, so it would block rerolls for the rest of the visit.
                Traverse shop = Traverse.Create(_shop);
                bool detailsOpen = (_shop.upgradeConfirmationRelic != null && _shop.upgradeConfirmationRelic.gameObject.activeInHierarchy)
                    || (_shop.upgradeConfirmationOrbInspect != null && _shop.upgradeConfirmationOrbInspect.gameObject.activeInHierarchy);
                return !shop.Field("_storeClosed").GetValue<bool>()
                    && !detailsOpen
                    && !shop.Field("inventoryOpen").GetValue<bool>();
            }
        }

        /// <summary>Shop rerolls left in this visit under the per-shop limit.</summary>
        public static int RemainingInShop => RerollRules.RemainingUnderLimit(Instance.RerollsUsed, Plugin.ShopRerollLimit.Value);

        public static bool CanRerollNow => RerollState.Count > 0 && RemainingInShop > 0 && _anySlotCanChange;

        /// <summary>Why the shop reroll button is greyed out, or null if rerolling is allowed.</summary>
        public static string DisabledReason
        {
            get
            {
                if (RemainingInShop <= 0)
                {
                    return Plugin.ShopRerollLimit.Value == 0
                        ? "disabled in shops"
                        : $"disabled, this shop's {Plugin.ShopRerollLimit.Value} rerolls have been used";
                }
                if (!_hasUnboughtRelics)
                {
                    return "no relics left in this shop to reroll";
                }
                if (!_anySlotCanChange)
                {
                    return "disabled, no more relics to reroll into";
                }
                return null;
            }
        }

        /// <summary>Recomputes what can still be rerolled; call after the shop's relics change.</summary>
        public static void Refresh()
        {
            List<ShopRelicSlot> slots = ShopRelics();
            _hasUnboughtRelics = slots.Count > 0;
            _anySlotCanChange = false;
            if (_shop == null || slots.Count == 0)
            {
                return;
            }
            RelicManager relicManager = _shop.relicManager;
            Relic[] onShelf = slots.Select(slot => slot.Relic).ToArray();
            foreach (IGrouping<bool, ShopRelicSlot> group in slots.GroupBy(slot => slot.ShopOnly))
            {
                if (group.Key)
                {
                    // Shop-only relics: another unowned one not already on the shelf.
                    _anySlotCanChange |= RelicPool.AvailableShopOnly(relicManager, onShelf).Count > 0;
                    continue;
                }
                foreach (RelicRarity rarity in group.Select(slot => slot.Rarity).Distinct())
                {
                    int unseen = RelicPool.CountAvailable(relicManager, rarity, Instance.Seen);
                    int notOnShelf = RelicPool.CountAvailable(relicManager, rarity, onShelf);
                    _anySlotCanChange |= RerollRules.AvailableToReroll(unseen, notOnShelf) > 0;
                }
            }
        }

        public static void TryReroll()
        {
            if (!IsActive || !CanRerollNow || !RerollState.TrySpend())
            {
                UIAudioManager.InvalidSelectionSfxRequested?.Invoke();
                return;
            }
            RelicManager relicManager = _shop.relicManager;
            List<ShopRelicSlot> slots = ShopRelics();
            Relic[] onShelf = slots.Select(slot => slot.Relic).ToArray();

            // Every relic left has already been offered in this shop: start the list over, keeping what's on the shelf.
            bool unseenLeft = slots.Any(slot => slot.ShopOnly
                ? RelicPool.AvailableShopOnly(relicManager, Instance.Seen).Count > 0
                : RelicPool.CountAvailable(relicManager, slot.Rarity, Instance.Seen) > 0);
            if (!unseenLeft)
            {
                Instance.RecycleSeen(onShelf);
                ModLog.Info("Shop do-not-offer list was full: recycled it, keeping only the relics on the shelf.");
            }

            List<Relic> chosen = new List<Relic>();
            int changed = 0;
            TooltipManager.Instance?.HideTooltip();
            foreach (ShopRelicSlot slot in slots)
            {
                IEnumerable<Relic> exclude = Instance.Seen.Concat(onShelf).Concat(chosen);
                Relic replacement = slot.ShopOnly
                    ? RelicPool.DrawShopOnly(relicManager, exclude)
                    : RelicPool.Draw(relicManager, slot.Rarity, 1, exclude).FirstOrDefault();
                if (replacement == null)
                {
                    // Nothing new for this slot: leave it as it is.
                    continue;
                }
                ReplaceRelic(slot, replacement);
                RerollAnimation.FlipIn(slot.ShopItem.itemImage != null ? slot.ShopItem.itemImage.transform : slot.ShopItem.transform, changed);
                chosen.Add(replacement);
                changed++;
            }

            if (changed == 0)
            {
                RerollState.Refund();
                UIAudioManager.InvalidSelectionSfxRequested?.Invoke();
                ModLog.Info("Shop reroll refunded: no new relics to offer.");
                Refresh();
                return;
            }
            RerollAnimation.PlaySound();
            Instance.MarkSeen(chosen);
            Instance.RecordReroll();
            Refresh();
            ModLog.Info($"Shop reroll: {changed} of {slots.Count} relic(s) changed: {string.Join(", ", chosen.Select(RelicPool.DisplayName))}. " +
                $"Rerolls left: {RerollState.Count}, shop rerolls left here: {(RemainingInShop == int.MaxValue ? "unlimited" : RemainingInShop.ToString())}");
            ModLog.Info($"Shop do-not-offer list ({Instance.Seen.Count}): {string.Join(", ", Instance.Seen.Select(RelicPool.DisplayName))}");
        }

        /// <summary>Swaps a shop slot's relic in place, keeping its price and any price increases it had.</summary>
        private static void ReplaceRelic(ShopRelicSlot slot, Relic relic)
        {
            Traverse oldItem = Traverse.Create(slot.Item);
            PurchasableRelic newItem = new PurchasableRelic(relic, _shop.relicManager, oldItem.Field("_costMultiplier").GetValue<int>());
            Traverse newTraverse = Traverse.Create(newItem);
            newTraverse.Field("_cost").SetValue(oldItem.Field("_cost").GetValue<int>());
            newTraverse.Field("_numPriceIncreases").SetValue(oldItem.Field("_numPriceIncreases").GetValue<int>());

            // Re-initialising adds new hover/selection callbacks; clear the old ones so they don't show the old relic.
            Component hover = slot.ShopItem.GetComponent("MouseOverDetectorPointerHandler");
            if (hover != null)
            {
                Traverse.Create(hover).Field("OnPointerEntered").SetValue(null);
                Traverse.Create(hover).Field("OnPointerExited").SetValue(null);
            }
            SelectionEventListener selection = slot.ShopItem.GetComponentInChildren<SelectionEventListener>(includeInactive: true);
            if (selection != null)
            {
                Traverse.Create(selection).Field("OnSelectionEvent").SetValue(null);
                Traverse.Create(selection).Field("OnDeselectionEvent").SetValue(null);
            }
            // Initialize adds a random tilt to the item background; reset it so rerolls don't keep twisting it.
            if (slot.ShopItem.itemBackground != null)
            {
                slot.ShopItem.itemBackground.transform.localRotation = Quaternion.identity;
            }

            Traverse shop = Traverse.Create(_shop);
            slot.ShopItem.Initialize(newItem, _shop, shop.Field("_playerHealthController").GetValue<PlayerHealthController>(), _shop.relicManager);
            IPurchasableItem[] purchasable = shop.Field("_purchasableRelics").GetValue<IPurchasableItem[]>();
            int index = Array.IndexOf(purchasable, slot.Item);
            if (index >= 0)
            {
                purchasable[index] = newItem;
            }
        }

        /// <summary>The shop's relics that haven't been bought yet (the reroll die is not a relic).</summary>
        private static List<ShopRelicSlot> ShopRelics()
        {
            List<ShopRelicSlot> slots = new List<ShopRelicSlot>();
            List<ShopItem> items = _shop != null ? Traverse.Create(_shop).Field("relicItems").GetValue<List<ShopItem>>() : null;
            if (items == null)
            {
                return slots;
            }
            foreach (ShopItem shopItem in items)
            {
                if (shopItem == null || Traverse.Create(shopItem).Field("_purchased").GetValue<bool>())
                {
                    continue;
                }
                if (Traverse.Create(shopItem).Field("_item").GetValue<IPurchasableItem>() is PurchasableRelic item)
                {
                    Relic relic = Traverse.Create(item).Field("_relic").GetValue<Relic>();
                    if (relic != null)
                    {
                        bool shopOnly = _shop.relicManager.IsRelicShopOnlyRelic(relic);
                        slots.Add(new ShopRelicSlot(shopItem, item, relic, shopOnly, RelicPool.RarityOf(_shop.relicManager, relic)));
                    }
                }
            }
            return slots;
        }

        private readonly struct ShopRelicSlot
        {
            public ShopRelicSlot(ShopItem shopItem, PurchasableRelic item, Relic relic, bool shopOnly, RelicRarity rarity)
            {
                ShopItem = shopItem;
                Item = item;
                Relic = relic;
                ShopOnly = shopOnly;
                Rarity = rarity;
            }

            public ShopItem ShopItem { get; }

            public PurchasableRelic Item { get; }

            public Relic Relic { get; }

            public bool ShopOnly { get; }

            public RelicRarity Rarity { get; }
        }
    }

    [HarmonyPatch(typeof(ShopManager), "SetUpRelicOffer")]
    internal static class ShopRelicOfferPatch
    {
        private static void Postfix(ShopManager __instance, List<ShopItem> ___relicItems,
            IPurchasableItem[] ____purchasableRelics, PlayerHealthController ____playerHealthController)
        {
            RerollHud.Ensure();
            Relic removedForDie = Plugin.EnableShopReroll.Value
                ? PlaceDie(__instance, ___relicItems, ____purchasableRelics, ____playerHealthController)
                : null;
            ShopRerollSession.Begin(__instance, removedForDie);
        }

        /// <summary>
        /// Swaps one of the shop's relics for the reroll die: the last common relic, otherwise the last rare one.
        /// Never the class's shop-only relic, which the game adds last and which can only be found in shops.
        /// Returns the relic that was removed, or null if the shop had no slot to give up.
        /// </summary>
        private static Relic PlaceDie(ShopManager shop, List<ShopItem> relicItems, IPurchasableItem[] purchasableRelics,
            PlayerHealthController playerHealthController)
        {
            RelicManager relicManager = shop.relicManager;
            List<ShopSlotKind> kinds = relicItems.Select(item => KindOf(relicManager, item)).ToList();
            ModLog.Info($"Shop relic slots: {string.Join(", ", kinds.Select((kind, i) => $"{i + 1}={kind}"))}");
            int dieSlot = RerollRules.DieSlot(kinds);
            if (dieSlot < 0)
            {
                ModLog.Info("Shop has no common or rare relic to swap for the reroll die; no die this time.");
                return null;
            }

            ShopItem replaced = relicItems[dieSlot];
            IPurchasableItem replacedItem = Traverse.Create(replaced).Field("_item").GetValue<IPurchasableItem>();
            Relic replacedRelic = replacedItem is PurchasableRelic ? Traverse.Create(replacedItem).Field("_relic").GetValue<Relic>() : null;
            int slot = Array.IndexOf(purchasableRelics, replacedItem);
            ArrowSelection replacedArrow = replaced.GetComponentInChildren<ArrowSelection>();

            ShopItem shopItem = Object.Instantiate(shop.purchasablePrefab, shop.relicContainer.transform).GetComponent<ShopItem>();
            shopItem.transform.SetSiblingIndex(replaced.transform.GetSiblingIndex());
            replaced.gameObject.SetActive(false);
            Object.Destroy(replaced.gameObject);

            // Doubled by Wand of Greed and in act 4, the same as the game doubles relic prices.
            bool act4 = MapController.instance != null && MapController.instance.Act == 4;
            int cost = RerollRules.ShopRerollCost(Plugin.ShopRerollCost.Value, relicManager.WandOfGreedEffectActive(), act4);
            PurchasableReroll reroll = new PurchasableReroll(cost);
            shopItem.Initialize(reroll, shop, playerHealthController, relicManager);
            ArrowSelection arrow = shopItem.GetComponentInChildren<ArrowSelection>();
            if (arrow != null && replacedArrow != null)
            {
                Traverse.Create(arrow).Field("rewiredEventSystem")
                    .SetValue(Traverse.Create(replacedArrow).Field("rewiredEventSystem").GetValue());
            }

            relicItems[dieSlot] = shopItem;
            if (slot >= 0)
            {
                purchasableRelics[slot] = reroll;
            }
            ModLog.Info($"Reroll die placed in shop slot {dieSlot + 1} for {cost}g, replacing {RelicPool.DisplayName(replacedRelic)}.");
            return replacedRelic;
        }

        private static ShopSlotKind KindOf(RelicManager relicManager, ShopItem shopItem)
        {
            if (!(Traverse.Create(shopItem).Field("_item").GetValue<IPurchasableItem>() is PurchasableRelic item))
            {
                return ShopSlotKind.Other;
            }
            Relic relic = Traverse.Create(item).Field("_relic").GetValue<Relic>();
            if (relic == null)
            {
                return ShopSlotKind.Other;
            }
            if (relicManager.IsRelicShopOnlyRelic(relic))
            {
                return ShopSlotKind.ShopOnly;
            }
            switch (RelicPool.RarityOf(relicManager, relic))
            {
                case RelicRarity.RARE:
                    return ShopSlotKind.Rare;
                case RelicRarity.BOSS:
                    return ShopSlotKind.Boss;
                default:
                    return ShopSlotKind.Common;
            }
        }
    }

    /// <summary>
    /// Keeps the shop's "what can be rerolled" state current after anything is bought. Hooks the item rather than
    /// ShopManager.PurchaseItem, because the item only marks itself as bought after the shop's method returns.
    /// </summary>
    [HarmonyPatch(typeof(ShopItem), nameof(ShopItem.PurchaseItem))]
    internal static class ShopPurchasePatch
    {
        private static void Postfix()
        {
            ShopRerollSession.Refresh();
        }
    }

    /// <summary>
    /// Clicking a shop item normally opens the relic/orb details panel. The reroll has no
    /// panel, so buy it directly instead of leaving the shop waiting on a panel that never opens.
    /// </summary>
    [HarmonyPatch(typeof(ShopItem), nameof(ShopItem.ShowUpgradeInformation))]
    internal static class ShopItemClickPatch
    {
        private static bool Prefix(ShopItem __instance, IPurchasableItem ____item)
        {
            if (____item is PurchasableReroll)
            {
                __instance.PurchaseItem();
                return false;
            }
            return true;
        }
    }
}
