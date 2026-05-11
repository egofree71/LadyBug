# Lady Bug Remake

A personal remake of the 1981 arcade game **Lady Bug**, built with **Godot 4.6.2 .NET** and **C#**.

## About the project

This project is an attempt to recreate the gameplay of the original arcade version of **Lady Bug** in a modern engine, while keeping the codebase readable, testable and maintainable.

The goal is not only to make a playable remake, but also to understand how the original arcade game works internally. A significant part of the project is based on reverse engineering: observing the original game in MAME, studying memory behavior, analyzing Z80 routines, comparing movement patterns, and translating arcade-era logic into modern C# systems.

The project is also heavily **AI-assisted**. I use AI as a coding and reverse-engineering partner: sometimes for precise technical analysis, sometimes in a more exploratory “vibe coding” style. The results are still manually tested, adjusted and compared against the arcade original.

## Current status

The project is currently a playable prototype.

Implemented systems include:

- title screen to gameplay to game-over loop
- maze rendering
- player movement with arcade-style assisted turns
- rotating gates
- flowers, hearts, letters and skulls
- scoring and score multipliers
- bonus vegetables with bonus scoring
- SPECIAL / EXTRA word progress
- lives, player death sequence, and HUD-to-maze life-entry animation
- audio cues for pickups, death, enemy release, timer events, gates, level endings, and player entry into the maze
- enemy release through the animated border timer
- enemy freeze after collecting the central bonus vegetable, while enemies remain fatal
- first playable enemy movement system with simulator-refined center decisions
- level progression through PART transition screens
- HUD with score, sprite-based lives, SPECIAL, EXTRA and multipliers
- arcade-style Press Start 2P typography on the title prompt, HUD labels, PART transition labels, score, and GAME OVER overlay

Recent gameplay refinements include arcade-style life handling. The HUD renders ladybug sprites for spare lives, while the current active life is represented by the player in the maze. At the very first PART screen the HUD shows the initial total stock of lives; during gameplay and later PART screens it shows only the remaining spare lives. A new life enters the maze from the HUD only when the game starts or after a death. Normal level transitions keep the active player life and simply place the player at the next level's start cell. The entry animation moves at one arcade pixel per simulation tick and plays the `enter_maze.wav` cue as soon as it starts.

Other recent refinements include UI typography polish. The title prompt, HUD word labels and multipliers, score, PART transition labels, and GAME OVER overlay now use the bundled Press Start 2P font for a more coherent arcade-style look. The title prompt pulses between white and light gray, and the prompt ladybug position has been adjusted to sit cleanly between the left edge and the text.

Other recent refinements include bonus vegetables, their score award, and the arcade-style enemy-freeze behavior where enemies stop moving for a short time but remain dangerous on contact. Recent enemy-movement refinements also include a more arcade-like decision order at intersections: enemies now try their preferred direction first, keep their current direction when it remains valid, and only then scan fallback directions. A later simulator comparison showed that broad gate / boundary checks were too aggressive outside decision centers, so enemies now keep their current direction outside centers instead of reversing from a high-level gate block. Local movement probes now use simulator-derived directional offsets, while exact tile-shape filtering around rotating gates remains a future refinement.

Some systems are still incomplete or approximate, especially:

- full original arcade screen flow beyond the current title / gameplay / game-over loop
- high-score screens and persistence
- exact arcade transition-screen tile / color RAM rendering

## Technology

- **Godot 4.6.2 .NET**
- **C#**
- **MAME** for observation, debugging and runtime traces
- **Ghidra** for reverse-engineering work
- **AI-assisted development**

## Reverse engineering approach

The original arcade game contains many small timing and movement details that are hard to reproduce by visual observation alone.

This remake uses a mix of:

- gameplay observation
- MAME debugger sessions
- memory inspection
- Z80 disassembly analysis
- runtime traces
- comparison with the behavior of the original arcade game
- high-level reimplementation in C#

The goal is not to reproduce the original hardware literally. Instead, the project tries to preserve the important gameplay behavior while using a cleaner and more maintainable modern architecture.

## Project structure

```text
assets/   Visual assets used by the remake
data/     JSON data for the maze and collectibles
doc/      Notes, reverse-engineering documents and implementation details
scenes/   Godot scenes
scripts/  C# gameplay and runtime code
```

## Documentation

Additional documentation is available in the `doc/` folder. It contains implementation notes and reverse-engineering material used during development.

## Disclaimer

This is a personal, non-commercial fan project made for learning, preservation and technical exploration.

**Lady Bug** is the property of its respective rights holders. This project is not affiliated with or endorsed by the original creators, publishers or rights holders.
