# Collectibles Reverse Engineering

Project: Lady Bug remake in Godot 4.6.1 / 4.6.2 (.NET) with C#

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
- temporary pickup popup / freeze behavior for hearts and letters
- recommended Godot implementation rules
- current Godot implementation status for the collectible systems

It does **not** currently cover:

- vegetable spawning and enemy-freeze behavior
- the enemy-release maze-border timer in detail
  - see `gameplay_timers_reverse_engineering.md` for the separate maze-border / enemy-release timer
- exact low-level color RAM animation outside gameplay-relevant color mode
- the detailed player death animation internals; those are documented separately in the player death sequence reverse-engineering notes

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

## Current Godot Letter Spritesheet Mapping

The Godot logical letter selection is separate from the visual spritesheet frame.

The current `assets/sprites/props/collectibles.png` letter frame mapping used by
`scripts/level/Collectible.cs` is:

| LetterKind | Spritesheet frame |
|---|---:|
| E | 4 |
| X | 5 |
| T | 6 |
| R | 7 |
| A | 8 |
| S | 9 |
| P | 10 |
| C | 11 |
| I | 12 |
| L | 13 |

Important:

- changing this mapping affects only which image is shown for a logical letter
- it does **not** affect the start-of-level random letter selection
- the start-of-level selection continues to use `LetterKind` values generated by `CollectibleSpawnPlanner`

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

Current Godot implementation:

- `CollectibleColorCycle` tracks the global color mode
- `CollectibleFieldRuntime` applies the current color to visible hearts and letters
- `CollectibleScoreService` receives the same color in `CollectiblePickupResult`
- `WordProgressState` receives the same color for letter progress
- `HeartMultiplierState` advances only when the collected heart color is blue

## Cross-System Timer Note

The heart / letter color cycle is **not** the same timer as the animated maze-border /
enemy-release timer.

Confirmed split:

```text
heart / letter color cycle:
	3956  TickCollectibleColorCycle
	6199/619A  CollectibleColorCycleCounter
	600-tick red/yellow/blue cycle

maze-border / enemy-release timer:
	35E3  InitMazeBorderTimerForLevel
	39B1  UpdateMazeBorderTimer
	60AA  MazeBorderCountdown
	60AB  MazeBorderPeriod
	9 / 6 / 3 ticks per border step depending on level
```

The two routines are called next to each other in the gameplay loop, but they keep
separate RAM state. The border timer is documented in
`gameplay_timers_reverse_engineering.md`.

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

## Temporary Score / Multiplier Popup and Gameplay Freeze

When the player collects a heart or a letter, the arcade briefly pauses the normal
gameplay action and replaces the collectible position with a temporary visual popup.

Observed behavior:

- the player sprite is hidden during the popup
- the current collectible is visually replaced for a short time
- the upper part of the popup shows the score value for the collected object
- the lower part shows the current multiplier indicator when a multiplier is active
- the playfield action appears frozen during this short display
- after the popup, the temporary sprite is removed, the player is restored, and the
  collectible tile is finally cleared

Reverse-engineering notes:

```text
3DBD = ApplyCollectibleScoreOrEffect
3DE5 = clears player sprite/state bits at 6026, hiding the player during the popup
3DED = prepares a temporary sprite/object at 6049..604D
3E0C = red score popup tile selector,  E=3 -> 604C = E0
3E12 = yellow score popup tile selector, E=2 -> 604C = DC
3E18 = blue score popup tile selector,   E<2 -> 604C = D8
3E20 = sets 6000 bit 2 while the popup is active
3E22 = LD B,0x1E, popup wait loop duration
3E25 = calls timing / video update routines during the wait loop
3E32 = clears 6049, removing the temporary popup sprite/object
3E38 = restores player sprite/state visibility via 6026 bit 1
3E3B = writes FF to the collected tile location
```

Duration:

```text
0x1E ticks = 30 ticks
30 / 60.1145 Hz ≈ 0.50 seconds
```

Current Godot behavior:

```text
when heart/letter pickup is confirmed:
	compute pickup result and score using current color mode and multiplier
	apply SPECIAL / EXTRA progress or blue-heart multiplier update if relevant
	start a 30-tick pickup popup state
	hide the player sprite during the popup
	pause normal player/enemy movement during the popup
	display score text above the collectible anchor
	display multiplier text below the collectible anchor when multiplier is active
	after 30 ticks:
		remove the popup
		show the player again
		resume normal gameplay
```

For the remake, this is intentionally implemented as a high-level gameplay state rather
than reproducing the original temporary sprite RAM layout literally.

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

Recommended and current Godot logic:

```text
score += baseScore(currentMode) * multiplierForCurrentStep()

if collectible is heart and currentMode is Blue:
	multiplierStep = min(multiplierStep + 1, 3)
	update HUD x2/x3/x5 display
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

Current Godot interpretation:

- the bitfield format is not reproduced literally
- `WordProgressState` stores semantic letter flags in display order
- progress is applied only when the current color and letter family allow it
- if the relevant letter is already active, the pickup awards points only
- the HUD renders inactive letters in light grey
- active SPECIAL letters are rendered red
- active EXTRA letters are rendered yellow

Current Godot word definitions:

```text
SPECIAL = S P E C I A L
EXTRA   = E X T R A
```

Current Godot completion behavior:

- completing `EXTRA` immediately awards one extra life through `PlayerLifeState.AddLife()`
- completing `SPECIAL` increments a prototype free-game award counter and prints a debug message
- the final arcade behavior says completing either word immediately advances to the next level, but the current remake does not implement that transition yet
- the final arcade behavior says SPECIAL gives a free credit / free game, but the current remake does not implement arcade credit flow yet

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

Current Godot behavior:

- the skull is removed by `CollectibleFieldRuntime.TryConsume`
- no score is awarded
- `Level` decrements the current lives state
- the HUD lives display is updated
- normal gameplay is frozen
- `PlayerController` starts the player death visual sequence
- after death, the player respawns at `PlayerStartCell` if lives remain
- if no lives remain, the current game-over placeholder is entered

The current death visual sequence is based on separate player-death reverse-engineering notes:

- red shrink sequence
- ghost apparition sequence
- ghost zigzag upward sequence
- total duration: 240 ticks, about 4 seconds

## Flower Behavior

Flowers are not affected by the color cycle.

Current Godot behavior:

```text
if collectible is flower:
	score += 10 * currentMultiplier
	remove flower
```

Completing all required flowers / dots should eventually participate in the normal
level-clear logic, but that level-clear system is not implemented yet.

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
  - temporary score / multiplier popup for hearts and letters
  - short gameplay freeze while the popup is visible

Current runtime services / concepts:

```text
CollectibleFieldRuntime
CollectibleColorCycle
CollectiblePickupResult
CollectibleScoreService
WordProgressState
HeartMultiplierState
CollectiblePickupPopupState
PlayerLifeState
PlayerDeathSequenceState
Hud
```

Current `TryConsume` result model is semantic through `CollectiblePickupResult`:

```text
Consumed flag
Kind
Color
Letter
```

This is enough for the current implementation, but a richer explicit union-style result may still be useful later.

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
| `3DBD` | `ApplyCollectibleScoreOrEffect` | score/multiplier popup, short freeze, player hide/restore |
| `3E5E` | `DecodeLetterTileToIndex` | maps tile code to letter index |
| `3E96` | `ApplyLetterWordProgress` | applies SPECIAL / EXTRA progress |

## Current Godot Implementation Status

Implemented:

- start-of-level letter / heart / skull placement
- corrected Godot logical-cell anchor families
- 2 to 6 skull count by level
- global heart / letter color cycle
- color-based heart / letter scoring
- blue-heart multiplier advancement
- HUD multiplier display
- SPECIAL / EXTRA semantic word progress
- EXTRA extra-life reward
- SPECIAL placeholder award
- skull lethality
- arcade-style player death visual sequence after skull pickup
- 30-tick score / multiplier popup for heart / letter pickups
- gameplay freeze while the popup or death sequence is active
- corrected collectible letter spritesheet mapping

Not implemented yet:

- immediate next-level transition when SPECIAL or EXTRA is completed
- exact free-credit / free-game behavior for SPECIAL
- level clear when all flowers / required collectibles are eaten
- vegetable spawning, score, and enemy-freeze duration
- enemy-release border timer behavior
- exact visual score-popup tile mapping for all score/multiplier cases
- exact visual color RAM writes for all letter/heart tiles
- whether the first visible color at level start should always be blue or depends on game state
