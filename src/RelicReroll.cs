using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PeglinRelicRerolls.Core;
using PeglinUI;
using PeglinUI.PostBattle;
using Relics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Tracks the relic choice screen that is currently open (post-battle or chest) and
    /// swaps its offered relics when the player rerolls.
    /// </summary>
    internal static class RelicRerollSession
    {
        private static readonly RelicScreenInstance<Relic> Instance = new RelicScreenInstance<Relic>();

        private static BattleUpgradeCanvas _canvas;
        private static RelicRarity _baseRarity;
        private static bool _isTreasure;

        /// <summary>
        /// Every relic slot the game set up for this screen. A reroll that comes up short hides its empty slots,
        /// so later rerolls refill from this list rather than from whatever happens to be visible.
        /// </summary>
        private static RelicIcon[] _slots = new RelicIcon[0];

        /// <summary>
        /// The rarity each slot rerolls at. A screen set up with mixed rarities (for example a mod's draft of commons
        /// and rares) keeps each slot's own rarity; every vanilla screen has one rarity, so nothing changes there.
        /// Chests always use the chest rules instead.
        /// </summary>
        private static int[] _slotKinds = new int[0];

        private static bool MixedRarities => !_isTreasure && RerollRules.GroupSlots(_slotKinds).Count > 1;

        /// <summary>The rarities whose pools a reroll on this screen draws from.</summary>
        private static IEnumerable<RelicRarity> ScreenRarities =>
            MixedRarities ? RerollRules.GroupSlots(_slotKinds).Select(g => (RelicRarity)g.Key) : new[] { _baseRarity };

        /// <summary>The floor boss reward (not a chest that happened to upgrade to a boss relic).</summary>
        private static bool IsBossReward => !_isTreasure && _baseRarity == RelicRarity.BOSS;

        // Counts for the screen's own rarity, refreshed when the screen opens and after each reroll:
        // relics not yet offered on this screen, and relics other than the ones showing right now.
        private static int _unseenInScreenPool;
        private static int _notOnScreenInPool;

        private static int AvailableInScreenPool => RerollRules.AvailableToReroll(_unseenInScreenPool, _notOnScreenInPool);

        // A limit another mod set for this screen through the API; null means the normal rules apply. Asked once,
        // when the screen opens. A limit of 0 means no reroll button at all.
        private static int? _apiLimit;

        public static int RemainingOnScreen =>
            RerollRules.RemainingOnScreen(IsBossReward, Instance.RerollsUsed, Plugin.BossRerollLimit.Value, _apiLimit);

        public static bool CanRerollNow => RerollRules.CanReroll(RerollState.Count, RemainingOnScreen, AvailableInScreenPool);

        public static bool ScreenLimitReached => RemainingOnScreen <= 0;

        public static bool PoolExhausted => AvailableInScreenPool <= 0;

        /// <summary>Why the reroll button is greyed out on this screen, or null if rerolling is allowed.</summary>
        public static string DisabledReason
        {
            get
            {
                if (ScreenLimitReached)
                {
                    if (_apiLimit.HasValue)
                    {
                        return "disabled, this screen's reroll limit has been used";
                    }
                    return Plugin.BossRerollLimit.Value == 0
                        ? "disabled for floor boss rewards"
                        : "disabled, the floor boss reroll has been used";
                }
                if (PoolExhausted)
                {
                    return IsBossReward
                        ? "disabled, no more boss relics to reroll into"
                        : "disabled for this non floor boss encounter, no more relics to reroll into";
                }
                // Out of rerolls: the greyed-out button with "Reroll (0)" says enough.
                return null;
            }
        }

        public static void Begin(BattleUpgradeCanvas canvas, RelicRarity rarity, bool isTreasure)
        {
            _canvas = canvas;
            _baseRarity = rarity;
            _isTreasure = isTreasure;
            _apiLimit = RelicRerollsApi.RelicScreenLimit(rarity, isTreasure);
            // Keyed by the relic screen and how it was set up, not the map node: every room load (including the bonus
            // chest and dev jumps, which reuse the same node without passing the map) creates a new screen and starts
            // fresh, and so does a different pick on the same panel (e.g. a mod's common draft, then the floor boss
            // pick). The same screen set up the same way again (e.g. by another mod) keeps its history and boss-limit count.
            Instance.Enter(new ScreenKey(canvas, (int)rarity, isTreasure));
            _slots = OfferedIcons();
            RelicManager relicManager = RelicManagerFor(canvas);
            _slotKinds = RerollRules.SlotKinds(
                _slots.Select(icon => SlotRarity(relicManager, icon != null ? icon.relic : null)).ToArray(),
                _slots.Length, (int)rarity);
            Instance.MarkSeen(OfferedRelics());
            RefreshPoolCounts(relicManager);
            RerollHud.Ensure();
            ModLog.Info($"Relic screen opened: rarity={rarity}, chest={isTreasure}" +
                (_apiLimit.HasValue ? $", limit from another mod: {(_apiLimit.Value < 0 ? "unlimited" : _apiLimit.Value.ToString())}" : string.Empty));
            LogSeen();
        }

        private static void RefreshPoolCounts(RelicManager relicManager)
        {
            _unseenInScreenPool = RerollRules.SafeSum(ScreenRarities.Select(r => RelicPool.CountAvailable(relicManager, r, Instance.Seen)));
            _notOnScreenInPool = RerollRules.SafeSum(ScreenRarities.Select(r => RelicPool.CountAvailable(relicManager, r, OfferedRelics())));
        }

        /// <summary>The rarity a slot's relic rerolls at, or null to use the screen's own (no relic, or a rarity the mod can't reroll).</summary>
        private static int? SlotRarity(RelicManager relicManager, Relic relic)
        {
            // Only a rarity the game's own lists give counts: the Consolation Prize and other unlisted relics follow the screen.
            if (relic == null || relicManager == null || relic == relicManager.consolationPrize)
            {
                return null;
            }
            RelicRarity? rarity = RelicPool.ListedRarityOf(relicManager, relic);
            return rarity.HasValue && RelicPool.IsRerollable(rarity.Value) ? (int)rarity.Value : (int?)null;
        }

        /// <summary>
        /// Draws a new relic for every slot at that slot's own rarity, in slot order. Each rarity recycles on its own, as a
        /// single-rarity screen does: once every relic of that rarity has been offered here, it draws from those not on
        /// screen, so one rarity running out of unseen relics never empties its slots while others are still available.
        /// </summary>
        private static Relic[] DrawPerSlot(RelicManager relicManager)
        {
            var drawn = new Dictionary<int, IList<Relic>>();
            var drawnSoFar = new List<Relic>(); // never the same relic twice across rarities
            Relic[] onScreen = OfferedRelics();
            foreach (KeyValuePair<int, int> group in RerollRules.GroupSlots(_slotKinds))
            {
                RelicRarity rarity = (RelicRarity)group.Key;
                List<Relic> exclude = Instance.Seen.Concat(drawnSoFar).ToList();
                int unseen = RelicPool.CountAvailable(relicManager, rarity, exclude);
                if (RerollRules.ShouldRecycleSeen(unseen, RelicPool.CountAvailable(relicManager, rarity, onScreen.Concat(drawnSoFar))))
                {
                    exclude = onScreen.Concat(drawnSoFar).ToList();
                    ModLog.Info($"Every {rarity} relic has been offered here: drawing {rarity} from those not on screen.");
                }
                Relic[] offer = RelicPool.Draw(relicManager, rarity, group.Value, exclude);
                drawn[group.Key] = offer;
                drawnSoFar.AddRange(offer);
            }
            return RerollRules.InSlotOrder(_slotKinds, drawn).ToArray();
        }

        private static RelicManager RelicManagerFor(BattleUpgradeCanvas canvas)
        {
            return canvas != null ? Traverse.Create(canvas).Field("_relicManager").GetValue<RelicManager>() : null;
        }

        /// <summary>Called once the player has moved on to the map, or a new run starts.</summary>
        public static void EndInstance()
        {
            if (Instance.Seen.Count > 0)
            {
                ModLog.Info($"Moved on: cleared {Instance.Seen.Count} relic(s) from the do-not-offer list.");
            }
            Instance.Clear();
        }

        /// <summary>Writes the do-not-offer list for the current relic screen to the BepInEx log.</summary>
        private static void LogSeen()
        {
            string names = string.Join(", ", Instance.Seen.Select(RelicPool.DisplayName));
            ModLog.Info($"Do-not-offer list for this screen ({Instance.Seen.Count}): {names}");
            ModLog.Info($"{_baseRarity} relics left: {_unseenInScreenPool} unseen, {_notOnScreenInPool} not on screen (shop relics excluded)");
        }

        public static bool IsActive
        {
            get
            {
                if (_canvas == null || !_canvas.isActiveAndEnabled || _apiLimit == 0)
                {
                    return false;
                }
                // Deck view, map preview and similar windows cover the relic screen; don't act on it underneath.
                if (GameBlockingWindow.windowOpen)
                {
                    return false;
                }
                Traverse canvas = Traverse.Create(_canvas);
                if (canvas.Field("_state").GetValue<BattleUpgradeCanvas.State>() != BattleUpgradeCanvas.State.RELIC)
                {
                    return false;
                }
                // The game makes the canvas non-interactable once a relic is taken or skipped and it starts closing.
                CanvasGroup canvasGroup = canvas.Field("_mainCanvasGroup").GetValue<CanvasGroup>();
                if (canvasGroup != null && !canvasGroup.interactable)
                {
                    return false;
                }
                GameObject relicPanel = canvas.Field("_relicPanel").GetValue<GameObject>();
                if (relicPanel == null || !relicPanel.activeInHierarchy)
                {
                    return false;
                }
                // Hide while the player is inspecting a relic so the overlay doesn't cover the confirm panel.
                UpgradeConfirmationRelic confirmation = canvas.Field("_upgradeConfirmationRelic").GetValue<UpgradeConfirmationRelic>();
                return confirmation == null || !confirmation.gameObject.activeInHierarchy;
            }
        }

        public static void TryReroll()
        {
            if (!IsActive || PauseMenu.Paused)
            {
                return;
            }
            RelicManager relicManager = RelicManagerFor(_canvas);
            if (_slots.Any(icon => icon == null))
            {
                // A slot was destroyed: drop it and its rarity together, so the rest stay paired.
                _slotKinds = _slots.Select((icon, i) => icon != null && i < _slotKinds.Length ? (int?)_slotKinds[i] : null)
                    .Where(kind => kind.HasValue).Select(kind => kind.Value).ToArray();
                _slots = _slots.Where(icon => icon != null).ToArray();
            }
            RelicIcon[] icons = _slots;
            if (relicManager == null || icons.Length == 0 || !CanRerollNow || !RerollState.TrySpend())
            {
                UIAudioManager.InvalidSelectionSfxRequested?.Invoke();
                return;
            }

            // Every relic left has already been offered here: start the do-not-offer list over, keeping only
            // what's on screen so this reroll still changes something.
            if (RerollRules.ShouldRecycleSeen(_unseenInScreenPool, _notOnScreenInPool))
            {
                Instance.RecycleSeen(OfferedRelics());
                _unseenInScreenPool = _notOnScreenInPool;
                ModLog.Info("Do-not-offer list was full: recycled it, keeping only the relics on screen.");
            }

            // Exclude everything already offered on this screen (not just what is showing now) and all shop relics.
            // No fallback to a lower rarity: a slot with nothing left shows nothing rather than a cheaper relic.
            bool mixed = MixedRarities;
            RelicRarity rarity = mixed ? _baseRarity : RollRarity(relicManager);
            Relic[] offer = mixed ? DrawPerSlot(relicManager) : RelicPool.Draw(relicManager, rarity, icons.Length, Instance.Seen);

            // Safety net if the pool count and the game's queue ever disagree: don't charge for nothing new.
            if (!Instance.OffersSomethingNew(offer))
            {
                RerollState.Refund();
                UIAudioManager.InvalidSelectionSfxRequested?.Invoke();
                ModLog.Info("Reroll refunded: no unseen relics left to offer.");
                return;
            }

            RelicIcon.HideTooltip?.Invoke(false);
            for (int i = 0; i < icons.Length; i++)
            {
                // Show a slot again if an earlier, shorter reroll hid it; hide slots this offer can't fill.
                bool filled = i < offer.Length;
                icons[i].transform.parent.gameObject.SetActive(filled);
                if (filled)
                {
                    icons[i].SetRelic(offer[i]);
                    icons[i].shouldShowTooltip = false;
                    RerollAnimation.FlipIn(icons[i].transform.parent, i);
                }
            }
            RerollAnimation.PlaySound();
            KeepControllerSelectionVisible(icons);
            Instance.MarkSeen(offer);
            Instance.RecordReroll();
            RefreshPoolCounts(relicManager);
            ModLog.Info($"Rerolled at {(mixed ? "each slot's own rarity" : rarity.ToString())}:{offer.Length} of {icons.Length} slot(s) filled: {string.Join(", ", offer.Select(RelicPool.DisplayName))}. " +
                $"Rerolls left: {RerollState.Count}");
            LogSeen();
            RelicRerollsApi.RerollHappened(_isTreasure ? RerollPlaces.Chest : RerollPlaces.RelicScreen, OfferedRelics());

            if (rarity == RelicRarity.BOSS && _baseRarity != RelicRarity.BOSS)
            {
                ModLog.Info("Chest rerolled into a boss relic!");
                BombCelebration.Play();
            }
        }

        /// <summary>
        /// If a shorter reroll hid the slot a controller/keyboard player had selected, move the selection to the
        /// first slot so navigation doesn't get stuck on a hidden object.
        /// </summary>
        private static void KeepControllerSelectionVisible(RelicIcon[] icons)
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == null || selected.activeInHierarchy || icons.Length == 0)
            {
                return;
            }
            eventSystem.SetSelectedGameObject(icons[0].transform.parent.gameObject);
        }

        /// <summary>Only icons that are visible are on offer; the game hides unused slots.</summary>
        private static RelicIcon[] OfferedIcons()
        {
            GameObject relicPanel = _canvas != null ? Traverse.Create(_canvas).Field("_relicPanel").GetValue<GameObject>() : null;
            return relicPanel != null ? relicPanel.GetComponentsInChildren<RelicIcon>() : new RelicIcon[0];
        }

        private static Relic[] OfferedRelics()
        {
            return OfferedIcons().Select(icon => icon.relic).Where(relic => relic != null).ToArray();
        }

        private static RelicRarity RollRarity(RelicManager relicManager)
        {
            // Only common and rare chests can upgrade; any other rarity, known or not, is rerolled as itself.
            ChestTier? baseTier = ToTier(_baseRarity);
            if (!_isTreasure || baseTier == null || baseTier == ChestTier.Boss)
            {
                return _baseRarity;
            }
            // Deterministic per run seed and reroll number, and leaves the game's seeded UnityEngine.Random untouched.
            double roll = RerollRules.UpgradeRoll(StaticGameData.currentSeed, RerollState.Used);
            ChestTier chest = baseTier.Value;
            ChestTier rolled = RerollRules.ChestRerollTier(chest, roll, Plugin.ChestRareUpgradeChance.Value, Plugin.ChestBossUpgradeChance.Value);
            int unseenInRolled = rolled == chest ? _unseenInScreenPool : RelicPool.CountAvailable(relicManager, ToRarity(rolled), Instance.Seen);
            return ToRarity(RerollRules.AvailableTier(chest, rolled, unseenInRolled));
        }

        /// <summary>The chest tier for a rarity, or null for a rarity the mod doesn't know (never assumed to be Common).</summary>
        private static ChestTier? ToTier(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.COMMON:
                    return ChestTier.Common;
                case RelicRarity.RARE:
                    return ChestTier.Rare;
                case RelicRarity.BOSS:
                    return ChestTier.Boss;
                default:
                    return null;
            }
        }

        private static RelicRarity ToRarity(ChestTier tier)
        {
            switch (tier)
            {
                case ChestTier.Common:
                    return RelicRarity.COMMON;
                case ChestTier.Rare:
                    return RelicRarity.RARE;
                case ChestTier.Boss:
                    return RelicRarity.BOSS;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(tier), tier, "Unknown chest tier.");
            }
        }
    }

    [HarmonyPatch(typeof(BattleUpgradeCanvas), "SetupRelicGrant")]
    internal static class SetupRelicGrantPatch
    {
        private static void Postfix(BattleUpgradeCanvas __instance, RelicRarity rarity, bool isTreasure)
        {
            bool enabled = isTreasure ? Plugin.EnableChestReroll.Value : Plugin.EnableBattleReroll.Value;
            if (enabled)
            {
                RelicRerollSession.Begin(__instance, rarity, isTreasure);
            }
        }
    }
}
