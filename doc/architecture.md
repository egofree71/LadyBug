===============================================================================
PROJECT ARCHITECTURE
===============================================================================

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

Purpose of this document:
- describe the intended long-term architecture of the project
- show how all major game systems are expected to fit together
- keep a clear separation between:
  1) the final target architecture
  2) the currently implemented foundation

Important:
This document is intentionally more future-oriented than
current_implementation.md.

- current_implementation.md = what already exists in the codebase now
- architecture.md = the global target structure of the whole game

===============================================================================
1. ARCHITECTURE GOAL
===============================================================================

The goal is not only to reproduce Lady Bug visually.
The goal is to build a structure that supports:

- faithful arcade movement
- enemies and their AI / path logic
- rotating gates
- collectibles and bonus items
- score / lives / stage progression
- screen flow (title, gameplay, game over, high score)
- future reverse-engineering refinements without turning the codebase into a
  monolithic prototype

The architecture should remain:
- simple enough to understand
- modular enough to grow
- explicit about the difference between gameplay logic and rendering

===============================================================================
2. HIGH-LEVEL ARCHITECTURE
===============================================================================

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
   - PlayerController and its helpers
   - EnemyController and its helpers
   - future shared movement / maze helper services where useful

5) Logical Gameplay Systems
   - MazeGrid
   - dynamic gate state
   - item / pickup rules
   - score / bonus logic
   - enemy target / movement logic

6) Rendering / UI
   - HUD
   - title/game over/high-score screens
   - animations
   - sprite flipping / visual offsets

===============================================================================
3. TARGET FOLDER STRUCTURE
===============================================================================

The current repository does not contain all these files yet.
This section describes the intended long-term structure.

assets/
├─ images/
│  ├─ maze/
│  ├─ player/
│  ├─ enemies/
│  ├─ props/
│  └─ ui/
├─ audio/
│  ├─ music/
│  └─ sfx/
└─ fonts/

data/
├─ maze.json
├─ stage_config.json
├─ items.json
└─ high_scores.json

doc/
├─ architecture.md
├─ current_implementation.md
├─ player_movement.md
├─ enemy_movement.md
└─ gates.md

scenes/
├─ Main.tscn
├─ screens/
│  ├─ TitleScreen.tscn
│  ├─ GameplayScreen.tscn
│  ├─ GameOverScreen.tscn
│  └─ HighScoreScreen.tscn
├─ level/
│  └─ Level.tscn
├─ player/
│  └─ Player.tscn
├─ enemies/
│  ├─ Enemy.tscn
│  ├─ Soldier.tscn
│  ├─ Bulldozer.tscn
│  └─ OtherEnemyVariants.tscn
├─ props/
│  ├─ RotatingGate.tscn
│  ├─ Collectible.tscn
│  ├─ BonusVegetable.tscn
│  └─ BonusLetter.tscn
└─ ui/
   └─ Hud.tscn

scripts/
├─ Main.cs
├─ screens/
│  ├─ TitleScreen.cs
│  ├─ GameplayScreen.cs
│  ├─ GameOverScreen.cs
│  └─ HighScoreScreen.cs
├─ level/
│  └─ Level.cs
├─ actors/
│  ├─ PlayerController.cs
│  ├─ PlayerInputState.cs
│  ├─ PlayerMovementMotor.cs
│  ├─ PlayerMovementStepResult.cs
│  ├─ PlayerMovementTuning.cs
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
│  │  ├─ RotatingGateController.cs
│  │  ├─ RotatingGateState.cs
│  │  └─ GateInteractionResult.cs
│  ├─ items/
│  │  ├─ CollectibleController.cs
│  │  ├─ BonusVegetableController.cs
│  │  ├─ BonusLetterController.cs
│  │  └─ ItemSpawnLogic.cs
│  ├─ scoring/
│  │  ├─ ScoreService.cs
│  │  ├─ BonusRules.cs
│  │  └─ HighScoreEntry.cs
│  └─ session/
│     ├─ StageDefinition.cs
│     ├─ StageFlowController.cs
│     └─ LifeState.cs
├─ ui/
│  └─ HudController.cs
└─ autoload/
   └─ GameSession.cs

===============================================================================
4. MAIN APPLICATION FLOW
===============================================================================

-------------------------------------------------------------------------------
4.1 Main
-------------------------------------------------------------------------------

Scene:
- scenes/Main.tscn

Role:
- global root scene of the whole application
- owner of screen switching

Responsibilities:
- start on title screen
- switch to gameplay
- switch to game over / high score screens
- return to title screen when needed

Important:
Main should not contain gameplay logic.

-------------------------------------------------------------------------------
4.2 TitleScreen
-------------------------------------------------------------------------------

Role:
- title / attract screen

Responsibilities:
- show the game title
- show prompt to start
- optionally show a small animated demo or visual attract loop
- transition to gameplay

-------------------------------------------------------------------------------
4.3 GameplayScreen
-------------------------------------------------------------------------------

Role:
- container for one active gameplay session

Responsibilities:
- instantiate the current level
- instantiate the HUD
- connect level events with GameSession
- react to loss / stage complete / bonus flow
- transition to game over or next stage

-------------------------------------------------------------------------------
4.4 GameOverScreen
-------------------------------------------------------------------------------

Role:
- present game over state
- optionally allow entering initials / name for high score

Responsibilities:
- show final score
- store a new high-score entry if needed
- transition to high-score or title screen

-------------------------------------------------------------------------------
4.5 HighScoreScreen
-------------------------------------------------------------------------------

Role:
- display the saved high-score table

Responsibilities:
- load and display score entries
- return to title screen or attract loop

===============================================================================
5. GAME SESSION ARCHITECTURE
===============================================================================

The game needs a session-level model that lives above a single Level instance.

Expected global state:
- current score
- lives remaining
- current stage number
- number of collected letters / bonuses
- high score table
- current game state (title / gameplay / game over / high score)

Proposed location:
- autoload/GameSession.cs

GameSession should be responsible for:
- persistent session data during play
- transitions between stages
- saving / loading high scores
- exposing global state to screens and HUD

Level should NOT own long-term session state.
Level should only manage one active playfield runtime.

===============================================================================
6. LEVEL ARCHITECTURE
===============================================================================

-------------------------------------------------------------------------------
6.1 Level scene role
-------------------------------------------------------------------------------

Level is intended to represent one active gameplay board.

Responsibilities:
- load the logical maze
- expose coordinate conversion helpers
- instantiate and connect runtime actors
- own the active rotating gates of the level
- own the active collectibles / bonus items of the level
- expose the current runtime playfield state

Level should remain the source of truth for:
- logical cell <-> arcade-pixel conversion
- arcade-pixel <-> scene-space conversion
- active board objects belonging to the level

-------------------------------------------------------------------------------
6.2 Level node structure (target)
-------------------------------------------------------------------------------

Level (Node2D)
├─ MazeBackground (Sprite2D)
├─ Gates (Node2D)
│  └─ RotatingGate instances
├─ Items (Node2D)
│  ├─ Collectible instances
│  ├─ BonusVegetable instances
│  └─ BonusLetter instances
├─ Actors (Node2D)
│  ├─ Player
│  └─ Enemy instances
└─ Effects (Node2D)

Notes:
- the static maze background remains a Sprite2D
- moving / interactive objects should remain separate from the static background

===============================================================================
7. PLAYER ARCHITECTURE
===============================================================================

The player movement subsystem is already the most advanced subsystem in the codebase.

Long-term player architecture:

PlayerController
- orchestrates input, movement ticks and rendering

PlayerInputState
- resolves intended movement direction

PlayerMovementMotor
- applies movement rules per fixed tick

PlayerMovementStepResult
- describes tick outcomes

PlayerMovementTuning
- centralizes movement constants

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
- item / score interactions

===============================================================================
8. ENEMY ARCHITECTURE
===============================================================================

The final game will need one or more enemy systems that stay compatible with
the same maze and coordinate model as the player.

Expected enemy architecture:

EnemyController
- high-level orchestration of one enemy

EnemyMovementMotor
- effective movement logic on the maze

EnemyAiState
- target choice / chase logic / patrol logic / home logic

EnemyMovementStepResult
- structured tick result, similar in spirit to the player motor

Possible shared concepts:
- same arcade-pixel coordinate system
- same maze validation helper
- same gate interaction framework
- similar tick-based movement structure

Important:
Enemy logic should probably reuse the same maze-step legality model, but not
necessarily the exact same player movement rules.

===============================================================================
9. ROTATING GATE ARCHITECTURE
===============================================================================

Rotating gates are one of the most important missing gameplay systems.

They should not be treated as a visual-only feature.

Expected architecture:

RotatingGateController
- own the gate's current state and animation

RotatingGateState
- represent orientation and movement-blocking consequences

GateInteractionResult
- describe what happens when player or enemy interacts with a gate

Level / Maze integration:
- gates should influence movement legality
- the static MazeGrid should remain the base maze
- dynamic gate state should be applied as an additional movement constraint
  on top of the static maze

In practice, long-term movement legality should become something like:

    static maze legality
    + lane / alignment legality
    + dynamic rotating gate legality

This is the big next gameplay architecture step after the current player work.

===============================================================================
10. ITEM / COLLECTIBLE ARCHITECTURE
===============================================================================

The final game should include several categories of items and pickups.

Expected item families:
- standard collectibles
- flowers / hearts / letters if relevant to game behavior
- bonus vegetables
- special score items

Expected responsibilities:
- placement / visibility
- pickup rules
- score contribution
- bonus progression

Possible architecture:

CollectibleController
- base logic for common collectible behavior

BonusVegetableController
- logic specific to bonus vegetable appearance / lifetime / score

BonusLetterController
- logic specific to letter collection or bonus progression

ItemSpawnLogic
- stage-specific appearance and timing rules

===============================================================================
11. HUD / UI ARCHITECTURE
===============================================================================

HudController should eventually represent the gameplay HUD.

Expected HUD responsibilities:
- display current score
- display remaining lives
- display stage / bonus information
- optionally display letters / collected bonus state

Important:
HUD is not the owner of session data.
It should observe GameSession and/or GameplayScreen.

===============================================================================
12. LOGICAL MAZE ARCHITECTURE
===============================================================================

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

Dynamic gameplay systems should be added around it, not by turning MazeGrid
into a giant all-purpose gameplay object.

In other words:
- MazeGrid = static maze truth
- rotating gates = dynamic movement overlay
- actors = movement clients
- Level = runtime coordinator

===============================================================================
13. COORDINATE SYSTEM DESIGN
===============================================================================

A central architectural principle of the whole project is the separation between:

1) logical cell coordinates
2) gameplay arcade-pixel coordinates
3) scene-space rendering coordinates

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

===============================================================================
14. CURRENT IMPLEMENTED FOUNDATION
===============================================================================

The following part of the target architecture is already implemented now:

- Main scene
- Level scene
- Player scene
- logical maze loading from JSON
- MazeGrid / MazeCell / WallFlags / MazeLoader
- PlayerController
- PlayerInputState
- PlayerMovementMotor
- PlayerMovementStepResult
- PlayerMovementTuning
- MazeStepResult
- pixel-step maze validation
- fixed tick player movement
- lane snap and conservative recentering

This section is intentionally short.
For the detailed current state, see:
- current_implementation.md

===============================================================================
15. MAIN SYSTEMS STILL TO IMPLEMENT
===============================================================================

The largest remaining systems are:

- TitleScreen
- GameplayScreen
- GameOverScreen
- HighScoreScreen
- GameSession
- EnemyController and enemy runtime logic
- enemy AI / movement helpers
- rotating gate runtime logic
- dynamic gate interaction with movement legality
- collectibles / letters / bonus vegetables
- score service and high-score persistence
- gameplay HUD
- stage progression / stage flow controller

===============================================================================
16. ARCHITECTURAL GUIDING PRINCIPLES
===============================================================================

The architecture should continue to follow these rules:

1) Keep scenes simple and readable
2) Keep gameplay state separated from rendering state
3) Keep static maze logic separated from dynamic gate logic
4) Keep controllers as orchestrators when possible
5) Prefer small helper classes over giant monolithic scripts
6) Add systems only when they have a clear responsibility
7) Stay close to reverse-engineered arcade behavior where it matters

===============================================================================
17. SUMMARY
===============================================================================

The final architecture is intended to support the whole game, not only the
player movement subsystem.

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

The project already has a solid movement and maze foundation.
The next major architectural expansion should now happen around:
- rotating gates
- enemies
- session / screen flow
