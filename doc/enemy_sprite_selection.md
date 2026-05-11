# Enemy sprite selection by level and slot

This document summarizes the currently implemented arcade-facing enemy visual selection logic.

## Source of the rule

The rule comes from the reverse-engineered enemy initialization path:

- FUN_ram_05AE finds a free enemy slot.
- FUN_ram_3061 initializes the enemy slot at `0x602B + enemySlot * 5`.
- FUN_ram_3087 computes the enemy sprite code and attribute.

Each enemy slot has five relevant bytes in the arcade RAM model:

```text
slot + 0 = direction / active flags
slot + 1 = X pixel coordinate
slot + 2 = Y pixel coordinate
slot + 3 = sprite code
slot + 4 = attribute / palette value
```

The Godot implementation keeps the useful arcade-facing values as:

```text
MonsterEntity.SpriteCode
MonsterEntity.SpriteAttribute
```

The view still renders through extracted Godot spritesheets.

## Levels 1 through 8

For levels 1..8, all four enemy slots use the same insect for the current level.
The slot id is ignored.

```text
Level | spriteCode | attr | current Godot sheet
------+------------+------+---------------------
1     | 0x18       | 0x01 | enemy_level1.png
2     | 0x30       | 0x02 | enemy_level2.png
3     | 0x60       | 0x04 | enemy_level3.png
4     | 0x48       | 0x03 | enemy_level4.png
5     | 0x78       | 0x05 | enemy_level5.png
6     | 0x90       | 0x06 | enemy_level6.png
7     | 0xA8       | 0x07 | enemy_level7.png
8     | 0xC0       | 0x08 | enemy_level8.png
```

Important detail: levels 3 and 4 are crossed relative to the natural arcade sprite-code order.
Do not derive `attr = level` blindly.

## Levels 9 and later

From level 9 onward, each level uses four different insects.
The result depends on both the visible level number and the enemy slot id.

```text
start = (level - 1) & 0x07

if start >= 5:
    start -= 5

n = start + enemySlot

spriteCode = 0x18 + 0x18 * n
attr       = n + 1
```

With:

```text
level     = visible arcade level number, base 1
enemySlot = 0, 1, 2, or 3
n         = natural arcade insect index
```

## Natural arcade order

```text
n | spriteCode | attr
--+------------+-----
0 | 0x18       | 0x01
1 | 0x30       | 0x02
2 | 0x48       | 0x03
3 | 0x60       | 0x04
4 | 0x78       | 0x05
5 | 0x90       | 0x06
6 | 0xA8       | 0x07
7 | 0xC0       | 0x08
```

The current extracted Godot sheets are named by the first visible level where the insect appears.
Because visible levels 3 and 4 are crossed, the mapping is:

```text
0x18 -> enemy_level1.png
0x30 -> enemy_level2.png
0x48 -> enemy_level4.png
0x60 -> enemy_level3.png
0x78 -> enemy_level5.png
0x90 -> enemy_level6.png
0xA8 -> enemy_level7.png
0xC0 -> enemy_level8.png
```

## Validation table

```text
Level 1  : 18/01, 18/01, 18/01, 18/01
Level 2  : 30/02, 30/02, 30/02, 30/02
Level 3  : 60/04, 60/04, 60/04, 60/04
Level 4  : 48/03, 48/03, 48/03, 48/03
Level 5  : 78/05, 78/05, 78/05, 78/05
Level 6  : 90/06, 90/06, 90/06, 90/06
Level 7  : A8/07, A8/07, A8/07, A8/07
Level 8  : C0/08, C0/08, C0/08, C0/08

Level 9  : 18/01, 30/02, 48/03, 60/04
Level 10 : 30/02, 48/03, 60/04, 78/05
Level 11 : 48/03, 60/04, 78/05, 90/06
Level 12 : 60/04, 78/05, 90/06, A8/07
Level 13 : 78/05, 90/06, A8/07, C0/08
Level 14 : 18/01, 30/02, 48/03, 60/04
Level 15 : 30/02, 48/03, 60/04, 78/05
Level 16 : 48/03, 60/04, 78/05, 90/06
Level 17 : 18/01, 30/02, 48/03, 60/04
Level 18 : 30/02, 48/03, 60/04, 78/05
Level 19 : 48/03, 60/04, 78/05, 90/06
Level 20 : 60/04, 78/05, 90/06, A8/07
Level 21 : 78/05, 90/06, A8/07, C0/08
Level 22 : 18/01, 30/02, 48/03, 60/04
```

## Current implementation notes

- `EnemyLevelCatalog.Get(levelNumber, enemySlot)` computes the arcade sprite information.
- `EnemyLevelDefinition` carries `SpriteInfo`, `SpriteCode`, `Attr`, `NaturalVisualIndex`, and the Godot spritesheet path.
- `EnemyRuntime.CreateSlotsAndViews()` asks the catalog for each slot separately.
- `MonsterEntity` stores the assigned `SpriteCode` and `SpriteAttribute` for trace/debug parity with the arcade logic.

## Test focus

The most useful visual test is level 9:

```text
slot 0 -> enemy_level1.png / 18/01
slot 1 -> enemy_level2.png / 30/02
slot 2 -> enemy_level4.png / 48/03
slot 3 -> enemy_level3.png / 60/04
```

This test catches both the level-9 slot-specific behavior and the level-3 / level-4 inversion.
