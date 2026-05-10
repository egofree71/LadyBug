# Lady Bug Remake

A personal remake of the 1981 arcade game **Lady Bug**, built with **Godot 4.6.1 .NET** and **C#**.

## About the project

This project is an attempt to recreate the gameplay of the original arcade version of **Lady Bug** in a modern engine, while keeping the codebase readable, testable and maintainable.

The goal is not only to make a playable remake, but also to understand how the original arcade game works internally. A significant part of the project is based on reverse engineering: observing the original game in MAME, studying memory behavior, analyzing Z80 routines, comparing movement patterns, and translating arcade-era logic into modern C# systems.

The project is also heavily **AI-assisted**. I use AI as a coding and reverse-engineering partner: sometimes for precise technical analysis, sometimes in a more exploratory “vibe coding” style. The results are still manually tested, adjusted and compared against the arcade original.

## Current status

The project is currently a playable prototype with a first version of the arcade-style screen flow.

Implemented systems include:

- title screen with the Lady Bug logo, animated enemy sprites, animated ladybug prompt marker, and `PRESS ANY KEY` start prompt
- initial `PART 1` transition screen before the first playable board starts
- maze rendering
- player movement with arcade-style assisted turns
- rotating gates
- flowers, hearts, letters and skulls
- scoring and score multipliers
- bonus vegetables with bonus scoring
- SPECIAL / EXTRA word progress
- lives and player death sequence
- visible `GAME OVER` screen with automatic return to the title screen
- enemy release through the animated border timer
- enemy freeze after collecting the central bonus vegetable, while enemies remain fatal
- first playable enemy movement system with simulator-refined center decisions
- level progression through arcade-style PART transition screens
- HUD with score, lives, SPECIAL, EXTRA and multipliers

Recent gameplay refinements include bonus vegetables, their score award, and the arcade-style enemy-freeze behavior where enemies stop moving for a short time but remain dangerous on contact. Recent enemy-movement refinements also include a more arcade-like decision order at intersections: enemies now try their preferred direction first, keep their current direction when it remains valid, and only then scan fallback directions. A later refinement keeps forced / opposite-direction rescue separate from normal center decisions: late reversal is allowed only for gate-blocked movement outside decision centers. Together, these changes removed a visible issue where enemies could appear stuck between logical cells while keeping the center-decision logic easier to reason about.

Recent screen-flow work adds a title screen, routes a new game through the same PART transition system used between later levels, and displays a GAME OVER panel before returning to the title screen.

Some systems are still incomplete or approximate, especially:

- exact bonus-vegetable timing and low-level arcade rendering details
- remaining pixel-perfect enemy movement edge cases around rotating gates and later-level behavior
- later-level enemy rotation
- high-score screen flow and persistence
- persistent session / GameSession architecture
- exact coin, credit, free-game and top-score behavior
- arcade-accurate tile / color RAM rendering for transition and overlay screens

## Technology

- **Godot 4.6.1 .NET**
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
