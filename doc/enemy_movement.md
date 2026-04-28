# Enemy Movement

## Goal

This document summarizes what we currently know about enemy movement in the arcade
game Lady Bug, based on reverse engineering of:

- the MAME driver (ladybug.cpp)
- the Ghidra disassembly (LadyBug_CPU.txt)
- the ROM set

The goal is not to emulate the Z80 instruction by instruction, but to reconstruct
the gameplay logic as faithfully as possible in Godot 4.6.1 with C#.

## Confidence Levels

**This document uses three confidence levels:**

- Confirmed
  Backed directly by code paths we analyzed.

- Probable
  Strongly suggested by the code and data, but not fully proven in every detail.

- Open
  Still partially unclear and should be treated carefully during implementation.

## High-Level Overview

Enemy movement in Lady Bug is not purely random and not a permanent direct chase.

**It combines:**

- a base preferred direction
- local validation against the maze and doors
- temporary BFS-guided chase phases toward Lady Bug
- forced corrections in some door-related situations

**So the system is hybrid:**

- sometimes enemies follow a general movement pattern
- sometimes one of them temporarily gets a much more direct path toward the player

**Relevant routines:**

- 0x407E : global enemy update loop
- 0x42BA : per-enemy decision and movement
- 0x447D : BFS map construction from Lady Bug
- 0x46D8 : BFS override on enemy preferred directions


## Enemy Release / Maze-Border Timer

Confirmed

Enemy movement logic starts once an enemy is active in the maze. The cadence for releasing
enemies from the central area is driven separately by the animated maze-border timer.

The border timer is initialized at `0x35E3` and updated at `0x39B1`.

Relevant RAM:

```text
60AA = MazeBorderCountdown
60AB = MazeBorderPeriod
```

The period depends on the current level:

| Level | Period |
|---:|---:|
| 1 | 9 ticks per border step |
| 2 to 4 | 6 ticks per border step |
| 5+ | 3 ticks per border step |

Practical meaning

Enemy movement, chase, BFS, and local door validation should stay in the enemy movement
system. Enemy release timing should be synchronized with the maze-border timer documented
in `gameplay_timers_reverse_engineering.md`.

Implementation note

Do not reuse the heart / letter color-cycle counter for enemy release. The collectible
color cycle uses `6199/619A`; the maze-border / enemy-release timer uses `60AA/60AB`.

## Enemy Update Loop

Confirmed

The global enemy update loop is centered around routine 0x407E.

**Its role is roughly:**

1. update some global enemy state
2. prepare preferred directions for enemies
3. iterate through the 4 enemies
4. update each active enemy

The per-enemy update routine is 0x42BA.

Practical meaning

In a Godot rewrite, this should become something like:

1. prepare shared enemy state for the frame
2. for each active enemy:
   - determine preferred direction
   - validate direction
   - move one pixel

**Relevant routines:**

- 0x407E : global enemy loop
- 0x42BA : per-enemy update
- 0x40F8 : build a global enemy-control value
- 0x40CC : transforms global control state
- 0x2E5C : prepares base preferred directions

## Enemy Data Structure

Confirmed

The 4 enemies are stored from 0x602B, with 5 bytes per enemy.

**Practical structure:**

- +0 : direction + flags
- +1 : x
- +2 : y
- +3 : sprite-related
- +4 : attribute-related

**Direction encoding is:**

- 1 = left
- 2 = up
- 4 = right
- 8 = down

Bit 1 in the first byte is used as an active/enabled flag.

Suggested C# model

public sealed class MonsterEntity
{
```text
	public int Id;
	public int X;
	public int Y;
	public Dir Direction;
	public bool Active;
```

```text
	public Dir PreferredDirection;
	public int ChaseTimer;
```
}

**Relevant routines:**

- 0x43F0 : copies current enemy direction/x/y into temporary work state
- 0x43CE : writes updated direction/x/y back into enemy structure
- 0x1FC7 : sprite-building code that reads enemy structures
- 0x3061 : enemy initialization path

## Movement Granularity

Confirmed

Enemy movement is pixel by pixel.

Routine 0x4224 shows that one update step moves the enemy by exactly one pixel
in the current direction.

**Equivalent behavior:**

- left  -> x--
- up    -> y--
- right -> x++
- down  -> y++

Implementation note

Do not move enemies tile by tile.
Use integer pixel positions.

**Relevant routines:**

- 0x4224 : one-pixel movement step

## Decision Points

Confirmed

An enemy does not choose a new direction at every pixel.

The main decision is normally taken only when the enemy reaches the logical center
of a maze cell.

**The center test is equivalent to:**

- x & 0x0F == 0x08
- y & 0x0F == 0x06

Practical meaning

**Between two cell centers:**

- the enemy usually keeps going in the same direction
- unless a special door-related rule forces a reversal

**Relevant routines:**

- 0x427E : decision-center test
- 0x42D2 : loads current candidate direction/x/y before center test

## Preferred Direction

Confirmed

Each enemy gets a preferred direction stored in 0x61C4..0x61C7.

This preferred direction is later used by the per-enemy logic at intersections.

Confirmed / Probable

**The preferred direction can come from:**

- a base behavior
- a temporary BFS chase override

The base behavior is built by routines including 0x2E5C, 0x40F8, and 0x40CC.

**What is directly supported by the code:**

- there is a global state influencing base preferred directions
- this state depends on:
  - level
  - elapsed time
  - difficulty
- one branch also uses the Z80 R register, so there is a pseudo-random component

Important wording note

**It is safest to say:**

"Outside BFS chase phases, enemies receive a preferred direction from routines
driven by a global gameplay state. That state is influenced by level, elapsed
time, and difficulty, and some branches also include a pseudo-random component."

This is more rigorous than saying simply "they move randomly".

**Relevant routines:**

- 0x2E5C : base preferred-direction preparation
- 0x40F8 : builds global control value in 0x61C3
- 0x40CC : derives movement-control state from 0x61C3
- 0x42E6 : tries preferred direction at an intersection

## BFS-Guided Chase

Confirmed

The game builds a navigation map from Lady Bug’s current position.

This happens in routine 0x447D.

This map is stored in the low nibble of cells in the logical maze map at 0x6200.

The BFS source is derived from Lady Bug’s position:

- 0x6027 = player x
- 0x6028 = player y

Confirmed

The BFS result does not store "distance only".
For each reachable cell, it stores the direction that should be taken from that
cell to move back toward Lady Bug.

**So in modern terms:**

- this is a BFS parent-direction map
- not just a scalar distance field

Practical meaning

If an enemy is in a cell whose BFS direction is "left", that means:

- from this cell, going left moves it toward Lady Bug

**Relevant routines:**

- 0x447D : BFS construction
- 0x45C4 : clears low nibbles before BFS rebuild
- 0x45DC : converts pixel coordinates to maze cell index
- 0x44CA : one half of BFS wave propagation
- 0x4542 : other half of BFS wave propagation

## Chase Timers

Confirmed

The 4 bytes 0x61CE..0x61D1 are per-enemy chase timers.

**If the timer of enemy i is greater than zero:**

1. the game reads that enemy’s current cell in the BFS map
2. if a BFS direction is available there
3. it overrides the normal preferred direction

**So:**

ChaseTimer > 0 means the enemy is temporarily allowed to use direct BFS guidance
toward Lady Bug.

Practical meaning

In Godot/C#, model this explicitly as:

- monster.ChaseTimer

When ChaseTimer > 0, replace the enemy’s base preferred direction with the BFS
direction from its current cell.

**Relevant routines:**

- 0x46D8 : overrides preferred direction from BFS when timer > 0
- 0x3A4C : decrements chase timers about once per second
- 0x0751 : startup/reset path that clears these counters

## Chase Activation Pattern

Confirmed

Chase is not always active.

The game activates it in time windows.

**There is a frame divider / second-like counter system involving:**

- 0x61B6
- 0x61B7
- 0x61B8
- 0x61D2

Confirmed

**Rough behavior:**

- about once per second:
  - 0x61B8 increments
  - active chase timers decrement
- on a very specific frame near the start of that second,
  the game checks whether a new chase activation should happen

Confirmed

A round-robin selector in 0x61D2 chooses which enemy is considered next.

So the game does not activate all enemies at once.
It tries them one by one in rotation.

Confirmed

If the chosen enemy already has an active chase timer, that activation opportunity
is simply lost.

Practical meaning

**This is an important part of the feel of the original game:**

- enemies do not all switch into direct pursuit together
- the game creates alternating pressure

**Relevant routines:**

- 0x46D8 : main chase activation logic
- 0x3A4C : one-second-like timing updates
- 0x471E..0x4731 : round-robin choice and skip if already active

## Chase Frequency By Level

Confirmed

The code derives a small pattern value from the current level, then checks
conditions on 0x61B8.

**Equivalent behavior:**

- some levels activate chase windows roughly every 8 seconds
- others every 4 seconds
- later levels every 2 seconds

So higher levels produce more frequent BFS-driven pursuit windows.

Practical meaning

Difficulty is not only about speed.
It also changes how often enemies get direct guidance toward the player.

**Relevant routines:**

- 0x1F26 : current level lookup
- 0x4788 : level-to-pattern table
- 0x47A6 : pattern translation table
- 0x46FB..0x4714 : checks activation window against 0x61B8

## Chase Duration

Confirmed

When chase is activated for an enemy, the timer value is loaded from one of two
tables.

The table selection depends on bit 0 of the difficulty DIP switch (0x9002 bit 0).

The chosen duration also grows with elapsed time.

Practical meaning

**As the round continues:**

- chase windows tend to last longer

This should be reproduced, because it contributes to the escalating tension.

**Relevant routines:**

- 0x4734..0x4752 : chooses chase duration table and loads timer
- 0x47AE : first duration table
- 0x47CD : second duration table
- 0x9002 : DIP switch input (difficulty bits)

## LOGICAL MAZE MAP (0x6200)

Confirmed

The logical maze map is stored in 0x6200..0x62AF.

It behaves like an 11x16 grid.

**Each cell uses:**

- high nibble = allowed directions
- low nibble  = BFS direction toward Lady Bug

Confirmed

**Allowed direction bits use the same encoding:**

- 1 = left
- 2 = up
- 4 = right
- 8 = down

Important correction

The high nibble represents allowed directions, not blocked ones.

**This matters because:**

- maze validation checks whether the requested direction is present
- BFS propagation also follows these allowed-direction bits

**Relevant routines:**

- 0x45FD : initializes high-nibble maze structure from ROM tables
- 0x3911 : validates direction against logical maze cell
- 0x447D : writes low-nibble BFS guidance
- 0x45DC : maps pixel coordinates to cell index

## Door Influence On Navigation

Confirmed

Doors dynamically modify the logical maze map.

**Two central door tile states are clearly recognized:**

- 0x36 = horizontal opening
- 0x3E = vertical opening

Confirmed

Specific routines modify the allowed directions around 20 special door locations.

**This means door orientation changes:**

- which passages are open
- how BFS propagates
- which directions an enemy may legally choose

Practical meaning

Doors are not just a rendering concern.
They are part of enemy AI and navigation.

**Relevant routines:**

- 0x463A : initializes door influence in logical maze map
- 0x467B : updates door influence dynamically
- 0x46C4 : table of 20 special door cell indices
- 0x0D1D : table used to locate relevant video/door tiles

## Local Direction Validation

Confirmed

At a decision point, the enemy does not automatically accept its preferred
direction.

**It is checked in two stages:**

1. maze logic validation
2. door graphic / local tile validation

1. Maze logic validation

Routine 0x3911 checks whether the preferred direction is allowed in the current
logical cell.

2. Door-local validation

Routine 0x4130 checks nearby special tiles and can reject a direction even if
the cell-level maze logic allowed it.

Practical meaning

In a Godot rewrite, keep these as two separate tests:

- IsDirectionAllowedByMazeCell(...)
- IsDirectionBlockedByDoorGeometry(...)

Do not merge them too early.

**Relevant routines:**

- 0x3911 : cell-level direction validation
- 0x4130 : door-local validation
- 0x42E6 : calls both tests for preferred direction
- 0x4241 : calls both tests while searching fallbacks

## Fallback Direction

Confirmed

If the preferred direction is rejected, the code searches for another valid
direction.

This is handled by 0x4241.

Practical meaning

**The enemy logic is not:**

- "pick one direction and stop if blocked"

**It is:**

1. try preferred direction
2. if invalid, search fallback direction
3. move using the accepted one

**Relevant routines:**

- 0x4241 : fallback-direction search
- 0x42E6 : first tries preferred direction before fallback
- 0x430C : writes accepted direction back into candidate state

## Forced Reversal Outside Intersections

Confirmed

When the enemy is between decision centers, routine 0x4189 can trigger a forced
direction reversal.

This is linked to special door-related tile situations.

Practical meaning

**Outside intersections:**

- normally keep going straight
- but if a door state makes the current path invalid in a special way,
  reverse direction

This is important for faithful edge cases around rotating doors.

**Relevant routines:**

- 0x4189 : special reversal test
- 0x433A : non-intersection movement branch
- 0x4347..0x4356 : computes opposite direction

## Enemy Movement Algorithm (Readable Summary)

Normal case

**For each enemy:**

1. if inactive, do nothing
2. if at a decision center:
   - determine preferred direction
   - if chase timer active, use BFS direction toward Lady Bug
   - otherwise use base preferred direction
   - validate the direction against:
```text
	 - allowed directions in the maze map
	 - local door constraints
```
   - if invalid, choose a fallback direction
3. if not at a decision center:
   - continue in the current direction
   - unless a door rule forces reversal
4. move by one pixel

**Relevant routines:**

- 0x407E : frame-level enemy update loop
- 0x42BA : per-enemy decision logic
- 0x4224 : one-pixel movement
- 0x427E : center-of-cell test
- 0x42E6 : preferred-direction attempt
- 0x4241 : fallback selection
- 0x4189 : forced reversal outside intersections

## Pseudo-Code (Semi-Technical)

**For each active enemy:**

```text
	if enemy is at cell decision center:
```

```text
		preferred_dir = base preferred direction
```

```text
		if ChaseTimer > 0:
			if BFS direction exists in current cell:
				preferred_dir = BFS direction
```

```text
		if preferred_dir is allowed by maze cell
		   and not blocked by local door rule:
			final_dir = preferred_dir
		else:
			final_dir = choose fallback valid direction
```

```text
	else:
```

```text
		final_dir = current direction
```

```text
		if special door rule forces reversal:
			final_dir = opposite(final_dir)
```

```text
	move one pixel in final_dir
```

**Relevant routines:**

- 0x42D2 : loads candidate dir/x/y
- 0x42DD : branches depending on center-of-cell test
- 0x43BA : performs movement step on candidate state
- 0x43CE : commits final dir/x/y back to enemy structure

## Recommended Godot/C# Architecture

**Suggested responsibilities:**

- MazeManager
  Owns static maze data and cell conversions.

- DoorManager
  Tracks the 20 special doors and updates allowed directions.

- NavigationGrid
  Stores allowed directions + BFS directions.

- BfsNavigator
  Rebuilds BFS map from Lady Bug.

- ChaseSystem
  Manages per-enemy chase timers and round-robin activation.

- MonsterPreferenceSystem
  Builds base preferred directions.

- MovementValidator
  Validates directions against maze and door logic.

- MonsterAi
  Applies final per-enemy movement.

- MonsterEntity
  Stores enemy state.

**Relevant routines for correspondence:**

- 0x45DC : cell conversion logic
- 0x45FD / 0x463A / 0x467B : logical maze + door updates
- 0x447D : BFS builder
- 0x46D8 : chase override
- 0x3911 / 0x4130 / 0x4189 : validation and reversal logic

## Recommended Implementation Order

1. implement logical cell grid
2. implement enemy pixel movement
3. implement decision-center logic
4. implement dynamic doors
5. implement BFS navigation from Lady Bug
6. implement chase timers and round-robin activation
7. implement local door rejection and forced reversal
8. refine base preferred direction generation

**Useful correspondence while implementing:**

- step 1:
  - 0x45FD
  - 0x45DC

- step 2:
  - 0x4224
  - 0x43CE

- step 3:
  - 0x427E
  - 0x42BA

- step 4:
  - 0x463A
  - 0x467B

- step 5:
  - 0x447D

- step 6:
  - 0x46D8
  - 0x3A4C

- step 7:
  - 0x4130
  - 0x4189
  - 0x4241

## What Is Solid Enough To Implement Now

Confirmed enough for implementation

- per-enemy movement is pixel-based
- decision points occur at cell centers
- chase uses a BFS direction map toward Lady Bug
- chase is timed and not always active
- one enemy at a time is selected in round-robin for chase activation
- doors modify navigation
- door state can block direction choices and force reversals

Still somewhat open

- exact gameplay meaning of some global variables involved in base preference
  generation
- exact naming of all special door tile values
- exact conceptual label for the "base behavior" outside BFS chase

These are not blockers for a faithful first implementation.

## Recommended Implementation Philosophy

For Godot/C#, do not try to mimic the original memory layout literally.

Instead, preserve the original gameplay principles:

- integer pixel positions
- decision only at logical centers
- dynamic allowed directions per cell
- BFS toward player
- temporary chase timers
- round-robin chase activation
- door-based local corrections

That should get you very close to the original behavior while keeping the code
clean.

**Recommended documentation habit:**

- when implementing a subsystem, add a short comment with the original arcade
  routine address
- keep a mapping table between Godot classes and arcade routines
- use the addresses above as anchors for later verification in Ghidra

## Arcade Routine Index

This section provides a compact mapping between the reverse-engineered arcade
routines and their practical meaning for enemy movement.

Use it as a quick reference while reading the disassembly or implementing the
Godot/C# version.

## Core Enemy Update

- 0x407E : global enemy update loop
- 0x42BA : per-enemy decision and movement
- 0x43F0 : copies current enemy dir/x/y into temporary work state
- 0x43CE : writes updated dir/x/y back into enemy structure

## Movement And Decision Points

- 0x4224 : one-pixel movement step
- 0x427E : center-of-cell test
- 0x42D2 : loads candidate dir/x/y before center test
- 0x43BA : applies movement step to candidate state

## Direction Selection

- 0x42E6 : tries preferred direction at an intersection
- 0x4241 : searches fallback direction if preferred one fails
- 0x430C : writes accepted direction back into candidate state

## Maze And Door Validation

- 0x3911 : validates direction against logical maze cell
- 0x4130 : door-local validation near the enemy
- 0x4189 : special test that can force a reversal outside intersections
- 0x4347..0x4356 : computes opposite direction

## Base Preferred-Direction Generation

- 0x2E5C : prepares base preferred directions for enemies
- 0x40F8 : builds a global control value in 0x61C3
- 0x40CC : transforms global control state into movement-related state

## BFS Chase System

- 0x447D : builds BFS guidance map from Lady Bug
- 0x45C4 : clears low nibbles before BFS rebuild
- 0x44CA : one half of BFS wave propagation
- 0x4542 : other half of BFS wave propagation
- 0x46D8 : overrides preferred directions from BFS when chase timer is active

## Coordinate And Grid Helpers

- 0x45DC : converts pixel coordinates to maze cell index
- 0x45FD : initializes logical maze map from ROM data

## Door Handling

- 0x463A : initializes door influence in logical maze map
- 0x467B : updates door influence dynamically
- 0x46C4 : table of 20 special door cell indices
- 0x0D1D : table used to locate relevant door/video tiles

## Chase Timers And Activation

- 0x3A4C : one-second-like timing update, decrements chase timers
- 0x0751 : startup/reset path that clears chase counters
- 0x46FB..0x4714 : checks chase activation window against 0x61B8
- 0x471E..0x4731 : round-robin enemy selection and skip-if-active logic
- 0x4734..0x4752 : loads chase duration from tables
- 0x4788 : level-to-pattern table
- 0x47A6 : pattern translation table
- 0x47AE : first chase-duration table
- 0x47CD : second chase-duration table

## Player-Related Input To Enemy AI

- 0x6027 / 0x6028 : Lady Bug position used as BFS source
- 0x9002 : DIP switch input, including difficulty bits
- 0x9001 : hardware-related status input used by some timing paths

## Enemy Data Locations

- 0x602B..        : enemy structures, 5 bytes per enemy
- 0x61C4..0x61C7  : preferred directions for enemies
- 0x61CE..0x61D1  : per-enemy chase timers
- 0x61D2          : round-robin enemy selector
- 0x6200..0x62AF  : logical maze map
