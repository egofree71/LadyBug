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
- viewport height = 880
- the viewport height has been increased to leave room for the upper HUD strip above the maze and the score / lives HUD below the maze

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

**Important currently used scene files:**

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

**Important currently used scripts:**

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
│  ├─ PlayerTurnWindowMaps.cs
│  └─ PlayerTurnWindowResolver.cs
├─ gameplay/
│  ├─ collectibles/
│  │  ├─ CollectibleAnchorFamilies.cs
│  │  ├─ CollectibleAnchorFamily.cs
│  │  ├─ CollectibleColor.cs
│  │  ├─ CollectibleColorCycle.cs
│  │  ├─ CollectibleKind.cs
│  │  ├─ CollectibleLayoutFile.cs
│  │  ├─ CollectibleLoader.cs
│  │  ├─ CollectiblePickupPopupState.cs
│  │  ├─ CollectiblePickupResult.cs
│  │  ├─ CollectiblePlacement.cs
│  │  ├─ CollectibleSpawnPlan.cs
│  │  ├─ CollectibleSpawnPlanner.cs
│  │  ├─ LetterKind.cs
│  │  └─ WordProgressState.cs
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
│  ├─ player/
│  │  ├─ PlayerDeathSequenceState.cs
│  │  ├─ PlayerDeathState.cs
│  │  ├─ PlayerDeathVisualSheet.cs
│  │  └─ PlayerLifeState.cs
│  ├─ scoring/
│  │  ├─ CollectibleScoreCalculation.cs
│  │  ├─ CollectibleScoreService.cs
│  │  ├─ HeartMultiplierState.cs
│  │  └─ ScoreState.cs
│  ├─ PlayfieldCollisionResolver.cs
│  ├─ PlayfieldStepKind.cs
│  └─ PlayfieldStepResult.cs
├─ level/
│  ├─ Collectible.cs
│  ├─ CollectibleFieldRuntime.cs
│  ├─ CollectiblePickupPopupView.cs
│  ├─ Level.cs
│  ├─ LevelCoordinateSystem.cs
│  ├─ LevelGateRuntime.cs
│  └─ RotatingGateView.cs
└─ ui/
   └─ Hud.cs
```

**Important current data files:**

```text
data/
├─ collectibles_layout.json
└─ maze.json
```

**Important current documentation files:**

```text
doc/
├─ architecture.md
├─ current_implementation.md
├─ collectibles_reverse_engineering.md
├─ enemy_movement.md
├─ player_movement.md
└─ reverse_engineering.txt
```

**Important currently used visual assets:**
- assets/images/maze_background.png
- assets/sprites/player/lady_bug_spritesheet.png
- assets/sprites/player/player_dead_red.png
- assets/sprites/player/player_dead_ghost.png
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
├─ Player (instance of scenes/player/Player.tscn)
└─ Hud (CanvasLayer)
   └─ Root (Control)
      ├─ SpecialWordLabel (RichTextLabel)
      ├─ ExtraWordLabel (RichTextLabel)
      ├─ MultipliersLabel (RichTextLabel)
      ├─ ScoreLabel (Label)
      └─ LivesLabel (Label)
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
- scene position = Vector2(0, 40)
- the maze is shifted down to make room for the upper HUD strip

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
- gate scene positions are shifted down consistently with the maze background

**Player node:**
- instance of scenes/player/Player.tscn

**Hud node:**
- type = CanvasLayer
- script = scripts/ui/Hud.cs
- displays SPECIAL progress, EXTRA progress, blue-heart multipliers, lives, and score
- the upper HUD uses three RichTextLabel nodes:
  - SPECIAL aligned to the left third of the screen
  - EXTRA centered in the middle third
  - x2 x3 x5 aligned to the right third
- the bottom HUD displays lives on the left and score on the right
- visual layout, font sizes, anchors, and positions are controlled in Level.tscn rather than hardcoded in Hud.cs

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
  - MainSprite = tinted outer ring
  - OverlaySprite = fixed inner heart overlay
- letters use one tinted sprite layer
- heart and letter colors are applied through Sprite2D.Modulate

**Current color constants used for hearts and letters:**

```text
Red    = #FF5100
Yellow = #FFFF00
Blue   = #00AEFF
```

**Current letter frame mapping in assets/sprites/props/collectibles.png:**

```text
E = 4
X = 5
T = 6
R = 7
A = 8
S = 9
P = 10
C = 11
I = 12
L = 13
```

Important:
- this mapping affects only which spritesheet frame is displayed for a logical LetterKind
- it does not affect which logical letters are selected by CollectibleSpawnPlanner

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

**Current living-player visual setup:**
- AnimatedSprite2D uses assets/sprites/player/lady_bug_spritesheet.png
- two animations are currently defined:
  - move_right
  - move_up
- left is handled by FlipH
- down is handled by FlipV
- the player sprite can be temporarily hidden while a heart / letter pickup popup is active

**Current death visual setup:**
- PlayerController creates a runtime Sprite2D named DeathSprite
- the death sprite uses:
  - assets/sprites/player/player_dead_red.png
  - assets/sprites/player/player_dead_ghost.png
- the death sequence is tick-driven by PlayerDeathSequenceState
- the normal AnimatedSprite2D is hidden while the death sprite is active

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

## 6. Level Runtime Logic

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
- initialize collectible color cycling for hearts and letters
- own the current prototype score state
- own the current prototype heart multiplier state
- own the current prototype SPECIAL / EXTRA word progress state
- own the current prototype life state
- own the short heart / letter pickup popup state
- own the player death sequence state at board-coordinator level
- own the minimal game-over guard
- own the fixed gameplay tick for board-level systems and the player
- expose runtime MazeGrid and GateSystem
- expose coordinate conversion wrapper methods
- expose TryConsumeCollectible(...) for the player pickup path
- expose TryPushGate(...) for movement / gate interactions
- initialize the player after maze, gates, collectibles, HUD, and collision resolver have been prepared
- update player and gate previews in the editor

**Delegated responsibilities:**
- coordinate conversion math: LevelCoordinateSystem
- gate view/runtime management: LevelGateRuntime
- collectible field management: CollectibleFieldRuntime
- maze + gate collision evaluation: PlayfieldCollisionResolver
- collectible scoring calculation: CollectibleScoreService
- SPECIAL / EXTRA word progress: WordProgressState
- lives: PlayerLifeState
- player death animation timing: PlayerDeathSequenceState
- HUD rendering: Hud
- pickup popup rendering: CollectiblePickupPopupView

**Fixed tick ownership:**
- Level owns the fixed gameplay simulation tick
- board-level systems are advanced before the player
- while a pickup popup is active, normal gameplay is frozen and only the popup timer advances
- while the player death sequence is active, normal gameplay is frozen and only the death animation advances
- when the death sequence completes, the player either respawns at PlayerStartCell or the minimal game-over placeholder is entered

## 7. Collectible System

Collectibles are implemented as a separate runtime and visual system.

**Current structure:**
- the base flower layout is loaded from JSON
- collectible views are spawned at runtime under Level/Collectibles
- start-of-level special collectibles are generated from anchor families and replace some of the base flowers visually
- CollectibleFieldRuntime owns the runtime lookup and spawned views
- collectible visuals are kept separate from the static maze and gates
- CollectiblePickupResult carries semantic pickup information back to Level

### 7.1 Base flower layout

**Current model:**
- data/collectibles_layout.json stores the base flower mask
- the file currently uses a 2D grid matching the 11 x 11 logical maze
- one cell value means one flower should be spawned at that logical cell
- empty cells are explicitly represented in the layout

### 7.2 Start-of-level special collectible placement

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

### 7.3 CollectibleFieldRuntime

**File:**
- scripts/level/CollectibleFieldRuntime.cs

**Current responsibilities:**
- spawn the base flower layout at level initialization
- keep one collectible view per occupied logical cell
- store semantic runtime state for each collectible:
  - kind
  - current color
  - letter kind, if any
- apply the start-of-level special collectible plan
- apply the global color cycle to hearts and letters
- support consumption of one collectible by logical cell
- return a CollectiblePickupResult when something is consumed
- clear tracked collectible views if needed

### 7.4 Collectible color cycle

**Files:**
- scripts/gameplay/collectibles/CollectibleColorCycle.cs
- scripts/gameplay/collectibles/CollectibleColor.cs

**Current behavior:**
- only hearts and letters are affected by the color cycle
- flowers and skulls remain visually fixed
- the implemented cycle uses a 600-tick loop based on the reverse-engineered timing:
  - red: 31 ticks
  - yellow: 149 ticks
  - blue: 420 ticks
- the remake starts the visible cycle in blue
- the same current color is used for rendering, scoring, word progress, and blue-heart multiplier effects

### 7.5 Pickup timing

**Current pickup behavior:**
- PlayerController consumes collectibles when the movement motor reports that the player crossed a collectible anchor
- PlayerMovementStepResult exposes the real movement segments completed during the tick
- assisted turns can produce more than one movement segment in one tick
- checking all movement segments avoids missing collectibles during assisted turns
- Level.TryConsumeCollectible(...) delegates to CollectibleFieldRuntime.TryConsume(...)

### 7.6 Pickup consequences currently implemented

**Flower:**
- consumed immediately
- adds 10 points multiplied by the active heart multiplier
- no popup is shown

**Heart:**
- consumed and scored according to the current color
- blue = base 100 points
- yellow = base 300 points
- red = base 800 points
- active heart multiplier is applied to the score
- if the heart is blue, the heart multiplier advances after the score for that heart is calculated
- the HUD multiplier display updates when the multiplier step advances
- triggers the temporary pickup popup

**Letter:**
- consumed and scored according to the current color
- blue = base 100 points
- yellow = base 300 points
- red = base 800 points
- active heart multiplier is applied to the score
- blue letters are score-only
- red letters can progress SPECIAL when the letter belongs to SPECIAL
- yellow letters can progress EXTRA when the letter belongs to EXTRA
- A and E can progress either SPECIAL or EXTRA depending on color
- triggers the temporary pickup popup
- when EXTRA completes, the player gains one extra life immediately
- when SPECIAL completes, a placeholder free-game award counter is incremented and a debug message is printed
- level transition on completed SPECIAL / EXTRA is not implemented yet

**Skull:**
- consumed with no score
- starts the player death sequence
- decrements lives immediately
- freezes normal gameplay while the death sequence runs
- respawns the player at PlayerStartCell if lives remain
- enters a minimal game-over placeholder if no lives remain

## 8. Scoring, Heart Multiplier, Lives, and Word Progress

Current scoring and session-like files:

```text
scripts/gameplay/scoring/
├─ ScoreState.cs
├─ HeartMultiplierState.cs
├─ CollectibleScoreCalculation.cs
└─ CollectibleScoreService.cs

scripts/gameplay/collectibles/
└─ WordProgressState.cs

scripts/gameplay/player/
└─ PlayerLifeState.cs
```

### 8.1 ScoreState

**Purpose:**
- owns the current score value for the prototype level
- supports resetting and adding points

**Important:**
- score is currently owned by Level
- later this should probably move to GameSession when screen flow and persistent session state exist

### 8.2 HeartMultiplierState

**Purpose:**
- stores the current blue-heart multiplier step
- exposes the active multiplier value

**Current multiplier values:**

```text
step 0 = x1
step 1 = x2
step 2 = x3
step 3 = x5
```

**Current blue-heart behavior:**
- the score for a blue heart is calculated using the multiplier active before the heart is collected
- after the blue-heart score is applied, the multiplier step advances
- multiplier step is capped at step 3, which corresponds to x5
- the HUD highlights x2, x3, and x5 as they are unlocked

### 8.3 CollectibleScoreService

**Purpose:**
- calculates the base score and final score delta for a consumed collectible
- keeps the Level class from hardcoding collectible score tables directly

**Current score values:**

```text
Flower = 10
Blue heart / letter = 100
Yellow heart / letter = 300
Red heart / letter = 800
Skull = 0
```

**Current rule:**
- final score delta = base score × active heart multiplier

### 8.4 WordProgressState

**Purpose:**
- tracks collected SPECIAL letters
- tracks collected EXTRA letters
- applies color-based word progress rules
- reports whether a pickup changed a word or completed a word

**Current SPECIAL word:**

```text
S P E C I A L
```

**Current EXTRA word:**

```text
E X T R A
```

**Current rules:**
- red letters can progress SPECIAL
- yellow letters can progress EXTRA
- blue letters are score-only
- letters already active in the relevant word do not progress again
- letters not present in the relevant word do not progress

### 8.5 PlayerLifeState

**Purpose:**
- tracks current player lives
- supports resetting lives, losing one life, and adding lives

**Current rules:**
- default initial lives = 3
- skull death removes one life
- EXTRA completion adds one life
- game over is true when lives <= 0

## 9. Temporary Pickup Popup

Current popup-related files:

```text
scripts/gameplay/collectibles/CollectiblePickupPopupState.cs
scripts/level/CollectiblePickupPopupView.cs
```

**Current behavior:**
- shown only when the player collects a heart or a letter
- displayed at the logical cell where the heart / letter was consumed
- top label displays the base score value: 100, 300, or 800
- bottom label displays the active multiplier when multiplier > 1
- player sprite is hidden while the popup is active
- normal gameplay is frozen while the popup is active
- popup duration = 30 simulation ticks
- after the popup finishes:
  - popup view is removed
  - player sprite is restored
  - normal gameplay resumes

**Current popup tuning:**
- font size = 22
- score label position = Vector2(-4, 4)
- multiplier label position = Vector2(-8, 26)
- label line size = Vector2(48, 26)

## 10. Player Lives and Death Sequence

Current player-life and death-related files:

```text
scripts/gameplay/player/
├─ PlayerDeathSequenceState.cs
├─ PlayerDeathState.cs
├─ PlayerDeathVisualSheet.cs
└─ PlayerLifeState.cs
```

**Current skull-death behavior:**
- the skull is consumed and removed from the collectible field
- no score is awarded
- one life is removed immediately
- the HUD lives display updates immediately
- normal gameplay freezes
- the player plays the arcade-style death sequence
- when the death sequence completes:
  - if lives remain, the player respawns at PlayerStartCell
  - if no lives remain, the player remains hidden and a game-over placeholder is entered

**Current death visual behavior:**
- phase 1: red player shrink sequence
- phase 2: ghost apparition sequence
- phase 3: ghost zigzag upward sequence
- total duration = 240 simulation ticks, about 4 seconds at arcade timing
- the movement motor is not advanced during death
- the normal player sprite is hidden while the runtime death sprite is visible

**Current limitations:**
- proper game-over screen flow is not implemented yet
- enemy interactions during death are not relevant yet because enemies are not implemented

## 11. HUD

**Current HUD script:**
- scripts/ui/Hud.cs

**Current scene setup:**

```text
Level
└─ Hud (CanvasLayer)
   └─ Root (Control)
      ├─ SpecialWordLabel (RichTextLabel)
      ├─ ExtraWordLabel (RichTextLabel)
      ├─ MultipliersLabel (RichTextLabel)
      ├─ ScoreLabel (Label)
      └─ LivesLabel (Label)
```

**Current responsibilities:**
- find the score label
- find the lives label
- find the SPECIAL word RichTextLabel
- find the EXTRA word RichTextLabel
- find the multiplier RichTextLabel
- display the current score
- display the current number of lives
- display SPECIAL with inactive letters in grey and active letters in red
- display EXTRA with inactive letters in grey and active letters in yellow
- display x2 / x3 / x5 with inactive entries in grey and active entries in blue

**Important:**
- Hud.cs does not hardcode label positions, anchors, sizes, or editor layout
- Hud.cs does build the BBCode text used to color individual word letters and multiplier entries
- visual placement is controlled by Level.tscn
- credits, top score, title screen HUD, and full arcade screen flow are not implemented yet

## 12. Rotating Gate System

Rotating gates are implemented as a dynamic gameplay system.

**Current structure:**
- gate views are authored in Level.tscn
- LevelGateRuntime scans those placed views
- LevelGateRuntime builds the runtime GateSystem
- gate logic is kept separate from the static maze
- gate collision is evaluated by PlayfieldCollisionResolver
- gate timers are advanced by the Level-owned fixed simulation tick

Current gate-related files:
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

## 13. Playfield Step Evaluation

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

## 14. Player Movement System

The current player movement system is an arcade-oriented fixed-tick model.
It includes reverse-engineered assisted-turn behavior and is split into dedicated helper classes.

Current player-related files:
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
- PlayerTurnWindowMaps.cs
- PlayerTurnWindowResolver.cs

### 14.1 PlayerController

**File:**
- scripts/actors/PlayerController.cs

**Current role:**
- orchestrate input, player movement, sprite facing, render offset, debug overlay updates, and collectible pickup checks
- receive the Level reference
- forward input events to PlayerInputState
- advance PlayerMovementMotor only when Level asks it to run one fixed simulation tick
- update facing animation immediately from input
- apply gameplay position and render offset to the scene node
- update PlayerDebugOverlay when debug drawing is enabled
- consume collectibles along every movement segment reported by the motor
- hide or show the gameplay sprite when Level starts / finishes a pickup popup
- run the visual player death sequence through a separate runtime death sprite
- respawn the player at the level start cell after death when lives remain

**Important:**
- PlayerController no longer owns the global fixed tick accumulator
- Level owns the fixed gameplay tick and calls PlayerController.AdvanceOneSimulationTick()
- PlayerController is intentionally light for movement rules; gameplay movement rules are implemented in PlayerMovementMotor
- PlayerController currently also owns the death visual orchestration because it owns the player sprites

### 14.2 Player movement helpers

**PlayerInputState:**
- tracks held movement directions
- tracks the relative order in which they were pressed
- resolves the currently intended direction
- if several movement keys are held, the last pressed one wins

**PlayerMovementMotor:**
- owns gameplay movement state
- applies arcade-style movement rules one fixed tick at a time
- validates every committed pixel segment against the active playfield
- resolves pushable rotating gates and re-evaluates the same step
- returns the complete set of one-pixel movement segments completed during the tick
- supports resetting the gameplay position to PlayerStartCell during respawn

**PlayerTurnWindowMaps:**
- generates available player turn lanes from MazeGrid
- keeps generated Y lanes in the mirrored original-screen order expected by the turn-window policy

**PlayerTurnWindowResolver:**
- applies the arcade-style pixel window policy around the generated lanes
- chooses high-level turn paths

**PlayerMovementStepResult / PlayerMovementSegment:**
- represent the outcome of one movement-motor tick
- expose the real one-pixel movement segments completed during that tick

## 15. Current Player Movement Behavior

The current player movement model includes:

- fixed tick update owned by Level
- integer arcade-pixel gameplay position
- 1 pixel straight movement per normal tick
- assisted-turn ticks that can include two one-pixel segments
- buffered multi-key input using last-pressed-wins
- request latching for some direction changes
- preserved movement context during short taps
- explicit current / wanted / facing / offset directions
- lane alignment inside 16x16 logical cells
- turn lanes generated from MazeGrid through PlayerTurnWindowMaps
- turn-window selection through PlayerTurnWindowResolver
- close-range alignment assists
- full assisted turns with orthogonal correction
- static maze validation for every committed movement segment
- dynamic rotating-gate validation layered on top of the static maze
- same-tick gate push resolution and re-evaluation
- short gate turning visuals
- collectible pickup checks across all movement segments in a tick
- pause of normal player movement while a heart / letter popup is active
- pause of normal player movement while the player death sequence is active
- respawn reset to PlayerStartCell after a completed death sequence when lives remain

## 16. What Is Currently Working

The following is already implemented and functional:

- Main scene launches correctly
- Level scene is instantiated from Main
- maze background is displayed
- upper HUD strip has room above the maze
- lower HUD displays lives and score below the maze
- SPECIAL, EXTRA, and x2/x3/x5 are displayed in the upper HUD
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
- Level owns the fixed simulation tick
- player movement runs pixel by pixel in gameplay coordinates
- buffered multi-key input works
- short-tap turn behavior preserves movement context
- assisted turns work at intersections
- blocked wall corrections no longer cause unintended sideways sliding
- pushable gates can be handled from assisted-turn contexts
- movement segments are reported to the controller
- collectibles can be consumed reliably during assisted turns
- movement architecture is refactored into dedicated helper classes
- player turn-lane candidates are generated from the logical maze instead of hardcoded ROM-style masks
- the base flower layout is loaded from JSON and spawned at runtime
- collectible runtime state is managed through CollectibleFieldRuntime
- flowers are displayed at the correct logical cells of the maze
- start-of-level letters, hearts, and skulls are generated and displayed
- special collectible placement uses corrected Godot logical-cell anchors
- hearts use an overlay-based visual setup
- hearts and letters use a global visual color cycle
- heart and letter colors use the arcade-style measured RGB values
- collectible letter sprite mapping matches the current spritesheet
- flowers add score immediately
- hearts and letters add score according to current color and multiplier
- blue hearts advance the heart multiplier after their own score is computed
- the upper HUD highlights x2 / x3 / x5 as blue hearts unlock multiplier steps
- heart / letter pickup shows a temporary score / multiplier popup
- player sprite is hidden and normal gameplay is frozen during the popup
- popup is removed and player sprite is restored after 30 ticks
- SPECIAL word progress works for valid red letters
- EXTRA word progress works for valid yellow letters
- EXTRA completion grants one extra life
- SPECIAL completion records a placeholder free-game award
- lives are tracked and displayed
- skull pickup is lethal
- skull death removes one life and starts the player death sequence
- player death uses the red shrink, ghost apparition, and ghost zigzag sequence
- the player respawns at PlayerStartCell when lives remain
- no-lives game over placeholder exists

## 17. What Is Not Implemented Yet

The following systems are still not implemented yet:

- enemies
- bonus vegetables
- enemy freeze caused by vegetables
- title screen flow
- gameplay screen / screen transition architecture
- proper game-over screen flow
- high score screen flow
- persistent session state / GameSession
- high-score persistence
- level-clear logic when all flowers / required collectibles are eaten
- immediate next-level transition when SPECIAL or EXTRA is completed
- exact free-credit / free-game behavior from SPECIAL
- credits / coin / arcade-style free-play handling
- top score display
- automated movement regression tests

## 18. Current Limitations

The movement and gate systems are stable and much closer to the arcade behavior than the early prototype.
Player turn-lane candidates are generated from the logical maze, while the arcade-style pixel window policy remains implemented in the movement resolver.
However, this is still a practical remake implementation rather than a literal ROM-level reproduction.

Current limitations include:
- score, lives, multiplier, word progress, and special-award placeholder are still owned by Level rather than a future GameSession
- HUD is functional but still scene-local rather than part of a full screen-flow architecture
- pickup popup uses Label-based temporary text, not original tile-based popup graphics
- SPECIAL completion is only a placeholder award and does not implement credits/free games yet
- SPECIAL / EXTRA completion does not yet trigger stage transition
- game over is only a placeholder state
- enemies are not implemented yet
- exact low-level tile / color RAM behavior is not reproduced literally

## 19. Current Development Priority

A reasonable current priority is now:

1) keep the current movement, gate, scoring, collectible, HUD, lives, and death systems stable
2) document and protect validated movement behavior with regression scenarios
3) implement level-clear / stage transition flow
4) decide the remake behavior for SPECIAL completion
5) introduce a GameSession or GameplayScreen-level session model when persistent state starts outgrowing Level
6) implement enemies and remaining gameplay systems
7) continue refining arcade fidelity only where reverse engineering or testing justifies it
