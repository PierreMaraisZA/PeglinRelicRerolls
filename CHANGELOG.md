# Changelog

## 1.1.0

- Added a public API (`RelicRerollsApi`) so other mods can work with rerolls: read and change the reroll count, set the starting count, limit or switch off rerolls per relic screen, switch off shop rerolls, keep chosen relics out of rerolls, and keep rerolls in their own save slot. With no mod using it, nothing changes.
- Mods with their own save flow can switch off the automatic saving and loading of the reroll count and save it themselves.
- Mods can listen for rerolls: they're told where each reroll happened (a relic screen, a chest or a shop) and which relics are now offered.
- A relic screen that shows a rarer relic than its own rarity (such as a mod's relic draft with rares among commons) rerolls that slot at the relic's own rarity. Every other slot, including the game's own fallback when a pool runs dry, rerolls at the screen's rarity as before.
- Reroll state can now live in separate slots, one file per slot. The default slot is the same `PeglinRelicRerolls.state` file as before, so your current run's rerolls carry over.
- A relic with a rarity the mod doesn't know is no longer treated as Common: it can't be picked for the reroll die and isn't rerolled in shops.
- The reroll die's price now follows the price multiplier of the shop slot it replaces (Wand of Greed, act 4) instead of checking for act 4, so it stays in step with relic prices. Prices are unchanged.
- Fixed the floor boss reroll being counted as used when another relic pick was opened on the same screen first (for example a mod's relic draft before the boss relic).
- Fixed shop purchases with the curse that makes things cost HP: buying the reroll die or a relic took the HP but gave nothing, and heals and damage in later battles threw errors until the game was restarted.

## 1.0.1

- Fixed the controller reroll button (R3 by default) doing nothing when pressed.
- The Reroll button shows the reroll die before its text.
- The reroll counter's number is bigger and sits in the die's corner, like the game's relic counts.

## 1.0.0

- First release: relic rerolls for miniboss, floor boss and chest rewards, a reroll die and relic rerolls in shops, a reroll counter above your relics, and fewer rerolls at Cruciball 5, 10 and 15.
