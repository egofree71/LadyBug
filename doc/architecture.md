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
Some elements are still empty or provisional, but the folder structure and scene
names already reflect the intended long-term organization.

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
├─ maze/
│  ├─ Direction.cs
│  ├─ MazeCell.cs
│  └─ MazeGrid.cs
├─ actors/
│  ├─ GridActor.cs
│  ├─ PlayerController.cs
│  └─ EnemyController.cs
├─ props/
│  ├─ Gate.cs
│  ├─ Collectible.cs
│  └─ BonusVegetable.cs
├─ ui/
│  └─ Hud.cs
└─ autoload/
   └─ GameSession.cs

docs/
├─ architecture.md
└─ movement.md

data/
└─ level_01.json

===============================================================================
2. GLOBAL ARCHITECTURE INTENT
===============================================================================

The project is meant to be organized into several layers:

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

3) Core logic
   - MazeGrid
   - MazeCell
   - Direction
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
- Main is NOT supposed to contain gameplay logic
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

Expected node structure:

Level (Node2D)
├─ BackgroundLayer (TileMapLayer)
├─ WallLayer (TileMapLayer)
├─ GateContainer (Node2D)
├─ CollectibleContainer (Node2D)
├─ EnemyContainer (Node2D)
├─ PlayerContainer (Node2D)
├─ PlayerSpawn (Marker2D)
├─ EnemySpawn (Marker2D)
└─ BonusSpawn (Marker2D)

Purpose:
- represent one playable stage

Responsibilities:
- contain the maze layout
- instantiate the player
- instantiate enemies
- place collectibles and bonus items
- manage stage-specific gameplay logic

Important:
- Level is the concrete playable space
- Level should not handle the global application flow

===============================================================================
5. ACTOR SCENES
===============================================================================

-------------------------------------------------------------------------------
5.1 Player
-------------------------------------------------------------------------------

Scene:
- scenes/player/Player.tscn

Script:
- scripts/actors/PlayerController.cs

Current node structure:

Player (Node2D)
└─ AnimatedSprite2D

Purpose:
- represent the player entity

Responsibilities:
- read player input
- handle player movement
- manage current direction and wanted direction
- update player animation

Status:
- this is currently the first implemented gameplay actor

-------------------------------------------------------------------------------
5.2 Enemy
-------------------------------------------------------------------------------

Scene:
- scenes/enemies/Enemy.tscn

Script:
- scripts/actors/EnemyController.cs

Expected node structure:

Enemy (Node2D)
└─ AnimatedSprite2D

Purpose:
- represent one enemy entity

Responsibilities:
- follow maze movement rules
- manage enemy behavior
- use shared movement structure from GridActor

===============================================================================
6. PROP SCENES
===============================================================================

-------------------------------------------------------------------------------
6.1 Gate
-------------------------------------------------------------------------------

Scene:
- scenes/props/Gate.tscn

Script:
- scripts/props/Gate.cs

Expected node structure:

Gate (Node2D)
└─ Sprite2D or AnimatedSprite2D

Purpose:
- represent one rotating gate in the maze

Responsibilities:
- represent a door visually
- rotate when pushed
- update logical paths in the maze

-------------------------------------------------------------------------------
6.2 Collectible
-------------------------------------------------------------------------------

Scene:
- scenes/props/Collectible.tscn

Script:
- scripts/props/Collectible.cs

Expected node structure:

Collectible (Node2D)
└─ Sprite2D

Purpose:
- represent flowers, hearts, letters, or other collectible items

Responsibilities:
- exist as a collectible gameplay element
- notify the level when collected

-------------------------------------------------------------------------------
6.3 BonusVegetable
-------------------------------------------------------------------------------

Scene:
- scenes/props/BonusVegetable.tscn

Script:
- scripts/props/BonusVegetable.cs

Purpose:
- represent temporary bonus items

Responsibilities:
- appear at specific moments
- provide bonus score or stage reward

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

===============================================================================
8. CORE LOGIC CLASSES
===============================================================================

-------------------------------------------------------------------------------
8.1 MazeGrid
-------------------------------------------------------------------------------

Location:
- scripts/maze/MazeGrid.cs

Purpose:
- represent the logical structure of the maze

Responsibilities:
- define walkable paths
- know which directions are available at a given location
- model intersections, walls, and door states
- provide movement constraints to player and enemies

Important:
- MazeGrid is a logical model
- it is not just a visual tilemap

-------------------------------------------------------------------------------
8.2 MazeCell
-------------------------------------------------------------------------------

Location:
- scripts/maze/MazeCell.cs

Purpose:
- represent one logical maze cell

Responsibilities:
- store cell properties
- indicate allowed movement directions
- hold gameplay elements if needed

-------------------------------------------------------------------------------
8.3 Direction
-------------------------------------------------------------------------------

Location:
- scripts/maze/Direction.cs

Purpose:
- provide a clear direction type for maze logic

Responsibilities:
- standardize movement direction values
- avoid magic values in code

-------------------------------------------------------------------------------
8.4 GridActor
-------------------------------------------------------------------------------

Location:
- scripts/actors/GridActor.cs

Purpose:
- base class for moving actors

Responsibilities:
- handle shared movement logic
- store position and direction information
- provide reusable movement behavior for player and enemies

Intent:
- avoid duplicating movement code

-------------------------------------------------------------------------------
8.5 PlayerController
-------------------------------------------------------------------------------

Location:
- scripts/actors/PlayerController.cs

Purpose:
- handle player-specific behavior

Responsibilities:
- read player input
- update wanted direction
- move using arcade-style logic
- control player animation

Current implementation focus:
- fixed tick update
- integer pixel position
- current direction / wanted direction
- provisional lane alignment

-------------------------------------------------------------------------------
8.6 EnemyController
-------------------------------------------------------------------------------

Location:
- scripts/actors/EnemyController.cs

Purpose:
- handle enemy-specific behavior

Responsibilities:
- use shared movement rules
- implement enemy decision-making
- respect maze constraints

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

===============================================================================
10. CURRENT IMPLEMENTATION STATUS
===============================================================================

Currently implemented:
- player scene
- animated player sprite
- player input
- intermediate arcade-style movement system
- documentation

Planned but not yet fully implemented:
- Main screen flow
- level scene logic
- maze logical model
- enemies
- collectibles
- rotating gates
- score flow and game over sequence

===============================================================================
11. DESIGN PHILOSOPHY
===============================================================================

This project is not using a minimal "everything in one script" structure.

It is also not trying to build a heavy architecture too early.

The current design aims for:
- simple structure
- clear responsibilities
- future scalability
- faithful arcade behavior

In short:
- simple enough to work with now
- structured enough to avoid rebuilding everything later
