# Release notes: PeglinRelicRerolls 1.0.1

## Changelog (`CHANGELOG.md`)

```markdown
## 1.0.1

- Fixed the controller reroll button (R3 by default) doing nothing when pressed.
- The Reroll button shows the reroll die before its text.
- The reroll counter's number is bigger and sits in the die's corner, like the game's relic counts.
```

## GitHub release

Title: `PeglinRelicRerolls 1.0.1` · Tag: `v1.0.1` · Asset: `PierreMarais-PeglinRelicRerolls-1.0.1.zip`

```markdown
Reroll the relics you're offered after minibosses, bosses, chests and in shops. Every run starts with 5 relic rerolls, and higher Cruciball levels take some away.

## What it does

- **Start every run with 5 relic rerolls.** Cruciball 5, 10 and 15 each take one away.
- **Reroll relic rewards** after minibosses, floor bosses and chests.
- **No repeats:** you won't see the same relic twice on one screen, or a relic you already own. If there's nothing left to reroll into, the button tells you.
- **Floor boss rewards** can be rerolled once.
- **Chests:** rerolling a common chest has a chance to offer a rare relic, and rerolling a rare chest has an ultra rare chance of something special.
- **Shop die:** one common relic in each shop is swapped for a reroll die you can buy for 20 gold (doubled in act 4 and with Wand of Greed, like relic prices). One die per shop.
- **Shop rerolls:** spend a reroll to swap the shop's unbought relics for new ones, up to 3 times per shop. Prices stay the same.
- **Reroll counter:** a die above your first relic shows how many rerolls you have, with the count in its corner like the game's own relic counts.
- **Saved with your run:** quitting and continuing can't give you extra rerolls or refund spent ones.
- **Stored on this computer only:** your rerolls are kept in their own file, separate from your Peglin save, which the mod doesn't touch. They aren't synced by Steam Cloud, so continuing a run on another computer starts you with the default number of rerolls.

## How it works

Press **T** on a keyboard, **R3** (click the right stick) on a controller, or click the **Reroll** button. The button appears on relic reward screens and in shops, shows the die and your remaining rerolls, and greys out with the reason when you can't reroll.

Every reroll spends one from your counter. The counter follows the run, so a relic screen you reopen, a shop you come back to, or a run you continue later all see the right number.

## Settings

In `BepInEx/config/PeglinRelicRerolls.cfg` (start the game once to create it, then restart after changing them):

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
- Works alongside my other mods.

## Known limitations

- The controller button assumes an Xbox-style layout, which is what Steam Input uses. Without Steam Input, a PlayStation controller may need a different `ControllerRerollButton`.
- The starting relic choice and relics from events can't be rerolled.
- Rerolls are stored on this computer only, not in your Peglin save, so they aren't synced by Steam Cloud.
- English only.

## Installation

### With a mod manager (recommended)

1. Install r2modman or the Thunderstore Mod Manager.
2. Search for **PeglinRelicRerolls** and install it.
3. Launch Peglin.

### Manually

1. Install [BepInExPack_Peglin](https://thunderstore.io/c/peglin/p/BepInEx/BepInExPack_Peglin/).
2. Download `PeglinRelicRerolls.dll` from this release.
3. Put it in `Peglin/BepInEx/plugins/`.
4. Start Peglin once to create the config file.

Built for Peglin v2.0.12.

## Some of my other mods

- [PeglinBalatroScore](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinBalatroScore/) — a Balatro-style pegs × damage score, flames and a bomb chest.
- [PeglinFonts](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinFonts/) — change the font of every text in the game, with five weights.
- [PeglinMaxSpeed](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinMaxSpeed/) — play at up to 6× speed without physics bugs.
- [PeglinQuickRestart](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinQuickRestart/) — hold a button to restart your run.

[Check out all my Peglin mods](https://old.thunderstore.io/c/peglin/p/PierreMarais/)

### [Please consider buying me a coffee ☕](https://ko-fi.com/pierremarais)
```

## Thunderstore

- **Description:** `README.md` from the zip (it carries the same features, settings, compatibility, limitations, installation and closing links as above).
- **Version notes:** `CHANGELOG.md` from the zip.
- **One-line summary:** the `description` in `thunderstore/manifest.json`, unchanged for this release: *"Reroll the relics you're offered after minibosses, bosses, chests and in shops. Start each run with 5 rerolls and buy more in shops."*

## Steam discussion post (BBCode)

Update the existing PeglinRelicRerolls post:

```
[h1]Mod: PeglinRelicRerolls - Available now![/h1]

Don't like the relics you're offered? Reroll them.

[b]Reroll relic rewards[/b] after minibosses, floor bosses and chests. Press [b]T[/b] on a keyboard, [b]R3[/b] (click the right stick) on a controller, or click the Reroll button.

[h2]What it does[/h2]
[list]
[*][b]Start every run with 5 relic rerolls.[/b] Cruciball 5, 10 and 15 each take one away.
[*][b]No repeats:[/b] you won't see the same relic twice on one screen, or a relic you already own. If there's nothing left to reroll into, the button tells you.
[*][b]Floor boss rewards[/b] can be rerolled once.
[*][b]Chests:[/b] rerolling a common chest has a chance to offer a rare relic, and rerolling a rare chest has an ultra rare chance of something special.
[*][b]Shop die:[/b] one common relic in each shop is swapped for a reroll die you can buy for 20 gold (doubled in act 4 and with Wand of Greed, like relic prices). One die per shop.
[*][b]Shop rerolls:[/b] spend a reroll to swap the shop's unbought relics for new ones, up to 3 times per shop. Prices stay the same.
[*][b]Reroll counter:[/b] a die above your first relic shows how many rerolls you have, with the count in its corner like the game's own relic counts.
[*][b]Saved with your run:[/b] quitting and continuing can't give you extra rerolls or refund spent ones.
[*][b]Stored on this computer only:[/b] your rerolls are kept in their own file, separate from your Peglin save, and aren't synced by Steam Cloud.
[/list]

[h2]How it works[/h2]
Every reroll spends one from your counter. The counter follows the run, so a relic screen you reopen, a shop you come back to, or a run you continue later all see the right number. The button greys out with the reason when you can't reroll.

[h2]Settings[/h2]
In [i]BepInEx/config/PeglinRelicRerolls.cfg[/i] (restart the game after changing them):
[list]
[*][b]StartingRerolls[/b] (default 5): rerolls at the start of every new run
[*][b]RerollHotkey[/b] (default T) and [b]ControllerRerollButton[/b] (default R3; set to None to turn it off)
[*][b]ShowRerollBadge[/b] (default true): show the reroll counter above your relics
[*][b]EnableBattleReroll[/b], [b]EnableChestReroll[/b], [b]EnableShopReroll[/b], [b]EnableShopRelicReroll[/b] (default true): turn each reroll type on or off
[*][b]BossRerollLimit[/b] (default 1; -1 for no limit) and [b]ShopRerollLimit[/b] (default 3; -1 for no limit)
[*][b]ShopRerollCost[/b] (default 20): gold price of the reroll die
[*][b]ChestRareUpgradeChance[/b] (default 0.15) and [b]ChestBossUpgradeChance[/b] (default 0.001)
[*][b]PenaltyLevels[/b] (default 5,10,15) and [b]PenaltyPerLevel[/b] (default 1; 0 turns it off)
[/list]

[h2]Known limitations[/h2]
[list]
[*]The controller button assumes an Xbox-style layout, which is what Steam Input uses. Without Steam Input, a PlayStation controller may need a different ControllerRerollButton.
[*]The starting relic choice and relics from events can't be rerolled.
[*]Rerolls are stored on this computer only, so they aren't synced by Steam Cloud.
[*]English only.
[/list]

[h2]Installation[/h2]
[b]With a mod manager (recommended)[/b]
[list=1]
[*]Install r2modman or the Thunderstore Mod Manager.
[*]Search for [b]PeglinRelicRerolls[/b] and install it.
[*]Launch Peglin.
[/list]

[b]Manually[/b]
[list=1]
[*]Install [url=https://thunderstore.io/c/peglin/p/BepInEx/BepInExPack_Peglin/]BepInExPack_Peglin[/url].
[*]Download [i]PeglinRelicRerolls.dll[/i] from the [url=https://github.com/PierreMaraisZA/PeglinRelicRerolls/releases]release[/url].
[*]Put it in [i]Peglin/BepInEx/plugins/[/i].
[*]Start Peglin once to create the config file.
[/list]

Built for Peglin v2.0.12.

[h2]Some of my other mods[/h2]
[list]
[*][url=https://thunderstore.io/c/peglin/p/PierreMarais/PeglinBalatroScore/]PeglinBalatroScore[/url] - a Balatro-style pegs x damage score, flames and a bomb chest.
[*][url=https://thunderstore.io/c/peglin/p/PierreMarais/PeglinFonts/]PeglinFonts[/url] - change the font of every text in the game, with five weights.
[*][url=https://thunderstore.io/c/peglin/p/PierreMarais/PeglinMaxSpeed/]PeglinMaxSpeed[/url] - play at up to 6x speed without physics bugs.
[*][url=https://thunderstore.io/c/peglin/p/PierreMarais/PeglinQuickRestart/]PeglinQuickRestart[/url] - hold a button to restart your run.
[/list]

[url=https://old.thunderstore.io/c/peglin/p/PierreMarais/]Check out all my Peglin mods[/url]

[h1][url=https://ko-fi.com/pierremarais]Please consider buying me a coffee ☕[/url][/h1]
```

## Steam guide

Set the guide title to **Mod: Reroll Relics - Available now!**, the summary to **Reroll relic rewards and shop relics in Peglin**, and the image to `icon.png`. Sections, in order, using the BBCode above:

1. **PeglinRelicRerolls description** — the two-line intro plus what it does.
2. **What it does** — the feature list.
3. **How it works** — pressing T / R3 / the button, and what the counter does.
4. **Settings** — the config list.
5. **Known limitations** — the four bullets.
6. **Installation** — mod manager and manual steps, ending with **Built for Peglin v2.0.12.**
7. **Some of my other mods**, the all-mods link and the coffee link.

## Ko-fi

Update the existing PeglinRelicRerolls post:

```markdown
**PeglinRelicRerolls: reroll the relics you're offered**

Don't like the relics you're offered? Reroll them. Press T, R3 on a controller, or click the Reroll button on relic reward screens and in shops.

**What it does**

- Start every run with 5 relic rerolls; Cruciball 5, 10 and 15 each take one away
- Reroll relic rewards after minibosses, floor bosses and chests
- No repeats: never the same relic twice on one screen, or a relic you already own
- Floor boss rewards can be rerolled once
- Rerolling a common chest can offer a rare relic
- A shop's unbought relics can be rerolled up to 3 times, at the same prices
- One common shop relic is swapped for a reroll die you can buy for 20 gold
- A die above your first relic shows how many rerolls you have
- Rerolls are saved with your run, so quitting and continuing can't grant or refund any

**Known limitations**

- The starting relic choice and relics from events can't be rerolled.
- The controller button assumes an Xbox-style layout; without Steam Input, a PlayStation controller may need a different button.
- Rerolls are stored on this computer only and aren't synced by Steam Cloud.
- English only.

**Install**

1. Install r2modman or the Thunderstore Mod Manager.
2. Search for PeglinRelicRerolls and install it.
3. Launch Peglin.

Or install BepInExPack_Peglin, put PeglinRelicRerolls.dll in `Peglin/BepInEx/plugins/`, and start the game once.

Built for Peglin v2.0.12.

**Some of my other mods**

- [PeglinBalatroScore](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinBalatroScore/)
- [PeglinFonts](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinFonts/)
- [PeglinMaxSpeed](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinMaxSpeed/)
- [PeglinQuickRestart](https://thunderstore.io/c/peglin/p/PierreMarais/PeglinQuickRestart/)

[Check out all my Peglin mods](https://old.thunderstore.io/c/peglin/p/PierreMarais/)

### [Please consider buying me a coffee ☕](https://ko-fi.com/pierremarais)
```
