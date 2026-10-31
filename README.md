# PeglinRelicRerolls

Don't like the relics you're offered? Reroll them.

## Features

- **Start every run with 5 relic rerolls.** Higher Cruciball means fewer: one less at Cruciball 5, 10 and 15.
- **Reroll relic rewards** after minibosses, floor bosses and chests. Press **T** on keyboard, **R3** (click the right stick) on a controller, or click the Reroll button.
- **No repeats:** you won't see the same relic twice on one screen, or a relic you already own. If there's nothing left to reroll into, the button tells you.
- **Floor boss rewards** can be rerolled **once**.
- **Chests:** rerolling a common chest has a chance to offer a rare relic. There is an ultra rare chance when rerolling a rare chest that something very special could happen...
- **Shop:** one common relic is swapped for a **reroll die** you can buy for **20 gold** (doubled in act 4 and with Wand of Greed, like relic prices). One die per shop.
- **Shop rerolls:** spend a reroll to swap the shop's unbought relics for new ones, up to **3 times per shop**. Prices stay the same.
- **Reroll counter:** a die above your relics shows how many rerolls you have.
- **Saved with your run:** quitting and continuing can't give you extra rerolls or refund spent ones.
- **Stored on this computer only:** your rerolls are kept in their own file, separate from your Peglin save, which the mod doesn't touch. They aren't synced by Steam Cloud, so continuing a run on another computer starts you with the default number of rerolls.

## Installation

**With a mod manager (recommended):** install PeglinRelicRerolls from Thunderstore with r2modman or the Thunderstore Mod Manager. BepInEx is installed for you.

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

## Known limitations

- The controller button assumes an Xbox-style layout, which is what Steam Input uses. Without Steam Input, a PlayStation controller may need a different `ControllerRerollButton`.
- The starting relic choice and relics from events can't be rerolled.
- English only.
