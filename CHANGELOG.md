# Changelog

## 1.0.1

- Fixed upgrade cards failing to drop with repeated null-reference errors
- Made card-level count labels non-interactive so they cannot block pointer input
- Reworked upgrade targeting to avoid duplicate placement checks
- Made the Marker upgrade glow use a safe plot tint on current game builds
- Persisted the selected 3x/4x/10x speed across runs and game restarts

## 1.0.0

First public release.

- Troop render cap with optional animator pause (big FPS win in late Endless)
- FPS limit slider and a "9 Qualities of Life" category in the options menu
- 3x button cycles 3x → 4x → 10x
- Level counts under loot, shop and hand cards ("Lv. 1: 2   Lv. 2: 1")
- Upgrade plot glow: every plot the held card would level up is marked
- Always-visible plot level labels ("Lv 2/4", gold at max)
- Hotkeys: Space starts the battle, F rerolls the king, hold V to preview
  blessing/cataclysm plots
- Faster animations between battles (idle speed boost, snappier UI animators,
  shorter event delays)
