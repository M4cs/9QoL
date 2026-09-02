# Changelog

## 1.4.0

- Upgrade plot glow reworked: every plot the held card would upgrade is marked
  for as long as the card is held, using the game's own blessing/cataclysm plot
  marker (`UpgradeGlowStyle` can switch to a blue tint or the effect outline).
- Hotkeys: Space starts the battle, F rerolls the king offer, holding V marks
  the plots the upcoming blessing/cataclysm will hit. All configurable.
- Always-visible plot level labels ("Lv 2/4", gold at max level).
- Faster animations: idle time-scale boost between battles, faster gameplay-UI
  animators, halved event delays. One toggle, three settings.
- Card level breakdown under loot/shop cards now reads "Lv. 1: 2   Lv. 2: 1"
  and shrinks to fit the card width.
- Main-menu performance guard: mod ticks no longer touch gameplay UI while no
  gameplay scene is loaded.

## 1.3.1

- Card level breakdown under loot, shop and hand cards.
- Upgrade hover glow.
- In-game settings category with troop cap, FPS limit and feature toggles.

## 1.2.0

- Card level breakdown and upgrade hover glow (first version).

## 1.1.0

- In-game settings (max visible troops, FPS limit).
- Multi-level triple speed (3x → 4x → 10x).

## 1.0.0

- Troop render cap with optional animator pause.
