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

        /// <summary>The floor boss reward (not a chest that happened to upgrade to a boss relic).</summary>
        private static bool IsBossReward => !_isTreasure && _baseRarity == RelicRarity.BOSS;

        // Counts for the screen's own rarity, refreshed when the screen opens and after each reroll:
        // relics not yet offered on this screen, and relics other than the ones showing right now.
        private static int _unseenInScreenPool;
        private static int _notOnScreenInPool;

        private static int AvailableInScreenPool => RerollRules.AvailableToReroll(_unseenInScreenPool, _notOnScreenInPool);

        public static int RemainingOnScreen =>
            RerollRules.RemainingOnScreen(IsBossReward, Instance.RerollsUsed, Plugin.BossRerollLimit.Value);

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
            // Keyed by the relic screen itself, not the map node: every room load (including the bonus chest and
            // dev jumps, which reuse the same node without passing the map) creates a new screen and starts fresh,
            // while the same screen being set up again (e.g. by another mod) keeps its history and boss-limit count.
            Instance.Enter(canvas);
            _slots = OfferedIcons();
            Instance.MarkSeen(OfferedRelics());
            RefreshPoolCounts(RelicManagerFor(canvas));
            RerollHud.Ensure();
            ModLog.Info($"Relic screen opened: rarity={rarity}, chest={isTreasure}");
            LogSeen();
        }

        private static void RefreshPoolCounts(RelicManager relicManager)
        {
            _unseenInScreenPool = RelicPool.CountAvailable(relicManager, _baseRarity, Instance.Seen);
            _notOnScreenInPool = RelicPool.CountAvailable(relicManager, _baseRarity, OfferedRelics());
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
                if (_canvas == null || !_canvas.isActiveAndEnabled)
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
            RelicIcon[] icons = _slots.Where(icon => icon != null).ToArray();
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
            RelicRarity rarity = RollRarity(relicManager);
            Relic[] offer = RelicPool.Draw(relicManager, rarity, icons.Length, Instance.Seen);

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
            ModLog.Info($"Rerolled at {rarity}: {offer.Length} of {icons.Length} slot(s) filled: {string.Join(", ", offer.Select(RelicPool.DisplayName))}. " +
                $"Rerolls left: {RerollState.Count}");
            LogSeen();

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
            if (!_isTreasure || (_baseRarity != RelicRarity.COMMON && _baseRarity != RelicRarity.RARE))
            {
                return _baseRarity;
            }
            // Deterministic per run seed and reroll number, and leaves the game's seeded UnityEngine.Random untouched.
            double roll = RerollRules.UpgradeRoll(StaticGameData.currentSeed, RerollState.Used);
            ChestTier chest = ToTier(_baseRarity);
            ChestTier rolled = RerollRules.ChestRerollTier(chest, roll, Plugin.ChestRareUpgradeChance.Value, Plugin.ChestBossUpgradeChance.Value);
            int unseenInRolled = rolled == chest ? _unseenInScreenPool : RelicPool.CountAvailable(relicManager, ToRarity(rolled), Instance.Seen);
            return ToRarity(RerollRules.AvailableTier(chest, rolled, unseenInRolled));
        }

        private static ChestTier ToTier(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.BOSS:
                    return ChestTier.Boss;
                case RelicRarity.RARE:
                    return ChestTier.Rare;
                default:
                    return ChestTier.Common;
            }
        }

        private static RelicRarity ToRarity(ChestTier tier)
        {
            switch (tier)
            {
                case ChestTier.Boss:
                    return RelicRarity.BOSS;
                case ChestTier.Rare:
                    return RelicRarity.RARE;
                default:
                    return RelicRarity.COMMON;
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
