<p align="center">
  <!-- Header banner: 1300 x 372 -->
  <img src="docs/header.png" alt="9 Qualities of Life" width="1300">
</p>

<h1 align="center">9 Qualities of Life</h1>

<p align="center">
  <b>Nine-and-then-some quality-of-life upgrades for <a href="https://store.steampowered.com/app/2784470/9_Kings/">9 Kings</a>.</b><br>
  Less waiting, more information, smoother late-game. Nothing that changes the balance.
</p>

<p align="center">
  <a href="https://github.com/M4cs/9QoL/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/M4cs/9QoL?style=for-the-badge&label=Download&color=e0a63a"></a>
  <a href="https://thunderstore.io/c/9-kings/"><img alt="Thunderstore" src="https://img.shields.io/badge/Thunderstore-9%20Kings-2b6cb0?style=for-the-badge"></a>
  <a href="https://www.nexusmods.com/9kings"><img alt="Nexus Mods" src="https://img.shields.io/badge/Nexus%20Mods-9%20Kings-d97a2b?style=for-the-badge"></a>
  <a href="LICENSE"><img alt="MIT" src="https://img.shields.io/badge/License-MIT-6b7280?style=for-the-badge"></a>
</p>

---

## ✨ What you get

| | Feature | What it does |
|---|---|---|
| ⚔️ | **Troop render cap** | Only draws the first N allies and N enemies. Everyone still exists and still fights, they just aren't rendered. Turns late-Endless slideshows back into a game. |
| 🎚️ | **In-game settings** | A **9 Qualities of Life** tab in the options menu. Troop cap, FPS limit and every feature toggle, styled like the game's own. |
| ⏩ | **4x and 10x speed** | Click the 3x button again. 3x → 4x → 10x → 3x. Icon tints amber, then red. |
| 🃏 | **Card level counts** | Loot, shop and hand cards show how many of that card you own at each level (`Lv. 1: 2   Lv. 2: 1`). Spot the pending level-up before you pick. |
| 🔵 | **Upgrade plot glow** | Pick up a card and every plot it would *level up* lights up with the game's own plot marker. No more hovering plot by plot. |
| 🏷️ | **Plot level labels** | Every occupied plot shows `Lv 2/4` on the map at all times. Gold when maxed. |
| ⌨️ | **Hotkeys** | **Space** starts the battle. **F** rerolls the king. Hold **V** to see which plots the next blessing or cataclysm will hit. |
| 🚀 | **Faster animations** | Card reveals, fades, popups and level-up chains run at 2x between battles, UI animators run at 1.5x, and the pause between end-of-wave events is halved. Combat speed is untouched. |
| 🖥️ | **FPS limit** | Cap the frame rate from inside a run, not just the main menu. |

Every feature can be switched off in the options menu or the config file.

## 📸 Screenshots

<!-- Screenshot slots. Drop PNGs into docs/screenshots/ with these names. -->

<table>
  <tr>
    <td align="center"><img src="docs/screenshots/plot-labels.png" alt="Plot level labels" width="440"><br><sub><b>Plot level labels</b> — see every level at a glance</sub></td>
    <td align="center"><img src="docs/screenshots/upgrade-glow.png" alt="Upgrade plot glow" width="440"><br><sub><b>Upgrade glow</b> — hold a card, upgradeable plots light up</sub></td>
  </tr>
  <tr>
    <td align="center"><img src="docs/screenshots/card-counts.png" alt="Card level counts" width="440"><br><sub><b>Card level counts</b> — under loot and shop cards</sub></td>
    <td align="center"><img src="docs/screenshots/options-menu.png" alt="Options menu" width="440"><br><sub><b>Options menu</b> — everything is a toggle</sub></td>
  </tr>
  <tr>
    <td align="center"><img src="docs/screenshots/speed-10x.png" alt="10x speed" width="440"><br><sub><b>10x speed</b> — the 3x button goes further</sub></td>
    <td align="center"><img src="docs/screenshots/event-preview.png" alt="Event plot preview" width="440"><br><sub><b>Event preview</b> — hold V to see the next cataclysm's plots</sub></td>
  </tr>
</table>

## 📦 Install

**Thunderstore / r2modman / Gale** — install *9 Qualities of Life* from the 9 Kings community. BepInEx is pulled in automatically.

**Manual**

1. Install [BepInEx 6 IL2CPP](https://thunderstore.io/c/9-kings/p/BepInEx/BepInExPack_IL2CPP/) into the game folder and launch the game once.
2. Grab the latest `NineQoL-x.y.z-nexus.zip` from [Releases](https://github.com/M4cs/9QoL/releases/latest) and drop its `BepInEx` folder onto your game folder, so the DLL lands in `BepInEx/plugins/NineQoL.dll`.
3. Play. The **9 Qualities of Life** tab appears in the options menu.

Requires 9 Kings on Steam (Unity 6000.3, IL2CPP) and BepInEx 6.0.0-be.755 or newer.

## 🎮 Controls

| Key | Action | When |
|---|---|---|
| `Space` | Start battle | Placing phase |
| `F` | Reroll the king's offer | Placing phase, when the reroll button is shown |
| `V` (hold) | Mark the plots the next blessing / cataclysm will hit | Placing phase |
| `3x` button | Cycle 3x → 4x → 10x | During battle |

Keys are configurable. Avoid keys the game already uses: `R` (quick restart), `C`, `I`, `K`, `O`, `P`, `Q`, `Tab`, `1` `2` `3`.

## ⚙️ Configuration

Everything lives in `BepInEx/config/dev.oglabs.9kings.qol.cfg` (created on first launch). The common ones also have a slider or toggle in the options menu.

<details>
<summary><b>All settings</b></summary>

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch for the troop render cap. |
| `MaxVisibleAllies` / `MaxVisibleEnemies` | `150` | How many of each side are drawn at once. |
| `RefreshInterval` | `0.5` | Seconds between visibility passes. |
| `DisableHiddenAnimators` | `true` | Also pause Unity animators on hidden troops (most of the CPU savings). |
| `AlwaysShowBosses` | `true` | Bosses are never hidden and never count toward the cap. |
| `FpsLimit` | `0` | Frame-rate cap, 0 = off. |
| `SpeedCycleSpeeds` | `3,4,10` | Speeds the 3x button cycles through. First entry must be 3. |
| `SelectedSpeedCycleLevel` | `0` | Last selected speed-cycle entry; maintained automatically across runs and restarts. |
| `ShowCardLevelBreakdown` | `true` | Level counts under loot / shop / hand cards. |
| `UpgradeGlow` | `true` | Mark upgradeable plots while holding a card. |
| `UpgradeGlowStyle` | `Marker` | `Marker` (game's plot marker), `Tint` (blue plot tint) or `Outline`. |
| `UpgradeGlowColor` | `#4C8CFF` | Tint colour for the `Tint` style. `auto` samples the game's blessing colour. |
| `PlotLevelLabels` | `true` | Always-visible level label on occupied plots. |
| `PlotLevelLabelFormat` | `Lv {level}/{max}` | Label text. `/{max}` is dropped for cards without a cap. |
| `PlotLevelLabelScale` | `1` | Label size multiplier. |
| `PlotLevelLabelVerticalOffset` | `-0.3` | Label position in plot heights (0 = centre, -0.5 = bottom corner). |
| `Hotkeys.Enabled` | `true` | Keyboard shortcuts on/off. |
| `StartBattleKey` / `KingRerollKey` / `CataclysmPreviewKey` | `Space` / `F` / `V` | Key names from Unity's InputSystem. `None` disables one. |
| `FasterAnimations` | `true` | Master switch for the three speed-ups below. |
| `IdleTimeScale` | `2` | Game speed held while no battle is running. 1 = off. |
| `UiAnimatorSpeed` | `1.5` | Playback speed of the gameplay UI's animators. |
| `EventDelayScale` | `0.5` | Multiplier for the delay between end-of-wave events and plot popups. |
| `VerboseLogging` | `false` | Extra diagnostics in `BepInEx/LogOutput.log`. |

</details>

## 🧠 How it works (for the curious)

- **Render cap.** A background component walks the game's live troop list twice a second and flips `Renderer.forceRenderingOff` on troops past the cap. That flag is rendering-only; combat, targeting, movement and wave logic never read it. Animators the mod pauses are remembered and only those are resumed.
- **Upgrade glow.** The game marks blessing / cataclysm plots by toggling a marker object on each plot. The mod toggles the same object for every plot where the game's own placement check says the held card would go on top of an existing one.
- **Plot labels.** World-space TextMeshPro objects parented to each plot, using the plot's own damage-text font and sorting layer.
- **Faster animations.** Almost every fade, reveal and popup in the game runs on scaled time through the game's `Progress` helper, so holding the time scale at 2x between battles speeds all of them at once. The boost only engages while a between-battle screen is up and no enemy is alive.
- **Hotkeys** press the real buttons, so the game's own sound effects and state changes happen exactly as with a click.

## 🛠️ Build from source

```
dotnet build NineQoL.csproj -c Release
```

The project references BepInEx and the game's generated interop assemblies straight out of the game folder (`GamePath` in the csproj, or the `NINEKINGS_PATH` environment variable) and copies the DLL into `BepInEx/plugins/` and `dist/` after every build.

<details>
<summary><b>Releasing</b></summary>

Releases are cut from git tags by `.github/workflows/release.yml`. The plugin needs the game's interop assemblies to compile, so it is built locally and `dist/NineQoL.dll` is committed; the workflow verifies and packages it.

1. Set the same version in `Plugin.cs` (`PluginVersion`) and `manifest.json` (`version_number`), add a `## <version>` section to `CHANGELOG.md`.
2. `dotnet build NineQoL.csproj -c Release`, commit.
3. `git tag v<version> && git push --tags`.

The workflow refuses to publish if the versions disagree, and attaches `NineQoL-<version>-thunderstore.zip` (upload as-is to Thunderstore) and `NineQoL-<version>-nexus.zip` (`BepInEx/plugins/` layout for Nexus / manual installs) to the GitHub Release.

</details>

## 🙋 Notes

- Hidden troops still spawn hit effects, blood and projectiles; only the bodies are culled. When a visible troop dies, a hidden one takes its slot within half a second.
- The mod reads the game's own types by name. If a game update renames them, the affected feature logs a warning and turns itself off; rebuild against the new interop assemblies to bring it back.
- Found a bug or want another quality of life? [Open an issue](https://github.com/M4cs/9QoL/issues).
