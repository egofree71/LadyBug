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
- dynamic rotating-gate validation layered on top of the static maze

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
- PlayfieldStepResult
- Level runtime gate evaluation

The design is now split as follows:

1) PlayerController orchestrates input, movement ticks and rendering
2) PlayerInputState resolves movement intention using "last pressed wins"
3) PlayerMovementMotor owns the gameplay movement state and applies one tick
4) PlayerMovementTuning centralizes calibrated constants
5) MazeGrid evaluates static maze legality
6) Level combines static maze legality with dynamic rotating-gate legality

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
9. UPDATED REVERSE-ENGINEERED TURN MODEL
===============================================================================

The original arcade turn logic is now understood as more than a simple
"turn window" check.

The current best reverse-engineered model is:

1) input is normalized into logical direction bits
2) a turn may be armed
3) the effective direction may be committed or recommitted
4) the player may enter a special interactive turn mode
5) while in that mode, the next sub-step depends on:
   - current effective direction
   - current special direction
   - fine alignment inside the 16x16 cell
   - internal state/flags

Important:
This means the arcade game does NOT appear to use:
- a purely scripted post-commit sequence
- a free diagonal
- a single fixed tolerance rule that fully explains all turns

Instead, the post-commit logic is interactive and branch-driven.

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

Reverse engineering now strongly supports checks of the form:

	X & 0x0F == 0x08
	Y & 0x0F == 0x06

Historical earlier observations also saw:

	Y & 0x0F == 0x07

But the current best-supported special-mode branching uses:
- X & 0x0F == 0x08
- Y & 0x0F == 0x06

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
- special-mode branching uses this as the preferred X alignment
- when this is true, the code can use an X-aligned path instead of the
  intermediate dispatcher

-------------------------------------------------------------------------------
11.2 Horizontal alignment
-------------------------------------------------------------------------------

Observed condition:

	Y & 0x0F == 0x06

Interpretation:
- special-mode branching uses this as the preferred Y alignment
- when this is true, the code can use a Y-aligned path instead of the
  intermediate dispatcher

-------------------------------------------------------------------------------
11.3 About Y & 0x0F == 0x07
-------------------------------------------------------------------------------

Historical earlier reverse engineering observed Y & 0x0F == 0x07 in some
movement/turning contexts.

Current status:
- 0x07 should not be treated as the main confirmed branching test
- the exact meaning of 0x07 is still not fully resolved
- it may reflect another lane-related detail, sprite-origin relation, or a
  nearby operational row used in other parts of the movement system

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
This remains provisional for the remake side.
The reverse engineering now strongly supports:
- X special alignment at +8
- Y special alignment at +6 for the special dispatcher
but the exact final interpretation of the visual/gameplay anchor relationship
is still not considered fully closed.

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
14. STATIC MAZE VALIDATION
===============================================================================

Movement legality is connected directly to the logical maze.

The static path is:

- PlayerMovementMotor requests an attempted pixel step
- MazeGrid.EvaluateArcadePixelStep(...) evaluates that step
- the maze helper:
  - converts arcade-pixel positions to logical cells
  - applies the directional collision probe
  - checks whether the step remains inside the current cell
  - if the probe crosses into another cell, validates the move through CanMove(...)

So movement legality is no longer just "alignment-only".
It includes a probe-based validation against the static maze.

===============================================================================
15. ROTATING GATE INTEGRATION
===============================================================================

The movement model is now also connected to the rotating-gate system.

Current integration:
- Level owns the runtime GateSystem
- PlayerMovementMotor requests one attempted step from the active playfield
- Level combines:
  - static MazeGrid legality
  - probe-based gate blocking
  - boundary-crossing gate blocking
- if a step is blocked by a gate and the gate can be pushed from the contacted
  half:
  - the gate logical state toggles immediately
  - the attempted step is re-evaluated in the same tick
- gate visual turning is kept briefly through a separate runtime timer

In practice, the current movement rule is approximately:

	step allowed = lane/alignment state + maze legality + gate state

This is now implemented.
Rotating gates are no longer just a planned extension.

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
17. SPECIAL TURN MODE - KEY VARIABLES FROM REVERSE ENGINEERING
===============================================================================

Several RAM variables are now important for interpreting the original turn logic.

-------------------------------------------------------------------------------
17.1 6026 - effective current direction
-------------------------------------------------------------------------------

6026 is the effective runtime direction byte.

Observed commits/recommits:
- D=08 -> 6026 becomes 82
- D=01 -> 6026 becomes 12
- D=04 -> 6026 becomes 42
- D=02 -> 6026 becomes 22

So 381E -> 382C acts as the commit/recommit gate for 6026.

-------------------------------------------------------------------------------
17.2 605F - special-mode state byte
-------------------------------------------------------------------------------

605F bit 7 is now considered the key marker for entry into the special
interactive turn mode.

Strong working interpretation:
- bit 7 clear  -> simpler/non-special flow
- bit 7 set    -> special interactive turn flow

Observed values commonly include:
- 01 / 09 before special mode
- 81 / 89 in the special mode

Important:
the values 01 <-> 09 and 81 <-> 89 should not be overinterpreted as if they
represented multiple fundamentally different turn phases.

Current best interpretation:
- bit 7 is the major structural distinction
- the additional +0x08 oscillation reflects an auxiliary helper bit changing in
  parallel
- this auxiliary oscillation does not currently look like the core state of the
  special turn mode itself

So for practical turn-logic purposes:
- without bit 7 active -> not yet in full special turn mode
- with bit 7 active    -> full special turn mode active

The exact meaning of the lower bits remains unresolved.

-------------------------------------------------------------------------------
17.3 6198 - current special direction
-------------------------------------------------------------------------------

6198 is now strongly understood as the current special direction.

Observed values:
- 08 = up
- 01 = left
- 04 = right
- 02 = down

This is not just an abstract phase id.
It behaves like the current direction used by the special turn dispatcher.

-------------------------------------------------------------------------------
17.4 61E0 - transition / recentering selector
-------------------------------------------------------------------------------

61E0 is no longer treated as a purely mysterious pulse or generic auxiliary flag.

Current best interpretation:
- 61E0 belongs to the transition / recentering phase before or around full
  entry into the special interactive turn mode
- it is not the final 3677 dispatcher state itself

Observed writes include:
- 36C6 often writes 00
- 3754 can write 02
- 37EF can write 01
- 4948 writes 00 again during aligned/simplified handoff

Useful working interpretation:
- 61E0 = 00 -> no extra transition recentering active
- 61E0 = 02 -> one transition-axis correction case
- 61E0 = 01 -> the other transition-axis correction case

The exact symbolic meaning of 01 versus 02 is still not considered fully closed,
but 61E0 is now best understood as a transition/recentering selector, not as a
final dispatcher phase id.

-------------------------------------------------------------------------------
17.5 6196 / 6197 - target coordinates
-------------------------------------------------------------------------------

6196 and 6197 act as runtime target coordinates during turn handling and
alignment/correction.

===============================================================================
18. SPECIAL INTERACTIVE TURN MODE
===============================================================================

The current best reverse-engineered picture is:

1) input is normalized at 3652 into:
   - 01 = left
   - 02 = down
   - 04 = right
   - 08 = up

2) if the game is not yet in special mode, it uses the simpler path

3) when 605F bit 7 becomes active, the game enters the special interactive mode

4) inside that mode, it branches by fine alignment:
   - if X & 0x0F == 8 -> path via 4943
   - else if Y & 0x0F == 6 -> path via 366F
   - else -> path via 494B -> 3677

This means 494B -> 3677 is the central dispatcher for the intermediate
"between alignments" case.

===============================================================================
19. DISPATCHER TABLE OF 3677
===============================================================================

The intermediate dispatcher now has a confirmed 4-direction table.

Observed and now considered strongly established:

- up    -> 36A1
- right -> 36B9
- down  -> 369A
- left  -> 36C0

So 3677 should no longer be treated as an unknown black box.
Its branch table is now substantially mapped.

===============================================================================
20. ROLE OF 3868
===============================================================================

3868 appears as a more generic / aligned-state handler.

Current best interpretation:
- the intermediate direction-specific kernels are used in the "between
  alignments" part of the sequence
- once the motion is in a more aligned/simplified state, 3868 takes over

This is clearly observed for:
- up
- left
- down

For right, this is still very plausible by symmetry, but slightly less directly
observed.

===============================================================================
21. ENTRY TIMING OF THE SPECIAL MODE
===============================================================================

Reverse engineering now shows that entry into the special interactive turn mode
is not instantaneous.

The current best timing model is:

1) 3652 sees and normalizes the requested direction
2) 381E -> 382C may already commit or recommit 6026
3) the game may still be outside full special mode at that moment
4) 61E0 can participate in a transition/recentering phase
5) only afterwards does 605F gain bit 7
6) then 6198 becomes the current special direction
7) only then does the code enter:
   - 494B -> 3677 -> direction-specific kernel
   - or 3868 if the state is already aligned/simplified enough

Important:
This means the special dispatcher is not the beginning of the special sequence.
There is a real transition phase before fully active special-mode behavior.

===============================================================================
22. SHORT TAPS / SMALL SUCCESSIVE INPUTS
===============================================================================

Additional reverse engineering now shows an important timing detail:

With small successive taps toward the new direction, the first tap can already:
- normalize input
- commit 6026

without immediately entering the full special dispatcher path.

Observed sequence in a "small successive up taps" test:

1) input is normalized at 3652 (A=08)
2) 381E -> 382C commits 6026 to 82
3) only later does 605F gain bit 7 and enter the special mode
4) 6198 then becomes 08
5) then the game begins using:
   494B -> 3677 -> 36A1
6) once X reaches the preferred alignment, the flow leaves the intermediate
   dispatcher and 3868 takes over

Important:
small taps done by hand on a keyboard do not necessarily correspond to a single
arcade sub-step each.
So the exact tap-to-substep relation is still not considered fully resolved.

But the current strong timing conclusion is:
- commit can happen before full special-mode entry
- special-mode entry can happen after the commit
- intermediate dispatcher use can happen after special-mode entry, not
  necessarily immediately on the first accepted tap

===============================================================================
23. CURRENT IMPLEMENTATION STATUS
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
- static maze validation for each attempted pixel step
- dynamic rotating-gate validation layered on top of the static maze
- immediate gate push resolution and same-tick step re-evaluation
- short rotating-gate turning visual state
- refactored movement architecture using helpers

The implementation is no longer just a monolithic PlayerController.
It is now split into dedicated helpers and is much closer to a maintainable
arcade-style structure.

===============================================================================
24. WHAT IS ALREADY BELIEVED TO BE CORRECT
===============================================================================

The following points are considered strong working assumptions:

- movement should use a fixed tick system
- movement should use integer arcade-pixel positions
- movement should advance in small discrete steps
- direction changes should be buffered
- the most recently pressed held direction should drive intention
- turning depends on alignment
- the player moves on internal lanes, not just generic tile centers
- static maze legality should be checked at the per-step level, not only at high level
- rotating-gate interaction belongs in the step-validation chain, not in a purely
  visual script
- a successful gate push should toggle the logical gate state immediately and
  re-evaluate the attempted step in the same tick
- 605F bit 7 marks entry into the special interactive turn mode
- 381E -> 382C is the commit/recommit gate for 6026
- 6198 stores the current special direction
- 494B -> 3677 is the central intermediate dispatcher
- 3677 now has a mapped 4-direction kernel table
- 3868 acts as a more generic/aligned follow-up handler
- the special mode has a real entry/transition phase before full dispatcher use
- 61E0 belongs primarily to that transition/recentering phase

===============================================================================
25. WHAT IS STILL UNCERTAIN
===============================================================================

The following points still require more reverse engineering:

- the exact meaning of the lower bits of 605F besides bit 7
- the exact meaning of Y & 0x0F == 0x07
- the exact conditions under which the game chooses 61E0 = 01 versus 61E0 = 02
- the exact internal code inside 3677, if a literal ROM-level reproduction is desired
- the exact moment and conditions under which 3868 takes over in every possible
  case, especially right
- the exact relation between a human keyboard "small tap" and a single arcade
  sub-step of the special turn sequence
- whether the current collision leads match the original checks or are only a
  practical approximation
- whether the current gate probe/boundary combination matches the original code path
  exactly or is still an approximation
- whether straight-line recentering in the arcade original is identical to the
  current conservative implementation

===============================================================================
26. DESIGN PHILOSOPHY
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
- static maze legality is explicit
- dynamic rotating-gate legality is explicit

===============================================================================
27. SUMMARY
===============================================================================

The movement system is no longer best described as:
- "simple per-pixel movement with a turn window"

The current best reverse-engineered picture is:

- fixed tick movement
- integer arcade-pixel gameplay position
- buffered intention
- commit/recommit of effective direction
- entry into a special interactive turn mode
- fine alignment checks
- intermediate dispatcher (494B -> 3677)
- direction-specific intermediate kernels
- aligned/generic follow-up handling

So the remaining movement work is no longer mainly about the existence of a
turn window.

It is now mainly about:
- reproducing the interactive special post-commit turn logic faithfully
- reproducing the transition phase before fully active special mode
- refining the remaining uncertain low-level details when needed


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
- 0x380A : consumes the prepared DE pair from stack and enters the commit/transition path
- 0x3810 : clears 605F bit 7 inside the commit/transition path
- 0x381E : commit / recommit gate just before the effective direction write
- 0x382C : writes the effective direction byte to 0x6026
- 0x388C : re-enters the post-commit path and sets 605F bit 7
- 0x3891 : observed write site for 0x89 to 0x605F during entry into special mode
- 0x3A99 : player-related update path using current dir/x/y

===============================================================================
INPUT AND CURRENT PLAYER STATE
===============================================================================

- 0x3652 : normalizes directional input into logical direction bits
		   (01 left / 02 down / 04 right / 08 up)
- 0x6026 : current effective player direction
- 0x6027 : player X position
- 0x6028 : player Y position
- 0x605F : internal movement/special-mode state byte
		   (bit 7 strongly linked to entry into the special turn mode)
- 0x6196 : runtime target X
- 0x6197 : runtime target Y
- 0x6198 : current special direction
- 0x61E0 : transition/recentering selector
- 0x9000 / 0x9001 : hardware input ports used by joystick / status logic

===============================================================================
TURN WINDOWS AND LANE ALIGNMENT
===============================================================================

Historical turn-center routines / tables:
- 0x36DA : loads row-based vertical turn-center mask
- 0x36F5 : scans vertical turn-center mask
- 0x377A : loads column-based horizontal turn-center mask
- 0x379D : scans horizontal turn-center mask
- 0x0DE4 : vertical turn-center table by row
- 0x0DFA : horizontal turn-center table by column

Special-mode branching by fine alignment:
- 0x3662 : checks X & 0x0F against the preferred X alignment
- 0x366C : checks Y & 0x0F against the preferred Y alignment
- 0x4943 : aligned-entry path when X & 0x0F == 8
- 0x366F : aligned-entry path when Y & 0x0F == 6
- 0x494B : intermediate path when neither preferred alignment is reached
- 0x3677 : dispatcher used for the "between alignments" case

Practical lane / alignment findings from the current analysis:
- X % 16 == 8 : preferred vertical alignment and special X-aligned branch
- Y % 16 == 6 : preferred horizontal alignment for special Y-aligned branch
- Y % 16 == 7 : still a useful practical horizontal lane travel center in the
				current remake-side interpretation, but not the main confirmed
				special-branch test

===============================================================================
SPECIAL INTERACTIVE TURN MODE
===============================================================================

State/control routines:
- 0x36C1 : simpler producer path before / outside the full special dispatcher
- 0x36C6 : clears 0x61E0 on the 0x36C1 path
- 0x366F : PUSH DE on the Y-aligned special path
- 0x3754 : writes 0x61E0 = 0x02 in observed turn-handling sequences
- 0x37EF : writes 0x61E0 = 0x01 in observed aligned follow-up sequences
- 0x388C : activates the special-mode phase after the initial commit/recommit
- 0x3891 : observed write site setting 0x605F to 0x89
- 0x4943 : PUSH DE on the X-aligned special path
- 0x4948 : clears 0x61E0 again during aligned/simplified handoff

Prepared direction package / commit helpers:
- 0x36C1 : PUSH DE on the simpler path
- 0x366F : PUSH DE on the Y-aligned special path
- 0x4943 : PUSH DE on the X-aligned special path
- 0x380A : POP DE from the stack and continue with direction commit logic

Observed prepared direction values pushed toward 0x380A:
- 0x0805 : up
- 0x0105 : left
- 0x0405 : right
- 0x0205 : down

===============================================================================
INTERMEDIATE DISPATCHER AND KERNELS
===============================================================================

Dispatcher:
- 0x494B : reloads current direction from 0x6026, shifts it, then jumps to 0x3677
- 0x3677 : central dispatcher for the intermediate "between alignments" case

Confirmed dispatcher table:
- up    -> 0x36A1
- right -> 0x36B9
- down  -> 0x369A
- left  -> 0x36C0

Observed kernel families:
- 0x368F / 0x3695 / 0x369A : down-oriented intermediate path
- 0x36AB / 0x36B4 / 0x36B9 : right-oriented intermediate path
- 0x36AB / 0x36BB / 0x36C0 : left-oriented intermediate path
- 0x368F / 0x369C / 0x36A1 : up-oriented intermediate path

Generic / aligned follow-up:
- 0x3868 : generic or aligned-state follow-up handler
- 0x3856 : pure vertical-step continuation observed in upward aligned follow-up
- 0x385B : pure horizontal-step continuation observed in left aligned follow-up
- 0x3860 : pure vertical-step continuation observed in downward aligned follow-up

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
