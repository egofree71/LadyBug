# Collectibles Reverse Engineering

Project: Lady Bug remake in Godot 4.6.2 (.NET) with C#

## Purpose

This document summarizes the reverse-engineering findings currently used for the
Lady Bug collectible system in Godot.

It covers:

- the logical anchor system used for special collectibles
- the random selection rules used at level start
- the initial placement of letters, hearts, and skulls
- the runtime color cycle used by hearts and letters
- score values and multiplier behavior
- EXTRA / SPECIAL letter progression
- skull collision behavior
- recommended Godot implementation rules

It does **not** currently cover:

- vegetable spawning and enemy-freeze behavior
- the enemy-release maze-border timer in detail
- exact low-level color RAM animation outside gameplay-relevant color mode

## General Placement Rule

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
| Skulls | 2 to 6 | 3rd draw of families A/B/C, then 4th draw if more are needed |

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

## Start-of-Level Pseudocode

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

## Runtime Pickup Timing

Collectible pickup should be evaluated **after each validated one-pixel movement segment**.

This is important because one simulation tick can contain two movement segments during an
assisted turn:

1. one orthogonal correction pixel toward the target lane
2. one pixel in the requested direction

If pickup were checked only at the final position of the tick, the player could cross a
collectible anchor during the correction segment and miss it.

Recommended Godot rule:

```text
for each committed movement segment:
    if the segment crosses the destination logical-cell anchor:
        consume collectible at that logical cell
```

If the movement motor reports an explicit snapped anchor, check that exact anchor too.

## Runtime Color Cycle for Hearts and Letters

Only hearts and letters use the global color mode.

Unaffected by the color cycle:

- flowers
- skulls

The arcade uses a global counter for the gameplay color classification of hearts and
letters. The relevant counter observed during collection is the 16-bit value held at:

```text
619A:6199
```

The runtime classification behaves as follows:

| Counter range | Mode | Gameplay meaning |
|---:|---|---|
| `< 0x001F` | Red | red letters can progress SPECIAL |
| `< 0x00B4` | Yellow | yellow letters can progress EXTRA |
| `>= 0x00B4` | Blue | blue hearts increase the score multiplier |
| `0x0258` | Reset | cycle wraps around |

This gives a 600-tick color cycle:

| Mode | Ticks | Approx. duration at 60.1145 Hz |
|---|---:|---:|
| Red | 31 | 0.52 s |
| Yellow | 149 | 2.48 s |
| Blue | 420 | 6.99 s |
| Total | 600 | 9.98 s |

For the Godot remake, the same durations can be used even if the local phase is initialized
so the visible order starts with blue:

```text
Blue -> Red -> Yellow -> Blue
```

The key gameplay requirement is that the visible color and the collected effect must use
the same current mode.

## Score Values

The color mode affects the base score for hearts and letters.

| Collectible | Base score |
|---|---:|
| Flower | 10 |
| Blue letter / heart | 100 |
| Yellow letter / heart | 300 |
| Red letter / heart | 800 |
| Skull | no score; lethal |

The active heart multiplier applies to these values.

| Multiplier step | Score multiplier |
|---:|---:|
| 0 | ×1 |
| 1 | ×2 |
| 2 | ×3 |
| 3 | ×5 |

## Blue Heart Multiplier

A heart only changes the multiplier when it is collected in **blue** mode.

Observed player-1 RAM:

```text
609F = blue heart multiplier step for player 1
60A2 = blue heart multiplier step for player 2
```

Confirmed behavior from MAME tests:

```text
1st blue heart: 609F 00 -> 01, score +100
2nd blue heart: 609F 01 -> 02, score +200
3rd blue heart: 609F 02 -> 03, score +300
```

So the score for the heart itself is computed using the multiplier active **before** the
heart increments the multiplier step.

Recommended Godot logic:

```text
score += baseScore(currentMode) * multiplierForCurrentStep()

if collectible is heart and currentMode is Blue:
    multiplierStep = min(multiplierStep + 1, 3)
```

MAME showed the first three blue hearts produce `+100`, `+200`, then `+300`, while the
next scoring values after step 3 should use `×5`.

## Letter Progression: SPECIAL and EXTRA

Color determines which word can progress.

| Letter color | Word effect |
|---|---|
| Red | progress SPECIAL |
| Yellow | progress EXTRA |
| Blue | points only |

Letter-family rules:

- `S`, `P`, `C`, `I`, `L` can only progress `SPECIAL`, and only when red.
- `X`, `T`, `R` can only progress `EXTRA`, and only when yellow.
- `A` and `E` can progress either `SPECIAL` or `EXTRA` depending on current color.

Observed player RAM:

```text
609D = SPECIAL progress for player 1
609E = EXTRA progress for player 1
60A0 = SPECIAL progress for player 2
60A1 = EXTRA progress for player 2
```

The original stores progress as bitfields, with bits being cleared to mark letters as
collected.

Confirmed MAME tests:

```text
P red:
    D=03, E=03
    609D FF -> FD

X yellow:
    D=07, E=02
    609E FF -> FD
```

Recommended Godot interpretation:

- do not reproduce the bitfield format literally unless needed
- store word progress as semantic letter flags or sets
- apply progress only if the current color and letter family allow it
- if the relevant letter is already completed, award only points

Completing `SPECIAL` gives a free credit / free game in the arcade.
Completing `EXTRA` gives an extra life.
Completing either word immediately advances to the next level and resets the completed word.

## Skull Collision Behavior

Skulls are not affected by the color cycle.

The gameplay collision probe can hit skull tile `0x63`, which branches to the skull
collision handler.

Observed chain:

```text
0877 CP 63
0879 JP Z,0AEA
0AEA = HandleSkullCollision
0AF3 = HandlePlayerFatalCollision
2D64 = Player_PlayDeathAnimation
```

Recommended Godot behavior:

```text
if collectible is skull:
    remove skull
    kill player
```

The arcade appears to remove or clear the skull before entering the common player-death
sequence.

## Flower Behavior

Flowers are not affected by the color cycle.

Recommended Godot behavior:

```text
if collectible is flower:
    score += 10 * currentMultiplier
    remove flower
```

Completing all required flowers / dots should participate in the normal level-clear logic.

## Recommended Godot Interpretation

For the current remake, the practical implementation guideline is:

- treat each collectible as **one logical object** centered on its anchor
- do **not** reproduce the original multi-tile VRAM stamping literally
- preserve the gameplay-relevant structure:
  - anchor families
  - four draws per family
  - skull count by level
  - random permutation of letters
  - global color mode for hearts and letters
  - color-based scoring and letter progression
  - blue-heart multiplier
  - skull death behavior

Suggested runtime services / concepts:

```text
CollectibleFieldRuntime
CollectibleColorCycle
CollectiblePickupResult
CollectibleScoreService
WordProgressState
HeartMultiplierState
```

`TryConsume` should eventually return a rich result rather than a simple boolean, for example:

```text
None
FlowerCollected(score)
HeartCollected(color, score, multiplierStepChanged)
LetterCollected(letter, color, score, wordProgressChanged, completedWord)
SkullTouched
```

## Recommended Color Cycle Implementation

One straightforward implementation is to keep a 600-tick counter and interpret it through
thresholds.

If Godot wants the visible cycle to start in blue, initialize `_tick` inside the blue range
or use a phase-order wrapper.

```csharp
public enum CollectibleColorMode
{
    Red,
    Yellow,
    Blue
}

public sealed class CollectibleColorCycle
{
    private const int TotalTicks = 0x0258; // 600
    private const int RedEnd = 0x001F;     // 31 ticks
    private const int YellowEnd = 0x00B4;  // 180 ticks total from cycle origin

    private int _tick;

    public CollectibleColorMode CurrentMode
    {
        get
        {
            int t = _tick % TotalTicks;

            if (t < RedEnd)
                return CollectibleColorMode.Red;

            if (t < YellowEnd)
                return CollectibleColorMode.Yellow;

            return CollectibleColorMode.Blue;
        }
    }

    public void AdvanceOneTick()
    {
        _tick = (_tick + 1) % TotalTicks;
    }
}
```

If the desired visible order is `Blue -> Red -> Yellow -> Blue`, use the same durations with
a different phase origin:

```text
Blue:   420 ticks
Red:     31 ticks
Yellow: 149 ticks
```

## Arcade RAM / Routine Reference

Important runtime variables:

| Address | Meaning |
|---:|---|
| `619A:6199` | global collectible color-cycle counter used for collection classification |
| `609D` | SPECIAL progress, player 1 |
| `609E` | EXTRA progress, player 1 |
| `609F` | blue-heart multiplier step, player 1 |
| `60A0` | SPECIAL progress, player 2 |
| `60A1` | EXTRA progress, player 2 |
| `60A2` | blue-heart multiplier step, player 2 |

Useful routines / labels:

| Address | Suggested name | Meaning |
|---:|---|---|
| `3192` | `PlaceInitialSpecialCollectibles` | start-of-level letters, hearts, skulls |
| `09FE` | `HandleLetterCollected` | letter collection dispatcher |
| `09CC` | `HandleHeartCollected` | heart collection / blue-heart multiplier path |
| `094A` | `HandleFlowerCollected` | flower collection path |
| `0AEA` | `HandleSkullCollision` | skull collision path |
| `0AF3` | `HandlePlayerFatalCollision` | common fatal collision path |
| `2D64` | `Player_PlayDeathAnimation` | starts player death animation |
| `3D9F` | `ClassifyCollectibleColorOrType` | classifies current collectible color/type |
| `3E5E` | `DecodeLetterTileToIndex` | maps tile code to letter index |
| `3E96` | `ApplyLetterWordProgress` | applies SPECIAL / EXTRA progress |

## Open Items

Still worth verifying later:

- exact visual color RAM writes for all letter/heart tiles
- whether the first visible color at level start should always be blue or depends on game state
- exact level-clear interaction when `SPECIAL` or `EXTRA` is completed
- exact free-credit behavior for `SPECIAL` in the remake context
- vegetable spawning, score, and enemy-freeze duration
- enemy-release border timer behavior
