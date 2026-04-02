===============================================================================
CURRENT IMPLEMENTATION
===============================================================================

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

Purpose of this document:
- describe only what is actually implemented in the repository now
- provide a reliable starting point for future discussions
- avoid mixing current code with longer-term architectural ideas

This document is intentionally concrete.
It does not describe systems that are only planned.

===============================================================================
1. PROJECT ENTRY POINT
===============================================================================

The Godot project currently starts from:

- scenes/Main.tscn

In project.godot:
- main scene = Main.tscn
- viewport width = 746
- viewport height = 768

Declared movement input actions:
- move_left
- move_right
- move_up
- move_down

===============================================================================
2. CURRENT FILE / FOLDER STATE
===============================================================================

Relevant folders currently present in the repository:

assets/
data/
doc/
scenes/
scripts/

Important currently used files:

scenes/
├─ Main.tscn
├─ level/
│  └─ Level.tscn
└─ player/
   └─ Player.tscn

scripts/
├─ Main.cs
├─ actors/
│  └─ PlayerController.cs
├─ gameplay/
│  └─ maze/
│     ├─ WallFlags.cs
│     ├─ MazeCell.cs
│     ├─ MazeDataFile.cs
│     ├─ MazeGrid.cs
│     └─ MazeLoader.cs
└─ level/
   └─ Level.cs

data/
└─ maze.json

===============================================================================
3. CURRENT SCENE STRUCTURE
===============================================================================

-------------------------------------------------------------------------------
3.1 Main scene
-------------------------------------------------------------------------------

Scene:
- scenes/Main.tscn

Current structure:

Main (Node)
└─ Level (instance of scenes/level/Level.tscn)

Current script:
- scripts/Main.cs

Current behavior:
- prints "LadyBug project started." in _Ready()
- does not yet manage screen flow or gameplay transitions

-------------------------------------------------------------------------------
3.2 Level scene
-------------------------------------------------------------------------------

Scene:
- scenes/level/Level.tscn

Current structure:

Level (Node2D)
├─ Maze (Sprite2D)
└─ Player (instance of scenes/player/Player.tscn)

Important current properties:
- script = scripts/level/Level.cs
- PlayerStartCell = Vector2i(5, 8)

Maze node:
- type = Sprite2D
- texture = assets/images/maze_background.png
- centered = false
- offset = Vector2(16, 24)

Player node:
- instance of scenes/player/Player.tscn

-------------------------------------------------------------------------------
3.3 Player scene
-------------------------------------------------------------------------------

Scene:
- scenes/player/Player.tscn

Current structure:

Player (Node2D)
└─ AnimatedSprite2D

Current script:
- scripts/actors/PlayerController.cs

Current visual setup:
- AnimatedSprite2D uses the player spritesheet
- two animations are defined:
  - move_right
  - move_up

Left and down are currently handled by sprite flipping.

===============================================================================
4. LOGICAL MAZE SYSTEM
===============================================================================

The project already includes a logical maze system separated from the visual maze.

Visual maze:
- represented by the Maze Sprite2D background image in Level.tscn

Logical maze:
- stored in data/maze.json
- loaded at runtime by MazeLoader
- represented by MazeGrid and MazeCell

Current maze JSON:
- width = 11
- height = 11
- cells = flat array of wall bitmasks

-------------------------------------------------------------------------------
4.1 WallFlags
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/WallFlags.cs

Purpose:
- represent walls around a logical cell with a bitmask

Current supported flags:
- Up
- Down
- Left
- Right

-------------------------------------------------------------------------------
4.2 MazeCell
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeCell.cs

Purpose:
- represent one logical maze cell

Current responsibilities:
- store wall information
- expose HasWallUp / HasWallDown / HasWallLeft / HasWallRight
- answer whether movement is allowed in a cardinal direction

-------------------------------------------------------------------------------
4.3 MazeDataFile
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeDataFile.cs

Purpose:
- represent the serialized JSON structure

Current use:
- intermediate deserialization model between maze.json and MazeGrid

-------------------------------------------------------------------------------
4.4 MazeGrid
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeGrid.cs

Purpose:
- runtime logical maze representation

Current responsibilities:
- store the 2D logical cell grid
- validate maze bounds
- return cells by logical position
- determine whether movement is allowed from one logical cell to another

Important:
- CanMove() also blocks movement outside maze bounds

-------------------------------------------------------------------------------
4.5 MazeLoader
-------------------------------------------------------------------------------

File:
- scripts/gameplay/maze/MazeLoader.cs

Purpose:
- load the logical maze from JSON

Current behavior:
- reads res://data/maze.json
- deserializes MazeDataFile
- builds MazeGrid

===============================================================================
5. LEVEL RUNTIME LOGIC
===============================================================================

File:
- scripts/level/Level.cs

Level.cs is already a real runtime coordinator for the current prototype.

Current responsibilities:
- load the logical maze from res://data/maze.json
- expose the runtime MazeGrid through a property
- reposition the Player from PlayerStartCell
- convert logical cell coordinates into scene coordinates
- initialize the player after the maze has been loaded
- update the player position in the editor when PlayerStartCell changes

Important implementation details:
- Level uses [Tool]
- logical cell size is currently 16 arcade pixels
- render scale is currently 4
- placement offset inside a logical cell is currently Vector2I(13, 15)

===============================================================================
6. PLAYER CONTROLLER
===============================================================================

File:
- scripts/actors/PlayerController.cs

Current movement model:
- intermediate smooth cell-to-cell movement
- not yet the final arcade-accurate pixel-per-tick movement

Current responsibilities:
- receive Level and MazeGrid references from Level.Initialize()
- keep track of the current logical cell
- keep track of the target logical cell
- read movement input
- ask MazeGrid whether the requested move is allowed
- move smoothly toward the target cell in scene space
- update animation according to the requested direction

Current implementation details:
- movement speed = 220 scene pixels per second
- movement starts only if MazeGrid.CanMove() allows it
- once the target scene position is reached:
  - current logical cell is updated
  - movement stops
- current visual animations:
  - move_right
  - move_up
- left uses FlipH
- down uses FlipV

===============================================================================
7. WHAT IS CURRENTLY WORKING
===============================================================================

The following is already implemented and functional:

- Main scene launches correctly
- Level scene is instantiated from Main
- maze background is displayed
- player is displayed
- player start position is defined through Level.PlayerStartCell
- player position updates in the editor
- logical maze is loaded from JSON
- logical cell walls are interpreted correctly
- movement is validated against the logical maze
- movement outside the maze bounds is blocked
- player moves smoothly from one logical cell to another

===============================================================================
8. WHAT IS NOT IMPLEMENTED YET
===============================================================================

The following systems are not implemented yet:

- enemies
- rotating gates
- flowers / hearts / letters
- bonus vegetables
- HUD
- score system
- lives system
- title screen flow
- gameplay / game over / high score screen flow
- session state management
- final arcade-accurate player movement

===============================================================================
9. CURRENT LIMITATIONS
===============================================================================

The player movement is still an intermediate version.

It currently:
- moves cell to cell
- interpolates visually in scene space
- uses the logical maze correctly

But it does not yet:
- reproduce exact arcade tick timing
- reproduce exact arcade pixel-per-tick movement
- reproduce original turn windows and lane behavior
- handle dynamic gates

===============================================================================
10. CURRENT DEVELOPMENT PRIORITY
===============================================================================

The current priority should remain:

1) stabilize the logical maze and player behavior
2) move from intermediate movement toward more faithful arcade movement
3) only then expand into gameplay systems such as gates, enemies, and items
