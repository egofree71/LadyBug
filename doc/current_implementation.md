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

scenes/
├─ Main.tscn
├─ level/
│  └─ Level.tscn
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
│  └─ maze/
│     ├─ WallFlags.cs
│     ├─ MazeCell.cs
│     ├─ MazeDataFile.cs
│     ├─ MazeGrid.cs
│     ├─ MazeLoader.cs
│     └─ MazeStepResult.cs
└─ level/
   └─ Level.cs

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

Player node:
- instance of scenes/player/Player.tscn

-------------------------------------------------------------------------------
3.3 Player scene
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
- intermediate deserialization model between maze.json and MazeGrid

-------------------------------------------------------------------------------
4.4 MazeStepResult
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeStepResult.cs

Purpose:
- represent the result of evaluating one attempted arcade-pixel movement step
  against the logical maze

Current use:
- returned by MazeGrid.EvaluateArcadePixelStep(...)
- used by PlayerMovementMotor

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
- EvaluateArcadePixelStep(...) is the current bridge between pixel movement
  and logical maze legality

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
5. LEVEL RUNTIME LOGIC
===============================================================================

File:
- scripts/level/Level.cs

Level.cs is currently the runtime coordinator for the prototype level.

Current responsibilities:
- load the logical maze from res://data/maze.json
- expose the runtime MazeGrid through a property
- reposition the player from PlayerStartCell
- convert logical cells into gameplay arcade-pixel anchors
- convert arcade-pixel positions into logical cells
- convert arcade-pixel positions and deltas into scene-space positions
- initialize the player after the maze has been loaded
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

===============================================================================
6. PLAYER MOVEMENT SYSTEM
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
6.1 PlayerController
-------------------------------------------------------------------------------

File:
- scripts/actors/PlayerController.cs

Current role:
- orchestrate input, movement ticks and rendering
- receive the Level reference
- forward input events to PlayerInputState
- advance PlayerMovementMotor at fixed tick rate
- update facing animation
- apply gameplay position and render offset to the scene node

Important:
- PlayerController is intentionally much lighter than before
- gameplay movement rules are no longer implemented directly in this class

-------------------------------------------------------------------------------
6.2 PlayerInputState
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
6.3 PlayerMovementTuning
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
6.4 PlayerMovementStepResult
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
6.5 PlayerMovementMotor
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
- validate each attempted movement step against the maze
- move exactly one pixel per valid tick
- apply conservative straight-line recentering
- return a structured tick result

Important current behavior:
- movement runs at fixed tick rate
- gameplay position is integer arcade-pixel based
- movement speed is currently 1 pixel per tick
- buffered direction changes are supported
- perpendicular blocked direction requests stop the actor
- sprite facing and sprite render offset are intentionally separated

===============================================================================
7. CURRENT PLAYER MOVEMENT BEHAVIOR
===============================================================================

The current player movement model includes:

- fixed tick update
- integer arcade-pixel gameplay position
- 1 pixel movement per tick
- buffered input using "last pressed wins"
- explicit current / wanted / facing / offset directions
- lane alignment inside 16x16 logical cells
- rail snap when starting or resuming movement
- conservative straight-line recentering
- maze validation for each attempted pixel step
- separation between gameplay anchor and visual sprite offset

Important:
- the player no longer moves cell to cell
- the player no longer interpolates toward a target scene position
- movement is now driven by discrete gameplay ticks

===============================================================================
8. WHAT IS CURRENTLY WORKING
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
- player movement runs with a fixed tick model
- player movement runs pixel by pixel in gameplay coordinates
- buffered multi-key input works
- lane snap and conservative recentering work
- movement architecture is refactored into dedicated helper classes

===============================================================================
9. WHAT IS NOT IMPLEMENTED YET
===============================================================================

The following systems are still not implemented yet:

- enemies
- rotating gates
- flowers / hearts / letters
- bonus vegetables
- HUD
- score system
- lives system
- title screen flow
- gameplay / game over / high score screen flow
- session state management
- final rotating-gate interaction in player movement

===============================================================================
10. CURRENT LIMITATIONS
===============================================================================

The movement system is much more faithful than before, but it is not yet the
final verified arcade reproduction.

Open points include:
- exact original turn-window details
- exact original collision details from the ROM
- exact interpretation of all lane-alignment checks
- rotating gate interaction
- enemy movement system
- later gameplay-specific reactions using PlayerMovementStepResult

===============================================================================
11. CURRENT DEVELOPMENT PRIORITY
===============================================================================

A reasonable current priority is now:

1) keep the current movement system stable
2) integrate rotating gates into the movement logic
3) implement enemies and remaining gameplay systems
4) continue refining arcade fidelity where reverse engineering justifies it
