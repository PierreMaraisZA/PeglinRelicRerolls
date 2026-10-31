using HarmonyLib;
using I2.Loc;
using PeglinUI;
using Relics;
using UnityEngine;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Lets the shop die use the game's own relic tooltip. A stand-in Relic is passed to
    /// TooltipManager.ShowTooltipRelic, and the tooltip patch below fills in our text for it.
    /// </summary>
    internal static class RerollTooltip
    {
        public const string Title = "Reroll";

        // Colours taken from the game's tooltips: orange keyword (like "Activated"), green value (like "Heal 1").
        public const string Description =
            "Gain <color=#3A9A2A>1</color> <color=#D2662E>Reroll</color>. Use it to swap the relics offered after minibosses and bosses, in chests, or in the shop.";

        private static Relic _relic;

        public static Relic Relic
        {
            get
            {
                if (_relic == null)
                {
                    _relic = ScriptableObject.CreateInstance<Relic>();
                    _relic.name = "PeglinRelicRerollsReroll";
                    _relic.locKey = "peglinrerolls_reroll";
                    _relic.sprite = DieSprite.Get();
                    _relic.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                return _relic;
            }
        }
    }

    [HarmonyPatch(typeof(Tooltip), nameof(Tooltip.Initialize), typeof(Relic))]
    internal static class RelicTooltipPatch
    {
        private static bool Prefix(Tooltip __instance, Relic relic)
        {
            Localize localize = __instance.descriptionText.GetComponent<Localize>();
            if (relic == null || relic != RerollTooltip.Relic)
            {
                // A real relic: make sure the description is localized again after showing ours.
                if (localize != null)
                {
                    localize.enabled = true;
                }
                return true;
            }

            // Our text has no localization term, so stop Localize from overwriting it.
            if (localize != null)
            {
                localize.enabled = false;
            }
            __instance.nameText.gameObject.SetActive(true);
            __instance.damageInfo.gameObject.SetActive(false);
            __instance.nameText.text = RerollTooltip.Title;
            __instance.descriptionText.text = RerollTooltip.Description;
            __instance.keywordDesc?.UpdateString("none");
            Traverse.Create(__instance).Field("_keywordDescCount").SetValue(0);
            __instance.cruciballIndicator.gameObject.SetActive(false);
            return false;
        }
    }
}
