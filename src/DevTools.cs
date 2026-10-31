#if DEBUG
using System.Collections.Generic;
using Battle;
using Currency;
using Data;
using HarmonyLib;
using Map;
using Scenarios;
using Scenarios.Shop;
using UnityEngine;
using UnityEngine.SceneManagement;
using Worldmap;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Developer shortcuts. Only compiled into Debug builds, and only active when DevMode is on in the config.
    /// Each hotkey loads a room the same way walking into that node would, from anywhere in a run, so the
    /// mod's screens can be tested quickly.
    /// </summary>
    internal static class DevTools
    {
        // Separate from UnityEngine.Random so dev jumps never disturb the run's seeded RNG.
        private static readonly System.Random Picker = new System.Random();

        public static void Update()
        {
            if (!Plugin.DevModeActive || PauseMenu.Paused)
            {
                return;
            }
            if (Plugin.DevGoldHotkey.Value.IsDown())
            {
                AddGold(1000);
            }
            else if (Plugin.DevRerollsHotkey.Value.IsDown())
            {
                AddRerolls(10);
            }
            else if (Plugin.DevChestHotkey.Value.IsDown())
            {
                Load("chest", map => Traverse.Create(map).Field("_treasureMapData").GetValue<MapDataTreasure>());
            }
            else if (Plugin.DevRareChestHotkey.Value.IsDown())
            {
                // The game's own flag for a guaranteed rare chest; it's checked and cleared when the chest opens.
                ChestScenarioController.rareChestRequested = true;
                Load("rare chest", map => Traverse.Create(map).Field("_treasureMapData").GetValue<MapDataTreasure>());
            }
            else if (Plugin.DevShopHotkey.Value.IsDown())
            {
                Load("shop", map => Traverse.Create(map).Field("_shopMapData").GetValue<MapDataShop>());
            }
            else if (Plugin.DevMinibossHotkey.Value.IsDown())
            {
                Load("miniboss", map => PickRandom(Traverse.Create(map).Field("_potentialEliteBattles").GetValue<List<MapDataBattle>>()));
            }
            else if (Plugin.DevBossHotkey.Value.IsDown())
            {
                Load("boss", BossFor);
            }
        }

        private static void AddGold(int amount)
        {
            if (MapController.instance == null)
            {
                Plugin.Log.LogWarning("Dev gold: no run in progress.");
                return;
            }
            // Goes through the game's own currency events, so shop prices re-check affordability.
            CurrencyManager.Instance.AddGold(amount);
            ModLog.Info($"Dev gold: +{amount}, now {CurrencyManager.Instance.GoldAmount}.");
        }

        private static void AddRerolls(int amount)
        {
            if (MapController.instance == null)
            {
                Plugin.Log.LogWarning("Dev rerolls: no run in progress.");
                return;
            }
            // Like any reroll change, this is kept once the game next saves (on reaching the map).
            RerollState.Add(amount);
            ModLog.Info($"Dev rerolls: +{amount}, now {RerollState.Count}.");
        }

        private static void Load(string room, System.Func<MapController, MapData> pick)
        {
            // MapController survives every scene in a run (DontDestroyOnLoad), so this works from any node.
            MapController map = MapController.instance;
            if (map == null)
            {
                Plugin.Log.LogWarning($"Dev {room}: no run in progress.");
                return;
            }
            MapData data = pick(map);
            if (data == null)
            {
                Plugin.Log.LogWarning($"Dev {room}: this floor has no {room} data.");
                return;
            }

            // Leaving a shop or battle part-way skips the code that normally clears these flags.
            ShopManager.isOpen = false;
            BattleController.BattleActive = false;

            ModLog.Info($"Dev {room}: loading {data.name} from scene {SceneManager.GetActiveScene().name}.");
            try
            {
                map.LoadSceneFromMapData(data);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Dev {room}: failed to load: {e}");
            }
        }

        /// <summary>The boss this floor's boss node is set to, falling back to any of its possible bosses.</summary>
        private static MapData BossFor(MapController map)
        {
            MapNode bossNode = map.bossNode;
            if (bossNode == null || bossNode.potentialMapData == null || bossNode.potentialMapData.Length == 0)
            {
                return null;
            }
            int index = bossNode.selectedBossIndex;
            return index >= 0 && index < bossNode.potentialMapData.Length
                ? bossNode.potentialMapData[index]
                : bossNode.potentialMapData[0];
        }

        private static T PickRandom<T>(List<T> options) where T : class
        {
            return options == null || options.Count == 0 ? null : options[Picker.Next(options.Count)];
        }
    }
}
#endif
