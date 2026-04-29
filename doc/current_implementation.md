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
├─ enemies/
│  └─ Enemy.tscn
├─ level/
│  ├─ Collectible.tscn
│  ├─ Level.tscn
│  ├─ MazeBorderTimer.tscn
│  └─ RotatingGate.tscn
└─ player/
   └─ Player.tscn
```

**Important currently used scripts:**

```text
scripts/
├─ Main.cs
├─ actors/
│  ├─ EnemyController.cs
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
│  ├─ enemies/
│  │  ├─ EnemyChaseSystem.cs
│  │  ├─ EnemyMovementAi.cs
│  │  ├─ EnemyMovementTuning.cs
│  │  ├─ EnemyNavigationCell.cs
│  │  ├─ EnemyNavigationGrid.cs
│  │  ├─ EnemyReleaseBorderTimer.cs
│  │  ├─ EnemyRuntime.cs
│  │  ├─ MonsterDir.cs
│  │  ├─ MonsterEntity.cs
│  │  └─ MonsterRuntimeState.cs
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
│  ├─ MazeBorderTimerView.cs
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
- assets/sprites/enemies/enemy_level1.png
- assets/sprites/props/collectibles.png
- assets/sprites/props/maze_border_timer_tiles.png
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
├─ MazeBorderTimer (instance of scenes/level/MazeBorderTimer.tscn)
├─ Collectibles (Node2D)
├─ Gates (Node2D)
│  └─ 20 instances of scenes/level/RotatingGate.tscn
├─ Player (instance of scenes/player/Player.tscn)
├─ Enemies (runtime-created Node2D when not already present)
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

**MazeBorderTimer node:**
- instance of scenes/level/MazeBorderTimer.tscn
- script = scripts/level/MazeBorderTimerView.cs
- renders the animated white / green border tiles around the maze
- creates the individual Sprite2D tile views at runtime from assets/sprites/props/maze_border_timer_tiles.png
- keeps the border-timer graphics separate from the static purple maze background
- uses scripts/gameplay/enemies/EnemyReleaseBorderTimer.cs for the arcade-style countdown / reload timing
- starts the visual cycle near the middle of the top border and advances clockwise
- emits a completion condition used by Level to release the next waiting enemy
- is reset after player death so the next life begins a fresh enemy-release cadence

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

**Enemies node:**
- runtime-created Node2D named Enemies if it is not already present in Level.tscn
- used as the parent for the four enemy views created by EnemyRuntime
- inserted before the Player node so enemies render below the player but above the maze / collectibles / gates

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


### 3.6 Enemy scene

**Scene:**
- scenes/enemies/Enemy.tscn

**Current structure:**

```text
Enemy (Node2D)
```

**Current script:**
- scripts/actors/EnemyController.cs

**Current role:**
- lightweight visual node used by EnemyRuntime for each enemy slot
- creates and owns the AnimatedSprite2D runtime child used to display the enemy
- loads assets/sprites/enemies/enemy_level1.png for the current first-level enemy graphics
- keeps enemy rendering separate from enemy movement logic

**Current visual setup:**
- the spritesheet contains three right-moving frames followed by three upward-moving frames
- right and up animations are built at runtime
- left is handled by horizontal flip
- down is handled by vertical flip
- a small visual offset is applied so the sprite lines up with the maze while preserving the enemy decision-center anchor

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

**Enemy-specific note:**
- enemy decision centers use the reverse-engineered anchor X&0x0F=0x08 and Y&0x0F=0x06
- the visible waiting enemy in the lair is placed at logical cell (5, 5) using that enemy anchor
- enemy sprite rendering applies a small visual offset so the graphics align with the current maze art without changing gameplay coordinates

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
- create EnemyRuntime from the runtime Enemies parent, MazeGrid, GateSystem and coordinate converters
- generate the start-of-level special collectible plan
- initialize collectible color cycling for hearts and letters
- configure the maze-border enemy-release timer for the current level number
- own the current prototype score state
- own the current prototype heart multiplier state
- own the current prototype SPECIAL / EXTRA word progress state
- own the current prototype life state
- own the short heart / letter pickup popup state
- own the player death sequence state at board-coordinator level
- own the minimal game-over guard
- own the fixed gameplay tick for board-level systems, the maze-border timer, enemies, and the player
- expose runtime MazeGrid and GateSystem
- expose coordinate conversion wrapper methods
- expose TryConsumeCollectible(...) for the player pickup path
- expose TryPushGate(...) for movement / gate interactions
- check player/enemy collision after enemy and player movement have both advanced
- restart only the attempt-level enemy state after player death while preserving collectibles and rotating gates
- initialize the player after maze, gates, collectibles, HUD, and collision resolver have been prepared
- update player and gate previews in the editor

**Delegated responsibilities:**
- coordinate conversion math: LevelCoordinateSystem
- gate view/runtime management: LevelGateRuntime
- maze-border timer rendering and visual state: MazeBorderTimerView
- maze-border countdown / reload timing: EnemyReleaseBorderTimer
- collectible field management: CollectibleFieldRuntime
- maze + gate collision evaluation: PlayfieldCollisionResolver
- enemy state, enemy views, enemy navigation, chase, release and reset: EnemyRuntime
- collectible scoring calculation: CollectibleScoreService
- SPECIAL / EXTRA word progress: WordProgressState
- lives: PlayerLifeState
- player death animation timing: PlayerDeathSequenceState
- HUD rendering: Hud
- pickup popup rendering: CollectiblePickupPopupView

**Fixed tick ownership:**
- Level owns the fixed gameplay simulation tick
- board-level systems are advanced before the player
- the maze-border timer is advanced as a normal board-level system
- the enemy runtime is advanced before the player, then player/enemy collision is checked after player movement
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
- support enemy skull checks through TryConsumeSkullAt(...)
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
- consumed with no score when touched by the player
- starts the player death sequence when touched by the player
- decrements lives immediately when it kills the player
- freezes normal gameplay while the player death sequence runs
- respawns the player at PlayerStartCell if lives remain
- enters a minimal game-over placeholder if no lives remain
- can also kill an enemy through EnemyRuntime / TryConsumeSkullAt(...), in which case the enemy returns to the lair and the skull is removed

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

**Current enemy-death behavior:**
- touching an active enemy immediately hides all enemy views before the player death sequence starts
- no collectible is consumed and no score is awarded
- one life is removed immediately
- normal gameplay freezes during the death sequence
- when the death sequence completes and lives remain:
  - the player respawns at PlayerStartCell
  - all enemies active in the maze are cleared
  - one enemy is shown waiting in the lair again
  - the maze-border timer is reset
  - already consumed collectibles remain consumed
  - rotating gate orientations are preserved

**Current death visual behavior:**
- phase 1: red player shrink sequence
- phase 2: ghost apparition sequence
- phase 3: ghost zigzag upward sequence
- total duration = 240 simulation ticks, about 4 seconds at arcade timing
- the movement motor is not advanced during death
- the normal player sprite is hidden while the runtime death sprite is visible

**Current limitations:**
- proper game-over screen flow is not implemented yet
- player death still uses the current high-level red shrink / ghost sequence rather than exact tile-level arcade rendering

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


## 12. Maze Border Enemy Release Timer

Current maze-border timer files:

```text
scenes/level/MazeBorderTimer.tscn
scripts/level/MazeBorderTimerView.cs
scripts/gameplay/enemies/EnemyReleaseBorderTimer.cs
assets/sprites/props/maze_border_timer_tiles.png
```

**Current role:**
- render the animated white / green border tiles around the maze
- represent the arcade enemy-release clock visually
- keep the border clock separate from the collectible heart / letter color cycle
- return a completion signal used by Level / EnemyRuntime to release the next waiting enemy

**Current visual model:**
- MazeBorderTimerView creates Sprite2D tile instances at runtime
- the fixed purple maze wall remains inside assets/images/maze_background.png
- the timer layer uses transparent white / green tiles from assets/sprites/props/maze_border_timer_tiles.png
- the simplified tilesheet contains:
  - top-left corner
  - top-right corner
  - bottom-left corner
  - bottom-right corner
  - vertical border tile
  - horizontal border tile
- the right and bottom edge tiles use separate placement offsets so the simplified tileset can match the current maze background
- the visual sequence starts near the middle of the top border and proceeds clockwise

**Current timing model:**
- EnemyReleaseBorderTimer implements a countdown / reload model based on the reverse-engineered arcade RAM pair 60AA / 60AB
- the border timer is independent from the 600-tick collectible color cycle used by hearts and letters
- the first border step occurs only after a full countdown period
- current level-based periods are:

```text
Level 1       = 9 simulation ticks per border step
Levels 2 to 4 = 6 simulation ticks per border step
Level 5+      = 3 simulation ticks per border step
```

**Current Level integration:**
- Level finds the optional MazeBorderTimer node at startup
- Level configures it from the current _levelNumber
- Level advances it from AdvanceBoardSimulationOneTick()
- the timer is frozen during heart / letter popup pauses
- the timer is frozen during the player death sequence
- when the border cycle completes, Level asks EnemyRuntime to release the next waiting enemy
- after player death, Level resets the border timer as part of the attempt restart

**Current implementation note:**
- the border clock is now connected to enemy release
- every completed release cycle should provide a release opportunity; the current implementation avoids creating an extra visual cycle that releases no enemy
- the remake uses high-level Sprite2D rendering rather than reproducing the original VRAM / color RAM writes literally


## 13. Enemy System

Enemies are now implemented as a first playable runtime system.
The current implementation is intentionally high-level and maintainable: it keeps the reverse-engineered arcade principles but does not copy the original RAM layout literally.

Current enemy-related files:

```text
scenes/enemies/Enemy.tscn
assets/sprites/enemies/enemy_level1.png

scripts/actors/EnemyController.cs
scripts/gameplay/enemies/
├─ EnemyChaseSystem.cs
├─ EnemyMovementAi.cs
├─ EnemyMovementTuning.cs
├─ EnemyNavigationCell.cs
├─ EnemyNavigationGrid.cs
├─ EnemyReleaseBorderTimer.cs
├─ EnemyRuntime.cs
├─ MonsterDir.cs
├─ MonsterEntity.cs
└─ MonsterRuntimeState.cs
```

### 13.1 Runtime architecture

**EnemyRuntime** is the top-level enemy coordinator owned by Level.
It creates the runtime Enemies parent if needed, instantiates four enemy views, owns the four logical enemy slots, advances enemy navigation / chase / movement, checks skull deaths for enemies, exposes collision-active monsters to Level, handles release from the lair, and resets enemy state after player death.

**MonsterEntity** stores the gameplay state of one enemy slot:
- slot id
- arcade-pixel position
- current direction
- preferred direction
- chase timer
- runtime state
- movement / collision flags
- lair visibility flag

**EnemyController** owns only the visual node for one enemy.
It loads the first-level enemy spritesheet, builds right/up animations at runtime, mirrors the sprite for left/down, and applies the visual offset used to align the enemy art with the maze.

**EnemyNavigationGrid** builds an enemy navigation map from the static MazeGrid and the current GateSystem state.
It stores allowed directions and BFS guidance directions separately.

**EnemyMovementAi** advances one active monster by one arcade pixel.
It handles decision-center checks, preferred-direction validation, fallback directions, straight movement, and a simple forced reversal when the current path becomes blocked outside a decision center.

**EnemyChaseSystem** owns the arcade-inspired chase timing state:
- divider
- B8-like activation counter
- round-robin enemy selector
- activation index / duration sequence

### 13.2 Current enemy behavior

Current implemented behavior:
- four enemy slots exist
- one enemy is visible in the central lair before the first release
- the visible waiting enemy is placed at logical cell (5, 5), using the enemy decision anchor X&0x0F=0x08 and Y&0x0F=0x06
- enemies are released by the maze-border timer
- after one enemy leaves the lair, another waiting enemy becomes visible if a slot is available
- active enemies move one arcade pixel per fixed simulation tick
- enemies make direction choices at decision centers
- enemy directions use a separate MonsterDir enum: Left=0x01, Up=0x02, Right=0x04, Down=0x08
- navigation considers the static maze and current rotating-gate states
- a BFS guidance map can temporarily override preferred directions during chase phases
- chase activation uses a level-dependent first activation threshold and a round-robin enemy selector
- enemies collide with the player using the strict arcade-style window: abs(dx) < 9 and abs(dy) < 9
- enemies can be killed by skulls and return to the lair

### 13.3 Player death from enemy

When the player touches an active enemy:
- enemy views are hidden immediately before the red shrink / ghost death sequence starts
- the player loses one life
- normal board simulation freezes while the death sequence runs
- if lives remain, the board attempt restarts after the death sequence

The attempt restart resets only the enemy-related attempt state:
- active enemies in the maze are cleared
- one enemy is placed back in the lair as waiting / visible
- chase timers and round-robin state are reset
- the maze-border release timer is reset

The attempt restart deliberately preserves:
- consumed flowers, hearts, letters and skulls
- current rotating-gate orientations
- score
- lives after the already-applied life loss
- SPECIAL / EXTRA progress
- heart multiplier state

### 13.4 Current enemy limitations

The current enemy system is a first playable approximation.
The following details are still approximate or not implemented yet:
- exact base preferred direction generation outside BFS chase phases
- exact enemy path while leaving the lair
- exact local door rejection behavior from the arcade routines
- exact forced reversal semantics around rotating doors
- full chase activation tables for all levels and DIP settings
- enemy freeze caused by the future vegetable bonus
- enemy type / spritesheet selection for later levels
- exact visual state progression for lair / release transitions

## 14. Rotating Gate System

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

## 15. Playfield Step Evaluation

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

## 16. Player Movement System

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

### 16.1 PlayerController

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
- expose the current arcade-pixel position used by Level for player/enemy collision and enemy BFS guidance
- respawn the player at the level start cell after death when lives remain

**Important:**
- PlayerController no longer owns the global fixed tick accumulator
- Level owns the fixed gameplay tick and calls PlayerController.AdvanceOneSimulationTick()
- PlayerController is intentionally light for movement rules; gameplay movement rules are implemented in PlayerMovementMotor
- PlayerController currently also owns the death visual orchestration because it owns the player sprites

### 16.2 Player movement helpers

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

## 17. Current Player Movement Behavior

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

## 18. What Is Currently Working

The following is already implemented and functional:

- Main scene launches correctly
- Level scene is instantiated from Main
- maze background is displayed
- upper HUD strip has room above the maze
- lower HUD displays lives and score below the maze
- SPECIAL, EXTRA, and x2/x3/x5 are displayed in the upper HUD
- pre-placed rotating gates are displayed
- maze-border timer tiles are displayed around the maze
- maze-border timer animation starts near the middle of the top border and advances clockwise
- maze-border timer speed follows the reverse-engineered level periods: 9 ticks at level 1, 6 ticks at levels 2-4, and 3 ticks at level 5+
- maze-border timer completion is connected to enemy release
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
- maze-border timer is driven by the Level-owned fixed simulation tick
- maze-border timer is frozen during pickup popups and the player death sequence
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
- skull pickup is lethal to the player
- skull death removes one life and starts the player death sequence
- enemies are implemented as a first playable system
- one enemy is visible waiting in the central lair before the first release
- enemies are released by the maze-border timer
- enemy movement is fixed-tick and pixel-based
- enemies use a separate direction enum from player movement
- enemies use decision-center movement at X&0x0F=0x08 and Y&0x0F=0x06
- enemies use a navigation grid generated from the static maze and current rotating-gate states
- enemies can receive temporary BFS chase guidance toward the player
- enemy chase activation uses a round-robin selector and level-dependent timing thresholds
- enemies collide with the player using a strict <9 pixels window on both axes
- touching an enemy starts the player death sequence
- enemy views are hidden immediately when the player dies from an enemy
- after player death, the player respawns at PlayerStartCell when lives remain
- after player death, active enemies are cleared and one waiting enemy is restored in the lair
- after player death, the maze-border timer is reset
- after player death, consumed collectibles remain consumed and rotating gate states are preserved
- enemies can be killed by skulls and return to the lair
- player death uses the red shrink, ghost apparition, and ghost zigzag sequence
- the player respawns at PlayerStartCell when lives remain
- no-lives game over placeholder exists

## 19. What Is Not Implemented Yet

The following systems are still not implemented yet:

- bonus vegetables
- vegetable bonus scoring
- enemy freeze caused by vegetables
- level-specific enemy sprites beyond the first-level enemy spritesheet
- exact enemy type rotation from level 9 onward
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
- automated movement / enemy regression tests

## 20. Current Limitations

The movement, gate, collectible, scoring, HUD, death-sequence, and first enemy systems are functional enough to continue development from this point.
The enemy system is intentionally a first playable approximation rather than a fully verified arcade-perfect reproduction.

Current limitations include:
- score, lives, multiplier, word progress, and special-award placeholder are still owned by Level rather than a future GameSession
- HUD is functional but still scene-local rather than part of a full screen-flow architecture
- pickup popup uses Label-based temporary text, not original tile-based popup graphics
- SPECIAL completion is only a placeholder award and does not implement credits/free games yet
- SPECIAL / EXTRA completion does not yet trigger stage transition
- game over is only a placeholder state
- exact low-level tile / color RAM behavior is not reproduced literally
- enemy base preferred direction generation outside chase is approximate
- enemy movement around rotating doors is implemented through the current MazeGrid + GateSystem layer, but exact arcade local-door rejection / forced-reversal cases still need refinement
- enemy release from the lair is simplified and does not yet reproduce every visual / state transition from the arcade
- chase activation is based on currently observed levels and should remain configurable until more MAME traces cover later levels and DIP settings
- enemy skull death is implemented at the current high-level gameplay-cell level and may need additional pixel-level refinement
- vegetable freeze is not implemented yet, so frozen-but-fatal enemy behavior is still pending

## 21. Current Development Priority

A reasonable current priority is now:

1) commit the current first playable enemy system as a stable checkpoint
2) keep the current movement, gate, scoring, collectible, HUD, lives, death, and enemy reset systems stable
3) document and protect validated player/enemy movement behavior with regression scenarios
4) refine enemy movement around rotating doors using targeted MAME traces
5) refine base enemy preferred-direction generation outside chase phases
6) implement bonus vegetables and enemy freeze behavior
7) implement level-clear / stage transition flow
8) decide the remake behavior for SPECIAL completion
9) introduce a GameSession or GameplayScreen-level session model when persistent state starts outgrowing Level
10) add later-level enemy sprites / enemy-type selection
11) implement remaining screen-flow and persistence systems
12) continue refining arcade fidelity only where reverse engineering or testing justifies it
