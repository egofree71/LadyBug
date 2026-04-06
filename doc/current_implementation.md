===============================================================================
CURRENT IMPLEMENTATION
===============================================================================

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

Purpose of this document:
- describe only what is actually implemented in the repository now
- provide a reliable starting point for future discussions
- avoid mixing current code with longer-term architectural ideas

This document is intentionally concrete.
It does not describe systems that are only planned.

===============================================================================
1. PROJECT ENTRY POINT
===============================================================================

The Godot project currently starts from:

- scenes/Main.tscn

In project.godot:
- main scene = Main.tscn
- viewport width = 746
- viewport height = 768

Declared movement input actions:
- move_left
- move_right
- move_up
- move_down

===============================================================================
2. CURRENT FILE / FOLDER STATE
===============================================================================

Relevant folders currently present in the repository:

- assets/
- data/
- doc/
- scenes/
- scripts/

Important currently used files:

assets/
├─ images/
│  └─ maze_background.png
└─ sprites/
   ├─ player/
   │  └─ lady_bug_spritesheet.png
   └─ props/
	  └─ rotating_gate.png

scenes/
├─ Main.tscn
├─ level/
│  ├─ Level.tscn
│  └─ RotatingGate.tscn
└─ player/
   └─ Player.tscn

scripts/
├─ Main.cs
├─ actors/
│  ├─ PlayerController.cs
│  ├─ PlayerInputState.cs
│  ├─ PlayerMovementMotor.cs
│  ├─ PlayerMovementStepResult.cs
│  └─ PlayerMovementTuning.cs
├─ gameplay/
│  ├─ PlayfieldStepKind.cs
│  ├─ PlayfieldStepResult.cs
│  ├─ gates/
│  │  ├─ GateContactHalf.cs
│  │  ├─ GateLogicalState.cs
│  │  ├─ GateOrientation.cs
│  │  ├─ GateSystem.cs
│  │  ├─ GateTuning.cs
│  │  ├─ GateTurningVisual.cs
│  │  ├─ GateVisualState.cs
│  │  ├─ PivotDataFile.cs
│  │  ├─ RotatingGateDataFile.cs
│  │  └─ RotatingGateRuntimeState.cs
│  └─ maze/
│     ├─ WallFlags.cs
│     ├─ MazeCell.cs
│     ├─ MazeDataFile.cs
│     ├─ MazeGrid.cs
│     ├─ MazeLoader.cs
│     └─ MazeStepResult.cs
└─ level/
   ├─ Level.cs
   └─ RotatingGateView.cs

data/
└─ maze.json

doc/
├─ architecture.md
├─ current_implementation.md
└─ player_movement.md

===============================================================================
3. CURRENT SCENE STRUCTURE
===============================================================================

-------------------------------------------------------------------------------
3.1 Main scene
-------------------------------------------------------------------------------

Scene:
- scenes/Main.tscn

Current structure:

Main (Node)
└─ Level (instance of scenes/level/Level.tscn)

Current script:
- scripts/Main.cs

Current role:
- application entry point
- currently still minimal
- instantiates the Level scene directly
- does not yet manage screen flow or gameplay states

-------------------------------------------------------------------------------
3.2 Level scene
-------------------------------------------------------------------------------

Scene:
- scenes/level/Level.tscn

Current structure:

Level (Node2D)
├─ Maze (Sprite2D)
├─ Gates (Node2D)
└─ Player (instance of scenes/player/Player.tscn)

Current script:
- scripts/level/Level.cs

Important current properties:
- PlayerStartCell = Vector2I(5, 8)

Maze node:
- type = Sprite2D
- texture = assets/images/maze_background.png
- centered = false
- offset = Vector2(16, 24)

Gates node:
- type = Node2D
- owns rotating-gate scene instances at runtime

Player node:
- instance of scenes/player/Player.tscn

-------------------------------------------------------------------------------
3.3 RotatingGate scene
-------------------------------------------------------------------------------

Scene:
- scenes/level/RotatingGate.tscn

Current structure:

RotatingGate (Node2D)
└─ AnimatedSprite2D

Current script:
- scripts/level/RotatingGateView.cs

Current visual setup:
- uses assets/sprites/props/rotating_gate.png
- supports four visuals:
  - horizontal
  - vertical
  - slash
  - backslash
- the root node represents the logical pivot
- the sprite transform is intended to stay centered on that pivot

-------------------------------------------------------------------------------
3.4 Player scene
-------------------------------------------------------------------------------

Scene:
- scenes/player/Player.tscn

Current structure:

Player (Node2D)
└─ AnimatedSprite2D

Current main script:
- scripts/actors/PlayerController.cs

Current movement helper scripts:
- scripts/actors/PlayerInputState.cs
- scripts/actors/PlayerMovementMotor.cs
- scripts/actors/PlayerMovementStepResult.cs
- scripts/actors/PlayerMovementTuning.cs

Current visual setup:
- AnimatedSprite2D uses the player spritesheet
- two animations are currently defined:
  - move_right
  - move_up
- left is handled by FlipH
- down is handled by FlipV

===============================================================================
4. LOGICAL MAZE SYSTEM
===============================================================================

The project already includes a logical maze system separated from the visual maze.

Visual maze:
- represented by the Maze Sprite2D background image in Level.tscn

Logical maze:
- stored in data/maze.json
- loaded at runtime by MazeLoader
- represented by MazeGrid and MazeCell

Current maze JSON:
- width = 11
- height = 11
- cells = flat array of wall bitmasks
- gates = rotating-gate definitions with pivot coordinates and initial orientation

-------------------------------------------------------------------------------
4.1 WallFlags
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/WallFlags.cs

Purpose:
- represent walls around a logical cell with a bitmask

Current supported flags:
- Up
- Down
- Left
- Right

-------------------------------------------------------------------------------
4.2 MazeCell
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeCell.cs

Purpose:
- represent one logical maze cell

Current responsibilities:
- store wall information
- expose directional wall checks
- answer whether movement is allowed in a cardinal direction

-------------------------------------------------------------------------------
4.3 MazeDataFile
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeDataFile.cs

Purpose:
- represent the serialized JSON structure

Current use:
- intermediate deserialization model between maze.json and MazeGrid / GateSystem

Current serialized contents:
- width
- height
- cells
- gates

-------------------------------------------------------------------------------
4.4 MazeStepResult
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeStepResult.cs

Purpose:
- represent the result of evaluating one attempted arcade-pixel movement step
  against the logical static maze

Current use:
- returned by MazeGrid.EvaluateArcadePixelStep(...)
- wrapped later into PlayfieldStepResult when gate logic is applied

-------------------------------------------------------------------------------
4.5 MazeGrid
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeGrid.cs

Purpose:
- runtime logical maze representation

Current responsibilities:
- store the 2D logical cell grid
- validate maze bounds
- return cells by logical position
- determine whether movement is allowed from one logical cell to another
- evaluate one attempted arcade-pixel step through EvaluateArcadePixelStep(...)

Important:
- CanMove() blocks movement outside maze bounds
- EvaluateArcadePixelStep(...) remains the bridge between pixel movement
  and static maze legality
- rotating gates are not stored inside MazeGrid itself

-------------------------------------------------------------------------------
4.6 MazeLoader
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeLoader.cs

Purpose:
- load the logical maze from JSON

Current behavior:
- reads res://data/maze.json
- deserializes MazeDataFile
- builds MazeGrid

===============================================================================
5. ROTATING GATE SYSTEM
===============================================================================

Rotating gates are now implemented as a runtime gameplay system,
separate from the static maze.

Current core files:
- PlayfieldStepKind.cs
- PlayfieldStepResult.cs
- GateSystem.cs
- GateLogicalState.cs
- GateVisualState.cs
- GateTurningVisual.cs
- GateContactHalf.cs
- GateTuning.cs
- PivotDataFile.cs
- RotatingGateDataFile.cs
- RotatingGateRuntimeState.cs
- RotatingGateView.cs

-------------------------------------------------------------------------------
5.1 Gate data model
-------------------------------------------------------------------------------

Files:
- scripts/gameplay/gates/PivotDataFile.cs
- scripts/gameplay/gates/RotatingGateDataFile.cs

Purpose:
- deserialize rotating-gate entries from maze.json

Current serialized information:
- gate id
- pivot coordinates
- initial stable orientation

-------------------------------------------------------------------------------
5.2 Gate runtime state
-------------------------------------------------------------------------------

File:
- scripts/gameplay/gates/RotatingGateRuntimeState.cs

Purpose:
- represent one gate during active gameplay

Current runtime state includes:
- logical blocking state
- current visual state
- current turning visual
- rotating / lock state
- remaining turning ticks

Important current behavior:
- logical state toggles immediately when a push is accepted
- the gate then remains briefly in Turning state
- the gate is locked against immediate re-entry while rotating

-------------------------------------------------------------------------------
5.3 GateSystem
-------------------------------------------------------------------------------

File:
- scripts/gameplay/gates/GateSystem.cs

Purpose:
- own all runtime gates of the active level

Current responsibilities:
- build runtime gate state from maze JSON data
- expose runtime gates by id and pivot
- attempt pushes
- advance turning timers one simulation tick at a time

-------------------------------------------------------------------------------
5.4 PlayfieldStepResult
-------------------------------------------------------------------------------

File:
- scripts/gameplay/PlayfieldStepResult.cs

Purpose:
- represent the combined step result after applying:
  - static maze legality
  - dynamic gate legality

Current result kinds:
- Allowed
- BlockedByFixedWall
- BlockedByGate

Current additional gate data:
- gate id when relevant
- contacted gate half when relevant
- null contact half when the probe hits the pivot dead zone

-------------------------------------------------------------------------------
5.5 Gate rendering
-------------------------------------------------------------------------------

File:
- scripts/level/RotatingGateView.cs

Purpose:
- display the current stable or turning visual of one gate scene instance

Current behavior:
- show horizontal or vertical stable state
- show slash or backslash during Turning
- remain purely visual
- not own gameplay legality

===============================================================================
6. LEVEL RUNTIME LOGIC
===============================================================================

File:
- scripts/level/Level.cs

Level.cs is currently the runtime coordinator for the prototype level.

Current responsibilities:
- load the logical maze from res://data/maze.json
- build and expose the runtime GateSystem
- spawn and synchronize rotating-gate views
- expose the runtime MazeGrid through a property
- expose the runtime GateSystem through a property
- reposition the player from PlayerStartCell
- convert logical cells into gameplay arcade-pixel anchors
- convert arcade-pixel positions into logical cells
- convert arcade-pixel positions and deltas into scene-space positions
- evaluate one attempted playfield step using:
  - static maze legality
  - dynamic gate legality
- attempt gate pushes
- initialize the player after the level has been loaded
- update the player preview in the editor when PlayerStartCell changes

Important implementation details:
- Level uses [Tool]
- logical cell size is currently 16 arcade pixels
- render scale is currently 4
- gameplay anchor inside a logical cell is currently Vector2I(8, 7)

Important design point:
- Level is the source of truth for coordinate conversion between:
  - logical cells
  - arcade-pixel gameplay coordinates
  - scene-space coordinates
- Level also acts as the runtime integration point between MazeGrid,
  GateSystem and gate scene instances

===============================================================================
7. PLAYER MOVEMENT SYSTEM
===============================================================================

The current player movement system is no longer the old smooth cell-to-cell
prototype.

It is now a more faithful arcade-oriented movement model built around
small dedicated helper classes.

Current player-related files:
- PlayerController.cs
- PlayerInputState.cs
- PlayerMovementMotor.cs
- PlayerMovementStepResult.cs
- PlayerMovementTuning.cs

-------------------------------------------------------------------------------
7.1 PlayerController
-------------------------------------------------------------------------------

File:
- scripts/actors/PlayerController.cs

Current role:
- orchestrate input, movement ticks and rendering
- receive the Level reference
- forward input events to PlayerInputState
- advance PlayerMovementMotor at fixed tick rate
- advance rotating-gate timers once per simulation tick through Level
- update facing animation
- apply gameplay position and render offset to the scene node

Important:
- PlayerController is intentionally much lighter than before
- gameplay movement rules are no longer implemented directly in this class

-------------------------------------------------------------------------------
7.2 PlayerInputState
-------------------------------------------------------------------------------

File:
- scripts/actors/PlayerInputState.cs

Purpose:
- track held movement directions
- track the relative press order of directions
- resolve the currently intended direction

Current rule:
- if several movement keys are held, the last pressed one wins

-------------------------------------------------------------------------------
7.3 PlayerMovementTuning
-------------------------------------------------------------------------------

File:
- scripts/actors/PlayerMovementTuning.cs

Purpose:
- centralize stable movement calibration values

Current contents:
- fixed tick timing constants
- rail snap tolerances
- sprite render offsets
- directional collision probe distances

-------------------------------------------------------------------------------
7.4 PlayerMovementStepResult
-------------------------------------------------------------------------------

File:
- scripts/actors/PlayerMovementStepResult.cs

Purpose:
- represent the outcome of one movement-motor tick

Current use:
- returned by PlayerMovementMotor.Step(...)
- currently available for future hooks such as animation, sound or gameplay
  reactions

-------------------------------------------------------------------------------
7.5 PlayerMovementMotor
-------------------------------------------------------------------------------

File:
- scripts/actors/PlayerMovementMotor.cs

Purpose:
- own the gameplay movement state
- apply arcade-style movement rules one tick at a time

Current responsibilities:
- store the gameplay arcade-pixel position
- store the effective movement direction
- store the render-offset direction
- stop immediately when no input is held
- resume movement only when the requested lane can be used
- apply rail snap
- validate each attempted movement step against the active playfield
- react to BlockedByGate by attempting a push and re-evaluating the same step
- move exactly one pixel per valid tick
- apply conservative straight-line recentering
- return a structured tick result

Important current behavior:
- movement runs at fixed tick rate
- gameplay position is integer arcade-pixel based
- movement speed is currently 1 pixel per tick
- buffered direction changes are supported
- perpendicular blocked direction requests stop the actor
- gate pushes are integrated directly into the movement step validation loop
- sprite facing and sprite render offset are intentionally separated

===============================================================================
8. CURRENT PLAYER + GATE MOVEMENT BEHAVIOR
===============================================================================

The current movement model includes:

- fixed tick update
- integer arcade-pixel gameplay position
- 1 pixel movement per tick
- buffered input using "last pressed wins"
- explicit current / wanted / facing / offset directions
- lane alignment inside 16x16 logical cells
- rail snap when starting or resuming movement
- conservative straight-line recentering
- maze validation for each attempted pixel step
- dynamic rotating-gate validation for each attempted pixel step
- immediate logical gate toggle when a push is accepted
- immediate step re-evaluation after an accepted gate push
- short Turning visual during rotation
- separation between gameplay anchor and visual sprite offset

Important:
- the player no longer moves cell to cell
- the player no longer interpolates toward a target scene position
- movement is now driven by discrete gameplay ticks
- gate interaction is no longer visual-only; it directly changes movement legality

===============================================================================
9. WHAT IS CURRENTLY WORKING
===============================================================================

The following is already implemented and functional:

- Main scene launches correctly
- Level scene is instantiated from Main
- maze background is displayed
- player is displayed
- player start position is defined through Level.PlayerStartCell
- player preview updates in the editor
- logical maze is loaded from JSON
- logical cell walls are interpreted correctly
- movement is validated against the logical maze
- movement outside the maze bounds is blocked
- rotating-gate data is loaded from maze.json
- rotating gates are instantiated visually at runtime
- rotating gates have stable and turning visuals
- rotating gates influence movement legality
- player movement can push rotating gates
- accepted gate pushes toggle gate logic immediately
- accepted gate pushes re-evaluate the same step immediately
- gate turning is locked briefly to prevent immediate re-entry
- player movement runs with a fixed tick model
- player movement runs pixel by pixel in gameplay coordinates
- buffered multi-key input works
- lane snap and conservative recentering work
- movement architecture is refactored into dedicated helper classes

===============================================================================
10. WHAT IS NOT IMPLEMENTED YET
===============================================================================

The following systems are still not implemented yet:

- enemies
- flowers / hearts / letters
- bonus vegetables
- HUD
- score system
- lives system
- title screen flow
- gameplay / game over / high score screen flow
- session state management

===============================================================================
11. CURRENT LIMITATIONS
===============================================================================

The movement system is much more faithful than before, but it is not yet the
final verified arcade reproduction.

Open points include:
- exact original turn-window details
- exact original collision details from the ROM
- exact interpretation of all lane-alignment checks
- exact acceptance window for perpendicular turns
- remaining fine-grained interaction details between turn timing and gate pushes
- enemy movement system
- later gameplay-specific reactions using PlayerMovementStepResult

===============================================================================
12. CURRENT DEVELOPMENT PRIORITY
===============================================================================

A reasonable current priority is now:

1) keep the current movement + gate system stable
2) refine turn-window / alignment fidelity using reverse engineering
3) implement enemies and remaining gameplay systems
4) continue refining arcade fidelity where reverse engineering justifies it
