===============================================================================
PLAYER MOVEMENT SYSTEM
===============================================================================

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

Purpose of this document:
- describe the current movement model used for the player
- document what has already been implemented
- distinguish validated behavior from still-provisional assumptions
- keep a clear reference for future reverse-engineering refinements

===============================================================================
1. GENERAL GOAL
===============================================================================

The goal is NOT to use a modern free-movement system such as:

	Position += direction * Speed * delta;

Instead, the goal is to reproduce the original arcade behavior as closely as
possible.

The movement system is therefore designed around:
- fixed tick updates
- integer arcade-pixel gameplay position
- 1 pixel movement per tick
- buffered direction changes
- internal lane alignment inside 16x16 cells
- maze validation for each attempted pixel step

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

The current movement model is an arcade-oriented implementation built around
small dedicated helpers:

- PlayerController
- PlayerInputState
- PlayerMovementMotor
- PlayerMovementTuning
- MazeGrid / MazeStepResult

The design is now split as follows:

1) PlayerController orchestrates input, movement ticks and rendering
2) PlayerInputState resolves movement intention using "last pressed wins"
3) PlayerMovementMotor owns the gameplay movement state and applies one tick
4) PlayerMovementTuning centralizes calibrated constants
5) MazeGrid evaluates whether an attempted pixel step is legal

This is more faithful to the arcade structure than a modern free movement model,
while remaining easy to refine.

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
		RunOneTick();
	}

Purpose:
- reproduce hardware-style movement timing
- avoid frame-rate dependent behavior
- update movement in discrete simulation steps

===============================================================================
5. INTEGER ARCADE-PIXEL GAMEPLAY POSITION
===============================================================================

The gameplay position is stored as integer arcade-pixel coordinates:

	Vector2I _arcadePixelPos;

Purpose:
- avoid floating-point drift
- match original arcade-style movement
- support exact lane and cell alignment checks

Important:
The gameplay logic is based on integer arcade-pixel positions, not on
floating-point scene positions.

Rendering is synchronized from the gameplay position after the simulation step.

===============================================================================
6. MOVEMENT SPEED
===============================================================================

Current movement assumption:

- the player moves exactly 1 pixel per tick

Example:

	_arcadePixelPos += _currentDir;

This means:
- movement is discrete
- each simulation tick advances the player by one pixel
- movement is not expressed as "pixels per second" in the gameplay model

Status:
- this is currently the implemented model
- it appears much closer to the original game than free delta-based motion
- further reverse engineering may still refine timing details

===============================================================================
7. DIRECTION MODEL
===============================================================================

The current code distinguishes several movement-related directions.

-------------------------------------------------------------------------------
7.1 Wanted Direction
-------------------------------------------------------------------------------

	wantedDir / _wantedDir

Meaning:
- the direction currently intended by the player
- resolved from buffered input using "last pressed wins"

-------------------------------------------------------------------------------
7.2 Current Direction
-------------------------------------------------------------------------------

	_currentDir

Meaning:
- the direction actually used by effective gameplay movement

-------------------------------------------------------------------------------
7.3 Facing Direction
-------------------------------------------------------------------------------

	_facingDir

Meaning:
- the direction currently shown by the sprite

Important:
Facing may update immediately when the player changes input, even before the
movement motor has accepted that direction.

-------------------------------------------------------------------------------
7.4 Offset Direction
-------------------------------------------------------------------------------

	_offsetDir

Meaning:
- the direction used to choose the sprite render offset

Important:
This is intentionally separate from facing.
The sprite may visually point toward the wanted direction while the render
offset still follows the effective movement direction.

===============================================================================
8. INPUT HANDLING
===============================================================================

Current input actions:

- move_left
- move_right
- move_up
- move_down

Input is handled by PlayerInputState.

It tracks:
- which directions are currently held
- the relative order in which they were pressed

Rule used:
- if several directions are held at once, the most recently pressed one wins

So input does not directly move the player.
It produces the current intended direction that the movement motor will try
to apply.

===============================================================================
9. DIRECTION CHANGE LOGIC
===============================================================================

A turn is not always applied immediately.

The current logic is:

1) read current intended direction
2) if no input is held, stop immediately
3) if stopped, try to start or resume in the wanted direction
4) if already moving:
   - same-axis change is applied immediately
   - perpendicular change is accepted only if alignment and maze legality allow it
5) if a perpendicular requested direction is blocked, the actor stops instead
   of continuing in the old direction

This last point is important:
the current implementation is no longer the old intermediate rule
"otherwise continue moving in _currentDir".
It now behaves more strictly when the newly requested perpendicular path is
blocked.

===============================================================================
10. ALIGNMENT INSIDE 16x16 CELLS
===============================================================================

Important finding:
movement does not simply happen from tile center to tile center.

The player moves along internal lanes inside each 16x16 maze cell.

This means:
- the maze is based on 16x16 logical cells
- but movement rails exist at specific internal offsets
- turning depends on lane alignment, not just generic cell centers

===============================================================================
11. BITWISE ALIGNMENT CHECKS FROM REVERSE ENGINEERING
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
- the exact meaning of 0x06 / 0x07 is still not fully confirmed

===============================================================================
12. CURRENT PROVISIONAL ALIGNMENT RULES
===============================================================================

The current implementation uses the following working assumptions:

For vertical movement / turning:
- align to X = cell base + 0x08

For horizontal movement / turning:
- use Y = cell base + 0x07 as the current travel lane center

In practice, the gameplay anchor currently used by Level is:

	GameplayAnchorArcade = new(8, 7)

And the movement tuning currently uses:
- horizontal rail snap tolerance = 1
- vertical rail snap tolerance = 1

Purpose:
- approximate the observed arcade lane behavior
- keep alignment logic explicit and easy to refine later

Important:
This is still provisional.
It is a validated intermediate implementation, not yet a final claim about
the original ROM logic.

===============================================================================
13. LANE SNAP AND RECENTERING
===============================================================================

The current movement model uses two related ideas:

1) rail snap when starting, resuming or turning
2) conservative straight-line recentering after a successful step

Current idea:
- if movement resumes horizontally, snap Y to the horizontal rail if already close
- if movement resumes vertically, snap X to the vertical rail if already close
- after a valid straight movement step, re-center on the current rail if the
  orthogonal deviation is still within the existing snap tolerance

Purpose:
- keep the actor on the internal movement rails
- avoid accumulating off-lane drift
- stay conservative instead of introducing aggressive auto-correction

Status:
- implemented
- intentionally cautious
- still open to refinement if later reverse engineering shows a stronger or
  different recentering behavior in the arcade original

===============================================================================
14. MAZE VALIDATION
===============================================================================

Movement legality is now connected directly to the logical maze.

The current path is:

- PlayerMovementMotor requests an attempted pixel step
- MazeGrid.EvaluateArcadePixelStep(...) evaluates that step
- the maze helper:
  - converts arcade-pixel positions to logical cells
  - applies the directional collision probe
  - checks whether the step remains inside the current cell
  - if the probe crosses into another cell, validates the move through CanMove(...)

So movement legality is no longer just "alignment-only".
It is now:

	pixel-step legality = alignment + probe-based maze validation

This is an important milestone compared to the older intermediate versions.

===============================================================================
15. RELATION TO THE MAZE SYSTEM
===============================================================================

The movement model is now actively connected to the logical maze system.

Current integration:
- MazeGrid defines legal logical exits from each cell
- MazeGrid.EvaluateArcadePixelStep(...) evaluates one attempted pixel step
- PlayerMovementMotor uses this helper for movement legality

What is still missing:
- dynamic interaction with rotating gates
- any gate-specific override of movement legality

So at the moment, the movement rule is approximately:

	step allowed = lane/alignment state + maze legality

And later it will need to become:

	step allowed = lane/alignment state + maze legality + gate state

===============================================================================
16. RELATION TO ANIMATION AND VISUAL FACING
===============================================================================

Current visual setup:
- base horizontal animation: move_right
- base vertical animation: move_up
- FlipH used for left
- FlipV used for down

Important:
Animation is no longer driven purely by _currentDir.

The current behavior is:
- facing follows the current intended direction immediately
- sprite render offset follows the effective movement direction

This separation was introduced deliberately to get two useful properties:
- the sprite can "point" toward a requested direction before the turn is accepted
- the render offset remains consistent with the actual movement rail

So visually:
- facing follows input
- offset follows effective movement

===============================================================================
17. CURRENT IMPLEMENTATION STATUS
===============================================================================

The current implementation includes:

- fixed tick update
- integer arcade-pixel gameplay position
- one pixel per tick movement
- buffered input with "last pressed wins"
- current direction / wanted direction separation
- explicit facing direction
- explicit render-offset direction
- provisional lane alignment
- lane snap
- conservative straight-line recentering
- maze validation for each attempted pixel step
- refactored movement architecture using helpers

The implementation is no longer just a monolithic PlayerController.
It is now split into dedicated helpers and is much closer to a maintainable
arcade-style structure.

===============================================================================
18. WHAT IS ALREADY BELIEVED TO BE CORRECT
===============================================================================

The following points are considered strong working assumptions:

- movement should use a fixed tick system
- movement should use integer arcade-pixel positions
- movement should advance in small discrete steps
- direction changes should be buffered
- the most recently pressed held direction should drive intention
- turning depends on alignment
- the player moves on internal lanes, not just generic tile centers
- maze legality should be checked at the per-step level, not only at high level

===============================================================================
19. WHAT IS STILL UNCERTAIN
===============================================================================

The following points still require more reverse engineering:

- the exact meaning of Y & 0x0F == 0x06 / 0x07
- whether 0x06 and 0x07 are:
  - a tolerance window
  - two accepted rows
  - sprite offset compensation
  - another internal rule
- the exact original turn acceptance rules
- the exact original turn-window tables in final gameplay use
- the exact collision checks used by the original ROM
- whether the current collision leads match the original checks or are only a
  practical approximation
- the exact interaction with rotating gates
- whether movement ever uses a repeating sub-pattern instead of a strict constant
- whether straight-line recentering in the arcade original is identical to the
  current conservative implementation

===============================================================================
20. DESIGN PHILOSOPHY
===============================================================================

The goal is not to implement a modern clean-room movement system first and then
try to "make it feel arcade".

Instead, the goal is:
1) understand the original behavior
2) reproduce its structure
3) refine the details progressively

The current codebase now reflects that philosophy better than before:
- input is separated
- tuning is centralized
- movement logic is isolated
- maze legality is explicit

===============================================================================
21. SUMMARY
===============================================================================

Current player movement is based on:

- fixed tick timing
- integer arcade-pixel gameplay position
- 1 pixel per tick
- buffered input using "last pressed wins"
- explicit current / wanted / facing / offset directions
- lane alignment inside 16x16 cells
- rail snap
- conservative straight-line recentering
- maze validation for each attempted pixel step

This is no longer just an intermediate sketch.
It is now a structured arcade-oriented implementation with clear extension
points for future refinement.

The biggest remaining gameplay-specific unknowns are:
- exact turn-window details
- exact collision details from the original code
- rotating gate interaction

===============================================================================
PLAYER ROUTINE INDEX
===============================================================================

This section provides a compact mapping between the reverse-engineered arcade
routines and their practical meaning for player movement.

Use it as a quick reference while reading the disassembly or implementing the
Godot/C# version.

All addresses below refer to the currently analyzed Lady Bug ROM/disassembly.

===============================================================================
CORE PLAYER UPDATE
===============================================================================

- 0x35FF : main player movement / direction-handling path
- 0x380A : one-pixel player movement step
- 0x388C : post-step movement / target-handling path
- 0x3A99 : player-related update path using current dir/x/y

===============================================================================
INPUT AND CURRENT PLAYER STATE
===============================================================================

- 0x6026 : current player direction
- 0x6027 : player X position
- 0x6028 : player Y position
- 0x9000 / 0x9001 : hardware input ports used by joystick / status logic

===============================================================================
TURN WINDOWS AND LANE ALIGNMENT
===============================================================================

- 0x36DA : loads row-based vertical turn-center mask
- 0x36F5 : scans vertical turn-center mask
- 0x377A : loads column-based horizontal turn-center mask
- 0x379D : scans horizontal turn-center mask
- 0x0DE4 : vertical turn-center table by row
- 0x0DFA : horizontal turn-center table by column

Practical lane centers inferred from the current analysis:
- X % 16 == 8 : vertical lane center
- Y % 16 == 6 : horizontal turn decision line
- Y % 16 == 7 : horizontal lane travel center

===============================================================================
MAZE VALIDATION
===============================================================================

- 0x390D : loads target turn position
- 0x3911 : validates requested direction against logical maze cell
- 0x0DA2 : logical maze table used for direction-open tests

===============================================================================
TIMING AND MAIN LOOP LINKS
===============================================================================

- 0x0784..0x0888 : main gameplay loop path
- 0x1FC7 : vblank-related timing update
- 0x6059 : timing counter updated from 0x1FC7
- 0x605A : slower timing counter updated from 0x1FC7
