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
- screen flow: title, gameplay, game over, high score
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
   - SPECIAL / EXTRA progress
   - progression between screens and levels

3) Level Runtime
   - Level coordinator
   - fixed board simulation tick
   - coordinate system
   - maze background
   - runtime logical maze
   - playfield collision resolver
   - gate runtime
   - collectible field runtime
   - collectible color cycle
   - pickup popup state and view
   - player instance
   - future enemy instances
   - HUD integration

4) Actor Systems
   - PlayerController and player movement helpers
   - EnemyController and enemy movement helpers
   - future shared playfield/coordinate services where useful

5) Logical Gameplay Systems
   - MazeGrid
   - dynamic gate state
   - collectible placement / pickup rules
   - score / multiplier / bonus logic
   - lives and death flow
   - word progression rules for SPECIAL / EXTRA
   - enemy target / movement logic

6) Rendering / UI
   - HUD
   - title/game over/high-score screens
   - animations
   - sprite flipping / visual offsets
   - temporary pickup popups
   - debug overlays

## 3. Target Folder Structure

Some files below already exist, others are future-oriented.

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
├─ collectibles_reverse_engineering.md
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
│  ├─ LevelCoordinateSystem.cs
│  ├─ LevelGateRuntime.cs
│  ├─ CollectibleFieldRuntime.cs
│  ├─ CollectiblePickupPopupView.cs
│  ├─ RotatingGateView.cs
│  └─ Collectible.cs
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
│  │  ├─ CollectibleColorCycle.cs
│  │  ├─ CollectibleAnchorFamily.cs
│  │  ├─ CollectibleAnchorFamilies.cs
│  │  ├─ CollectibleLayoutFile.cs
│  │  ├─ CollectibleLoader.cs
│  │  ├─ CollectiblePlacement.cs
│  │  ├─ CollectibleSpawnPlan.cs
│  │  ├─ CollectibleSpawnPlanner.cs
│  │  ├─ CollectiblePickupPopupState.cs
│  │  ├─ CollectibleRuntimeState.cs
│  │  ├─ CollectibleField.cs
│  │  └─ CollectiblePickupResult.cs
│  ├─ scoring/
│  │  ├─ ScoreState.cs
│  │  ├─ HeartMultiplierState.cs
│  │  ├─ CollectibleScoreCalculation.cs
│  │  ├─ CollectibleScoreService.cs
│  │  ├─ BonusRules.cs
│  │  └─ HighScoreEntry.cs
│  ├─ words/
│  │  ├─ WordProgressState.cs
│  │  └─ LetterProgressRules.cs
│  ├─ session/
│  │  ├─ StageDefinition.cs
│  │  ├─ StageFlowController.cs
│  │  └─ LifeState.cs
│  ├─ PlayfieldCollisionResolver.cs
│  ├─ PlayfieldStepKind.cs
│  └─ PlayfieldStepResult.cs
├─ ui/
│  ├─ Hud.cs
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
- instantiate or connect the HUD
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
- collected SPECIAL / EXTRA letter state
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

Current temporary implementation note:
- Level currently owns ScoreState and HeartMultiplierState as a prototype-level bridge
- these should later move into GameSession or a GameplayScreen-owned session model when screen flow exists

## 6. Level Architecture

### 6.1 Level scene role

Level represents one active gameplay board.

Current role:
- coordinate the active board runtime
- own the fixed simulation tick for the active board
- create and connect the maze, gate runtime, collectible field and collision resolver
- integrate the current HUD and pickup popup
- expose a small public integration surface for actors
- initialize the player after board systems exist
- keep editor previews working

Level should remain the public integration point for:
- logical cell <-> arcade-pixel conversion
- gate pivot <-> arcade-pixel conversion
- arcade-pixel <-> scene-space conversion
- active board objects belonging to the level
- playfield step evaluation
- gate push requests
- collectible pickup requests
- temporary board-level pause states, such as pickup popups or death sequences

But Level should not own all implementation details directly.
The current direction is to keep Level as a coordinator while delegating specialized behavior.

### 6.2 Level node structure target

```text
Level (Node2D)
├─ MazeBackground or Maze (Sprite2D)
├─ Gates (Node2D)
│  └─ RotatingGate instances
├─ Collectibles (Node2D)
│  ├─ flower / heart / letter / skull instances
│  └─ future bonus item instances when useful
├─ Actors (Node2D)
│  ├─ Player
│  └─ Enemy instances
├─ Effects (Node2D)
│  └─ pickup / death / score effect nodes
└─ Hud or HudAnchor (CanvasLayer or screen-owned UI reference)
```

Notes:
- the static maze background remains a Sprite2D
- moving / interactive objects should remain separate from the static background
- gate view instances can be pre-placed in Level.tscn and converted into a separate runtime gate system during level initialization
- the current collectible direction is to spawn a base flower layout first, then replace selected cells with special collectibles
- temporary pickup popups are high-level gameplay effects, not literal emulation of sprite RAM

### 6.3 Current Level helper classes

The current implementation already delegates the major Level sub-responsibilities:

```text
LevelCoordinateSystem
- owns coordinate conversion math

PlayfieldCollisionResolver
- combines static maze legality with dynamic gate blocking

CollectibleFieldRuntime
- owns spawned collectible views and the logical-cell lookup

LevelGateRuntime
- owns placed gate views, GateSystem creation, gate ticking, pushes and view sync

CollectibleScoreService
- computes collectible base score and final score delta

CollectiblePickupPopupState
- tracks the short 30-tick heart / letter pickup pause state

CollectiblePickupPopupView
- renders the temporary score / multiplier popup
```

This makes Level closer to a coordinator and reduces the risk of a monolithic gameplay script.

## 7. Player Architecture

The player movement subsystem is currently the most advanced subsystem in the codebase.

Implemented pieces:
- PlayerController
- PlayerDebugOverlay
- PlayerInputState
- PlayerMovementMotor
- PlayerMovementTuning
- PlayerMovementStepResult
- PlayerMovementSegment
- PlayerTurnWindowResolver
- PlayerTurnWindowMaps
- PlayerTurnWindowDecision
- PlayerTurnPath
- PlayerTurnAssistFlags
- PlayerMovementDebugTrace

Current responsibility split:

PlayerController:
- handles player input, player movement ticks, sprite facing, render offset, debug overlay updates and collectible pickup checks
- no longer owns the global fixed tick accumulator
- exposes AdvanceOneSimulationTick() so Level can drive the player from the board-level tick
- exposes SetGameplaySpriteVisible(...) so Level can hide the player during popup / future death states without destroying gameplay state

PlayerInputState:
- resolves intended movement direction using last-pressed-wins input policy

PlayerMovementMotor:
- owns gameplay movement state and applies one arcade-style movement tick

PlayerTurnWindowMaps:
- generates available player turn lanes from the logical maze

PlayerTurnWindowResolver:
- applies arcade-style pixel-window policy around generated lanes and chooses high-level turn paths

PlayerMovementStepResult / PlayerMovementSegment:
- expose the actual movement path completed during a tick

PlayerMovementTuning:
- centralizes movement constants

PlayerDebugOverlay:
- draws optional player debug visuals above the playfield

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
- same Level-owned fixed tick structure

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
- LevelGateRuntime
  - bridges placed gate views and runtime gate state
  - builds GateSystem from scene-authored gates
  - advances gate timers
  - accepts gate push requests
  - syncs view visuals from runtime state
- GateSystem
  - owns all runtime gate states of the active level
- RotatingGateRuntimeState
  - represents one mutable runtime gate
- Gate-related enums and tuning types
  - represent stable orientation, logical blocking axis, turning state, contact half, etc.

Level / Maze integration:
- gates influence movement legality
- the static MazeGrid remains the base maze
- PlayfieldCollisionResolver applies dynamic gate state as an additional movement constraint on top of the static maze
- Level advances gate timers from the board-level simulation tick

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

The project now has a stronger collectible foundation.

Current implemented direction:
- base flower layout loaded from data/collectibles_layout.json
- one collectible or empty cell per logical maze cell
- CollectibleFieldRuntime spawns a visible flower field from that base layout
- start-of-level planner selects letters, hearts, and skulls using anchor families and random draws
- selected special collectibles replace some already spawned flowers
- player pickup removes the collectible view from the runtime lookup
- pickup timing follows all movement segments reported by the player motor
- CollectiblePickupResult returns semantic pickup data to Level
- CollectibleColorCycle drives the global heart / letter color cycle
- CollectibleScoreService calculates current score contribution
- CollectiblePickupPopupState and CollectiblePickupPopupView handle the temporary heart / letter popup and short freeze

This keeps a useful separation between:
- static base layout data
- start-of-level special placement logic
- runtime view lookup / removal
- current color classification
- score calculation
- visual pickup popup
- future pickup consequences such as death and SPECIAL / EXTRA progression

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
- temporary pickup effects

Likely long-term architecture:
- base collectible placement data
- collectible spawn planning
- collectible runtime state
- collectible field / lookup
- pickup result model
- scoring and word-progression rules
- bonus item appearance logic
- pickup / death / score visual effects

Important:
The collectible system should remain driven by logical maze cells, not by free-form scene-space placement.

## 11. Scoring, Multiplier, Lives, and Word Progression

The current prototype now has a small scoring foundation:

```text
ScoreState
- current score value

HeartMultiplierState
- current blue-heart multiplier step
- multiplier values x1 / x2 / x3 / x5

CollectibleScoreService
- base score and final score calculation for flowers, hearts, and letters

CollectibleScoreCalculation
- score calculation result object
```

Current implemented rules:
- flower = 10 × current multiplier
- blue heart / letter = 100 × current multiplier
- yellow heart / letter = 300 × current multiplier
- red heart / letter = 800 × current multiplier
- blue hearts advance the multiplier after their own score is computed

Future session architecture:
- ScoreState should move from Level to GameSession
- HeartMultiplierState may either reset per level or live in the stage/session state depending on the final arcade behavior being modeled
- lives should be owned by GameSession or a session-level LifeState
- EXTRA should call into the life/session system to award an extra life
- SPECIAL should call into the session/screen-flow system to award a credit/free game or remake-equivalent reward

Future word progression architecture:

```text
WordProgressState
- tracks collected SPECIAL letters
- tracks collected EXTRA letters

LetterProgressRules
- determines whether a collected letter can progress SPECIAL or EXTRA
- applies red/yellow/blue color rules
- reports completed words
```

## 12. Pickup Popup / Temporary Freeze Architecture

The reverse-engineered arcade behavior includes a short freeze and temporary score / multiplier popup when collecting hearts and letters.

Current remake implementation:
- Level starts the popup after a scored heart / letter pickup
- CollectiblePickupPopupState tracks the active popup and its 30-tick duration
- CollectiblePickupPopupView renders the popup text
- Level hides the player sprite while the popup is active
- Level freezes normal board simulation while the popup is active
- after the popup, Level clears the popup view and restores the player sprite

Long-term direction:
- this should remain a high-level gameplay state, not a literal reproduction of temporary sprite RAM
- enemy movement and other future timers should also pause while this state is active
- death and stage-clear transitions can use a similar board-level temporary state pattern

## 13. Playfield Collision Architecture

PlayfieldCollisionResolver is the current bridge between the static maze and dynamic gates.

Responsibilities:
- ask MazeGrid to evaluate the static movement step
- detect gate blocking at probe level
- detect gate blocking when crossing logical-cell boundaries
- return a PlayfieldStepResult indicating:
  - allowed
  - blocked by fixed wall
  - blocked by gate

It should remain independent of player-specific turn logic.
Players and future enemies can ask the playfield whether a step is legal without duplicating static maze + gate overlay checks.

## 14. HUD / UI Architecture

Hud should eventually represent the gameplay HUD.

Current implemented HUD:
- Hud is currently a CanvasLayer inside Level.tscn
- Hud.cs finds ScoreLabel and updates the score text
- layout and visual styling are controlled in the Godot editor

Expected future HUD responsibilities:
- display current score
- display remaining lives
- display multiplier, if desired
- display stage / bonus information
- display SPECIAL / EXTRA progress
- display top score / credits / arcade-style labels if desired

HUD is not the long-term owner of session data.
It should observe GameSession and/or GameplayScreen.

Debug overlays should remain separate from normal HUD.
PlayerDebugOverlay is currently a small actor-specific debug helper, not a final HUD system.

## 15. Logical Maze Architecture

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

## 16. Coordinate System Design

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
- popup / effect placement

Why this matters:
- gameplay logic should stay independent of scene-space float rendering
- reverse-engineering findings are naturally expressed in arcade-pixel terms
- visual offsets should not corrupt gameplay coordinates

Current implementation:
- LevelCoordinateSystem owns the conversion math
- Level exposes wrapper methods so gameplay systems do not need to know the concrete helper
- PlayerTurnWindowMaps generates turn-lane candidates from MazeGrid
- PlayerTurnWindowResolver handles original-style mirrored Y conversion and pixel-window policy locally
- PlayerDebugOverlay formats player debug coordinates separately from normal gameplay conversion

## 17. Current Implemented Foundation

The following part of the target architecture is already implemented now:

- Main scene
- Level scene
- RotatingGate scene
- Collectible scene
- Player scene
- basic Hud inside Level
- logical maze loading from JSON
- base collectible layout loading from JSON
- MazeGrid / MazeCell / WallFlags / MazeLoader
- LevelCoordinateSystem
- PlayfieldCollisionResolver
- CollectibleFieldRuntime
- CollectibleColorCycle
- CollectiblePickupResult
- CollectiblePickupPopupState
- CollectiblePickupPopupView
- ScoreState
- HeartMultiplierState
- CollectibleScoreService
- CollectibleScoreCalculation
- LevelGateRuntime
- collectible layout loading and flower field spawning
- start-of-level special collectible placement planning
- heart / letter visual color cycling
- flower scoring
- heart / letter scoring
- blue-heart score multiplier advancement
- temporary heart / letter score popup with short freeze and player hide/restore
- PlayerController
- PlayerDebugOverlay
- PlayerInputState
- PlayerMovementMotor
- PlayerMovementTuning
- PlayerMovementStepResult
- PlayerMovementSegment
- PlayerTurnWindowResolver
- PlayerTurnWindowMaps
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
- Level-owned fixed simulation tick
- generated player turn-lane maps from the logical maze
- assisted player turn movement
- dynamic rotating gate legality
- gate push resolution and turning visuals
- movement segment reporting for collectible pickup

For the detailed current state, see:
- current_implementation.md

## 18. Main Systems Still To Implement

The largest remaining systems are:

- TitleScreen
- GameplayScreen
- GameOverScreen
- HighScoreScreen
- GameSession
- lives / life-loss state
- skull lethality
- player death / respawn flow
- SPECIAL / EXTRA word progression
- EXTRA extra-life reward
- SPECIAL free-credit / free-game behavior or remake equivalent
- EnemyController and enemy runtime logic
- enemy AI / movement helpers
- enemy interaction with rotating gates
- bonus vegetables
- vegetable-based enemy freeze
- score service migration to session-level ownership
- high-score persistence
- full gameplay HUD
- stage progression / stage flow controller
- automated regression scenarios for movement-sensitive behavior

## 19. Architectural Guiding Principles

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
10) Avoid prematurely generalizing player movement into a shared actor motor before enemy behavior is understood
11) Prefer high-level gameplay states for pauses, popups, death and stage transitions instead of literal RAM-layout emulation

## 20. Current Architectural Direction

The player movement, level runtime, rotating gates, and early collectible scoring foundation are now stable enough to build on.

The next major architectural expansions should happen around:
- documenting and protecting movement behavior with regression scenarios
- lives and player death flow
- skull lethality
- SPECIAL / EXTRA word progression
- HUD expansion for lives and word progress
- GameSession / screen flow extraction when the level needs persistent state across screens and stages
- enemy movement and AI

Future refactoring candidates should be driven by new gameplay systems rather than by abstract cleanup alone.
The major current Level extractions have already been done:
- coordinate system
- playfield collision resolver
- collectible field runtime
- gate runtime
- scoring calculation
- pickup popup state / view

## 21. Summary

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
- lives and death flow
- HUD and other UI

The project already has a solid movement, maze, rotating-gate, coordinate, collision, and collectible/scoring foundation.

The most important architectural shift since the previous version of this document is that Level now owns the board-level fixed simulation tick and coordinates more gameplay states:
- coordinate conversion is isolated
- playfield collision is isolated
- gate runtime is isolated
- collectible runtime is isolated
- scoring calculation is isolated
- pickup popup state and view are isolated
- player movement is modular and stable

The next work should build gameplay systems on top of this foundation rather than keep reshaping it without a concrete need.
