# Project Architecture

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

**Purpose of this document:**
- describe the intended long-term architecture of the project
- show how all major game systems are expected to fit together
- keep a clear separation between:
  1) the final target architecture
  2) the currently implemented foundation

**Important:**
This document is intentionally more future-oriented than current_implementation.md.

- current_implementation.md = what already exists in the codebase now
- architecture.md = the global target structure of the whole game

This document should stay more stable than current_implementation.md.
It may evolve when reverse-engineering results change the global structure, but it is not intended to be updated for every implementation detail.

## 1. Architecture Goal

The goal is not only to reproduce Lady Bug visually.
The goal is to build a structure that supports:

- faithful arcade movement
- enemies and their AI / path logic
- rotating gates
- collectibles and bonus items
- score / lives / stage progression
- screen flow (title, gameplay, game over, high score)
- future reverse-engineering refinements without turning the codebase into a monolithic prototype

The architecture should remain:
- simple enough to understand
- modular enough to grow
- explicit about the difference between gameplay logic and rendering

## 2. High-Level Architecture

The final project architecture is intended to have these major layers:

1) Application / Screen Flow
   - Main
   - TitleScreen
   - GameplayScreen
   - GameOverScreen
   - HighScoreScreen

2) Gameplay Session / Global State
   - GameSession
   - score
   - lives
   - current stage
   - high scores
   - progression between screens and levels

3) Level Runtime
   - Level
   - maze background
   - runtime logical maze
   - player instance
   - enemy instances
   - rotating gate instances
   - collectibles / bonus items
   - HUD integration

4) Actor Systems
   - PlayerController and player movement helpers
   - EnemyController and enemy movement helpers
   - future shared playfield/coordinate services where useful

5) Logical Gameplay Systems
   - MazeGrid
   - dynamic gate state
   - collectible placement / pickup rules
   - score / bonus logic
   - enemy target / movement logic

6) Rendering / UI
   - HUD
   - title/game over/high-score screens
   - animations
   - sprite flipping / visual offsets
   - debug overlays

## 3. Target Folder Structure

The current repository does not contain all these files yet.
This section describes the intended long-term structure.

```text
assets/
├─ images/
│  ├─ maze/
│  └─ ui/
├─ sprites/
│  ├─ player/
│  ├─ enemies/
│  └─ props/
├─ audio/
│  ├─ music/
│  └─ sfx/
└─ fonts/
```

```text
data/
├─ maze.json
├─ collectibles_layout.json
├─ stage_config.json
├─ items.json
└─ high_scores.json
```

```text
doc/
├─ architecture.md
├─ current_implementation.md
├─ player_movement.md
├─ enemy_movement.md
├─ gates.md
└─ reverse_engineering.txt
```

```text
scenes/
├─ Main.tscn
├─ screens/
│  ├─ TitleScreen.tscn
│  ├─ GameplayScreen.tscn
│  ├─ GameOverScreen.tscn
│  └─ HighScoreScreen.tscn
├─ level/
│  ├─ Level.tscn
│  ├─ RotatingGate.tscn
│  └─ Collectible.tscn
├─ player/
│  └─ Player.tscn
├─ enemies/
│  ├─ Enemy.tscn
│  ├─ Soldier.tscn
│  ├─ Bulldozer.tscn
│  └─ OtherEnemyVariants.tscn
├─ props/
│  └─ BonusVegetable.tscn
└─ ui/
   └─ Hud.tscn
```

```text
scripts/
├─ Main.cs
├─ screens/
│  ├─ TitleScreen.cs
│  ├─ GameplayScreen.cs
│  ├─ GameOverScreen.cs
│  └─ HighScoreScreen.cs
├─ level/
│  ├─ Level.cs
│  ├─ RotatingGateView.cs
│  └─ Collectible.cs
├─ actors/
│  ├─ PlayerController.cs
│  ├─ PlayerInputState.cs
│  ├─ PlayerMovementDebugTrace.cs
│  ├─ PlayerMovementMotor.cs
│  ├─ PlayerMovementSegment.cs
│  ├─ PlayerMovementStepResult.cs
│  ├─ PlayerMovementTuning.cs
│  ├─ PlayerTurnAssistFlags.cs
│  ├─ PlayerTurnPath.cs
│  ├─ PlayerTurnWindowDecision.cs
│  ├─ PlayerTurnWindowResolver.cs
│  ├─ EnemyController.cs
│  ├─ EnemyAiState.cs
│  ├─ EnemyMovementMotor.cs
│  └─ EnemyMovementStepResult.cs
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
│  ├─ collectibles/
│  │  ├─ CollectibleKind.cs
│  │  ├─ CollectibleColor.cs
│  │  ├─ LetterKind.cs
│  │  ├─ CollectibleAnchorFamily.cs
│  │  ├─ CollectibleAnchorFamilies.cs
│  │  ├─ CollectibleLayoutFile.cs
│  │  ├─ CollectibleLoader.cs
│  │  ├─ CollectiblePlacement.cs
│  │  ├─ CollectibleSpawnPlan.cs
│  │  ├─ CollectibleSpawnPlanner.cs
│  │  ├─ CollectibleRuntimeState.cs
│  │  ├─ CollectibleField.cs
│  │  └─ CollectiblePickupResult.cs
│  ├─ scoring/
│  │  ├─ ScoreService.cs
│  │  ├─ BonusRules.cs
│  │  └─ HighScoreEntry.cs
│  ├─ session/
│  │  ├─ StageDefinition.cs
│  │  ├─ StageFlowController.cs
│  │  └─ LifeState.cs
│  ├─ PlayfieldStepKind.cs
│  └─ PlayfieldStepResult.cs
├─ ui/
│  ├─ HudController.cs
│  └─ DebugOverlay.cs
└─ autoload/
   └─ GameSession.cs
```

## 4. Main Application Flow

### 4.1 Main

**Scene:**
- scenes/Main.tscn

**Role:**
- global root scene of the whole application
- owner of screen switching

**Responsibilities:**
- start on title screen
- switch to gameplay
- switch to game over / high score screens
- return to title screen when needed

Main should not contain gameplay logic.

### 4.2 TitleScreen

**Role:**
- title / attract screen

**Responsibilities:**
- show the game title
- show prompt to start
- optionally show a small animated demo or visual attract loop
- transition to gameplay

### 4.3 GameplayScreen

**Role:**
- container for one active gameplay session

**Responsibilities:**
- instantiate the current level
- instantiate the HUD
- connect level events with GameSession
- react to life lost / stage complete / bonus flow
- transition to game over or next stage

### 4.4 GameOverScreen

**Role:**
- present game over state
- optionally allow entering initials / name for high score

**Responsibilities:**
- show final score
- store a new high-score entry if needed
- transition to high-score or title screen

### 4.5 HighScoreScreen

**Role:**
- display the saved high-score table

**Responsibilities:**
- load and display score entries
- return to title screen or attract loop

## 5. Game Session Architecture

The game needs a session-level model above a single Level instance.

Expected global state:
- current score
- lives remaining
- current stage number
- collected letters / bonus state
- high score table
- current game state

Proposed location:
- scripts/autoload/GameSession.cs

GameSession should be responsible for:
- persistent session data during play
- transitions between stages
- saving / loading high scores
- exposing global state to screens and HUD

Level should not own long-term session state.
Level should only manage one active playfield runtime.

## 6. Level Architecture

### 6.1 Level scene role

Level represents one active gameplay board.

Responsibilities:
- load the logical maze
- expose coordinate conversion helpers
- instantiate and connect runtime actors
- own the active rotating gates of the level
- own the active collectibles / bonus items of the level
- expose the current runtime playfield state

Level should remain the source of truth for:
- logical cell <-> arcade-pixel conversion
- gate pivot <-> arcade-pixel conversion
- arcade-pixel <-> scene-space conversion
- active board objects belonging to the level

### 6.2 Level node structure target

```text
Level (Node2D)
├─ MazeBackground (Sprite2D)
├─ Gates (Node2D)
│  └─ RotatingGate instances
├─ Collectibles (Node2D)
│  ├─ flower / heart / letter / skull instances
│  └─ future bonus item instances when useful
├─ Actors (Node2D)
│  ├─ Player
│  └─ Enemy instances
└─ Effects (Node2D)
```

Notes:
- the static maze background remains a Sprite2D
- moving / interactive objects should remain separate from the static background
- gate view instances can be pre-placed in Level.tscn and converted into a separate runtime gate system during level initialization
- the current collectible direction is to spawn a base flower layout first, then replace selected cells with special collectibles

### 6.3 Possible future Level refactor

Level currently coordinates several systems directly.
As more gameplay systems are added, the following extra helpers may become useful:

```text
LevelCoordinateSystem
PlayfieldCollisionResolver
LevelCollectibleRuntime / CollectibleField
LevelGateRuntime
```

This should be done gradually.
Level can remain the public coordinator while delegating specialized logic to smaller classes.

## 7. Player Architecture

The player movement subsystem is currently the most advanced subsystem in the codebase.

Implemented pieces:
- PlayerController
- PlayerInputState
- PlayerMovementMotor
- PlayerMovementTuning
- PlayerMovementStepResult
- PlayerMovementSegment
- PlayerTurnWindowResolver
- PlayerTurnWindowDecision
- PlayerTurnPath
- PlayerTurnAssistFlags
- PlayerMovementDebugTrace

Current responsibility split:

PlayerController:
- orchestrates input, fixed ticks, sprite facing, render offset, and collectible pickup checks

PlayerInputState:
- resolves intended movement direction using last-pressed-wins input policy

PlayerMovementMotor:
- owns gameplay movement state and applies one arcade-style movement tick

PlayerTurnWindowResolver:
- isolates reverse-engineered turn-window data and chooses high-level turn paths

PlayerMovementStepResult / PlayerMovementSegment:
- expose the actual movement path completed during a tick

PlayerMovementTuning:
- centralizes movement constants

PlayerMovementDebugTrace:
- optional debug tracing, disabled by default

Future possible additions:
- PlayerAnimationState
- PlayerDeathState
- PlayerPickupHandler
- PlayerScoringHooks

The player should remain split into:
- orchestration
- movement rules
- input policy
- rendering / animation concerns
- pickup / score interactions

The current movement system is stable enough that future changes should be protected by regression scenarios.

## 8. Enemy Architecture

The final game will need one or more enemy systems that stay compatible with the same maze and coordinate model as the player.

Expected enemy architecture:

EnemyController:
- high-level orchestration of one enemy

EnemyMovementMotor:
- effective movement logic on the maze

EnemyAiState:
- target choice / chase logic / patrol logic / home logic

EnemyMovementStepResult:
- structured tick result, similar in spirit to the player motor

Possible shared concepts:
- same arcade-pixel coordinate system
- same static maze validation
- same dynamic gate legality framework
- fixed tick movement structure

Important:
Enemy logic should probably reuse the same playfield-step legality model, but not necessarily the exact player movement rules.
Do not extract a generic ActorMovementMotor too early.
The player has special input and assisted-turn behavior that enemies may not share.

## 9. Rotating Gate Architecture

Rotating gates are already implemented as gameplay objects, not visual-only props.

Current architectural direction:
- RotatingGateView
  - editor-authored view instance inside Level.tscn
  - runtime visual synced from gate state
- GateSystem
  - owns all runtime gate states of the active level
- RotatingGateRuntimeState
  - represents one mutable runtime gate
- Gate-related enums and tuning types
  - represent stable orientation, logical blocking axis, turning state, etc.

Level / Maze integration:
- gates influence movement legality
- the static MazeGrid remains the base maze
- dynamic gate state is applied as an additional movement constraint on top of the static maze

Long-term movement legality remains:

```text
static maze legality
+ lane / movement legality
+ dynamic rotating gate legality
```

Future work:
- verify enemy interaction with gates
- refine rare player/gate edge cases only if testing shows mismatches

## 10. Item / Collectible Architecture

The project now has an initial collectible foundation, but not the final collectible gameplay architecture.

Current direction:
- base flower layout loaded from data/collectibles_layout.json
- one collectible or empty cell per logical maze cell
- level spawns a visible flower field from that base layout
- start-of-level planner selects letters, hearts, and skulls using anchor families and random draws
- selected special collectibles replace some already spawned flowers
- player pickup currently removes the collectible view from the runtime lookup
- pickup timing follows all movement segments reported by the player motor

This keeps a useful separation between:
- static base layout data
- start-of-level special placement logic
- future pickup / scoring / color-cycle gameplay logic

Expected long-term item families:
- flowers
- hearts
- letters
- skulls
- bonus vegetables

Expected responsibilities:
- placement / visibility
- pickup rules
- score contribution
- letter / bonus progression
- color-cycle state
- skull lethality

Likely long-term architecture:
- base collectible placement data
- collectible spawn planning
- collectible runtime state
- collectible field / lookup
- pickup result model
- scoring and word-progression rules
- bonus item appearance logic

Possible future types:

```text
CollectibleRuntimeState
CollectibleField
CollectiblePickupResult
CollectibleRules
```

Important:
The collectible system should remain driven by logical maze cells, not by free-form scene-space placement.

## 11. HUD / UI Architecture

HudController should eventually represent the gameplay HUD.

Expected HUD responsibilities:
- display current score
- display remaining lives
- display stage / bonus information
- display letter / collected bonus state

HUD is not the owner of session data.
It should observe GameSession and/or GameplayScreen.

A later DebugOverlay or CanvasLayer-based debug display may also be useful, especially for movement coordinates and diagnostics.

## 12. Logical Maze Architecture

The logical maze system already has a good foundation and should remain central.

Core pieces:
- WallFlags
- MazeCell
- MazeDataFile
- MazeGrid
- MazeLoader
- MazeStepResult

MazeGrid should remain responsible for:
- storing logical cell data
- validating static maze legality
- evaluating pixel-step movement against the static maze

Dynamic gameplay systems should be added around it, not by turning MazeGrid into a giant all-purpose gameplay object.

In other words:
- MazeGrid = static maze truth
- rotating gates = dynamic movement overlay
- collectibles = level-owned runtime objects on top of the board
- actors = movement clients
- Level = runtime coordinator

Important:
- maze.json should remain focused on static maze data
- collectible layout data should remain separate from static wall data
- rotating gates can be authored in the level scene while still being converted into a separate runtime system at initialization

## 13. Coordinate System Design

A central architectural principle is the separation between:

1) logical cell coordinates
2) gameplay arcade-pixel coordinates
3) scene-space rendering coordinates
4) original/debug coordinate representations used during reverse engineering

This must remain true for:
- player
- enemies
- gates
- pickups
- hit / interaction checks

Why this matters:
- gameplay logic should stay independent of scene-space float rendering
- reverse-engineering findings are naturally expressed in arcade-pixel terms
- visual offsets should not corrupt gameplay coordinates

Possible future improvement:
- extract coordinate conversion helpers into a LevelCoordinateSystem class when Level.cs becomes too large

## 14. Current Implemented Foundation

The following part of the target architecture is already implemented now:

- Main scene
- Level scene
- RotatingGate scene
- Collectible scene
- Player scene
- logical maze loading from JSON
- base collectible layout loading from JSON
- MazeGrid / MazeCell / WallFlags / MazeLoader
- collectible layout loading and flower field spawning
- start-of-level special collectible placement planning
- PlayerController
- PlayerInputState
- PlayerMovementMotor
- PlayerMovementTuning
- PlayerMovementStepResult
- PlayerMovementSegment
- PlayerTurnWindowResolver
- PlayerTurnWindowDecision
- PlayerTurnPath
- PlayerTurnAssistFlags
- PlayerMovementDebugTrace
- MazeStepResult
- PlayfieldStepKind
- PlayfieldStepResult
- GateSystem
- RotatingGateRuntimeState
- placed gate authoring in Level.tscn
- pixel-step playfield validation
- fixed tick player movement
- assisted player turn movement
- dynamic rotating gate legality
- gate push resolution and turning visuals
- movement segment reporting for collectible pickup
- prototype collectible removal

For the detailed current state, see:
- current_implementation.md

## 15. Main Systems Still To Implement

The largest remaining systems are:

- TitleScreen
- GameplayScreen
- GameOverScreen
- HighScoreScreen
- GameSession
- EnemyController and enemy runtime logic
- enemy AI / movement helpers
- enemy interaction with rotating gates
- collectible gameplay rules and pickup consequences
- collectible color cycling
- bonus vegetables
- score service and high-score persistence
- gameplay HUD
- stage progression / stage flow controller
- automated regression scenarios for movement-sensitive behavior

## 16. Architectural Guiding Principles

The architecture should continue to follow these rules:

1) Keep scenes simple and readable
2) Keep gameplay state separated from rendering state
3) Keep static maze logic separated from dynamic gate logic
4) Keep controllers as orchestrators when possible
5) Prefer small helper classes over giant monolithic scripts
6) Add systems only when they have a clear responsibility
7) Stay close to reverse-engineered arcade behavior where it matters
8) Isolate low-level reverse-engineering details behind readable gameplay names
9) Protect stable movement behavior before further movement refactoring

## 17. Current Architectural Direction

The player movement foundation is now stable enough to stop treating turn-window refinement as the immediate architectural bottleneck.

The next major architectural expansions should happen around:
- documenting and protecting movement behavior with regression scenarios
- collectible gameplay rules and scoring
- HUD and session state
- enemy movement and AI
- screen flow

Future refactoring candidates:
- extract PlayfieldCollisionResolver from Level.cs
- extract LevelCoordinateSystem when coordinate conversion grows further
- move debug coordinate rendering into a CanvasLayer-based debug overlay
- introduce richer collectible runtime state and pickup result types

## 18. Summary

The final architecture is intended to support the whole game, not only the player movement subsystem.

It should ultimately contain:
- screen flow
- session state
- level runtime
- player
- enemies
- rotating gates
- collectibles and bonus systems
- scoring and high scores
- HUD and other UI

The project already has a solid movement, maze, rotating-gate, and early collectible foundation.

The most important shift since the previous version of this document is that player movement is now much more mature:
- fixed tick movement is implemented
- assisted turns are implemented
- rotating-gate interaction is integrated
- collectible pickup during assisted turns is handled through movement segments
- low-level turn-window data is isolated from the movement motor

The next work should build on this foundation rather than keep reshaping it without a specific reason.
