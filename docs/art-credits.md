# Art credits

All art currently in the project is from **Kenney.nl**, released under
**CC0** (public domain — no attribution legally required; credited here
anyway as good practice).

## Packs used

- **Roguelike/RPG Pack** — https://kenney.nl/assets/roguelike-rpg-pack
  16x16 tile spritesheet (`roguelikeSheet_transparent.png`, 57x31 tiles at a
  17px pitch: 16px tile + 1px spacing, no separate margin). Source of the
  Explore dungeon-map room-type icons.
- **Roguelike Characters** — https://kenney.nl/assets/roguelike-characters
  16x16 modular top-down character parts, same grid pitch as above. A
  handful of pre-assembled single-frame characters (rows 5-9 of the sheet)
  were used directly rather than composited from the modular pieces.
- **UI Pack** (v2.0) — https://kenney.nl/assets/ui-pack
  Individually-cropped PNGs (no slicing needed). `button_square_depth_border.png`
  (Grey theme) is reused as a 9-sliced modal/panel background.

## What was extracted and how

Kenney's tile/character sheets ship as a single spritesheet PNG with no
named-region atlas (no XML/JSON). Grid cell identity was confirmed visually
(crop a region at high scale with nearest-neighbor upscaling, view it, adjust
column/row math, repeat) rather than guessed from the sheet thumbnail — the
first guess at the room-icon floor tile coordinates was off by 4 columns and
was caught this way before it shipped. The cropping tool used is a small
PowerShell + `System.Drawing` script (not checked into the repo — one-off
tooling, not project code).

## Current asset inventory (`Assets/Resources/Art/`)

- `RoomIcons/icon_{start,normal,encounter,boss,treasure,stairs}.png` — one
  per `RoomType`, used by `ExploreScreenView`'s dungeon map.
- `Characters/archetype_{warrior,cleric,mage,ranger,rogue}.png` — 5 hand
  -picked archetypes covering the 16 classes' 8 roles (see
  `ArchetypeIcons.RoleToArchetype`), used for party HUD avatars, the
  ClassSelect detail-panel portrait, and the Inventory member list.
- `Characters/enemy_base.png` — one base sprite reused for all 12 enemies,
  tinted per enemy id from a small fixed palette (`ArchetypeIcons.EnemyTint`)
  so encounters stay visually distinguishable without bespoke monster art.
- `UI/panel_border.png` — shared 9-sliced background for `.modal-box`
  (Explore's stairs/loot modals, Combat's victory/defeat modals, Inventory's
  trade modal).
- `Items/weapon_{blade,dagger,axe,mace,bow,staff}.png` — 6 icons covering the
  9 `weapon_type`s (see `ItemIcons.WeaponTypeToResource`): greatsword reuses
  the sword blade, wand and grimoire both reuse the staff icon. Used in
  Inventory's equipped/bag rows and as the party HUD's per-member equipped
  -gear row (`PartyHudBuilder`'s `.hud-equip-row`) so party members visibly
  show what they have equipped rather than just naming it in text.
- `Items/{armor,accessory,charm,potion}_icon.png` — one icon per category
  regardless of the specific item or `armor_type`, same reasoning as above.

## Deliberately out of scope this pass

A dedicated Menu-screen background/logo treatment and literal per-enemy
monster art (12 bespoke sprites, currently one tinted-reuse sprite per
`docs/art-credits.md`'s enemy section above) — revisit if the tinted-reuse
enemies feel too thin later.
