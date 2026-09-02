# 9 Qualities of Life — 9 Kings BepInEx mod

Formerly "Troop Render Cap". Performance and quality-of-life improvements for 9 Kings.

Caps how many troops are *drawn* on screen without changing how many actually
exist. All troops keep fighting, moving, and dealing damage at their real
numbers — the ones over the cap simply aren't rendered. This cuts draw calls
and (optionally) Unity Animator CPU time, which is the main source of slowdown
and input lag in late Endless runs.

Since 1.1.0 the mod also adds:

- **In-game settings** (a dedicated "9 Qualities of Life" category in the options menu, styled like the game's own categories, with toggles for the card QoL features):
  - *MAX VISIBLE TROOPS* — slider (25–1000) that drives the render caps below.
  - *FPS LIMIT* — a clone of the game's own framerate slider, which the base
    game only shows in the main-menu Graphics section. The clone proxies into
    the real slider, so the game's own apply/persist pipeline does the work.
    It hides itself whenever the real Graphics slider is visible.
- **Card level breakdown** (1.2.0): loot/shop cards show a per-level count
  under the card (e.g. `Lv. 1: 2   Lv. 2: 1`) of plots that already hold the same
  card, so you can spot buildings/troops due for a level up at a glance.
  Hidden for spells and cards you haven't placed yet.
- **Upgrade plot glow** (reworked in 1.4.0): while you hold a card, every
  plot the card would upgrade (same card placed there, not yet max level) is
  marked for as long as you hold it — no need to hover. By default the mark
  is the game's own plot marker, the one the calendar's blessing/cataclysm
  preview switches on, so it looks exactly like a blessing marking.
  `UpgradeGlowStyle` can switch this to `Tint` (whole plot tinted blue,
  colour from `UpgradeGlowColor`, brighter while hovered) or `Outline` (the
  card effect outline). Markings are re-asserted while the card is held, so
  the game's own previews can't strip them, and are removed the moment the
  card is placed or dropped.
- **Hotkeys** (1.4.0): during the placing phase, **Space** presses START
  BATTLE, **F** presses the king reroll button (when it is offered) and
  holding **V** marks the plots the upcoming blessing/cataclysm will hit
  (same as hovering the calendar entry). Keys are configurable
  (`StartBattleKey`, `KingRerollKey`, `CataclysmPreviewKey`; InputSystem key
  names, `None` to disable) and the whole feature has an in-game toggle. The
  click hotkeys never fire while a card is being dragged, a menu / policy
  screen is open, or a text field has focus. The game already uses R (quick
  restart), C, I, K, O, P, Q, Tab and 1/2/3, so avoid those.
- **Plot level labels** (1.4.0): every occupied plot shows an always-visible
  "Lv 2/4" label (gold at max level). World-space TextMeshPro objects that use
  the plot's own damage-text font and sorting layer. Format, size and vertical
  position are configurable (`PlotLevelLabelFormat`, `PlotLevelLabelScale`,
  `PlotLevelLabelVerticalOffset`).
- **Faster animations** (1.4.0), three levers under one toggle:
  - *Idle time-scale boost* (`IdleTimeScale`, default 2): between battles the
    game speed is held at 2x. The game's own `Progress` helper drives nearly
    every fade, card reveal, plot popup and level-up chain on scaled time, so
    this speeds all of them without touching combat. Only applied while a
    between-battle screen is up (placing view, loot, shop, prophet, policy)
    and no enemy is alive; never while the pause menu is open or the game
    itself has paused time.
  - *UI animator speed* (`UiAnimatorSpeed`, default 1.5): Unity Animators in
    the gameplay UI (card hover pops, panels, buttons) play faster. Only
    animators at the default speed are touched.
  - *Event pacing* (`EventDelayScale`, default 0.5): the wave options'
    DelayBetweenEvents, TimeToNextWave and plot popup delays are halved. The
    countdown before enemies arrive (TimeBeforeWave) is left alone.
- **Multi-level triple speed**: with 3x active, clicking the 3x button again
  cycles 3x → 4x → 10x → back to 3x. The button icon tints amber at 4x and red
  at 10x. Pausing, slow-motion effects, and switching to 1x/2x are untouched —
  the boost only engages while the game itself is running SuperFast (timescale
  above 2.25) and is enforced in LateUpdate so it wins against the game's
  timescale lerp. The speed steps are configurable (`SpeedCycleSpeeds`,
  default `3,4,10`).

## How it works

- A background component polls the game's `TroopSystem._validTroops` list on a
  configurable interval (default 0.5 s).
- Troops beyond the cap get `Renderer.forceRenderingOff = true` on every
  `SpriteRenderer` the game registered for them (`Entity.m_Renderers`).
  `forceRenderingOff` is a pure rendering flag — game logic never reads it, so
  combat, targeting, movement, healing, and wave logic are untouched.
- Optionally the Unity `Animator` on hidden troops is disabled too (default
  on). The mod remembers exactly which animators *it* disabled and re-enables
  only those when a troop becomes visible again, so it never fights the game's
  own animator management.
- Allies and enemies are capped independently. Bosses are always rendered.
- Disabling the mod in the config restores every troop to visible.

## Requirements

- 9 Kings (Steam, IL2CPP build, Unity 6000.3.x)
- BepInEx 6 Bleeding Edge IL2CPP x64 (tested with 6.0.0-be.755, which supports
  this game's IL2CPP metadata v39)

## Install

1. Install BepInEx IL2CPP into the game folder (Thunderstore package
   `BepInEx-BepInExPack_IL2CPP` works) and run the game once so
   `BepInEx/interop/` gets generated.
2. Drop `NineQoL.dll` into `BepInEx/plugins/`.

## Build from source

```
dotnet build NineQoL.csproj -c Release
```

The csproj references BepInEx core + interop assemblies straight out of the
game folder (`<GamePath>` property — edit it if your install lives elsewhere)
and copies the built DLL into `BepInEx/plugins/` automatically.

## Releasing

Releases are cut from git tags by `.github/workflows/release.yml`. The plugin
needs the game's interop assemblies to compile, so it is built locally and the
result in `dist/NineQoL.dll` is committed; the workflow only verifies and
packages it.

1. Bump `PluginVersion` in `Plugin.cs` and `version_number` in
   `manifest.json` to the same value, and add a `## <version>` section to
   `CHANGELOG.md`.
2. `dotnet build NineQoL.csproj -c Release` (updates `dist/NineQoL.dll`).
3. Commit, then `git tag v<version> && git push --tags`.

The workflow refuses to publish if the tag, `Plugin.cs` and `manifest.json`
disagree. It attaches two zips to the GitHub Release:

- `NineQoL-<version>-thunderstore.zip` — upload as-is to the
  [9 Kings Thunderstore community](https://thunderstore.io/c/9-kings/).
- `NineQoL-<version>-nexus.zip` — `BepInEx/plugins/NineQoL.dll` plus docs,
  for Nexus Mods and manual installs.

## Configuration

`BepInEx/config/dev.oglabs.9kings.qol.cfg` (created on first run):

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch; turning it off restores all troops to visible. |
| `MaxVisibleAllies` | `150` | Max allied troops drawn at once. |
| `MaxVisibleEnemies` | `150` | Max enemy troops drawn at once. |
| `RefreshInterval` | `0.5` | Seconds between visibility passes. |
| `DisableHiddenAnimators` | `true` | Also stop Unity Animators on hidden troops (most of the CPU savings). |
| `AlwaysShowBosses` | `true` | Bosses never get hidden and don't count toward caps. |
| `SpeedCycleSpeeds` | `3,4,10` | Game speeds the 3x button cycles through on repeated clicks (first entry must be the game's own 3x). |
| `ShowCardLevelBreakdown` | `true` | Per-level plot counts under loot/shop cards. |
| `UpgradeGlow` | `true` | Mark every plot the held card would upgrade while the card is held. |
| `UpgradeGlowStyle` | `Marker` | `Marker` (game's blessing/cataclysm plot marker), `Tint` (blue plot tint) or `Outline` (card effect outline). |
| `UpgradeGlowColor` | `#4C8CFF` | Tint colour for the `Tint` style (`#RRGGBB` or `#RRGGBBAA`; `auto` samples the game's blessing popup colour). |
| `Hotkeys.Enabled` | `true` | Placing-phase keyboard shortcuts on/off. |
| `StartBattleKey` | `Space` | Key that presses START BATTLE. `None` disables. |
| `KingRerollKey` | `F` | Key that presses the king reroll button. `None` disables. |
| `CataclysmPreviewKey` | `V` | Hold to mark the plots the upcoming blessing/cataclysm will hit. |
| `PlotLevelLabels` | `true` | Always-visible level label on occupied plots. |
| `PlotLevelLabelFormat` | `Lv {level}/{max}` | Label text; `/{max}` is dropped for cards without a level cap. |
| `PlotLevelLabelScale` | `1` | Label size multiplier. |
| `PlotLevelLabelVerticalOffset` | `-0.3` | Vertical position in plot sprite heights (0 = centre, -0.5 = bottom corner, 0.5 = top corner). |
| `FasterAnimations` | `true` | Master switch for the three speed-ups below. |
| `IdleTimeScale` | `2` | Game speed held while no battle runs (loot, shop, placing). 1 = off. |
| `UiAnimatorSpeed` | `1.5` | Playback speed of gameplay-UI animators. |
| `EventDelayScale` | `0.5` | Multiplier for DelayBetweenEvents / TimeToNextWave / plot popup delays. |

## Notes / limitations

- When a visible troop dies, a hidden one takes its render slot on the next
  refresh pass, so you may notice troops "popping in" within half a second.
- Hit effects, blood, and projectiles still spawn for hidden troops (they're
  separate objects); only the troop bodies are culled.
- The interop assemblies regenerate when the game updates; if an update renames
  `TroopSystem`/`Troop`/`m_Renderers`, rebuild against the new
  `BepInEx/interop/Assembly-CSharp.dll`.
