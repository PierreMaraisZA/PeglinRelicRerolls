using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PeglinRelicRerolls
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // BepInEx names the config file after the GUID, so this gives BepInEx/config/PeglinRelicRerolls.cfg.
        public const string Guid = "PeglinRelicRerolls";
        public const string Name = "PeglinRelicRerolls";
        public const string Version = "1.0.0";

        /// <summary>The Peglin version this mod was built and tested against (Unity's Application.version).</summary>
        public const string SupportedGameVersion = "2.0.12";

        // Names used before the mod was renamed, so existing settings and run state carry over.
        private const string LegacyConfigFile = "com.pierremarais.peglin.rerolls.cfg";
        internal const string LegacyStateFile = "PeglinRerolls.state";

        internal static ManualLogSource Log;

        internal static ConfigEntry<int> StartingRerolls;
        internal static ConfigEntry<bool> EnableBattleReroll;
        internal static ConfigEntry<bool> EnableChestReroll;
        internal static ConfigEntry<bool> EnableShopReroll;
        internal static ConfigEntry<bool> EnableShopRelicReroll;
        internal static ConfigEntry<int> ShopRerollLimit;
        internal static ConfigEntry<int> ShopRerollCost;
        internal static ConfigEntry<double> ChestRareUpgradeChance;
        internal static ConfigEntry<double> ChestBossUpgradeChance;
        // KeyboardShortcut rather than KeyCode: BepInEx writes the full ~400-key list under every KeyCode
        // setting in the .cfg file, but not for KeyboardShortcut. It also allows combos like "F9 + LeftShift".
        internal static ConfigEntry<KeyboardShortcut> RerollHotkey;
        internal static ConfigEntry<KeyboardShortcut> ControllerRerollButton;
        internal static ConfigEntry<bool> ShowRerollBadge;
        internal static ConfigEntry<bool> EnableLogging;
        internal static ConfigEntry<int> BossRerollLimit;
        internal static ConfigEntry<string> CruciballPenaltyLevels;
        internal static ConfigEntry<int> CruciballPenaltyPerLevel;

#if DEBUG
        // Dev mode only exists in Debug builds (dotnet build -c Debug). Release builds, the ones handed to
        // players, contain none of this code and no [Dev] config section, so it can't be switched on from the config.
        internal static ConfigEntry<bool> DevMode;
        internal static ConfigEntry<KeyboardShortcut> DevGoldHotkey;
        internal static ConfigEntry<KeyboardShortcut> DevRerollsHotkey;
        internal static ConfigEntry<KeyboardShortcut> DevChestHotkey;
        internal static ConfigEntry<KeyboardShortcut> DevRareChestHotkey;
        internal static ConfigEntry<KeyboardShortcut> DevMinibossHotkey;
        internal static ConfigEntry<KeyboardShortcut> DevBossHotkey;
        internal static ConfigEntry<KeyboardShortcut> DevShopHotkey;
        internal static ConfigEntry<int> DevStartingRerolls;

        internal static bool DevModeActive => DevMode != null && DevMode.Value;
#else
        internal static bool DevModeActive => false;
#endif

        private const string KeyHelp = " Key names: https://docs.unity3d.com/ScriptReference/KeyCode.html";

        /// <summary>
        /// Warns (always, regardless of EnableLogging) when Peglin isn't the version this mod was built for. The mod
        /// patches the game's own code, so a game update that renames or changes it can make features quietly stop
        /// working; this makes that visible in the log instead of failing silently.
        /// </summary>
        private void CheckGameVersion()
        {
            string gameVersion = Application.version;
            if (gameVersion == SupportedGameVersion)
            {
                return;
            }
            Log.LogWarning($"{Name} {Version} was built for Peglin v{SupportedGameVersion}, but this is Peglin v{gameVersion}. " +
                "Rerolls may not work correctly until the mod is updated for this game version.");
        }

        /// <summary>
        /// One-time move of the pre-rename config file to the new name, so existing settings are kept.
        /// BepInEx has already opened the (missing) new file by the time Awake runs, so reload it afterwards.
        /// </summary>
        private void MigrateLegacyConfig()
        {
            string legacy = System.IO.Path.Combine(Paths.ConfigPath, LegacyConfigFile);
            if (!System.IO.File.Exists(legacy) || System.IO.File.Exists(Config.ConfigFilePath))
            {
                return;
            }
            try
            {
                System.IO.File.Move(legacy, Config.ConfigFilePath);
                Config.Reload();
                Log.LogInfo($"Moved settings from {LegacyConfigFile} to {System.IO.Path.GetFileName(Config.ConfigFilePath)}.");
            }
            catch (System.Exception e)
            {
                Log.LogWarning($"Couldn't move {LegacyConfigFile} to the new config name: {e.Message}");
            }
        }

        private void Awake()
        {
            Log = Logger;
            MigrateLegacyConfig();
            CheckGameVersion();

            StartingRerolls = Config.Bind("General", "StartingRerolls", 5,
                "Rerolls granted at the start of every new run.");
            RerollHotkey = Config.Bind("General", "RerollHotkey", new KeyboardShortcut(KeyCode.T),
                "Keyboard shortcut for the reroll button on relic screens and in the shop." + KeyHelp);
            ControllerRerollButton = Config.Bind("General", "ControllerRerollButton", new KeyboardShortcut(KeyCode.JoystickButton9),
                "Controller button for rerolling. Default JoystickButton9 is clicking the right stick (R3) on Xbox-layout " +
                "controllers, including PlayStation and other pads when Steam Input is on. Set to None to disable." + KeyHelp);
            ShowRerollBadge = Config.Bind("General", "ShowRerollBadge", true,
                "Show a die with your reroll count above the relic bar, whenever the relic bar is on screen.");
            EnableLogging = Config.Bind("General", "EnableLogging", true,
                "Write reroll details (offers, do-not-offer lists, saves) to BepInEx/LogOutput.log. Warnings and errors are always logged.");

            EnableBattleReroll = Config.Bind("Relic Screens", "EnableBattleReroll", true,
                "Show the reroll button on the relic reward screen after miniboss/boss battles. Rerolls keep the same rarity.");
            EnableChestReroll = Config.Bind("Relic Screens", "EnableChestReroll", true,
                "Show the reroll button when opening a treasure chest.");
            ChestRareUpgradeChance = Config.Bind("Relic Screens", "ChestRareUpgradeChance", 0.15,
                new ConfigDescription("Chance that rerolling a common chest offers a rare relic instead.",
                    new AcceptableValueRange<double>(0.0, 1.0)));
            ChestBossUpgradeChance = Config.Bind("Relic Screens", "ChestBossUpgradeChance", 0.001,
                new ConfigDescription("Chance of something special when rerolling a rare chest.",
                    new AcceptableValueRange<double>(0.0, 1.0)));

            BossRerollLimit = Config.Bind("Relic Screens", "BossRerollLimit", 1,
                "Rerolls allowed on the floor boss relic reward. Set to -1 for no limit.");

            EnableShopReroll = Config.Bind("Shop", "EnableShopReroll", true,
                "Replace one common relic in each shop with a purchasable reroll (one per shop).");
            EnableShopRelicReroll = Config.Bind("Shop", "EnableShopRelicReroll", true,
                "Allow spending a reroll in the shop to reroll its unbought relics.");
            ShopRerollLimit = Config.Bind("Shop", "ShopRerollLimit", 3,
                "How many times each shop's relics can be rerolled. Set to -1 for no limit.");
            ShopRerollCost = Config.Bind("Shop", "ShopRerollCost", 20,
                "Gold cost of a reroll in the shop. Doubled by Wand of Greed and in act 4, like relic prices.");

            CruciballPenaltyLevels = Config.Bind("Cruciball", "PenaltyLevels", "5,10,15",
                "Comma-separated cruciball levels that each reduce your starting rerolls.");
            CruciballPenaltyPerLevel = Config.Bind("Cruciball", "PenaltyPerLevel", 1,
                "Rerolls removed for each penalty level reached. Set to 0 to disable.");

#if DEBUG
            DevMode = Config.Bind("Dev", "DevMode", false,
                "Enables developer shortcuts for testing. Leave off for normal play. Hotkeys work from anywhere in a run." + KeyHelp);
            DevGoldHotkey = Config.Bind("Dev", "GoldHotkey", new KeyboardShortcut(KeyCode.F4),
                "Dev mode only: add 1000 gold.");
            DevRerollsHotkey = Config.Bind("Dev", "RerollsHotkey", new KeyboardShortcut(KeyCode.F5),
                "Dev mode only: add 10 rerolls.");
            DevChestHotkey = Config.Bind("Dev", "ChestHotkey", new KeyboardShortcut(KeyCode.F6),
                "Dev mode only: jump to a chest room.");
            DevRareChestHotkey = Config.Bind("Dev", "RareChestHotkey", new KeyboardShortcut(KeyCode.F7),
                "Dev mode only: jump to a chest room that is guaranteed to be rare (rerolls can upgrade it to boss).");
            DevMinibossHotkey = Config.Bind("Dev", "MinibossHotkey", new KeyboardShortcut(KeyCode.F8),
                "Dev mode only: jump to a random miniboss fight for this floor.");
            DevBossHotkey = Config.Bind("Dev", "BossHotkey", new KeyboardShortcut(KeyCode.F9),
                "Dev mode only: jump to this floor's boss fight.");
            DevShopHotkey = Config.Bind("Dev", "ShopHotkey", new KeyboardShortcut(KeyCode.F10),
                "Dev mode only: jump to a shop. The shop isn't tied to the run seed, so its stock is random.");
            DevStartingRerolls = Config.Bind("Dev", "StartingRerolls", -1,
                "Dev mode only: starting rerolls for new runs, ignoring the cruciball penalty. -1 uses the normal [General] StartingRerolls.");
#endif

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
#if DEBUG
            Log.LogInfo($"{Name} {Version} loaded (DEBUG build, dev mode {(DevModeActive ? "on" : "off")}).");
#else
            Log.LogInfo($"{Name} {Version} loaded.");
#endif
        }
    }
}
