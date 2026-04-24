# Current Implementation

**Project:** Lady Bug remake in Godot 4.6.1 (.NET) with C#

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
│  ├─ Collectible.tscn
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
│  ├─ PlayerDebugOverlay.cs
│  ├─ PlayerInputState.cs
│  ├─ PlayerMovementDebugTrace.cs
│  ├─ PlayerMovementMotor.cs
│  ├─ PlayerMovementSegment.cs
│  ├─ PlayerMovementStepResult.cs
│  ├─ PlayerMovementTuning.cs
│  ├─ PlayerTurnAssistFlags.cs
│  ├─ PlayerTurnPath.cs
│  ├─ PlayerTurnWindowDecision.cs
│  └─ PlayerTurnWindowResolver.cs
├─ gameplay/
│  ├─ collectibles/
│  │  ├─ CollectibleAnchorFamilies.cs
│  │  ├─ CollectibleAnchorFamily.cs
│  │  ├─ CollectibleColor.cs
│  │  ├─ CollectibleKind.cs
│  │  ├─ CollectibleLayoutFile.cs
│  │  ├─ CollectibleLoader.cs
│  │  ├─ CollectiblePlacement.cs
│  │  ├─ CollectibleSpawnPlan.cs
│  │  ├─ CollectibleSpawnPlanner.cs
│  │  └─ LetterKind.cs
│  ├─ gates/
│  │  ├─ GateContactHalf.cs
│  │  ├─ GateLogicalState.cs
│  │  ├─ GateOrientation.cs
│  │  ├─ GateSystem.cs
│  │  ├─ GateTuning.cs
│  │  ├─ GateTurningVisual.cs
│  │  ├─ GateVisualState.cs
│  │  └─ RotatingGateRuntimeState.cs
│  ├─ maze/
│  │  ├─ WallFlags.cs
│  │  ├─ MazeCell.cs
│  │  ├─ MazeDataFile.cs
│  │  ├─ MazeGrid.cs
│  │  ├─ MazeLoader.cs
│  │  └─ MazeStepResult.cs
│  ├─ PlayfieldCollisionResolver.cs
│  ├─ PlayfieldStepKind.cs
│  └─ PlayfieldStepResult.cs
└─ level/
   ├─ Collectible.cs
   ├─ CollectibleFieldRuntime.cs
   ├─ Level.cs
   ├─ LevelCoordinateSystem.cs
   ├─ LevelGateRuntime.cs
   └─ RotatingGateView.cs
```

```text
data/
├─ collectibles_layout.json
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
- assets/sprites/props/collectibles.png
- assets/sprites/props/rotating_gate.png

## 3. Current Scene Structure

### 3.1 Main scene

**Scene:**
- scenes/Main.tscn

**Current structure:**

```text
Main (Node)
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

```text
Level (Node2D)
├─ Maze (Sprite2D)
├─ Collectibles (Node2D)
├─ Gates (Node2D)
│  └─ 20 instances of scenes/level/RotatingGate.tscn
└─ Player (instance of scenes/player/Player.tscn)
```

**Current main script:**
- scripts/level/Level.cs

**Important current properties:**
- PlayerStartCell = Vector2I(5, 8)

**Maze node:**
- type = Sprite2D
- texture = assets/images/maze_background.png
- centered = false
- offset = Vector2(16, 24)

**Collectibles node:**
- runtime parent for spawned collectible instances
- owned at runtime by CollectibleFieldRuntime

**Gates node:**
- contains the pre-placed rotating gate instances
- each gate instance stores:
  - GateId
  - GatePivot
  - InitialOrientation
- owned at runtime by LevelGateRuntime

**Player node:**
- instance of scenes/player/Player.tscn

### 3.3 Collectible scene

**Scene:**
- scenes/level/Collectible.tscn

**Current structure:**

```text
Collectible (Node2D)
├─ MainSprite (Sprite2D)
└─ OverlaySprite (Sprite2D)
```

**Current script:**
- scripts/level/Collectible.cs

**Current role:**
- visual collectible scene used at runtime for:
  - flowers
  - hearts
  - letters
  - skulls

**Current visual model:**
- one shared spritesheet is used for collectible graphics
- flowers and skulls use one visible sprite layer
- hearts use two visible sprite layers:
  - a tinted outer ring
  - a fixed inner heart overlay
- letters currently use one tinted sprite layer

**Current temporary visual state used by the prototype:**
- flowers use their static flower frame
- hearts are currently displayed in red
- letters are currently displayed in red
- skulls are currently displayed in white
- collectible color cycling is not implemented yet

### 3.4 RotatingGate scene

**Scene:**
- scenes/level/RotatingGate.tscn

**Current structure:**

```text
RotatingGate (Node2D)
└─ AnimatedSprite2D
```

**Current script:**
- scripts/level/RotatingGateView.cs

**Current role:**
- visual gate scene used both for:
  - editor authoring inside Level.tscn
  - runtime rendering synced from LevelGateRuntime / GateSystem

**Current editor-authored properties:**
- GateId
- GatePivot
- InitialOrientation

**Current visual states available:**
- horizontal
- vertical
- slash
- backslash

### 3.5 Player scene

**Scene:**
- scenes/player/Player.tscn

**Current structure:**

```text
Player (Node2D)
└─ AnimatedSprite2D
```

**Current main script:**
- scripts/actors/PlayerController.cs

**Current movement and debug helper scripts:**
- scripts/actors/PlayerDebugOverlay.cs
- scripts/actors/PlayerInputState.cs
- scripts/actors/PlayerMovementDebugTrace.cs
- scripts/actors/PlayerMovementMotor.cs
- scripts/actors/PlayerMovementSegment.cs
- scripts/actors/PlayerMovementStepResult.cs
- scripts/actors/PlayerMovementTuning.cs
- scripts/actors/PlayerTurnAssistFlags.cs
- scripts/actors/PlayerTurnPath.cs
- scripts/actors/PlayerTurnWindowDecision.cs
- scripts/actors/PlayerTurnWindowResolver.cs

**Current visual setup:**
- AnimatedSprite2D uses the player spritesheet
- two animations are currently defined:
  - move_right
  - move_up
- left is handled by FlipH
- down is handled by FlipV

**Current debug support:**
- PlayerDebugOverlay can draw the cyan gameplay anchor
- PlayerDebugOverlay can display debug arcade coordinates above gates and maze objects
- PlayerMovementDebugTrace can print detailed movement traces, but logging is disabled by default

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
- maze.json describes only the static maze
- rotating gates are no longer serialized in maze.json
- rotating gates are authored directly in Level.tscn
- collectibles use a separate data file

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

### 4.4 MazeStepResult

**File:**
- scripts/gameplay/maze/MazeStepResult.cs

**Purpose:**
- represent the result of evaluating one attempted arcade-pixel movement step against the logical static maze

**Current use:**
- returned by MazeGrid.EvaluateArcadePixelStep(...)
- wrapped into PlayfieldStepResult once dynamic gates are considered

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
- EvaluateArcadePixelStep(...) is the bridge between pixel movement and static maze legality only

### 4.6 MazeLoader

**File:**
- scripts/gameplay/maze/MazeLoader.cs

**Purpose:**
- load the logical maze from JSON

**Current behavior:**
- reads res://data/maze.json
- deserializes MazeDataFile
- builds MazeGrid

## 5. Coordinate System

Coordinate conversion has been extracted from Level into LevelCoordinateSystem.

**File:**
- scripts/level/LevelCoordinateSystem.cs

**Current coordinate spaces:**
- logical cell coordinates
- gameplay arcade-pixel coordinates
- gate pivot coordinates
- Godot scene-space coordinates

**Current constants:**
- logical cell size = 16 arcade pixels
- render scale = 4
- gameplay anchor inside a logical cell = Vector2I(8, 7)

**Current Level wrapper methods:**
- LogicalCellToArcadePixel(...)
- ArcadePixelToLogicalCell(...)
- ArcadePixelToScenePosition(...)
- ArcadeDeltaToSceneDelta(...)
- LogicalCellToScenePosition(...)
- GatePivotToScenePosition(...)

**Important:**
Level remains the public owner of coordinate conversion for other gameplay systems, but the conversion math lives in LevelCoordinateSystem.

## 6. Collectible System

Collectibles are implemented as a separate runtime and visual system.

**Current structure:**
- the base flower layout is loaded from JSON
- collectible views are spawned at runtime under Level/Collectibles
- start-of-level special collectibles are generated from anchor families and replace some of the base flowers visually
- CollectibleFieldRuntime owns the runtime lookup and spawned views
- collectible visuals are kept separate from the static maze and gates

Current collectible-related files:
- scripts/gameplay/collectibles/CollectibleAnchorFamilies.cs
- scripts/gameplay/collectibles/CollectibleAnchorFamily.cs
- scripts/gameplay/collectibles/CollectibleColor.cs
- scripts/gameplay/collectibles/CollectibleKind.cs
- scripts/gameplay/collectibles/CollectibleLayoutFile.cs
- scripts/gameplay/collectibles/CollectibleLoader.cs
- scripts/gameplay/collectibles/CollectiblePlacement.cs
- scripts/gameplay/collectibles/CollectibleSpawnPlan.cs
- scripts/gameplay/collectibles/CollectibleSpawnPlanner.cs
- scripts/gameplay/collectibles/LetterKind.cs
- scripts/level/Collectible.cs
- scripts/level/CollectibleFieldRuntime.cs

### 6.1 Base flower layout

**Current model:**
- data/collectibles_layout.json stores the base flower mask
- the file currently uses a 2D grid matching the 11 x 11 logical maze
- one cell value means one flower should be spawned at that logical cell
- empty cells are explicitly represented in the layout

**Purpose:**
- describe the initial flower field independently from the maze wall JSON
- keep flower placement easy to read and edit
- provide the visual/runtime base on top of which special collectibles are placed

### 6.2 CollectibleFieldRuntime

**File:**
- scripts/level/CollectibleFieldRuntime.cs

**Current responsibilities:**
- spawn the base flower layout at level initialization
- keep one collectible view per occupied logical cell
- apply the start-of-level special collectible plan
- support removal of one collectible by logical cell through the current player pickup hook
- clear tracked collectible views if needed

**Important current limitation:**
- the runtime collectible field does not yet store type-specific gameplay state beyond what is currently needed for visual setup and prototype removal

### 6.3 Start-of-level special collectible placement

**Current model:**
- the level start planner generates:
  - 3 letters
  - 3 hearts
  - 2 to 6 skulls depending on the level
- the planner uses three anchor families named A, B, and C
- four anchors are drawn without replacement in each family
- letters use draw[0], hearts use draw[1], and skulls use draw[2] then draw[3]
- the three letters are first selected by family, then permuted across the three family placements

Current implementation detail:
- anchor family coordinates are expressed directly in the Godot logical-cell coordinate system, with origin at the top-left of the maze

Current temporary visual behavior:
- letters are currently displayed in red
- hearts are currently displayed in red
- skulls are currently displayed in white
- color cycling is not implemented yet

### 6.4 Collectible rendering and pickup timing

**Current rendering behavior:**
- CollectibleFieldRuntime spawns one Collectible scene per occupied logical cell
- scene placement uses Level.LogicalCellToScenePosition(...)
- flowers are displayed from the base layout
- start-of-level letters, hearts, and skulls are displayed by changing the visual state of selected collectible instances
- the heart uses an overlay sprite so the inner heart can remain fixed while the outer ring uses the intended temporary color

**Current pickup behavior:**
- PlayerController consumes collectibles when the movement motor reports that the player crossed a collectible anchor
- PlayerMovementStepResult exposes the real movement segments completed during the tick
- assisted turns can produce more than one movement segment in one tick
- checking all movement segments avoids missing collectibles during assisted turns
- Level.TryConsumeCollectible(...) delegates to CollectibleFieldRuntime.TryConsume(...)

## 7. Rotating Gate System

Rotating gates are implemented as a dynamic gameplay system.

**Current structure:**
- gate views are authored in Level.tscn
- LevelGateRuntime scans those placed views
- LevelGateRuntime builds the runtime GateSystem
- gate logic is kept separate from the static maze
- gate collision is evaluated by PlayfieldCollisionResolver

**Current gate-related files:**
- scripts/gameplay/gates/GateContactHalf.cs
- scripts/gameplay/gates/GateLogicalState.cs
- scripts/gameplay/gates/GateOrientation.cs
- scripts/gameplay/gates/GateSystem.cs
- scripts/gameplay/gates/GateTuning.cs
- scripts/gameplay/gates/GateTurningVisual.cs
- scripts/gameplay/gates/GateVisualState.cs
- scripts/gameplay/gates/RotatingGateRuntimeState.cs
- scripts/level/LevelGateRuntime.cs
- scripts/level/RotatingGateView.cs

### 7.1 Gate authoring model

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

### 7.2 LevelGateRuntime

**File:**
- scripts/level/LevelGateRuntime.cs

**Current responsibilities:**
- cache placed RotatingGateView instances by id
- refresh editor-authored gate definitions
- build the runtime GateSystem from placed views
- expose the GateSystem used by other gameplay systems
- advance gate timers once per simulation tick
- accept push attempts from Level / player movement
- synchronize the visual gate views from runtime state

### 7.3 GateSystem and RotatingGateRuntimeState

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

### 7.4 Gate rendering

**Current rendering behavior:**
- LevelGateRuntime keeps references to the placed RotatingGateView instances
- runtime gate state is synchronized back to those views
- stable state shows horizontal or vertical
- short turning state shows slash or backslash

## 8. Playfield Step Evaluation

Dynamic movement legality is evaluated beyond the static maze.

**File:**
- scripts/gameplay/PlayfieldCollisionResolver.cs

Current flow:
- PlayerMovementMotor requests one attempted pixel step
- Level forwards the request to PlayfieldCollisionResolver
- PlayfieldCollisionResolver asks MazeGrid for the static result
- PlayfieldCollisionResolver overlays dynamic rotating-gate checks
- the final result is returned as PlayfieldStepResult
- if a gate blocks the step and can be pushed, PlayerMovementMotor asks Level to push the gate and then re-evaluates the same step

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

## 9. Level Runtime Logic

**File:**
- scripts/level/Level.cs

Level.cs is currently the runtime coordinator for one active board.

**Current direct responsibilities:**
- load the static logical maze from res://data/maze.json
- load the base flower layout from res://data/collectibles_layout.json
- create LevelGateRuntime from Level/Gates
- create CollectibleFieldRuntime from Level/Collectibles
- create PlayfieldCollisionResolver from MazeGrid and GateSystem
- generate the start-of-level special collectible plan
- expose runtime MazeGrid and GateSystem
- expose coordinate conversion wrapper methods
- expose TryConsumeCollectible(...) for the player pickup path
- expose TryPushGate(...) and AdvanceGateSimulationOneTick(...) for the movement path
- initialize the player after maze, gates, collectibles, and collision resolver have been prepared
- update player and gate previews in the editor

**Delegated responsibilities:**
- coordinate conversion math: LevelCoordinateSystem
- gate view/runtime management: LevelGateRuntime
- collectible field management: CollectibleFieldRuntime
- maze + gate collision evaluation: PlayfieldCollisionResolver

**Important design point:**
Level is now mostly a coordinator. It remains the public integration surface for actors, but most specialized runtime logic is delegated to smaller classes.

## 10. Player Movement System

The current player movement system is an arcade-oriented fixed-tick model.
It includes reverse-engineered assisted-turn behavior and is split into dedicated helper classes.

**Current player-related files:**
- PlayerController.cs
- PlayerDebugOverlay.cs
- PlayerInputState.cs
- PlayerMovementDebugTrace.cs
- PlayerMovementMotor.cs
- PlayerMovementSegment.cs
- PlayerMovementStepResult.cs
- PlayerMovementTuning.cs
- PlayerTurnAssistFlags.cs
- PlayerTurnPath.cs
- PlayerTurnWindowDecision.cs
- PlayerTurnWindowResolver.cs

**Additional movement-related integration:**
- PlayfieldCollisionResolver.cs
- PlayfieldStepKind.cs
- PlayfieldStepResult.cs
- MazeGrid / MazeStepResult
- LevelGateRuntime / GateSystem / RotatingGateRuntimeState
- Level.EvaluateArcadePixelStepWithGates(...)

### 10.1 PlayerController

**File:**
- scripts/actors/PlayerController.cs

**Current role:**
- orchestrate input, movement ticks and rendering
- receive the Level reference
- forward input events to PlayerInputState
- advance PlayerMovementMotor at fixed tick rate
- advance gate timers once per simulation tick
- update facing animation immediately from input
- apply gameplay position and render offset to the scene node
- update PlayerDebugOverlay when debug drawing is enabled
- consume collectibles along every movement segment reported by the motor

**Important:**
- PlayerController is intentionally light
- gameplay movement rules are not implemented directly in this class
- collectible pickup handling is still prototype-level, but now follows the real per-tick movement path

### 10.2 PlayerInputState

**File:**
- scripts/actors/PlayerInputState.cs

**Purpose:**
- track held movement directions
- track the relative order in which they were pressed
- resolve the currently intended direction

**Current rule:**
- if several movement keys are held, the last pressed one wins

### 10.3 PlayerMovementTuning

**File:**
- scripts/actors/PlayerMovementTuning.cs

**Purpose:**
- centralize stable movement calibration values

**Current contents:**
- fixed tick timing constants
- rail snap tolerances
- sprite render offsets
- directional collision probe distances

### 10.4 PlayerMovementMotor

**File:**
- scripts/actors/PlayerMovementMotor.cs

**Purpose:**
- own the player gameplay movement state
- apply arcade-style movement rules one fixed tick at a time

**Current responsibilities:**
- store the gameplay arcade-pixel position
- store the effective movement direction
- preserve movement context across short taps
- latch requested directions before some direction changes become effective
- use PlayerTurnWindowResolver to classify turn windows
- resolve normal movement, close-range alignment assists, and full assisted turns
- validate every committed pixel segment against the active playfield
- resolve pushable rotating gates and re-evaluate the same step
- return the complete set of one-pixel movement segments completed during the tick

Important current behavior:
- movement runs at fixed tick rate
- gameplay position is integer arcade-pixel based
- normal straight movement is 1 pixel per tick
- assisted turns may contain an orthogonal correction pixel plus a requested-direction pixel in one tick
- short taps preserve the previous movement context
- blocked fixed-wall corrections are rejected so the player does not slide sideways into walls
- pushable gate blocks are allowed to proceed so the committed step can push the gate
- sprite facing and sprite render offset are intentionally separated

### 10.5 PlayerTurnWindowResolver

**File:**
- scripts/actors/PlayerTurnWindowResolver.cs

**Purpose:**
- isolate the reverse-engineered turn-window data and selection policy from PlayerMovementMotor

**Current responsibilities:**
- map the current position and requested direction to a high-level PlayerTurnPath
- choose a target lane for assisted turns
- expose whether a close-range orthogonal correction is needed
- keep original mirrored Y coordinate handling local to the resolver
- keep the lane masks derived from reverse engineering out of the movement motor

### 10.6 PlayerMovementStepResult and PlayerMovementSegment

**Files:**
- scripts/actors/PlayerMovementStepResult.cs
- scripts/actors/PlayerMovementSegment.cs

**Purpose:**
- represent the outcome of one movement-motor tick
- expose the real one-pixel movement segments completed during that tick

**Current use:**
- PlayerController reads MovementSegments to detect collectible anchor crossings
- this is required because assisted turns can move along two axes during one simulation tick

### 10.7 PlayerDebugOverlay and PlayerMovementDebugTrace

**Files:**
- scripts/actors/PlayerDebugOverlay.cs
- scripts/actors/PlayerMovementDebugTrace.cs

**Purpose:**
- PlayerDebugOverlay draws optional player debug visuals above the playfield
- PlayerMovementDebugTrace provides optional console tracing for difficult movement edge cases

**Current behavior:**
- PlayerDebugOverlay is drawn as a top-level high-Z overlay so coordinates are visible above gates
- PlayerMovementDebugTrace is disabled by default through a private const bool

## 11. Current Player Movement Behavior

**The current player movement model includes:**

- fixed tick update
- integer arcade-pixel gameplay position
- 1 pixel straight movement per normal tick
- assisted-turn ticks that can include two one-pixel segments
- buffered input using "last pressed wins"
- request latching for some direction changes
- preserved movement context during short taps
- explicit current / wanted / facing / offset directions
- lane alignment inside 16x16 logical cells
- turn-window selection through PlayerTurnWindowResolver
- close-range alignment assists
- full assisted turns with orthogonal correction
- static maze validation for every committed pixel segment
- dynamic rotating-gate validation layered on top of the static maze
- same-tick gate push resolution and re-evaluation
- short gate turning visuals
- collectible pickup checks across all movement segments in a tick

**Important:**
- the player no longer moves cell to cell
- the player no longer interpolates toward a target scene position
- movement is driven by discrete gameplay ticks
- the current movement behavior has been manually tested across normal turns, short taps, blocked walls, pushable gates, and assisted-turn collectible pickup cases

## 12. What Is Currently Working

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
- coordinate conversion is centralized through LevelCoordinateSystem
- playfield collision evaluation is centralized through PlayfieldCollisionResolver
- movement is validated against the static maze
- movement outside the maze bounds is blocked
- rotating gates influence movement legality
- rotating gates can be pushed
- gate runtime state is managed through LevelGateRuntime
- gate logical state toggles immediately on successful push
- gate turning visuals are shown briefly
- player movement runs with a fixed tick model
- player movement runs pixel by pixel in gameplay coordinates
- buffered multi-key input works
- short-tap turn behavior preserves movement context
- assisted turns work at intersections
- blocked wall corrections no longer cause unintended sideways sliding
- pushable gates can be handled from assisted-turn contexts
- movement segments are reported to the controller
- collectibles can be consumed reliably during assisted turns
- movement architecture is refactored into dedicated helper classes
- the base flower layout is loaded from JSON and spawned at runtime
- collectible runtime state is managed through CollectibleFieldRuntime
- flowers are displayed at the correct logical cells of the maze
- start-of-level letters, hearts, and skulls are generated and displayed
- special collectible placement uses corrected Godot logical-cell anchors
- hearts currently use an overlay-based visual setup
- the prototype player movement can remove collectibles from the field
- player debug coordinates can be drawn above rotating gates

## 13. What Is Not Implemented Yet

**The following systems are still not implemented yet:**

- enemies
- collectible gameplay rules for hearts, letters, and skulls
- collectible color cycling
- heart multiplier logic
- EXTRA / SPECIAL word progression logic
- skull lethality logic
- bonus vegetables
- HUD
- score system
- lives system
- title screen flow
- gameplay / game over / high score screen flow
- session state management
- automated movement regression tests

## 14. Current Limitations

The movement and gate systems are now stable and much closer to the arcade behavior than the early prototype.
However, they are still a practical remake implementation rather than a literal ROM-level reproduction.

The collectible system is visually present at level start and supports prototype removal, but it does not yet implement final gameplay rules.

**Open points include:**
- automated non-regression coverage for the validated player movement cases
- exact original collision details from the ROM if a stricter low-level reproduction is desired
- exact gate/turn interaction windows in rare edge cases, if future testing reveals differences
- enemy movement system
- exact gameplay semantics for hearts, letters, and skulls
- collectible color cycling timing
- score and word-progression behavior
- whether collectible removal timing should remain exactly as in the current prototype once scoring and letter rules exist

## 15. Current Development Priority

**A reasonable current priority is now:**

1) keep the current movement and gate systems stable
2) document and protect validated movement behavior with regression scenarios
3) keep the collectible placement and display stable
4) implement collectible gameplay rules, scoring, and HUD foundations
5) implement enemies and remaining gameplay systems
6) continue refining arcade fidelity only where reverse engineering or testing justifies it
