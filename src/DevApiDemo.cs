#if DEBUG
using PeglinRelicRerolls.Core;
using Relics;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Debug builds only: registers through the public API the way another mod would, so the API can be tried in
    /// the game without writing a second mod. Switched on by [Dev] ApiTest in the config; compiled out of Release.
    /// </summary>
    internal static class DevApiDemo
    {
        private const string TestGuid = "PeglinRelicRerolls.DevApiTest";

        public static void Register()
        {
            if (Plugin.DevApiTest == null || !Plugin.DevApiTest.Value)
            {
                return;
            }
            bool hideShop = Plugin.DevApiHideShop.Value;
            RelicRerollsApi.SetStartingCountProvider(TestGuid, () => 7);
            // Floor boss: 2 rerolls. Rare chest: no reroll button. Everything else: no opinion.
            RelicRerollsApi.SetRelicScreenFilter(TestGuid, (rarity, isChest) =>
                !isChest && rarity == RelicRarity.BOSS ? 2 : isChest && rarity == RelicRarity.RARE ? 0 : (int?)null);
            RelicRerollsApi.SetShopFilter(TestGuid, () => !hideShop);
            // Never offer a relic whose name starts with "A".
            RelicRerollsApi.SetRelicFilter(TestGuid, relic => !RelicPool.DisplayName(relic).StartsWith("A"));
            RelicRerollsApi.SetStateSlotProvider(TestGuid, () => "devapitest");
            RelicRerollsApi.SetRerollListener(TestGuid, (where, offered) =>
                Plugin.Log.LogInfo($"DEV API test heard a reroll in {where}: {string.Join(", ", System.Linq.Enumerable.Select(offered, RelicPool.DisplayName))} ({RelicRerollsApi.RerollCount} left)."));
            Plugin.Log.LogInfo($"DEV API test registered: start 7 rerolls, boss limit 2, rare chests no reroll, relics starting with A never offered, " +
                $"shop {(hideShop ? "hidden" : "allowed")}, state file {StateSlots.FileName("devapitest")}.");
        }
    }
}
#endif
