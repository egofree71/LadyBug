===============================================================================
PROJECT ARCHITECTURE
===============================================================================

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

Goal:
- recreate the arcade game Lady Bug
- keep the project simple enough to understand
- already prepare a structure that can scale cleanly
- separate application flow, gameplay, maze logic, actors, and UI

The architecture is not just exploratory.
Some systems are still missing or provisional, but the folder structure and the
main responsibilities already reflect the intended long-term organization.

===============================================================================
1. FOLDER STRUCTURE
===============================================================================

assets/
├─ sprites/
│  ├─ player/
│  ├─ enemies/
│  ├─ props/
│  └─ ui/
├─ tilesets/
├─ audio/
└─ fonts/

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
│  └─ Enemy.tscn
├─ props/
│  ├─ Gate.tscn
│  ├─ Collectible.tscn
│  └─ BonusVegetable.tscn
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
├─ gameplay/
│  ├─ maze/
│  │  ├─ WallFlags.cs
│  │  ├─ MazeCell.cs
│  │  ├─ MazeDataFile.cs
│  │  ├─ MazeGrid.cs
│  │  └─ MazeLoader.cs
│  ├─ actors/
│  │  ├─ GridActor.cs
│  │  ├─ PlayerController.cs
│  │  └─ EnemyController.cs
│  └─ props/
│     ├─ Gate.cs
│     ├─ Collectible.cs
│     └─ BonusVegetable.cs
├─ ui/
│  └─ Hud.cs
└─ autoload/
   └─ GameSession.cs

docs/
├─ architecture.md
└─ movement.md

data/
└─ maze.json

===============================================================================
2. GLOBAL ARCHITECTURE INTENT
===============================================================================

The project is organized into several layers:

1) Application / Screen flow
   - Main
   - TitleScreen
   - GameplayScreen
   - GameOverScreen
   - HighScoreScreen

2) Gameplay / Level
   - Level
   - Player
   - Enemy
   - Gates
   - Collectibles
   - HUD

3) Logical gameplay systems
   - MazeGrid
   - MazeCell
   - WallFlags
   - MazeLoader
   - MazeDataFile
   - GridActor
   - GameSession

The goal is:
- not a one-scene / one-script prototype
- not an overly complex architecture either
- but a clean base that can evolve toward a faithful arcade remake

===============================================================================
3. MAIN SCENES
===============================================================================

-------------------------------------------------------------------------------
3.1 Main
-------------------------------------------------------------------------------

Scene:
- scenes/Main.tscn

Script:
- scripts/Main.cs

Expected node structure:

Main (Node)
└─ ScreenContainer (Node)

Purpose:
- root scene of the whole application
- entry point of the game

Responsibilities:
- load the title screen at startup
- switch from title screen to gameplay
- switch from gameplay to game over
- switch to high score screen or back to title screen

Important:
- Main must not contain gameplay logic
- Main is the global screen orchestrator

-------------------------------------------------------------------------------
3.2 TitleScreen
-------------------------------------------------------------------------------

Scene:
- scenes/screens/TitleScreen.tscn

Script:
- scripts/screens/TitleScreen.cs

Expected node structure:

TitleScreen (Control)
├─ Background
├─ TitleLabel
├─ InfoLabel
├─ AnimatedLadybug
└─ StartPrompt

Purpose:
- title / attract screen

Responsibilities:
- show the game title
- show introductory information
- show a small ladybug animation
- wait for player input to start the game

-------------------------------------------------------------------------------
3.3 GameplayScreen
-------------------------------------------------------------------------------

Scene:
- scenes/screens/GameplayScreen.tscn

Script:
- scripts/screens/GameplayScreen.cs

Expected node structure:

GameplayScreen (Node)
├─ LevelContainer (Node)
└─ Hud (CanvasLayer)

Purpose:
- contain one active gameplay session

Responsibilities:
- instantiate the current level
- connect level and HUD
- react to win / lose conditions
- manage gameplay-related transitions

Important:
- GameplayScreen is the container for active play
- it is not the level logic itself

-------------------------------------------------------------------------------
3.4 GameOverScreen
-------------------------------------------------------------------------------

Scene:
- scenes/screens/GameOverScreen.tscn

Script:
- scripts/screens/GameOverScreen.cs

Expected node structure:

GameOverScreen (Control)
├─ Background
├─ GameOverLabel
├─ ScoreLabel
├─ NamePromptLabel
├─ NameInput
└─ ConfirmLabel

Purpose:
- display game over and allow player name entry

Responsibilities:
- show final score
- allow player to enter initials or a name
- store a score entry
- transition to the next screen

-------------------------------------------------------------------------------
3.5 HighScoreScreen
-------------------------------------------------------------------------------

Scene:
- scenes/screens/HighScoreScreen.tscn

Script:
- scripts/screens/HighScoreScreen.cs

Expected node structure:

HighScoreScreen (Control)
├─ Background
├─ TitleLabel
├─ ScoresContainer
└─ ContinueLabel

Purpose:
- display the high score table

Responsibilities:
- show stored scores
- return to title screen or continue the flow

===============================================================================
4. LEVEL SCENE
===============================================================================

-------------------------------------------------------------------------------
4.1 Level
-------------------------------------------------------------------------------

Scene:
- scenes/level/Level.tscn

Script:
- scripts/level/Level.cs

Current practical node structure:

Level (Node2D)
├─ Maze (Sprite2D)
└─ Player (Node2D instance)

Long-term target structure:

Level (Node2D)
├─ Maze (Sprite2D)
├─ GateContainer (Node2D)
├─ CollectibleContainer (Node2D)
├─ EnemyContainer (Node2D)
├─ Player (Node2D instance)
└─ BonusSpawn / other helper nodes as needed

Purpose:
- represent one playable stage

Responsibilities:
- own the visual maze background
- load the logical maze from JSON
- expose the runtime MazeGrid to gameplay actors
- initialize the player after the maze has been loaded
- later instantiate enemies, collectibles, gates, and bonus items
- manage stage-specific gameplay logic

Important:
- the fixed maze graphics are currently part of the background image
  assets/images/maze_background.png
- the logical maze is a separate runtime structure loaded from data/maze.json
- Level should not handle the global application flow

-------------------------------------------------------------------------------
4.2 Visual Maze vs Logical Maze
-------------------------------------------------------------------------------

The project intentionally separates two different notions of maze:

1) Visual maze
   - currently represented by the background sprite image
   - responsible only for visual appearance

2) Logical maze
   - loaded from JSON through MazeLoader
   - represented at runtime by MazeGrid and MazeCell
   - responsible for walls and allowed movement directions

This separation is important because the arcade remake needs both:
- faithful graphics
- explicit gameplay logic

===============================================================================
5. ACTOR SCENES
===============================================================================

-------------------------------------------------------------------------------
5.1 Player
-------------------------------------------------------------------------------

Scene:
- scenes/player/Player.tscn

Script:
- scripts/gameplay/actors/PlayerController.cs

Current node structure:

Player (Node2D)
└─ AnimatedSprite2D

Purpose:
- represent the player entity

Responsibilities:
- read player input
- manage movement state
- query the logical maze before moving
- update player animation and facing direction

Status:
- this is the first implemented gameplay actor
- it is already connected to the logical maze loaded by Level

-------------------------------------------------------------------------------
5.2 Enemy
-------------------------------------------------------------------------------

Scene:
- scenes/enemies/Enemy.tscn

Script:
- scripts/gameplay/actors/EnemyController.cs

Expected node structure:

Enemy (Node2D)
└─ AnimatedSprite2D

Purpose:
- represent one enemy entity

Responsibilities:
- follow maze movement rules
- manage enemy behavior
- eventually use shared movement structure from GridActor

Status:
- planned, not yet implemented

===============================================================================
6. PROP SCENES
===============================================================================

-------------------------------------------------------------------------------
6.1 Gate
-------------------------------------------------------------------------------

Scene:
- scenes/props/Gate.tscn

Script:
- scripts/gameplay/props/Gate.cs

Expected node structure:

Gate (Node2D)
└─ Sprite2D or AnimatedSprite2D

Purpose:
- represent one rotating gate in the maze

Responsibilities:
- represent a door visually
- rotate when pushed
- later update logical paths in the maze

Status:
- planned, not yet implemented

-------------------------------------------------------------------------------
6.2 Collectible
-------------------------------------------------------------------------------

Scene:
- scenes/props/Collectible.tscn

Script:
- scripts/gameplay/props/Collectible.cs

Expected node structure:

Collectible (Node2D)
└─ Sprite2D

Purpose:
- represent flowers, hearts, letters, or other collectible items

Responsibilities:
- exist as a collectible gameplay element
- notify the level when collected

Status:
- planned, not yet implemented

-------------------------------------------------------------------------------
6.3 BonusVegetable
-------------------------------------------------------------------------------

Scene:
- scenes/props/BonusVegetable.tscn

Script:
- scripts/gameplay/props/BonusVegetable.cs

Purpose:
- represent temporary bonus items

Responsibilities:
- appear at specific moments
- provide bonus score or stage reward

Status:
- planned, not yet implemented

===============================================================================
7. UI
===============================================================================

-------------------------------------------------------------------------------
7.1 Hud
-------------------------------------------------------------------------------

Scene:
- scenes/ui/Hud.tscn

Script:
- scripts/ui/Hud.cs

Expected node structure:

Hud (CanvasLayer)
└─ Root (Control)
   ├─ ScoreLabel
   ├─ LivesLabel
   └─ StageLabel

Purpose:
- display gameplay information

Responsibilities:
- show score
- show lives
- show stage number

Status:
- planned, not yet implemented

===============================================================================
8. CORE GAMEPLAY / LOGICAL CLASSES
===============================================================================

-------------------------------------------------------------------------------
8.1 WallFlags
-------------------------------------------------------------------------------

Location:
- scripts/gameplay/maze/WallFlags.cs

Purpose:
- describe which walls exist around one logical cell

Responsibilities:
- encode up / down / left / right walls
- provide a compact representation used in MazeCell and JSON data

Important:
- this replaces the idea of a generic Direction enum as the primary maze data
  representation

-------------------------------------------------------------------------------
8.2 MazeCell
-------------------------------------------------------------------------------

Location:
- scripts/gameplay/maze/MazeCell.cs

Purpose:
- represent one logical maze cell

Responsibilities:
- store wall information for one cell
- expose movement constraints in each direction
- serve as the smallest logical maze unit

-------------------------------------------------------------------------------
8.3 MazeDataFile
-------------------------------------------------------------------------------

Location:
- scripts/gameplay/maze/MazeDataFile.cs

Purpose:
- represent the JSON data structure used to serialize the maze

Responsibilities:
- define the JSON format
- separate file-format concerns from runtime maze logic

-------------------------------------------------------------------------------
8.4 MazeGrid
-------------------------------------------------------------------------------

Location:
- scripts/gameplay/maze/MazeGrid.cs

Purpose:
- represent the logical structure of the maze at runtime

Responsibilities:
- store the 2D array of logical cells
- know whether a movement is allowed from a given cell
- validate bounds
- provide movement constraints to player and later enemies

Important:
- MazeGrid is a logical model
- it is not a visual tilemap
- it is the gameplay source of truth for maze walls

-------------------------------------------------------------------------------
8.5 MazeLoader
-------------------------------------------------------------------------------

Location:
- scripts/gameplay/maze/MazeLoader.cs

Purpose:
- load the logical maze from JSON

Responsibilities:
- read data/maze.json
- deserialize MazeDataFile
- construct the runtime MazeGrid

Important:
- file loading is intentionally separated from MazeGrid itself

-------------------------------------------------------------------------------
8.6 GridActor
-------------------------------------------------------------------------------

Location:
- scripts/gameplay/actors/GridActor.cs

Purpose:
- potential base class for moving actors

Responsibilities:
- eventually centralize shared movement logic
- store shared movement-related data
- reduce duplication between player and enemies

Current status:
- architectural placeholder / future refactoring point
- the current movement logic still lives directly inside PlayerController

-------------------------------------------------------------------------------
8.7 PlayerController
-------------------------------------------------------------------------------

Location:
- scripts/gameplay/actors/PlayerController.cs

Purpose:
- handle player-specific behavior

Responsibilities:
- read player input
- manage wanted direction and current direction
- update movement using arcade-oriented logic
- validate movement against the logical maze
- control animation and visual orientation

Current implementation focus:
- fixed tick update
- integer arcade pixel position
- current direction / wanted direction
- buffered direction changes
- provisional lane alignment and lane recentering
- turn capture based on alignment and maze validity
- animation driven by visual movement direction

Important:
- this is not free delta-based movement
- the current implementation is already oriented toward reproducing arcade
  behavior rather than modern smooth movement

-------------------------------------------------------------------------------
8.8 EnemyController
-------------------------------------------------------------------------------

Location:
- scripts/gameplay/actors/EnemyController.cs

Purpose:
- handle enemy-specific behavior

Responsibilities:
- use shared movement rules
- implement enemy decision-making
- respect maze constraints

Status:
- planned, not yet implemented

===============================================================================
9. GLOBAL STATE
===============================================================================

-------------------------------------------------------------------------------
9.1 GameSession
-------------------------------------------------------------------------------

Location:
- scripts/autoload/GameSession.cs

Purpose:
- store global session state

Responsibilities:
- current score
- lives
- current stage
- game state data shared across screens

Intended usage:
- GameSession should be used as an AutoLoad

Status:
- planned, not yet implemented

===============================================================================
10. CURRENT IMPLEMENTATION STATUS
===============================================================================

Implemented now:
- player scene
- animated player sprite
- level scene with maze background sprite
- logical maze JSON file
- logical maze loading pipeline:
  - MazeDataFile
  - MazeLoader
  - MazeGrid
  - MazeCell
  - WallFlags
- Level initialization of the player after maze loading
- player start positioning from level configuration
- player movement connected to maze validation
- intermediate arcade-oriented movement model
- project documentation

Partially implemented / still evolving:
- player movement accuracy
- lane alignment rules
- long-term actor base architecture through GridActor
- exact arcade behavior reproduction

Planned but not yet implemented:
- Main screen flow
- title screen
- gameplay screen container
- game over flow
- high score flow
- enemies
- collectibles
- rotating gates
- bonus vegetables
- score flow and HUD
- session / stage progression systems

===============================================================================
11. DESIGN PHILOSOPHY
===============================================================================

This project is not using a minimal "everything in one script" structure.

It is also not trying to build a heavy architecture too early.

The current design aims for:
- simple structure
- clear responsibilities
- separation between visual data and logical gameplay data
- future scalability
- faithful arcade behavior

In short:
- simple enough to work with now
- structured enough to avoid rebuilding everything later

===============================================================================
12. CURRENT DEVELOPMENT PRIORITY
===============================================================================

The current priority is not the full application flow yet.

The immediate focus is:
- establish a correct logical maze
- establish a reliable player controller
- progressively move from prototype behavior toward arcade-faithful behavior

This means:
- movement and maze logic are currently more important than menus and score flow
- architecture decisions should continue to support reverse engineering findings
- gameplay correctness takes priority over premature content expansion
