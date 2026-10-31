using Cruciball;
using HarmonyLib;
using Map;

namespace PeglinRelicRerolls
{
    [HarmonyPatch(typeof(GameInit), "Start")]
    internal static class NewRunPatch
    {
        private static void Prefix(GameInit __instance, CruciballManager ____cruciballManager)
        {
            if (Traverse.Create(__instance).Field("LoadData").Field("NewGame").GetValue<bool>())
            {
                RerollState.ResetForNewRun(____cruciballManager);
                RelicRerollSession.EndInstance();
            }
        }
    }

    /// <summary>
    /// Every new run passes through MapController.Start with NewGame set, before its first save, including paths
    /// that skip the GameInit scene (e.g. "restart run" buttons that load a map directly). Resetting here too means
    /// a new run can never start with the previous run's leftover rerolls.
    /// </summary>
    [HarmonyPatch(typeof(MapController), "Start")]
    internal static class NewRunMapPatch
    {
        private static void Prefix(MapController __instance, CruciballManager ____cruciballManager)
        {
            if (Traverse.Create(__instance).Field("_loadMapData").Field("NewGame").GetValue<bool>())
            {
                RerollState.ResetForNewRun(____cruciballManager);
                RelicRerollSession.EndInstance();
            }
        }
    }

    /// <summary>
    /// MapController.SaveData is the game's full run save (gold, relics, deck), so commit rerolls with it.
    /// It runs when the player reaches the map, which also means they've moved on from the last relic screen.
    /// </summary>
    [HarmonyPatch(typeof(MapController), "SaveData")]
    internal static class RunSavePatch
    {
        private static void Postfix()
        {
            RerollState.Commit();
            RelicRerollSession.EndInstance();
        }
    }

    /// <summary>Raised only when continuing a saved run, after the run seed has been restored.</summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.RequestLoad))]
    internal static class RunLoadPatch
    {
        private static void Postfix()
        {
            RerollState.Restore();
        }
    }
}
