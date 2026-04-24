# Player Movement System

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

**Purpose of this document:**
- describe the current movement model used for the player
- document how the reverse-engineered turn behavior is implemented in Godot
- distinguish implemented behavior from low-level reverse-engineering reference material
- keep a clear reference for future movement refinements and regression tests

## 1. General Goal

The goal is not to use a modern free-movement system such as:

```text
Position += direction * Speed * delta;
```

Instead, the player movement system is designed to reproduce the arcade behavior as closely as is practical inside the Godot remake.

The movement system is built around:
- fixed tick updates
- integer arcade-pixel gameplay position
- one-pixel committed movement segments
- buffered input and direction latching
- internal lane alignment inside 16x16 cells
- reverse-engineered turn windows
- assisted turns with short orthogonal correction steps
- static maze validation for each committed pixel segment
- dynamic rotating-gate validation layered on top of the static maze
- movement segment reporting for collectible pickup checks

## 2. Previous Approach

The first prototype movement used frame-based floating-point motion:

```text
Position += _moveDirection * Speed * delta;
```

This approach was abandoned because it did not match the arcade structure.
It was useful for early testing, but it did not reproduce:
- pixel-by-pixel movement
- exact lane alignment
- short-tap behavior
- turn-window behavior
- rotating-gate interaction at the movement-step level

## 3. Current Movement Architecture

The current player movement subsystem is split into small helper classes.

```text
PlayerController
PlayerDebugOverlay
PlayerInputState
PlayerMovementMotor
PlayerMovementTuning
PlayerMovementStepResult
PlayerMovementSegment
PlayerTurnWindowResolver
PlayerTurnWindowDecision
PlayerTurnPath
PlayerTurnAssistFlags
PlayerMovementDebugTrace
```

### 3.1 PlayerController

PlayerController is the scene-facing orchestrator.

It is responsible for:
- reading the intended direction from PlayerInputState
- running the movement motor at the fixed tick rate
- advancing gate visual timers once per simulation tick through Level
- updating sprite facing and render offset
- applying the gameplay position to the Godot Node2D
- updating PlayerDebugOverlay when player debug drawing is enabled
- consuming collectibles along all movement segments reported by the movement motor

PlayerController does not own the movement rules.

### 3.2 PlayerDebugOverlay

PlayerDebugOverlay draws optional player debug visuals above the playfield.

It is created and updated by PlayerController.
It is marked top-level and uses a high absolute Z index so that debug coordinates remain visible above rotating gates.

It can draw:
- the cyan gameplay anchor
- hexadecimal player debug coordinates near the player

### 3.3 PlayerInputState

PlayerInputState tracks the directional input actions:
- move_left
- move_right
- move_up
- move_down

It stores which directions are currently held and the order in which they were pressed.

Current rule:
- if several directions are held, the most recently pressed held direction wins

### 3.4 PlayerMovementMotor

PlayerMovementMotor owns the gameplay movement state.

It stores:
- current arcade-pixel position
- effective movement direction
- sprite render-offset direction
- latched requested direction
- target lane for assisted turns
- assisted-turn flags
- current assisted-turn state
- movement segments completed during the current tick

It applies one fixed simulation tick at a time.

### 3.5 PlayerTurnWindowResolver

PlayerTurnWindowResolver isolates the reverse-engineered turn-window data and selection policy.

It receives:
- current arcade-pixel position
- requested direction
- current effective direction
- previous target lane

It returns a PlayerTurnWindowDecision that tells the motor whether to use:
- normal movement
- a full assisted turn
- a close-range assist followed by normal movement

The lane tables and mirrored original-screen Y conversion are intentionally kept inside this resolver so that PlayerMovementMotor can remain higher level.

### 3.6 PlayerMovementStepResult and PlayerMovementSegment

PlayerMovementStepResult describes the outcome of one motor tick.

It includes:
- previous position
- current position
- previous direction
- current direction
- render-offset direction
- optional snapped position
- all committed one-pixel movement segments completed during the tick

PlayerMovementSegment represents one real one-pixel movement component:
- start arcade-pixel position
- end arcade-pixel position
- direction

This is important because a single assisted-turn tick can contain two movement segments:
1) one orthogonal correction pixel toward the target lane
2) one pixel in the requested direction

Collectible pickup checks use these segments so flowers are not missed during special turns.

### 3.7 PlayerMovementDebugTrace

PlayerMovementDebugTrace is an optional console trace for hard-to-debug movement cases.

It is disabled by default.
When enabled in code, it can print:
- input direction
- previous and current positions
- previous and current directions
- latched request
- target lane
- assist flags
- selected movement path
- block reason

Committed code should keep the trace disabled unless actively investigating a movement issue.

## 4. Fixed Tick Rate

Movement is updated at a fixed tick rate:

```text
TickRate = 60.1145 Hz
TickDuration = 1.0 / TickRate
```

PlayerController uses an accumulator:

```text
_accumulator += delta;
while (_accumulator >= TickDuration)
{
    _accumulator -= TickDuration;
    RunOneTick();
}
```

Purpose:
- reproduce hardware-style movement timing
- avoid frame-rate dependent movement
- keep movement logic discrete and deterministic enough for arcade-style behavior

## 5. Integer Arcade-Pixel Gameplay Position

The gameplay position is stored as integer arcade-pixel coordinates:

```text
Vector2I _arcadePixelPos;
```

This avoids floating-point drift and makes lane checks exact.

Scene-space rendering is derived from the gameplay position after simulation.
The gameplay system should not use scene-space floating-point coordinates for movement decisions.

## 6. Coordinate Spaces

The project currently uses several coordinate spaces:

```text
logical cell coordinates
arcade-pixel gameplay coordinates
Godot scene-space coordinates
gate pivot coordinates
original mirrored screen-Y coordinates used by turn-window tables
MAME-style debug coordinates
```

Level exposes the coordinate conversion API used by actors and level systems.
The conversion math is centralized in LevelCoordinateSystem:
- logical cell to arcade-pixel anchor
- arcade-pixel position to logical cell
- arcade-pixel position to scene-space position
- arcade-pixel delta to scene-space delta
- gate pivot to arcade-pixel and scene-space position

PlayerTurnWindowResolver locally handles the original mirrored screen-Y conversion used by the extracted turn-window data.
PlayerDebugOverlay formats the player debug coordinates separately for display.

## 7. Direction Model

The code distinguishes several movement-related directions.

### 7.1 Wanted direction

The wanted direction comes from PlayerInputState.
It is the direction currently intended by player input.

### 7.2 Latched requested direction

PlayerMovementMotor stores a latched requested direction.
Some direction changes first update this latch and only produce movement on a later tick.

This is important for reproducing short-tap behavior and the delayed entry into special turning.

### 7.3 Current direction

The current direction is the effective movement direction used by the motor.

Important behavior:
- the current direction is intentionally preserved when no input is held
- blocked steps do not automatically erase the current direction

This allows short successive taps to remain interpreted relative to the previous movement context.

### 7.4 Facing direction

PlayerController owns the visual facing direction.
It follows the current input direction immediately.

This allows the sprite to point toward a requested turn before the movement motor has fully accepted that turn.

### 7.5 Offset direction

The offset direction is used to choose the sprite render offset.
It follows the effective movement state rather than raw input.

This keeps the sprite visually aligned with the rail actually being used.

## 8. Basic Movement Rules

The current model uses:
- fixed ticks
- integer arcade-pixel positions
- one-pixel committed movement segments
- collision validation before every committed segment

Normal straight movement usually commits one segment:

```text
start -> start + direction
```

Assisted turns can commit two segments in one tick:

```text
start -> start + orthogonalCorrection
then
correctedPosition -> correctedPosition + requestedDirection
```

Each committed segment is stored in PlayerMovementStepResult.MovementSegments.

## 9. Turn Windows and Assisted Turns

The original arcade turn behavior is more complex than a simple “turn when centered” rule.

The current implementation uses a higher-level interpretation of the reverse-engineered behavior:

1) the player requests a direction
2) the movement motor keeps the previous movement context across short taps
3) PlayerTurnWindowResolver checks whether the requested perpendicular turn is inside a valid turn window
4) the resolver chooses a high-level turn path:
   - Normal
   - Assisted
   - CloseRangeAssistThenNormal
5) the movement motor applies the selected path while still validating every committed segment against the active playfield

### 9.1 Normal path

The normal path uses request latching and straight movement.
A newly requested direction may first be latched without movement.
On a later tick, the motor either moves straight or applies a stored one-axis alignment correction.

### 9.2 Assisted path

The assisted path is used when the requested turn is close enough to a valid turn lane.
The motor can first move one pixel toward the target lane and then move one pixel in the requested direction.

This can create the “special turn” effect visible in the arcade game.

### 9.3 Close-range assist then normal

This path is used when the actor is very close to a lane but the behavior should not enter a full assisted turn.
The motor may apply one alignment correction and then return to the ordinary path.

## 10. Wall and Gate Safety During Assisted Turns

A critical rule was added after testing:

Before committing an orthogonal correction for a requested turn, the motor checks whether the requested direction would be usable from the target turn lane.

This prevents the player from sliding sideways when the requested direction is blocked by a fixed wall.

However, if the block is caused by a pushable rotating gate, the correction is allowed.
The committed movement step will then push the gate through the normal gate-push path.

This distinction is important:
- fixed wall block: do not correct sideways
- pushable gate block: allow correction, then push gate on the committed step

## 11. Static Maze and Playfield Validation

Movement legality starts with the static maze and is then extended by dynamic gates.

Current flow:
- PlayerMovementMotor evaluates an attempted one-pixel step
- Level forwards the request to PlayfieldCollisionResolver
- PlayfieldCollisionResolver asks MazeGrid.EvaluateArcadePixelStep(...) for the static result
- if the static maze blocks the step, the final result is BlockedByFixedWall
- if the static step is allowed, PlayfieldCollisionResolver checks the runtime GateSystem overlay
- the final result is returned as PlayfieldStepResult

MazeGrid only knows about the static maze.
PlayfieldCollisionResolver owns the static maze + dynamic gate combination.

## 12. Rotating Gate Integration

LevelGateRuntime owns the runtime gate system and visual synchronization.
PlayfieldCollisionResolver reads GateSystem to evaluate gate blocking.

Current behavior:
- gates can block direct probe contact
- gates can also block cell-boundary crossing
- if a gate blocks a step and can be pushed from the contacted half:
  - PlayerMovementMotor asks Level to push it
  - Level delegates the push to LevelGateRuntime
  - the gate toggles its logical blocking state immediately
  - the same pixel step is evaluated again

This same-tick re-evaluation is necessary for the player to push a gate and move through it without requiring an extra artificial delay.

## 13. Collectible Pickup During Movement

Collectible pickup is currently prototype-level, but the movement timing is now robust.

PlayerController consumes collectibles by checking every PlayerMovementSegment reported by the movement motor.

This is necessary because an assisted turn can move along two axes in a single tick.
If PlayerController only checked the final segment, it could miss a collectible crossed during the orthogonal correction segment.

Current pickup behavior:
- if the motor reports a snapped anchor, PlayerController checks that exact anchor
- for each committed movement segment, PlayerController checks whether the segment crossed the destination cell anchor
- Level delegates removal to CollectibleFieldRuntime

Future scoring, letter, heart, and skull rules will need to replace the simple prototype removal behavior with richer pickup results.

## 14. Sprite Facing and Render Offset

The visual system intentionally separates facing from effective movement.

Current behavior:
- facing follows the input direction immediately
- sprite render offset follows the motor's offset direction
- left uses the right animation with FlipH
- down uses the up animation with FlipV

This allows the player sprite to visually respond to a turn request before the movement has fully turned.

## 15. Current Implementation Status

The current implementation includes:

- fixed tick update
- integer arcade-pixel gameplay position
- one-pixel committed movement segments
- input buffering with “last pressed wins”
- preserved movement context across short taps
- request latching
- current direction / wanted direction separation
- visual facing / render offset separation
- reverse-engineered turn-window data isolated in PlayerTurnWindowResolver
- high-level turn path selection
- assisted turns with orthogonal correction
- close-range correction path
- static maze validation for each committed segment
- dynamic rotating-gate validation through PlayfieldCollisionResolver
- immediate gate push resolution through LevelGateRuntime and same-tick step re-evaluation
- movement segment reporting for collectible pickup through CollectibleFieldRuntime
- optional movement debug tracing disabled by default
- optional player debug overlay drawn above gates

The player movement subsystem is no longer a monolithic PlayerController.
It is split into dedicated helpers and is currently one of the most advanced subsystems in the project.

## 16. Validated Behavior

The current movement behavior has been manually tested and is believed to handle:

- normal straight movement
- normal 90-degree turns
- assisted turns at intersections
- small successive taps without holding a direction continuously
- blocked wall turns without unwanted sideways sliding
- assisted-turn interactions with pushable gates
- collectible pickup during assisted turns
- same-tick gate push and movement re-evaluation
- debug coordinate drawing above rotating gates

These cases should become explicit regression scenarios before deeper movement refactoring.

## 17. Known Limitations and Open Questions

The current implementation is stable, but it is not a literal ROM-level reproduction.

Open movement-related questions include:
- exact original collision details from the ROM
- whether the current collision leads match the original checks exactly
- whether the current gate probe/boundary combination matches the original code path exactly
- whether straight-line recentering in the arcade original is identical to the current practical implementation
- exact relationship between a human keyboard tap and an arcade sub-step
- automated regression coverage for validated movement cases

The current behavior is good enough to move forward with broader gameplay systems unless future testing reveals a specific mismatch.

## 18. Design Philosophy

The movement code should stay close to the arcade behavior without becoming unreadable low-level assembly translation.

Current compromise:
- reverse-engineered data remains explicit where needed
- low-level turn-window details are isolated in PlayerTurnWindowResolver
- PlayerMovementMotor works with higher-level concepts such as request latch, assisted turn, target lane, and movement segments
- PlayerController stays focused on orchestration, rendering, debug overlay updates, and pickup checks
- Level delegates gate, coordinate, collectible, and collision details to smaller helpers

The goal is not to hide the reverse-engineering origin of the behavior.
The goal is to keep it contained and explainable.

## 19. Suggested Regression Scenarios

The following scenarios should be preserved for future tests or debug replays:

```text
straight movement in all four directions
normal right -> up turn
normal right -> down turn
normal left -> up turn
short taps toward a perpendicular direction
blocked perpendicular request against a fixed wall
assisted turn near a fixed wall that must not slide sideways
assisted turn into a pushable vertical gate
assisted turn into a pushable horizontal gate
assisted turn crossing a collectible anchor during the correction segment
assisted turn crossing a collectible anchor during the requested-direction segment
debug coordinate overlay above gates
```

A simple tick-replay harness would be enough at first.
It does not need to become a large testing framework immediately.

---

# Reverse-Engineering Reference

This section keeps the low-level findings that motivated the current implementation.
It is useful when comparing the Godot behavior against MAME/Ghidra, but the code should generally use the higher-level names described above.

## 20. Direction and State Variables

Several RAM variables were important during analysis:

- 0x6026 : effective current player direction
- 0x6027 : player X position
- 0x6028 : player Y position
- 0x605F : internal movement/special-mode state byte
- 0x6196 : runtime target X
- 0x6197 : runtime target Y
- 0x6198 : current special direction
- 0x61E0 : transition / recentering selector

Observed logical direction bits:

```text
01 = left
02 = down
04 = right
08 = up
```

Observed effective-direction writes:

```text
D=08 -> 6026 becomes 82
D=01 -> 6026 becomes 12
D=04 -> 6026 becomes 42
D=02 -> 6026 becomes 22
```

## 21. Lane Alignment Findings

Important alignment checks observed in the original movement logic include:

```text
X & 0x0F == 0x08
Y & 0x0F == 0x06
```

Historical observations also saw:

```text
Y & 0x0F == 0x07
```

Current practical remake interpretation:
- vertical movement / turning aligns around X = cell base + 8
- the gameplay anchor currently used by Level is Vector2I(8, 7)
- Y +6 appears in special-mode branching
- Y +7 remains the practical horizontal travel anchor in the current coordinate model

## 22. Special Turn Mode Findings

The original game does not appear to use a purely scripted diagonal sequence.
The behavior is interactive and branch-driven.

Important observed structure:
- input is normalized into logical direction bits
- direction commit/recommit can happen before full special-mode entry
- 605F bit 7 appears strongly linked to special interactive turn mode
- 6198 stores the current special direction
- 61E0 participates in transition/recentering behavior
- 6196 / 6197 act as runtime target coordinates during turn handling

## 23. Routine Index

All addresses below refer to the analyzed Lady Bug ROM/disassembly.
They are not used as method names in the current C# implementation.

### Core player update

- 0x35FF : main player movement / direction-handling path
- 0x380A : consumes the prepared DE pair from stack and enters the commit/transition path
- 0x3810 : clears 605F bit 7 inside the commit/transition path
- 0x381E : commit / recommit gate just before the effective direction write
- 0x382C : writes the effective direction byte to 0x6026
- 0x388C : re-enters the post-commit path and sets 605F bit 7
- 0x3891 : observed write site for 0x89 to 0x605F during entry into special mode
- 0x3A99 : player-related update path using current dir/x/y

### Input and player state

- 0x3652 : normalizes directional input into logical direction bits
- 0x6026 : current effective player direction
- 0x6027 : player X position
- 0x6028 : player Y position
- 0x605F : internal movement/special-mode state byte
- 0x6196 : runtime target X
- 0x6197 : runtime target Y
- 0x6198 : current special direction
- 0x61E0 : transition/recentering selector
- 0x9000 / 0x9001 : hardware input ports used by joystick / status logic

### Turn windows and lane alignment

- 0x36DA : loads row-based vertical turn-center mask
- 0x36F5 : scans vertical turn-center mask
- 0x377A : loads column-based horizontal turn-center mask
- 0x379D : scans horizontal turn-center mask
- 0x0DE4 : vertical turn-center table by row
- 0x0DFA : horizontal turn-center table by column
- 0x3662 : checks X & 0x0F against preferred X alignment
- 0x366C : checks Y & 0x0F against preferred Y alignment
- 0x4943 : aligned-entry path when X & 0x0F == 8
- 0x366F : aligned-entry path when Y & 0x0F == 6
- 0x494B : intermediate path when neither preferred alignment is reached
- 0x3677 : dispatcher used for the between-alignments case

### Special interactive turn mode

- 0x36C1 : simpler producer path before / outside the full special dispatcher
- 0x36C6 : clears 0x61E0 on the 0x36C1 path
- 0x366F : PUSH DE on the Y-aligned special path
- 0x3754 : writes 0x61E0 = 0x02 in observed turn-handling sequences
- 0x37EF : writes 0x61E0 = 0x01 in observed aligned follow-up sequences
- 0x388C : activates the special-mode phase after the initial commit/recommit
- 0x3891 : observed write site setting 0x605F to 0x89
- 0x4943 : PUSH DE on the X-aligned special path
- 0x4948 : clears 0x61E0 again during aligned/simplified handoff

Observed prepared direction values pushed toward 0x380A:

```text
0x0805 : up
0x0105 : left
0x0405 : right
0x0205 : down
```

### Intermediate dispatcher and kernels

- 0x494B : reloads current direction from 0x6026, shifts it, then jumps to 0x3677
- 0x3677 : central dispatcher for the intermediate between-alignments case

Confirmed dispatcher table:

```text
up    -> 0x36A1
right -> 0x36B9
down  -> 0x369A
left  -> 0x36C0
```

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

### Maze validation

- 0x390D : loads target turn position
- 0x3911 : validates requested direction against logical maze cell
- 0x0DA2 : logical maze table used for direction-open tests

### Timing and main loop links

- 0x0784..0x0888 : main gameplay loop path
- 0x1FC7 : vblank-related timing update
- 0x6059 : timing counter updated from 0x1FC7
- 0x605A : slower timing counter updated from 0x1FC7
