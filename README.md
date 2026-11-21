# PeglinRelicRerolls

Don't like the relics you're offered? Reroll them.

## Features

- **Start every run with 5 relic rerolls.** Higher Cruciball means fewer: one less at Cruciball 5, 10 and 15.
- **Reroll relic rewards** after minibosses, floor bosses and chests. Press **T** on a keyboard, **R3** (click the right stick) on a controller, or click the Reroll button.
- **No repeats.** You won't see the same relic twice on one screen, or a relic you already own. If there's nothing left to reroll into, the button tells you why.
- **Floor boss rewards** can be rerolled **once**.
- **Chests.** Rerolling a common chest has a chance to offer a rare relic. Rerolling a rare chest has an ultra rare chance that something very special happens...
- **Reroll die in every shop.** One common relic is swapped for a die you can buy for **20 gold** (doubled in act 4 and with Wand of Greed, like relic prices). One die per shop.
- **Shop rerolls.** Spend a reroll to swap the shop's unbought relics for new ones, up to **3 times per shop**. Prices stay the same.
- **Reroll counter.** A die above your first relic shows how many rerolls you have, with the count in its corner like the game's own relic counts.
- **Saved with your run.** Quitting and continuing can't give you extra rerolls or refund spent ones.

## For mod authors

Other mods can work with the rerolls through `RelicRerollsApi`, a static class in this plugin. This mod doesn't know about your mod: you find the class by name, check `ApiVersion`, and register with your own plugin GUID. Registering again with the same GUID replaces your earlier registration, and `RemoveAll(guid)` removes everything you registered. A callback that throws is logged once and treated as "no opinion". With nothing registered the mod behaves as normal.

| Member | What it does |
|---|---|
| `ApiVersion` | Currently `1` |
| `RerollCount` | Rerolls left in the current run |
| `AddRerolls(int)` | Adds rerolls, or removes them if negative (never below 0) |
| `SetStartingCountProvider(guid, Func<int?>)` | Starting rerolls for a new run. `null` means no opinion (the usual rule, with the Cruciball penalty). Only asked when a new run starts, never when a run is continued |
| `SetRelicScreenFilter(guid, Func<RelicRarity, bool, int?>)` | Per relic screen, given its rarity and whether it is a chest: `null` no opinion, `0` no reroll button, `-1` unlimited, `n` at most n rerolls. The smallest answer wins, and it replaces `BossRerollLimit` for that screen |
| `SetShopFilter(guid, Func<bool>)` | `false` from anyone hides the reroll die and shop relic rerolls |
| `SetRelicFilter(guid, Func<Relic, bool>)` | `false` from anyone keeps that relic out of every reroll |
| `SetStateSlotProvider(guid, Func<string>)` | The state slot for the current run. `null` or empty is the default slot (`PeglinRelicRerolls.state`); other names use `PeglinRelicRerolls.<slot>.state`. Use lowercase letters, digits, `-` and `_`; anything else is replaced and a short hash is added so different names never share a file |
| `SetAutoSaveFilter(guid, Func<bool>)` | `false` from anyone stops the mod saving and loading the count by itself when the game saves or continues a run; the mode then calls `Commit()` and `Restore()` at its own save and load points. A new run still resets the count |
| `Commit()` / `Restore()` | Save or load the count for the current slot now, for modes with their own save flow |
| `SetRerollListener(guid, Action<string, Relic[]>)` | Called after every reroll that changed the offer (never a refunded one), with where it happened (`relic_screen`, `chest` or `shop`; more names may be added, so ignore ones you don't know) and every relic now offered there, in slot order. Read `RerollCount` for the rerolls left |
| `RemoveAll(guid)` | Removes everything you registered |

Notes: a starting-count provider can be asked more than once while a new run starts, so return the same value each time. When a run is continued and its slot has no saved state, the normal starting count is used, not your provider's. If a slot provider throws, or calls `Commit()` or `Restore()` while it is being asked, nothing is saved or loaded that time, so one mode's rerolls never land in another's file.

Example, using reflection so your mod doesn't need a reference to this one:

```csharp
using System;
using System.Linq;
using BepInEx.Bootstrap;

// In your plugin, after BepInEx has loaded the other plugins (e.g. in Start, or declare
// [BepInDependency("PeglinRelicRerolls", BepInDependency.DependencyFlags.SoftDependency)]).
if (Chainloader.PluginInfos.TryGetValue("PeglinRelicRerolls", out var info))
{
    Type api = info.Instance.GetType().Assembly.GetTypes().FirstOrDefault(t => t.Name == "RelicRerollsApi");
    int version = api == null ? 0 : (int)api.GetProperty("ApiVersion").GetValue(null, null);
    if (version >= 1)
    {
        // Ten starting rerolls and no shop rerolls while my mode is running.
        api.GetMethod("SetStartingCountProvider").Invoke(null, new object[] { "my.mod.guid", new Func<int?>(() => 10) });
        api.GetMethod("SetShopFilter").Invoke(null, new object[] { "my.mod.guid", new Func<bool>(() => false) });
    }
}
```

## Installation

**With a mod manager (recommended):**
1. Install r2modman or the Thunderstore Mod Manager.
2. Search for PeglinRelicRerolls and install it. BepInEx is installed for you.
3. Launch Peglin.

**Manually:**
1. Install [BepInExPack_Peglin](https://thunderstore.io/c/peglin/p/BepInEx/BepInExPack_Peglin/).
2. Put `PeglinRelicRerolls.dll` in `Peglin/BepInEx/plugins/`.
3. Start the game once to create the config file.

## Settings

Change these in `BepInEx/config/PeglinRelicRerolls.cfg` (start the game once to create it). Restart the game after changing them.

| Setting | Default | What it does |
|---|---|---|
| `StartingRerolls` | 5 | Rerolls at the start of every new run |
| `RerollHotkey` | T | Keyboard key for rerolling |
| `ControllerRerollButton` | JoystickButton9 (R3) | Controller button for rerolling. `None` turns it off |
| `ShowRerollBadge` | true | Show the reroll counter above your relics |
| `EnableLogging` | true | Write reroll details to `BepInEx/LogOutput.log` |
| `EnableBattleReroll` | true | Allow rerolls after miniboss and boss battles |
| `EnableChestReroll` | true | Allow rerolls in chests |
| `ChestRareUpgradeChance` | 0.15 | Chance that rerolling a common chest offers a rare relic |
| `ChestBossUpgradeChance` | 0.001 | Chance of something special when rerolling a rare chest |
| `BossRerollLimit` | 1 | Rerolls allowed on floor boss rewards. `-1` for no limit |
| `EnableShopReroll` | true | Put a reroll die in every shop |
| `EnableShopRelicReroll` | true | Allow rerolling the shop's relics |
| `ShopRerollLimit` | 3 | Shop rerolls allowed per shop. `-1` for no limit |
| `ShopRerollCost` | 20 | Gold price of the reroll die |
| `PenaltyLevels` | 5,10,15 | Cruciball levels that each take rerolls away |
| `PenaltyPerLevel` | 1 | Rerolls taken away at each of those levels. `0` turns it off |

## Compatibility

- Built for **Peglin v2.0.12**. On other versions the mod writes a warning to the log, as some features may not work.
- Requires [BepInExPack_Peglin](https://thunderstore.io/c/peglin/p/BepInEx/BepInExPack_Peglin/).

## Good to know

- The starting relic choice and relics from events can't be rerolled.
- Your rerolls are kept in their own file in `BepInEx/config`, separate from your Peglin save, which the mod doesn't touch. They aren't synced by Steam Cloud, so continuing a run on another computer starts you with the default number of rerolls.
- Quitting before the game saves (for example on a reward screen) takes the run back to its last save, rerolls included.
- The controller button assumes an Xbox-style layout, which is what Steam Input uses. Without Steam Input, a PlayStation controller may need a different `ControllerRerollButton`.

## Some of my other mods

- [PeglinBalatroScore](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinBalatroScore/) — a Balatro-style pegs × damage score, with flames and a bomb chest.
- [PeglinFonts](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinFonts/) — change the font of every text in the game.
- [PeglinKeybinds](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinKeybinds/) — rebind keyboard and controller controls from inside the game.
- [PeglinMaxSpeed](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinMaxSpeed/) — play at up to 15× speed without physics bugs.
- [PeglinQuickRestart](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinQuickRestart/) — hold a button to restart your run.

[Check out all my Peglin mods](https://old.thunderstore.io/c/peglin/p/PierreMarais/)

### [Please consider buying me a coffee ☕](https://ko-fi.com/pierremarais)
