using HarmonyLib;
using Peglin.ClassSystem;
using PeglinRelicRerolls.Core;
using PeglinUI.MainMenu.Cruciball;
using Saving;

namespace PeglinRelicRerolls
{
    /// <summary>Adds the reroll penalty to the cruciball level description on the loadout screen.</summary>
    [HarmonyPatch(typeof(CruciballLevelSelector), "UpdateCruciballSelection")]
    internal static class CruciballDescriptionPatch
    {
        private static void Postfix(CruciballLevelSelector __instance, Class ____selectedClass)
        {
            int level = PersistentPlayerData.Instance.GetSelectedCruciballLevel(____selectedClass);
            int penalty = Plugin.CruciballPenaltyPerLevel.Value;
            int[] levels = RerollRules.ParsePenaltyLevels(Plugin.CruciballPenaltyLevels.Value);
            if (!RerollRules.IsPenaltyLevel(penalty, levels, level) || __instance.currentLevelDescText == null)
            {
                return;
            }
            string rerolls = penalty == 1 ? "relic reroll" : "relic rerolls";
            __instance.currentLevelDescText.text += $" Start with {penalty} fewer {rerolls}.";
        }
    }
}
