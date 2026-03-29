===============================================================================
PLAYER MOVEMENT SYSTEM
===============================================================================

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

Purpose of this document:
- describe the current movement model used for the player
- document what has already been inferred from reverse engineering
- separate confirmed behavior from provisional assumptions
- keep a clear reference for future refinements

===============================================================================
1. GENERAL GOAL
===============================================================================

The goal is NOT to use a modern free-movement system such as:

	Position += direction * Speed * delta;

Instead, the goal is to reproduce the original arcade behavior as closely as
possible.

The movement system is therefore designed around:
- fixed tick updates
- integer pixel position
- 1 pixel movement per tick
- buffered direction changes
- internal lane alignment inside 16x16 cells

===============================================================================
2. PREVIOUS APPROACH (ABANDONED)
===============================================================================

Previous prototype movement used:
- frame-based update
- floating-point position
- continuous movement using delta time

Example:

	Position += _moveDirection * Speed * delta;

Problems with this approach:
- not faithful to the arcade logic
- depends on a modern continuous movement model
- does not reflect pixel-by-pixel hardware stepping
- does not enforce directional alignment constraints

This approach was useful for early testing only.

===============================================================================
3. CURRENT MOVEMENT MODEL
===============================================================================

The current movement model is an intermediate arcade-style implementation.

It is based on the following ideas:

1) fixed tick timing
2) integer pixel coordinates
3) one pixel moved per tick
4) current direction + wanted direction
5) direction changes only when allowed
6) alignment handled explicitly

This is closer to the original game than a free delta-based movement system.

===============================================================================
4. FIXED TICK RATE
===============================================================================

Movement is updated at a fixed tick rate:

	TickRate = 60.1145 Hz

And:

	TickDuration = 1.0 / TickRate

The update loop uses an accumulator:

	_accumulator += delta;
	while (_accumulator >= TickDuration)
	{
		_accumulator -= TickDuration;
		StepOneTick();
	}

Purpose:
- reproduce hardware-style movement timing
- avoid frame-rate dependent behavior
- update movement in discrete simulation steps

===============================================================================
5. INTEGER PIXEL POSITION
===============================================================================

The player position is stored as integer pixel coordinates:

	Vector2I _pixelPos;

Purpose:
- avoid floating-point drift
- match original arcade-style movement
- support exact bitwise alignment checks

Visual position is synchronized from _pixelPos after simulation.

Important:
The gameplay logic is based on integer pixel positions, not on a floating-point
continuous position.

===============================================================================
6. MOVEMENT SPEED
===============================================================================

Current movement assumption:

- the player moves exactly 1 pixel per tick

Example:

	_pixelPos += _currentDir;

This means:
- movement is discrete
- each simulation tick advances the player by one pixel
- movement is not expressed as "pixels per second" in the gameplay model

Status:
- this is currently used as the intermediate implementation
- it appears much closer to the original than free delta-based motion
- further reverse engineering may still refine some details

===============================================================================
7. DIRECTION MODEL
===============================================================================

Two directions are tracked:

-------------------------------------------------------------------------------
7.1 Current Direction
-------------------------------------------------------------------------------

	_currentDir

Meaning:
- the direction the player is actually moving right now

-------------------------------------------------------------------------------
7.2 Wanted Direction
-------------------------------------------------------------------------------

	_wantedDir

Meaning:
- the direction requested by the player input

Purpose of this separation:
- allow direction buffering
- allow the player to press a direction slightly before an intersection
- only apply the turn when the turn is allowed

This is a classic arcade maze-game behavior.

===============================================================================
8. INPUT HANDLING
===============================================================================

Current input actions:

- move_left
- move_right
- move_up
- move_down

Input does not directly change the actual movement direction.
It only updates _wantedDir.

This means:
- input is buffered
- movement logic decides when the wanted direction can become current

===============================================================================
9. DIRECTION CHANGE LOGIC
===============================================================================

A turn is not applied immediately.

The current logic is:

1) read player input
2) update _wantedDir
3) at each tick, try to apply _wantedDir
4) only switch if turning is allowed
5) otherwise continue moving in _currentDir

In simplified form:

	if wanted direction is allowed
		align if needed
		current direction = wanted direction
	else
		continue in current direction

This is one of the key differences from a free movement model.

===============================================================================
10. ALIGNMENT INSIDE 16x16 CELLS
===============================================================================

Important finding:
movement does not simply happen from tile center to tile center.

The player appears to move along internal "lanes" inside each 16x16 cell.

This means:
- the maze is based on 16x16 cells
- but movement rails exist at specific offsets inside those cells
- turning depends on lane alignment, not just cell coordinates

===============================================================================
11. BITWISE ALIGNMENT CHECKS
===============================================================================

Reverse engineering suggests checks of the form:

	X & 0x0F == 0x08
	Y & 0x0F == 0x06
	Y & 0x0F == 0x07

Interpretation:
- 0x0F corresponds to 15
- using "& 0x0F" isolates the local position inside a 16-pixel block
- this is equivalent to checking the position modulo 16

So the game is checking sub-cell alignment.

-------------------------------------------------------------------------------
11.1 Vertical alignment
-------------------------------------------------------------------------------

Observed condition:

	X & 0x0F == 0x08

Interpretation:
- vertical movement or vertical turning appears to depend on X being aligned
  to an internal vertical lane

-------------------------------------------------------------------------------
11.2 Horizontal alignment
-------------------------------------------------------------------------------

Observed conditions:

	Y & 0x0F == 0x06
	Y & 0x0F == 0x07

Interpretation:
- horizontal movement or horizontal turning appears to depend on Y alignment
- the exact meaning of 0x06 / 0x07 is not fully confirmed yet

===============================================================================
12. CURRENT PROVISIONAL ALIGNMENT RULES
===============================================================================

The current intermediate implementation uses the following assumptions:

For vertical movement / turning:
- align to X = cell base + 0x08

For horizontal movement / turning:
- treat Y = cell base + 0x06 or 0x07 as valid
- currently snap horizontally to Y = cell base + 0x07

Purpose:
- approximate the observed arcade lane behavior
- keep alignment logic isolated and easy to refine later

Important:
This is still provisional.
It is a useful intermediate model, not a final claim about the original code.

===============================================================================
13. LANE SNAP
===============================================================================

When a turn is accepted, the player may be aligned to the corresponding lane.

Current idea:
- if turning vertically, snap X to the vertical lane
- if turning horizontally, snap Y to the horizontal lane

Purpose:
- keep the actor on the internal movement rails
- avoid accumulating off-lane drift
- mimic the original game's path-following behavior

Status:
- lane snap is implemented in an intermediate form
- exact original behavior still needs deeper confirmation

===============================================================================
14. CURRENT IMPLEMENTATION STATUS
===============================================================================

The current PlayerController already includes:

- fixed tick update
- integer pixel position
- one pixel per tick movement
- current direction / wanted direction
- provisional alignment checks
- provisional lane snap
- animation update based on current direction

This is already much closer to the arcade logic than the previous prototype.

===============================================================================
15. WHAT IS ALREADY BELIEVED TO BE CORRECT
===============================================================================

The following points are considered strong working assumptions:

- movement should use a fixed tick system
- movement should use integer pixel positions
- movement should advance in small discrete steps
- direction changes should be buffered
- turning depends on alignment
- the player moves on internal lanes, not just generic tile centers

===============================================================================
16. WHAT IS STILL UNCERTAIN
===============================================================================

The following points still require more reverse engineering:

- the exact meaning of Y & 0x0F == 0x06 / 0x07
- whether 0x06 and 0x07 are:
  - a tolerance window
  - two accepted rows
  - sprite offset compensation
  - another internal rule
- exact snap behavior when turning
- exact turn acceptance rules
- exact collision checks with walls
- exact interaction with rotating gates
- whether movement ever uses a repeating sub-pattern instead of a strict constant

===============================================================================
17. RELATION TO THE MAZE SYSTEM
===============================================================================

This movement model is only one part of the final gameplay logic.

It will later need to connect to a logical maze representation.

Planned integration:
- MazeGrid will define legal paths
- turning will require BOTH:
  - correct alignment
  - a valid open path in the maze
- gates will dynamically affect movement possibilities

So the final movement rule will be:

    turn allowed = alignment condition + maze condition

===============================================================================
18. RELATION TO ANIMATION
===============================================================================

Animation is currently driven by _currentDir.

Current visual setup:
- base horizontal animation: move_right
- base vertical animation: move_up
- FlipH used for left
- FlipV used for down

Important:
Animation follows actual movement direction, not just player input.

===============================================================================
19. DESIGN PHILOSOPHY
===============================================================================

The goal is not to implement a modern clean-room movement system first and then
try to "make it feel arcade".

Instead, the goal is:
1) understand the original behavior
2) reproduce its structure
3) refine the details progressively

This document is therefore meant to preserve reasoning and assumptions while the
reverse engineering continues.

===============================================================================
20. SUMMARY
===============================================================================

Current player movement is based on:

- fixed tick timing
- integer pixel position
- 1 pixel per tick
- buffered direction changes
- alignment checks inside 16x16 cells
- provisional lane snap

This is an intermediate but already arcade-oriented implementation.

The exact details of turning, horizontal lane alignment, and maze interaction
still need further reverse engineering.
