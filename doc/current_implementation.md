# Current Implementation

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

**Purpose of this document:**
- describe only what is actually implemented in the repository now
- provide a reliable starting point for future discussions
- avoid mixing current code with longer-term architectural ideas

This document is intentionally concrete.
It does not describe systems that are only planned.

## 1. Project Entry Point

**The Godot project currently starts from:**

- scenes/Main.tscn

**In project.godot:**
- main scene = Main.tscn
- viewport width = 746
- viewport height = 768

**Declared movement input actions:**
- move_left
- move_right
- move_up
- move_down

## 2. Current File / Folder State

**Relevant folders currently present in the repository:**

- assets/
- data/
- doc/
- scenes/
- scripts/

**Important currently used files:**

```text
scenes/
├─ Main.tscn
├─ level/
│  ├─ Level.tscn
│  └─ RotatingGate.tscn
└─ player/
   └─ Player.tscn
```

```text
scripts/
├─ Main.cs
├─ actors/
│  ├─ PlayerController.cs
│  ├─ PlayerInputState.cs
│  ├─ PlayerMovementMotor.cs
│  ├─ PlayerMovementStepResult.cs
│  └─ PlayerMovementTuning.cs
├─ gameplay/
│  ├─ maze/
│  │  ├─ WallFlags.cs
│  │  ├─ MazeCell.cs
│  │  ├─ MazeDataFile.cs
│  │  ├─ MazeGrid.cs
│  │  ├─ MazeLoader.cs
│  │  └─ MazeStepResult.cs
│  ├─ gates/
│  │  ├─ GateContactHalf.cs
│  │  ├─ GateLogicalState.cs
│  │  ├─ GateOrientation.cs
│  │  ├─ GateSystem.cs
│  │  ├─ GateTuning.cs
│  │  ├─ GateTurningVisual.cs
│  │  ├─ GateVisualState.cs
│  │  └─ RotatingGateRuntimeState.cs
│  ├─ PlayfieldStepKind.cs
│  └─ PlayfieldStepResult.cs
└─ level/
   ├─ Level.cs
   └─ RotatingGateView.cs
```

```text
data/
└─ maze.json
```

```text
doc/
├─ architecture.md
├─ current_implementation.md
├─ enemy_movement.md
├─ player_movement.md
└─ reverse_engineering.txt
```

**Important currently used visual assets:**
- assets/images/maze_background.png
- assets/sprites/player/lady_bug_spritesheet.png
- assets/sprites/props/rotating_gate.png

## 3. Current Scene Structure

### 3.1 Main scene

**Scene:**
- scenes/Main.tscn

**Current structure:**

Main (Node)
```text
└─ Level (instance of scenes/level/Level.tscn)
```

**Current script:**
- scripts/Main.cs

**Current role:**
- application entry point
- currently still minimal
- instantiates the Level scene directly
- does not yet manage screen flow or gameplay states

### 3.2 Level scene

**Scene:**
- scenes/level/Level.tscn

**Current structure:**

Level (Node2D)
```text
├─ Maze (Sprite2D)
├─ Gates (Node2D)
│  └─ 20 instances of scenes/level/RotatingGate.tscn
└─ Player (instance of scenes/player/Player.tscn)
```

**Current script:**
- scripts/level/Level.cs

**Important current properties:**
- PlayerStartCell = Vector2I(5, 8)

**Maze node:**
- type = Sprite2D
- texture = assets/images/maze_background.png
- centered = false
- offset = Vector2(16, 24)

**Gates node:**
- contains the pre-placed rotating gate instances
- each gate instance stores:
  - GateId
  - GatePivot
  - InitialOrientation

**Player node:**
- instance of scenes/player/Player.tscn

### 3.3 RotatingGate scene

**Scene:**
- scenes/level/RotatingGate.tscn

**Current structure:**

RotatingGate (Node2D)
```text
└─ AnimatedSprite2D
```

**Current script:**
- scripts/level/RotatingGateView.cs

**Current role:**
- visual gate scene used both for:
  - editor authoring inside Level.tscn
  - runtime rendering synced from GateSystem

**Current editor-authored properties:**
- GateId
- GatePivot
- InitialOrientation

**Current visual states available:**
- horizontal
- vertical
- slash
- backslash

### 3.4 Player scene

**Scene:**
- scenes/player/Player.tscn

**Current structure:**

Player (Node2D)
```text
└─ AnimatedSprite2D
```

**Current main script:**
- scripts/actors/PlayerController.cs

**Current movement helper scripts:**
- scripts/actors/PlayerInputState.cs
- scripts/actors/PlayerMovementMotor.cs
- scripts/actors/PlayerMovementStepResult.cs
- scripts/actors/PlayerMovementTuning.cs

**Current visual setup:**
- AnimatedSprite2D uses the player spritesheet
- two animations are currently defined:
  - move_right
  - move_up
- left is handled by FlipH
- down is handled by FlipV

## 4. Logical Maze System

The project already includes a logical maze system separated from the visual maze.

**Visual maze:**
- represented by the Maze Sprite2D background image in Level.tscn

**Logical maze:**
- stored in data/maze.json
- loaded at runtime by MazeLoader
- represented by MazeGrid and MazeCell

**Current maze JSON:**
- width = 11
- height = 11
- cells = flat array of wall bitmasks

**Important:**
- maze.json now describes only the static maze
- rotating gates are no longer serialized in maze.json
- rotating gates are authored directly in Level.tscn

### 4.1 WallFlags

**File:**
- scripts/gameplay/maze/WallFlags.cs

**Purpose:**
- represent walls around a logical cell with a bitmask

**Current supported flags:**
- Up
- Down
- Left
- Right

### 4.2 MazeCell

**File:**
- scripts/gameplay/maze/MazeCell.cs

**Purpose:**
- represent one logical maze cell

**Current responsibilities:**
- store wall information
- expose directional wall checks
- answer whether movement is allowed in a cardinal direction

### 4.3 MazeDataFile

**File:**
- scripts/gameplay/maze/MazeDataFile.cs

**Purpose:**
- represent the serialized JSON structure of the static maze

**Current use:**
- intermediate deserialization model between maze.json and MazeGrid

**Important:**
- this file no longer contains gate definitions

### 4.4 MazeStepResult

**File:**
- scripts/gameplay/maze/MazeStepResult.cs

**Purpose:**
- represent the result of evaluating one attempted arcade-pixel movement step
  against the logical static maze

**Current use:**
- returned by MazeGrid.EvaluateArcadePixelStep(...)
- wrapped later into PlayfieldStepResult once dynamic gates are considered

### 4.5 MazeGrid

**File:**
- scripts/gameplay/maze/MazeGrid.cs

**Purpose:**
- runtime logical maze representation

**Current responsibilities:**
- store the 2D logical cell grid
- validate maze bounds
- return cells by logical position
- determine whether movement is allowed from one logical cell to another
- evaluate one attempted arcade-pixel step through EvaluateArcadePixelStep(...)

**Important:**
- CanMove() blocks movement outside maze bounds
- EvaluateArcadePixelStep(...) is the current bridge between pixel movement
  and static maze legality only

### 4.6 MazeLoader

**File:**
- scripts/gameplay/maze/MazeLoader.cs

**Purpose:**
- load the logical maze from JSON

**Current behavior:**
- reads res://data/maze.json
- deserializes MazeDataFile
- builds MazeGrid

## 5. Rotating Gate System

Rotating gates are now implemented as a dynamic gameplay system.

**Current structure:**
- gate views are authored in Level.tscn
- runtime gate state is built from those placed views
- gate logic is kept separate from the static maze

**Current gate-related files:**
- scripts/gameplay/gates/GateContactHalf.cs
- scripts/gameplay/gates/GateLogicalState.cs
- scripts/gameplay/gates/GateOrientation.cs
- scripts/gameplay/gates/GateSystem.cs
- scripts/gameplay/gates/GateTuning.cs
- scripts/gameplay/gates/GateTurningVisual.cs
- scripts/gameplay/gates/GateVisualState.cs
- scripts/gameplay/gates/RotatingGateRuntimeState.cs
- scripts/level/RotatingGateView.cs

### 5.1 Gate authoring model

**Current model:**
- 20 gate instances are already present under Level/Gates
- each gate stores its authored definition directly in the scene:
  - GateId
  - GatePivot
  - InitialOrientation

**Purpose:**
- make gates visible and editable directly in the Godot editor
- avoid storing immutable gate placement in maze.json
- keep runtime logic separate from editor authoring

### 5.2 Gate runtime model

**Runtime source of truth:**
- GateSystem

**Current responsibilities:**
- own all runtime gate states
- lookup gates by id
- lookup gates by pivot
- accept push attempts
- lock gates during turning
- advance short turning timers

**Current runtime gate state includes:**
- logical blocking axis
- current visual state
- turning diagonal visual
- rotation lock
- remaining turning ticks

### 5.3 Gate rendering

**Current rendering behavior:**
- Level keeps references to the placed RotatingGateView instances
- runtime gate state is synchronized back to those views
- stable state shows horizontal or vertical
- short turning state shows slash or backslash

## 6. Level Runtime Logic

**File:**
- scripts/level/Level.cs

Level.cs is currently the runtime coordinator for the prototype level.

**Current responsibilities:**
- load the static logical maze from res://data/maze.json
- scan the Gates node and build the runtime GateSystem from placed views
- expose the runtime MazeGrid through a property
- expose the runtime GateSystem through a property
- convert logical cells into gameplay arcade-pixel anchors
- convert gate pivots into arcade-pixel and scene-space positions
- convert arcade-pixel positions into logical cells
- convert arcade-pixel positions and deltas into scene-space positions
- combine static maze legality and dynamic gate legality into PlayfieldStepResult
- initialize the player after the maze and gate system have been prepared
- update player and gate previews in the editor

**Important implementation details:**
- Level uses [Tool]
- logical cell size is currently 16 arcade pixels
- render scale is currently 4
- gameplay anchor inside a logical cell is currently Vector2I(8, 7)

**Important design point:**
- Level is the source of truth for coordinate conversion between:
  - logical cells
  - arcade-pixel gameplay coordinates
  - gate pivots
  - scene-space coordinates

## 7. Player Movement System

The current player movement system is no longer the old smooth cell-to-cell
prototype.

It is now a more faithful arcade-oriented movement model built around
small dedicated helper classes.

**Current player-related files:**
- PlayerController.cs
- PlayerInputState.cs
- PlayerMovementMotor.cs
- PlayerMovementStepResult.cs
- PlayerMovementTuning.cs

**Additional movement-related integration:**
- PlayfieldStepKind.cs
- PlayfieldStepResult.cs

### 7.1 PlayerController

**File:**
- scripts/actors/PlayerController.cs

**Current role:**
- orchestrate input, movement ticks and rendering
- receive the Level reference
- forward input events to PlayerInputState
- advance PlayerMovementMotor at fixed tick rate
- advance gate timers once per simulation tick
- update facing animation
- apply gameplay position and render offset to the scene node

**Important:**
- PlayerController is intentionally much lighter than before
- gameplay movement rules are no longer implemented directly in this class

### 7.2 PlayerInputState

**File:**
- scripts/actors/PlayerInputState.cs

**Purpose:**
- track held movement directions
- track the relative order in which they were pressed
- resolve the currently intended direction

**Current rule:**
- if several movement keys are held, the last pressed one wins

### 7.3 PlayerMovementTuning

**File:**
- scripts/actors/PlayerMovementTuning.cs

**Purpose:**
- centralize stable movement calibration values

**Current contents:**
- fixed tick timing constants
- rail snap tolerances
- sprite render offsets
- directional collision probe distances

### 7.4 PlayerMovementStepResult

**File:**
- scripts/actors/PlayerMovementStepResult.cs

**Purpose:**
- represent the outcome of one movement-motor tick

**Current use:**
- returned by PlayerMovementMotor.Step(...)
- currently available for future hooks such as animation, sound or gameplay
  reactions

### 7.5 PlayerMovementMotor

**File:**
- scripts/actors/PlayerMovementMotor.cs

**Purpose:**
- own the gameplay movement state
- apply arcade-style movement rules one tick at a time

**Current responsibilities:**
- store the gameplay arcade-pixel position
- store the effective movement direction
- store the render-offset direction
- stop immediately when no input is held
- resume movement only when the requested lane can be used
- apply rail snap
- validate each attempted movement step against the active playfield
- move exactly one pixel per valid tick
- apply conservative straight-line recentering
- resolve gate pushes and same-tick re-evaluation
- return a structured tick result

**Important current behavior:**
- movement runs at fixed tick rate
- gameplay position is integer arcade-pixel based
- movement speed is currently 1 pixel per tick
- buffered direction changes are supported
- perpendicular blocked direction requests stop the actor
- sprite facing and sprite render offset are intentionally separated

## 8. Playfield Step Evaluation

Dynamic movement legality is now evaluated beyond the static maze.

**Current flow:**
- PlayerMovementMotor requests one attempted step
- Level asks MazeGrid for the static result
- Level then overlays dynamic rotating-gate checks
- the final result is wrapped in PlayfieldStepResult

**Current possible outcomes:**
- Allowed
- BlockedByFixedWall
- BlockedByGate

**Current gate-specific behavior:**
- a gate can block from direct probe contact even without immediate logical-cell change
- a gate can also block when crossing a boundary into another logical cell
- if a blocked gate can be pushed from the contacted half:
  - the logical gate state toggles immediately
  - the attempted step is re-evaluated in the same tick

## 9. Current Player Movement Behavior

**The current player movement model includes:**

- fixed tick update
- integer arcade-pixel gameplay position
- 1 pixel movement per tick
- buffered input using "last pressed wins"
- explicit current / wanted / facing / offset directions
- lane alignment inside 16x16 logical cells
- rail snap and conservative recentering
- static maze validation for each attempted pixel step
- dynamic rotating-gate validation layered on top of the static maze
- same-tick gate push resolution and re-evaluation
- short gate turning visuals

**Important:**
- the player no longer moves cell to cell
- the player no longer interpolates toward a target scene position
- movement is now driven by discrete gameplay ticks

## 10. What Is Currently Working

**The following is already implemented and functional:**

- Main scene launches correctly
- Level scene is instantiated from Main
- maze background is displayed
- pre-placed rotating gates are displayed
- player is displayed
- player start position is defined through Level.PlayerStartCell
- player preview updates in the editor
- rotating gate preview updates in the editor
- logical static maze is loaded from JSON
- logical cell walls are interpreted correctly
- movement is validated against the static maze
- movement outside the maze bounds is blocked
- rotating gates influence movement legality
- rotating gates can be pushed
- gate logical state toggles immediately on successful push
- gate turning visuals are shown briefly
- player movement runs with a fixed tick model
- player movement runs pixel by pixel in gameplay coordinates
- buffered multi-key input works
- lane snap and conservative recentering work
- movement architecture is refactored into dedicated helper classes

## 11. What Is Not Implemented Yet

**The following systems are still not implemented yet:**

- enemies
- flowers / hearts / letters
- bonus vegetables
- HUD
- score system
- lives system
- title screen flow
- gameplay / game over / high score screen flow
- session state management

## 12. Current Limitations

The movement and gate systems are much more faithful than before,
but they are not yet the final verified arcade reproduction.

**Open points include:**
- exact original turn-window details
- exact original collision details from the ROM
- exact interpretation of all lane-alignment checks
- exact gate/turn interaction windows in edge cases
- enemy movement system
- later gameplay-specific reactions using PlayerMovementStepResult

## 13. Current Development Priority

**A reasonable current priority is now:**

1) keep the current movement and gate systems stable
2) refine turn-window / alignment fidelity through reverse engineering
3) implement enemies and remaining gameplay systems
4) continue refining arcade fidelity where reverse engineering justifies it
