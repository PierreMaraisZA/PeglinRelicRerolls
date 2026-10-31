# Building PeglinRelicRerolls

## Requirements

- Windows with **Peglin** installed (the build references the game's own DLLs, which can't be shared in this repo).
- **BepInEx 5** installed in the game folder ([BepInExPack_Peglin](https://thunderstore.io/c/peglin/p/BepInEx/BepInExPack_Peglin/)).
- **.NET 8 SDK** (`winget install Microsoft.DotNet.SDK.8`).

The project expects the game at the default Steam location,
`C:\Program Files (x86)\Steam\steamapps\common\Peglin`. If yours is elsewhere, add `-p:GamePath="D:\Games\Peglin"`
to the build commands (or `-GamePath` to `pack.ps1`).

## Build

```
dotnet build PeglinRelicRerolls.csproj -c Debug     # for development: includes dev mode
dotnet build PeglinRelicRerolls.csproj -c Release   # for players: dev mode is compiled out
```

Both builds copy the DLL into the game's `BepInEx\plugins\PeglinRelicRerolls\` folder, so the last one you built is
the one the game loads. Restart Peglin after building.

## Tests

```
dotnet test tests/PeglinRelicRerolls.Tests.csproj
```

The game rules (reroll counter, cruciball penalty, save format, chest roll, shop die slot and price, limits) live in
`src/Core` with no Unity references, so they can be tested without the game. The tests compile those files directly.
GitHub Actions runs them on every push (`.github/workflows/tests.yml`); the mod itself can't be built there because
it needs the game's DLLs.

## Project layout

| Path | What's there |
|---|---|
| `src/Core/` | Pure game rules, unit tested |
| `src/Plugin.cs` | Entry point and all config settings |
| `src/RelicReroll.cs` | Rerolls on post-battle and chest relic screens |
| `src/ShopReroll.cs` | The shop reroll die and shop relic rerolls |
| `src/RelicPool.cs` | What a reroll can draw from (uses the game's own relic queues and validity rules) |
| `src/RerollState.cs`, `src/SavePatches.cs` | The reroll count, saved and restored with the run |
| `src/RerollHud.cs` | The reroll button and the counter badge above the relic bar |
| `src/DevTools.cs` | Dev hotkeys (Debug builds only) |
| `tests/` | xUnit tests for `src/Core` |
| `thunderstore/manifest.json` | Thunderstore package metadata |
| `pack.ps1` | Builds the Thunderstore zip |

## Dev mode (Debug builds only)

Set `DevMode = true` in the `[Dev]` section of `BepInEx/config/PeglinRelicRerolls.cfg`. Hotkeys work from anywhere in
a run:

| Key | Action |
|---|---|
| F4 | +1000 gold |
| F5 | +10 rerolls |
| F6 | Chest |
| F7 | Rare chest |
| F8 | Random miniboss for this floor |
| F9 | This floor's boss |
| F10 | Shop (its stock isn't tied to the run seed) |

`[Dev] StartingRerolls` overrides the starting rerolls for new runs (`-1` to use the normal setting).

Known dev-only issue: pressing a room hotkey while the game is already moving to another scene (e.g. while walking
onto a map node) can make the two loads race, and the room may fail to set up. Wait for the current scene to finish
loading first.

## Releasing a new version

1. Bump the version in `src/Plugin.cs` (`Version`) and `thunderstore/manifest.json` (`version_number`).
2. Add the changes to `CHANGELOG.md`.
3. If the mod was updated for a new Peglin version, update `SupportedGameVersion` in `src/Plugin.cs`.
4. Run `powershell -ExecutionPolicy Bypass -File pack.ps1`. It checks the versions match, runs the tests, builds
   Release, confirms no dev tools are in the DLL, and writes `dist/PierreMarais-PeglinRelicRerolls-<version>.zip`.
5. Commit, tag `v<version>`, push, and create a GitHub release with the zip:
   `gh release create v<version> dist/PierreMarais-PeglinRelicRerolls-<version>.zip --notes "..."`.
6. Upload the same zip to Thunderstore (Peglin community, team `PierreMarais`).
