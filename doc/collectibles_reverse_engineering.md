# Collectibles Reverse Engineering

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

## Purpose

This document summarizes the reverse-engineering findings currently used for the
**start-of-level collectible placement** in Godot.

It is focused on:

- the logical anchor system used for special collectibles
- the random selection rules used at level start
- the initial placement of letters, hearts, and skulls

It does **not** document:

- color cycling
- scoring details
- EXTRA / SPECIAL progression after collection
- later gameplay behavior after the initial placement

## General Rule

At the start of a level, the game chooses **4 distinct anchors** in each of
**3 anchor families**.

The resulting draws are used as follows:

- **letters** use the 1st draw of each family
- **hearts** use the 2nd draw of each family
- **skulls** use the 3rd draw, then the 4th draw if more are needed

The visual order of the 3 letters is a **random permutation** of their 3 logical
families.

## Godot Coordinate System

The coordinates below are already expressed directly in the **Godot logical grid**:

- grid size: `11 × 11`
- origin: **top-left** of the maze
- coordinates: `x = 0..10`, `y = 0..10`

Each anchor corresponds to one collectible occupying a `2 × 2` arcade-tile footprint.

Important:

- the version below is already corrected for Godot
- **no additional Y inversion is needed**

## Types of Collectibles Generated at Level Start

| Type | Count | Placement Rule |
|---|---:|---|
| Letters | 3 | 1st draw of each family, then random permutation across the 3 family anchors |
| Hearts | 3 | 2nd draw of each family |
| Skulls | 2 to 6 | 3rd draw of families A/B/C, then 4th draw if needed |

## Number of Skulls by Level

| Level | Skull Count |
|---|---:|
| 1 | 2 |
| 2 to 4 | 3 |
| 5 to 9 | 4 |
| 10 to 16 | 5 |
| 17 and more | 6 |

## Anchor Families

In each family, the game draws **4 distinct anchors without replacement**.

### Family A (10 candidate anchors)

| Index | Anchor |
|---|---|
| 0 | `(0, 1)` |
| 1 | `(0, 0)` |
| 2 | `(3, 0)` |
| 3 | `(4, 0)` |
| 4 | `(5, 3)` |
| 5 | `(5, 2)` |
| 6 | `(5, 1)` |
| 7 | `(5, 0)` |
| 8 | `(6, 0)` |
| 9 | `(7, 0)` |

### Family B (15 candidate anchors)

| Index | Anchor |
|---|---|
| 0 | `(0, 10)` |
| 1 | `(0, 9)` |
| 2 | `(0, 8)` |
| 3 | `(0, 7)` |
| 4 | `(0, 6)` |
| 5 | `(0, 5)` |
| 6 | `(0, 4)` |
| 7 | `(1, 8)` |
| 8 | `(1, 5)` |
| 9 | `(1, 4)` |
| 10 | `(2, 8)` |
| 11 | `(2, 4)` |
| 12 | `(3, 10)` |
| 13 | `(4, 10)` |
| 14 | `(4, 5)` |

### Family C (15 candidate anchors)

| Index | Anchor |
|---|---|
| 0 | `(6, 10)` |
| 1 | `(6, 5)` |
| 2 | `(7, 10)` |
| 3 | `(8, 8)` |
| 4 | `(8, 4)` |
| 5 | `(9, 8)` |
| 6 | `(9, 5)` |
| 7 | `(9, 4)` |
| 8 | `(10, 10)` |
| 9 | `(10, 9)` |
| 10 | `(10, 8)` |
| 11 | `(10, 7)` |
| 12 | `(10, 6)` |
| 13 | `(10, 5)` |
| 14 | `(10, 4)` |

## Start-of-Level Placement Algorithm

1. In each family A, B, and C, draw 4 distinct anchors without replacement.
   - notation: `draw[0]`, `draw[1]`, `draw[2]`, `draw[3]`
2. Place the 3 letters on the `draw[0]` anchors of the 3 families.
3. Randomly permute which chosen letter goes to which of the 3 family anchors.
4. Place the 3 hearts on the `draw[1]` anchors of the 3 families.
5. Place skulls on `draw[2]` of A/B/C, then on `draw[3]` of A/B/C if more are needed.

Important:

- the memory order of the 3 selected letters is **not** their final visual order
- the game first chooses the 3 letters, then chooses a permutation for placement

## Letter Table

The game always chooses exactly 3 letters per level:

- one from the `A/E` family
- one from the `SPECIAL` family
- one from the `EXTRA` family

| Internal ID | Letter | Logical Family |
|---|---|---|
| 00 | A | A/E |
| 01 | E | A/E |
| 02 | S | SPECIAL |
| 03 | P | SPECIAL |
| 04 | C | SPECIAL |
| 05 | I | SPECIAL |
| 06 | L | SPECIAL |
| 07 | X | EXTRA |
| 08 | T | EXTRA |
| 09 | R | EXTRA |

## Recommended Godot Interpretation

For the current remake, the practical implementation guideline is:

- treat each collectible as **one logical object** centered on its anchor
- do **not** reproduce the original multi-tile VRAM stamping literally
- preserve the gameplay-relevant structure:
  - anchor families
  - four draws per family
  - skull count by level
  - random permutation of letters

## Pseudocode

```text
families = { A: [...], B: [...], C: [...] }
pickA = sample_without_replacement(families.A, 4)
pickB = sample_without_replacement(families.B, 4)
pickC = sample_without_replacement(families.C, 4)

letters = [ random(A_or_E), random(SPECIAL), random(EXTRA) ]
perm = random_permutation([0, 1, 2])

place_letter(letters[perm[0]], pickA[0])
place_letter(letters[perm[1]], pickB[0])
place_letter(letters[perm[2]], pickC[0])

place_heart(pickA[1])
place_heart(pickB[1])
place_heart(pickC[1])

skull_positions = [pickA[2], pickB[2], pickC[2], pickA[3], pickB[3], pickC[3]]
for i in range(skull_count_for_level(level)):
	place_skull(skull_positions[i])
```

## Current Scope Limit

This document is intentionally limited to the **start-of-level placement logic**.

Still outside scope:

- blue / yellow / red color cycling
- score values and multipliers
- EXTRA / SPECIAL word progression after collection
- later runtime gameplay behavior of hearts, letters, and skulls
