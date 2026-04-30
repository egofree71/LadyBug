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
It may evolve when reverse-engineering results change the global structure, but it is not intended to be updated for every small implementation detail.

## 1. Architecture Goal

The goal is not only to reproduce Lady Bug visually.
The goal is to build a structure that supports:

- faithful arcade movement
- enemies and their AI / path logic
- rotating gates
- collectibles and bonus items
- score / lives / stage progression
- player death and respawn flow
- SPECIAL / EXTRA word progression
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
   - credits / free-game state if the arcade behavior is modeled
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
   - central vegetable bonus runtime
   - maze border enemy-release timer
   - pickup popup state and view
   - level-clear / stage-transition temporary state
   - player death sequence coordination
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
   - maze border enemy-release clock
   - enemy target / movement logic

6) Rendering / UI
   - HUD
   - title/game over/high-score screens
   - animations
   - sprite flipping / visual offsets
   - temporary pickup popups
   - player death sprite sequence
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
│     ├─ maze_border_timer_tiles.png
│     └─ vegetables.png
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
│  ├─ MazeBorderTimer.tscn
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
│  ├─ Level.VegetableBonus.cs
│  ├─ LevelCoordinateSystem.cs
│  ├─ LevelGateRuntime.cs
│  ├─ CollectibleFieldRuntime.cs
│  ├─ CollectiblePickupPopupView.cs
│  ├─ LevelTransitionOverlay.cs
│  ├─ MazeBorderTimerView.cs
│  ├─ RotatingGateView.cs
│  ├─ Collectible.cs
│  ├─ VegetableBonusRuntime.cs
│  └─ VegetableBonusView.cs
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
│  ├─ gates/
│  ├─ collectibles/
│  ├─ enemies/
│  │  └─ EnemyReleaseBorderTimer.cs
│  ├─ scoring/
│  ├─ player/
│  ├─ session/
│  ├─ PlayfieldCollisionResolver.cs
│  ├─ PlayfieldStepKind.cs
│  └─ PlayfieldStepResult.cs
├─ ui/
│  ├─ Hud.cs
│  └─ DebugOverlay.cs
└─ autoload/
   └─ GameSession.cs
```

Important current deviation:
- `WordProgressState` currently lives in `scripts/gameplay/collectibles/` because it is directly tied to collectible letter pickup effects.
- A future refactor may move it to `scripts/gameplay/words/` or keep it under collectibles if that remains clearer.
- `PlayerLifeState` currently lives in `scripts/gameplay/player/`; a future GameSession may own lives instead.

## 4. Main Application Flow

### 4.1 Main

**Scene:**
- scenes/Main.tscn

**Role:**
- global root scene of the whole application
- owner of screen switching in the final architecture

**Responsibilities:**
- start on title screen
- switch to gameplay
- switch to game over / high score screens
- return to title screen when needed

Main should not contain gameplay logic.

Current implementation:
- Main still instantiates Level directly
- screen flow is not implemented yet

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
- current heart multiplier state if it should persist outside a level
- high score table
- current game state
- credit / free-game state if the remake models it

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
- Level currently owns ScoreState, HeartMultiplierState, WordProgressState, PlayerLifeState, a special-award placeholder counter, the current level number, a minimal game-over flag, and the current prototype level-transition state
- this is acceptable while the project has no screen flow
- these should later move into GameSession or a GameplayScreen-owned session model when title/gameplay/game-over flow exists

## 6. Level Architecture

### 6.1 Level scene role

Level represents one active gameplay board.

Current role:
- coordinate the active board runtime
- own the fixed simulation tick for the active board
- create and connect the maze, gate runtime, collectible field and collision resolver
- integrate the current HUD, pickup popup, level-transition overlay, and player death state
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
- temporary board-level pause states, such as pickup popups, level transitions or death sequences

But Level should not own all implementation details directly.
The current direction is to keep Level as a coordinator while delegating specialized behavior.

### 6.2 Level node structure target

```text
Level (Node2D)
├─ MazeBackground or Maze (Sprite2D)
├─ MazeBorderTimer (Node2D)
├─ Gates (Node2D)
│  └─ RotatingGate instances
├─ Collectibles (Node2D)
│  └─ flower / heart / letter / skull instances
├─ VegetableBonusRuntime (Node2D, runtime-created)
│  └─ central vegetable bonus visual
├─ Actors (Node2D)
│  ├─ Player
│  └─ Enemy instances
├─ Effects (Node2D)
│  └─ pickup / death / score effect nodes
├─ Hud or HudAnchor (CanvasLayer or screen-owned UI reference)
└─ TransitionOverlay / IntermissionOverlay (CanvasLayer, runtime-created or screen-owned)
```

Current implementation note:
- the HUD is currently a CanvasLayer directly inside Level.tscn
- the maze is shifted down in Level.tscn to make room for the upper HUD strip
- the static maze background remains a Sprite2D
- the maze-border timer is a separate Sprite2D-based visual layer around the static maze background
- moving / interactive objects remain separate from the static background
- gate view instances are pre-placed in Level.tscn and converted into a separate runtime gate system during initialization
- the current collectible direction is to spawn a base flower layout first, then replace selected cells with special collectibles
- temporary pickup popups, level transitions, vegetable freeze and player death are high-level gameplay states, not literal emulation of sprite RAM
- the current vegetable bonus is runtime-created by a partial Level helper and can later be integrated more directly into Level or a board-item runtime
- the current PART transition overlay is runtime-created by Level and can later become a screen-flow / intermission scene

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

MazeBorderTimerView
- owns the generated border-timer sprites and maps timer state to white / green visuals

EnemyReleaseBorderTimer
- owns the countdown / reload timing for the future enemy-release border clock

CollectibleScoreService
- computes collectible base score and final score delta

WordProgressState
- tracks SPECIAL / EXTRA letter progress and applies color-based word rules

PlayerLifeState
- tracks remaining lives and extra-life awards

PlayerDeathSequenceState
- provides tick-accurate visual state for the player death sequence

CollectiblePickupPopupState
- tracks the short 30-tick heart / letter pickup pause state

CollectiblePickupPopupView
- renders the temporary score / multiplier popup

VegetableBonusCatalog
- maps the current level to vegetable frame and fixed score

VegetableBonusRuntime
- owns central vegetable appearance, pickup detection, score award and enemy movement freeze

VegetableBonusView
- renders the current vegetable frame from assets/sprites/props/vegetables.png

Level.VegetableBonus
- current direct-copy integration bridge that installs VegetableBonusRuntime and exposes narrow Level support methods

LevelTransitionOverlay
- renders the temporary between-level PART screen
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
- PlayerLifeState
- PlayerDeathState
- PlayerDeathVisualSheet
- PlayerDeathSequenceState

Current responsibility split:

PlayerController:
- handles player input, player movement ticks, sprite facing, render offset, debug overlay updates and collectible pickup checks
- no longer owns the global fixed tick accumulator
- exposes AdvanceOneSimulationTick() so Level can drive the player from the board-level tick
- exposes SetGameplaySpriteVisible(...) so Level can hide the player during pickup popup states
- starts and advances the visual player death sequence through a separate death sprite
- respawns the player at PlayerStartCell after death when lives remain

PlayerInputState:
- resolves intended movement direction using last-pressed-wins input policy

PlayerMovementMotor:
- owns gameplay movement state and applies one arcade-style movement tick
- exposes the movement segments completed each tick
- supports resetting to the level start cell during respawn

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

PlayerDeathSequenceState:
- expresses the reverse-engineered death animation as deterministic tick state
- handles red shrink frames, ghost apparition frames, and ghost zigzag visual offsets

Future possible additions:
- PlayerAnimationState
- PlayerPickupHandler
- PlayerScoringHooks
- cleaner extraction of death visuals into a dedicated view class if PlayerController grows too much

The player should remain split into:
- orchestration
- movement rules
- input policy
- rendering / animation concerns
- pickup / score interactions
- death visual state

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
- same board-level temporary freeze states for pickup popups, death, and stage transitions

Important:
Enemy logic should probably reuse the same playfield-step legality model, but not necessarily the exact player movement rules.
Do not extract a generic ActorMovementMotor too early.
The player has special input and assisted-turn behavior that enemies may not share.


## 9. Maze Border Timer / Enemy Release Clock Architecture

The maze border timer is a board-level clock that visually communicates future enemy release.
It should remain separate from the collectible heart / letter color cycle.

Current implemented split:

```text
MazeBorderTimerView
- visual Node2D layer inside Level.tscn
- creates border tile Sprite2D nodes at runtime
- maps timer progress to white / green tile colors
- controls visual ordering, including the top-middle start point and clockwise progression

EnemyReleaseBorderTimer
- pure gameplay timing helper
- implements the countdown / reload model reverse-engineered from the arcade border timer
- exposes whether a visual step occurred
- exposes whether the green fill completed and an enemy should be released later
```

Current timing model:

```text
Level 1       -> one border step every 9 simulation ticks
Levels 2 to 4 -> one border step every 6 simulation ticks
Level 5+      -> one border step every 3 simulation ticks
```

Architectural rule:
- CollectibleColorCycle owns the 600-tick color mode for hearts and letters
- EnemyReleaseBorderTimer owns the maze-border countdown and future enemy-release cadence
- both advance from the Level-owned fixed tick, but they should not share logical state

Current Level integration:
- Level configures MazeBorderTimerView from the current level number
- Level advances MazeBorderTimerView as a normal board-level system
- completion calls into EnemyRuntime to release the next waiting enemy
- EnemyRuntime decides which queued enemy can be released and enforces the maximum active enemy rules
- the visual border timer resets according to the same high-level timing model after each release cycle
- pickup popups, player death, and stage transitions pause this timer with the rest of the board simulation

Expected future refinement:
- additional arcade traces may refine the exact visual phase semantics or release timing edge cases

## 10. Rotating Gate Architecture

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

## 11. Item / Collectible Architecture

The project now has a strong collectible foundation.

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
- the heart / letter color cycle remains separate from the maze-border enemy-release timer
- CollectibleScoreService calculates current score contribution
- WordProgressState applies SPECIAL / EXTRA letter effects
- HeartMultiplierState applies blue-heart multiplier effects
- PlayerLifeState supports EXTRA extra-life rewards
- skull pickup triggers the player death sequence
- CollectiblePickupPopupState and CollectiblePickupPopupView handle the temporary heart / letter popup and short freeze
- CollectibleFieldRuntime exposes whether any flower, heart or letter remains for level-clear checks
- skulls remain hazards and do not block level completion

This keeps a useful separation between:
- static base layout data
- start-of-level special placement logic
- runtime view lookup / removal
- current color classification
- score calculation
- word progress
- multiplier progress
- life and death consequences
- visual pickup popup
- pickup consequences such as stage clear and bonus vegetable effects

Current item families:
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
- level-clear participation

Current / likely long-term architecture:
- base collectible placement data
- collectible spawn planning
- collectible runtime state
- collectible field / lookup
- pickup result model
- scoring and word-progression rules
- bonus item appearance logic
- pickup / death / score visual effects
- stage transition hooks

Current vegetable architecture:
- the central vegetable is deliberately separate from CollectibleFieldRuntime
- it appears only when all four enemies are active in the maze
- it is positioned at the enemy lair visual anchor, not at the normal player / collectible anchor
- it gives score immediately, without popup and without heart multiplier
- it temporarily freezes enemy movement while keeping enemy collisions active
- it does not participate in level-clear progress

Important:
The collectible and item systems should remain driven by logical maze cells / arcade-pixel anchors, not by free-form scene-space placement.

## 12. Scoring, Multiplier, Lives, and Word Progression

The current prototype now has a useful scoring / bonus foundation:

```text
ScoreState
- current score value

HeartMultiplierState
- current blue-heart multiplier step
- multiplier values x1 / x2 / x3 / x5

CollectibleScoreService
- base score and final score calculation for flowers, hearts, and letters

VegetableBonusCatalog
- fixed vegetable score by level, from 1000 to 9500
- score is not multiplied by the heart multiplier

CollectibleScoreCalculation
- score calculation result object

WordProgressState
- SPECIAL and EXTRA progress
- color-based letter progression rules

PlayerLifeState
- current lives
- life loss and extra-life awards
```

Current implemented rules:
- flower = 10 × current multiplier
- blue heart / letter = 100 × current multiplier
- yellow heart / letter = 300 × current multiplier
- red heart / letter = 800 × current multiplier
- blue hearts advance the multiplier after their own score is computed
- red valid letters progress SPECIAL
- yellow valid letters progress EXTRA
- blue letters are score-only
- EXTRA completion awards one extra life
- SPECIAL completion currently increments a placeholder free-game award counter
- skulls give no score and trigger death
- vegetables give a fixed level-based bonus score without using the heart multiplier

Future session architecture:
- ScoreState should move from Level to GameSession
- HeartMultiplierState may either reset per level or live in the stage/session state depending on the final arcade behavior being modeled
- WordProgressState should probably move from Level to GameSession once stages and screen flow exist
- lives should be owned by GameSession or a session-level LifeState
- EXTRA should call into the life/session system to award an extra life
- SPECIAL should call into the session/screen-flow system to award a credit/free game or remake-equivalent reward
- stage transitions should be owned above Level or coordinated through explicit Level events

## 13. Pickup Popup / Temporary Freeze Architecture

The reverse-engineered arcade behavior includes a short freeze and temporary score / multiplier popup when collecting hearts and letters.

Current remake implementation:
- Level starts the popup after a scored heart / letter pickup
- CollectiblePickupPopupState tracks the active popup and its 30-tick duration
- CollectiblePickupPopupView renders the popup text
- Level hides the player sprite while the popup is active
- Level freezes normal board simulation while the popup is active
- after the popup, Level clears the popup view and restores the player sprite
- if that pickup cleared the board, Level starts the PART transition screen after the popup finishes

Long-term direction:
- this should remain a high-level gameplay state, not a literal reproduction of temporary sprite RAM
- enemy movement and other future timers should also pause while this state is active
- death, vegetable freeze and stage-clear transitions can use a similar board-level temporary state pattern
- vegetable pickup currently does not use the popup system; it awards score immediately and starts its own enemy-freeze state

### 13.1 Level completion and stage transition state

The project now has a first prototype of level completion and next-level transition.

Current completion rule:

```text
all flowers + hearts + letters consumed -> stage complete
skulls do not block stage completion
```

Current ownership is still inside Level:
- Level asks CollectibleFieldRuntime whether progression collectibles remain
- Level queues the next level when the board is cleared
- if a final heart / letter popup is active, Level waits for it to finish
- Level shows a simple LevelTransitionOverlay announcing the next PART number
- Level rebuilds board-local runtime state for the next level
- Level preserves prototype session-like state such as score, lives, word progress and multiplier

This is a practical intermediate architecture because Main still instantiates Level directly and there is no GameplayScreen / GameSession flow yet.

Long-term direction:
- Level should eventually emit a StageComplete event rather than own the whole transition policy
- GameplayScreen or GameSession should own the current stage number, session state and screen transitions
- LevelTransitionOverlay could become a dedicated intermission screen or a richer screen-owned overlay
- the current F1 next-level shortcut should remain debug-only or be removed from release builds

The current transition screen is intentionally simplified. It displays only the upcoming part number and does not yet reproduce the arcade screen that previews the upcoming collectible / vegetable information.

## 14. Player Death / Respawn Architecture

The player death path is now partially implemented.

Current remake implementation:
- skull pickup enters the death path
- Level decrements PlayerLifeState immediately
- Level freezes normal board simulation
- PlayerController hides the normal living sprite
- PlayerController shows a runtime death Sprite2D
- PlayerDeathSequenceState drives:
  - red shrink frames
  - ghost apparition frames
  - ghost zigzag upward movement
- after the sequence:
  - if lives remain, PlayerController resets movement state to PlayerStartCell
  - if no lives remain, Level enters a minimal game-over placeholder

Long-term direction:
- keep death as a board-level temporary gameplay state
- move life-loss and game-over decisions to GameSession or GameplayScreen later
- emit explicit events such as LifeLost, PlayerRespawned, GameOver, or StageComplete when the screen/session layer exists
- keep death visual timing isolated from movement rules

## 15. Playfield Collision Architecture

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

## 16. HUD / UI Architecture

Hud now represents the active gameplay HUD for the current Level prototype.

Current implemented HUD:
- Hud is a CanvasLayer inside Level.tscn
- Hud.cs finds and updates:
  - ScoreLabel
  - LivesLabel
  - SpecialWordLabel
  - ExtraWordLabel
  - MultipliersLabel
- ScoreLabel and LivesLabel are normal Label nodes
- SPECIAL, EXTRA, and x2/x3/x5 use RichTextLabel so individual letters or multiplier entries can be colored independently
- layout and visual styling are controlled in Level.tscn

Current display rules:
- inactive SPECIAL letters are grey
- active SPECIAL letters are red
- inactive EXTRA letters are grey
- active EXTRA letters are yellow
- inactive multiplier entries are grey
- active multiplier entries are blue
- lives are shown at bottom-left
- score is shown at bottom-right

Expected future HUD responsibilities:
- display top score
- display credits / free-game state if desired
- display stage / bonus information
- display game-over / pause / title overlays if not handled by separate screens
- observe GameSession or GameplayScreen rather than Level-owned prototype state

HUD is not the long-term owner of session data.
It should observe GameSession and/or GameplayScreen.

Debug overlays should remain separate from normal HUD.
PlayerDebugOverlay is currently a small actor-specific debug helper, not a final HUD system.

## 17. Logical Maze Architecture

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

## 18. Coordinate System Design

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
- death visual offsets
- HUD layout boundaries when the maze is moved in the scene

Why this matters:
- gameplay logic should stay independent of scene-space float rendering
- reverse-engineering findings are naturally expressed in arcade-pixel terms
- visual offsets should not corrupt gameplay coordinates

Current implementation:
- LevelCoordinateSystem owns the conversion math
- Level exposes wrapper methods so gameplay systems do not need to know the concrete helper
- PlayerTurnWindowMaps generates turn-lane candidates from MazeGrid
- PlayerTurnWindowResolver handles original-style mirrored Y conversion and pixel-window policy locally
- PlayerDeathSequenceState stores offsets in arcade-pixel terms and PlayerController converts them to scene-space deltas
- PlayerDebugOverlay formats player debug coordinates separately from normal gameplay conversion

## 19. Current Implemented Foundation

The following part of the target architecture is already implemented now:

- Main scene
- Level scene
- RotatingGate scene
- Collectible scene
- Player scene
- gameplay Hud inside Level
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
- VegetableBonusCatalog
- VegetableBonusRuntime
- VegetableBonusView
- Level.VegetableBonus integration bridge
- LevelTransitionOverlay
- MazeBorderTimerView
- EnemyReleaseBorderTimer
- EnemyRuntime / enemy movement runtime helpers
- level-aware enemy visual catalog for enemy_level1 through enemy_level8
- ScoreState
- HeartMultiplierState
- CollectibleScoreService
- CollectibleScoreCalculation
- WordProgressState
- PlayerLifeState
- PlayerDeathSequenceState
- PlayerDeathVisualSheet
- LevelGateRuntime
- collectible layout loading and flower field spawning
- start-of-level special collectible placement planning
- heart / letter visual color cycling
- flower scoring
- heart / letter scoring
- blue-heart score multiplier advancement
- SPECIAL / EXTRA word progress
- EXTRA extra-life reward
- SPECIAL placeholder award
- skull lethality
- central vegetable bonus appearance, scoring and enemy movement freeze
- frozen enemies remain fatal through unchanged collision flags
- player death sequence and respawn
- temporary heart / letter score popup with short freeze and player hide/restore
- board-clear detection when all flowers, hearts and letters are consumed
- simplified PART transition screen between levels
- Level-owned next-level board rebuild while preserving prototype session state
- HUD display for score, lives, SPECIAL, EXTRA, and multipliers
- maze-border timer visual layer
- reverse-engineered maze-border timer cadence by level
- Level-driven maze-border timer ticking and pause behavior
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

## 20. Main Systems Still To Implement

The largest remaining systems are:

- TitleScreen
- GameplayScreen
- GameOverScreen
- HighScoreScreen
- GameSession
- proper stage progression / stage flow controller
- arcade-accurate intermission screen with upcoming item / vegetable preview
- immediate next-level transition when SPECIAL or EXTRA is completed
- final SPECIAL free-credit / free-game behavior or remake equivalent
- further enemy AI / movement refinements toward arcade accuracy
- enemy interaction with rotating gates
- arcade-exact vegetable timing / freeze cadence if future traces justify it
- score / lives / word-progress migration to session-level ownership
- high-score persistence
- top score / credits / final arcade HUD elements
- automated regression scenarios for movement-sensitive behavior

## 21. Architectural Guiding Principles

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
10) Keep the collectible color cycle independent from the maze-border enemy-release timer
11) Avoid prematurely generalizing player movement into a shared actor motor before enemy behavior is understood
12) Prefer high-level gameplay states for pauses, popups, death and stage transitions instead of literal RAM-layout emulation
13) Move session-wide state out of Level when screen flow and stage transitions make that necessary

## 22. Current Architectural Direction

The player movement, level runtime, rotating gates, maze-border timer, collectible scoring, lives, death, HUD, and word-progress foundation are now stable enough to build on.

The next major architectural expansions should happen around:
- documenting and protecting movement behavior with regression scenarios
- moving level clear and stage transition flow out of Level into GameplayScreen / GameSession when screen flow exists
- deciding final SPECIAL reward behavior in a remake context
- GameSession / GameplayScreen extraction when state needs to persist cleanly across levels and screens
- refining the maze-border / enemy-release interaction where arcade traces justify it
- refining enemy movement and AI
- refining vegetable timing / arcade details only where testing justifies it

Future refactoring candidates should be driven by new gameplay systems rather than by abstract cleanup alone.
The major current Level extractions have already been done:
- coordinate system
- playfield collision resolver
- collectible field runtime
- vegetable bonus runtime
- gate runtime
- scoring calculation
- word progress state
- life state
- death sequence state
- pickup popup state / view
- HUD rendering
- maze-border timer rendering and timing

## 23. Summary

The final architecture is intended to support the whole game, not only the player movement subsystem.

It should ultimately contain:
- screen flow
- session state
- level runtime
- player
- enemies
- enemy-release border clock
- rotating gates
- collectibles and bonus systems
- scoring and high scores
- lives and death flow
- SPECIAL / EXTRA flow
- HUD and other UI

The project already has a solid movement, maze, rotating-gate, maze-border timer, coordinate, collision, collectible, vegetable bonus, scoring, HUD, lives, death, word-progress, enemy-runtime, and simplified level-transition foundation.

The most important architectural shift since the previous version of this document is that Level now coordinates more board-level temporary states and prototype session-like state:
- coordinate conversion is isolated
- playfield collision is isolated
- gate runtime is isolated
- collectible runtime is isolated
- vegetable bonus runtime is isolated
- scoring calculation is isolated
- word progress is isolated
- life state is isolated
- death visual timing is isolated
- pickup popup state and view are isolated
- level-transition overlay rendering is isolated
- maze-border timer visual and timing concerns are isolated
- HUD rendering is separated from gameplay state
- player movement is modular and stable

The next work should build gameplay systems on top of this foundation rather than keep reshaping it without a concrete need.
