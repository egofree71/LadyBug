===============================================================================
ENEMY MOVEMENT
===============================================================================

===============================================================================
GOAL
===============================================================================

This document summarizes what we currently know about enemy movement in the arcade
game Lady Bug, based on reverse engineering of:

- the MAME driver (ladybug.cpp)
- the Ghidra disassembly (LadyBug_CPU.txt)
- the ROM set

The goal is not to emulate the Z80 instruction by instruction, but to reconstruct
the gameplay logic as faithfully as possible in Godot 4.6.1 with C#.

===============================================================================
CONFIDENCE LEVELS
===============================================================================

This document uses three confidence levels:

- Confirmed
  Backed directly by code paths we analyzed.

- Probable
  Strongly suggested by the code and data, but not fully proven in every detail.

- Open
  Still partially unclear and should be treated carefully during implementation.

===============================================================================
HIGH-LEVEL OVERVIEW
===============================================================================

Enemy movement in Lady Bug is not purely random and not a permanent direct chase.

It combines:

- a base preferred direction
- local validation against the maze and doors
- temporary BFS-guided chase phases toward Lady Bug
- forced corrections in some door-related situations

So the system is hybrid:

- sometimes enemies follow a general movement pattern
- sometimes one of them temporarily gets a much more direct path toward the player

===============================================================================
ENEMY UPDATE LOOP
===============================================================================

Confirmed

The global enemy update loop is centered around routine 0x407E.

Its role is roughly:

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

===============================================================================
ENEMY DATA STRUCTURE
===============================================================================

Confirmed

The 4 enemies are stored from 0x602B, with 5 bytes per enemy.

Practical structure:

- +0 : direction + flags
- +1 : x
- +2 : y
- +3 : sprite-related
- +4 : attribute-related

Direction encoding is:

- 1 = left
- 2 = up
- 4 = right
- 8 = down

Bit 1 in the first byte is used as an active/enabled flag.

Suggested C# model

public sealed class MonsterEntity
{
	public int Id;
	public int X;
	public int Y;
	public Dir Direction;
	public bool Active;

	public Dir PreferredDirection;
	public int ChaseTimer;
}

===============================================================================
MOVEMENT GRANULARITY
===============================================================================

Confirmed

Enemy movement is pixel by pixel.

Routine 0x4224 shows that one update step moves the enemy by exactly one pixel
in the current direction.

Equivalent behavior:

- left  -> x--
- up    -> y--
- right -> x++
- down  -> y++

Implementation note

Do not move enemies tile by tile.
Use integer pixel positions.

===============================================================================
DECISION POINTS
===============================================================================

Confirmed

An enemy does not choose a new direction at every pixel.

The main decision is normally taken only when the enemy reaches the logical center
of a maze cell.

The center test is equivalent to:

- x & 0x0F == 0x08
- y & 0x0F == 0x06

Practical meaning

Between two cell centers:

- the enemy usually keeps going in the same direction
- unless a special door-related rule forces a reversal

===============================================================================
PREFERRED DIRECTION
===============================================================================

Confirmed

Each enemy gets a preferred direction stored in 0x61C4..0x61C7.

This preferred direction is later used by the per-enemy logic at intersections.

Confirmed / Probable

The preferred direction can come from:

- a base behavior
- a temporary BFS chase override

The base behavior is built by routines including 0x2E5C, 0x40F8, and 0x40CC.

What is directly supported by the code:

- there is a global state influencing base preferred directions
- this state depends on:
  - level
  - elapsed time
  - difficulty
- one branch also uses the Z80 R register, so there is a pseudo-random component

Important wording note

It is safest to say:

"Outside BFS chase phases, enemies receive a preferred direction from routines
driven by a global gameplay state. That state is influenced by level, elapsed
time, and difficulty, and some branches also include a pseudo-random component."

This is more rigorous than saying simply "they move randomly".

===============================================================================
BFS-GUIDED CHASE
===============================================================================

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

So in modern terms:

- this is a BFS parent-direction map
- not just a scalar distance field

Practical meaning

If an enemy is in a cell whose BFS direction is "left", that means:

- from this cell, going left moves it toward Lady Bug

===============================================================================
CHASE TIMERS
===============================================================================

Confirmed

The 4 bytes 0x61CE..0x61D1 are per-enemy chase timers.

If the timer of enemy i is greater than zero:

1. the game reads that enemy’s current cell in the BFS map
2. if a BFS direction is available there
3. it overrides the normal preferred direction

So:

ChaseTimer > 0 means the enemy is temporarily allowed to use direct BFS guidance
toward Lady Bug.

Practical meaning

In Godot/C#, model this explicitly as:

- monster.ChaseTimer

When ChaseTimer > 0, replace the enemy’s base preferred direction with the BFS
direction from its current cell.

===============================================================================
CHASE ACTIVATION PATTERN
===============================================================================

Confirmed

Chase is not always active.

The game activates it in time windows.

There is a frame divider / second-like counter system involving:

- 0x61B6
- 0x61B7
- 0x61B8
- 0x61D2

Confirmed

Rough behavior:

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

This is an important part of the feel of the original game:

- enemies do not all switch into direct pursuit together
- the game creates alternating pressure

===============================================================================
CHASE FREQUENCY BY LEVEL
===============================================================================

Confirmed

The code derives a small pattern value from the current level, then checks
conditions on 0x61B8.

Equivalent behavior:

- some levels activate chase windows roughly every 8 seconds
- others every 4 seconds
- later levels every 2 seconds

So higher levels produce more frequent BFS-driven pursuit windows.

Practical meaning

Difficulty is not only about speed.
It also changes how often enemies get direct guidance toward the player.

===============================================================================
CHASE DURATION
===============================================================================

Confirmed

When chase is activated for an enemy, the timer value is loaded from one of two
tables.

The table selection depends on bit 0 of the difficulty DIP switch (0x9002 bit 0).

The chosen duration also grows with elapsed time.

Practical meaning

As the round continues:

- chase windows tend to last longer

This should be reproduced, because it contributes to the escalating tension.

===============================================================================
LOGICAL MAZE MAP (0x6200)
===============================================================================

Confirmed

The logical maze map is stored in 0x6200..0x62AF.

It behaves like an 11x16 grid.

Each cell uses:

- high nibble = allowed directions
- low nibble  = BFS direction toward Lady Bug

Confirmed

Allowed direction bits use the same encoding:

- 1 = left
- 2 = up
- 4 = right
- 8 = down

Important correction

The high nibble represents allowed directions, not blocked ones.

This matters because:

- maze validation checks whether the requested direction is present
- BFS propagation also follows these allowed-direction bits

===============================================================================
DOOR INFLUENCE ON NAVIGATION
===============================================================================

Confirmed

Doors dynamically modify the logical maze map.

Two central door tile states are clearly recognized:

- 0x36 = horizontal opening
- 0x3E = vertical opening

Confirmed

Specific routines modify the allowed directions around 20 special door locations.

This means door orientation changes:

- which passages are open
- how BFS propagates
- which directions an enemy may legally choose

Practical meaning

Doors are not just a rendering concern.
They are part of enemy AI and navigation.

===============================================================================
LOCAL DIRECTION VALIDATION
===============================================================================

Confirmed

At a decision point, the enemy does not automatically accept its preferred
direction.

It is checked in two stages:

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

===============================================================================
FALLBACK DIRECTION
===============================================================================

Confirmed

If the preferred direction is rejected, the code searches for another valid
direction.

This is handled by 0x4241.

Practical meaning

The enemy logic is not:

- "pick one direction and stop if blocked"

It is:

1. try preferred direction
2. if invalid, search fallback direction
3. move using the accepted one

===============================================================================
FORCED REVERSAL OUTSIDE INTERSECTIONS
===============================================================================

Confirmed

When the enemy is between decision centers, routine 0x4189 can trigger a forced
direction reversal.

This is linked to special door-related tile situations.

Practical meaning

Outside intersections:

- normally keep going straight
- but if a door state makes the current path invalid in a special way,
  reverse direction

This is important for faithful edge cases around rotating doors.

===============================================================================
ENEMY MOVEMENT ALGORITHM (READABLE SUMMARY)
===============================================================================

Normal case

For each enemy:

1. if inactive, do nothing
2. if at a decision center:
   - determine preferred direction
   - if chase timer active, use BFS direction toward Lady Bug
   - otherwise use base preferred direction
   - validate the direction against:
	 - allowed directions in the maze map
	 - local door constraints
   - if invalid, choose a fallback direction
3. if not at a decision center:
   - continue in the current direction
   - unless a door rule forces reversal
4. move by one pixel

===============================================================================
PSEUDO-CODE (SEMI-TECHNICAL)
===============================================================================

For each active enemy:

	if enemy is at cell decision center:

		preferred_dir = base preferred direction

		if ChaseTimer > 0:
			if BFS direction exists in current cell:
				preferred_dir = BFS direction

		if preferred_dir is allowed by maze cell
		   and not blocked by local door rule:
			final_dir = preferred_dir
		else:
			final_dir = choose fallback valid direction

	else:

		final_dir = current direction

		if special door rule forces reversal:
			final_dir = opposite(final_dir)

	move one pixel in final_dir

===============================================================================
RECOMMENDED GODOT/C# ARCHITECTURE
===============================================================================

Suggested responsibilities:

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

===============================================================================
RECOMMENDED IMPLEMENTATION ORDER
===============================================================================

1. implement logical cell grid
2. implement enemy pixel movement
3. implement decision-center logic
4. implement dynamic doors
5. implement BFS navigation from Lady Bug
6. implement chase timers and round-robin activation
7. implement local door rejection and forced reversal
8. refine base preferred direction generation

===============================================================================
WHAT IS SOLID ENOUGH TO IMPLEMENT NOW
===============================================================================

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

===============================================================================
RECOMMENDED IMPLEMENTATION PHILOSOPHY
===============================================================================

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
