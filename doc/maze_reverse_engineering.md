# Maze Reverse Engineering

Project: Lady Bug remake in Godot 4.6.1 (.NET) with C#

## Purpose

This document summarizes the reverse-engineering findings currently used for:

- the visible VRAM screen layout
- the way the maze is built on screen
- the method used to reconstruct the logical maze JSON for Godot

This note is intentionally short and practical.
It focuses only on the findings that were useful to derive the current maze data.

## Screen Graphics Layout

Video RAM starts at `D080`.

The screen is built **column by column, from bottom to top**:

```text
D080–D09F -> 1st column
D0A0–D0BF -> 2nd column
...
D3E0–D3FF -> last column
```

## Maze Reconstruction

The maze is **not** stored in one memory area and then copied into video RAM.
Instead, it is built manually, tile by tile, by the routine at `215C–22D4`.

One logical maze cell is displayed using:

- two vertical tiles
- two horizontal tiles

To draw the fixed walls, the game uses:

- wall tiles
- and also some mixed tiles that contain both:
  - a wall fragment
  - a pivot-door fragment

## Logical Maze Representation in Godot

To reconstruct a logical maze in Godot, the project stores, for each logical cell,
whether there is a wall on:

- top
- bottom
- left
- right

In `maze.json`, each cell value is therefore stored as a bitmask in one integer:

| Value | Meaning |
|---|---|
| 1 | top wall |
| 2 | bottom wall |
| 4 | left wall |
| 8 | right wall |

Examples:

| Bitmask | Meaning |
|---|---|
| 5 | top + left |
| 10 | bottom + right |
| 15 | all 4 walls |

## Procedure Used to Generate the JSON

The JSON values are generated with the following process:

1. Read a VRAM dump.
2. Convert the screen data from **columns** into normal **rows**.
3. Start with the first row.
4. For the current logical cell:
   - check for value `32` (vertical wall) in the previous and next columns
   - check for value `31` (horizontal wall) in the previous and next rows
5. Convert those wall observations into the bitmask value for the cell.
6. Move two columns to the right and repeat.
7. Once the row is finished, move two rows down and repeat until the last row is reached.

## Practical Summary

The key findings used by the current implementation are:

- visible VRAM starts at `D080`
- the screen is stored **column by column**
- each logical maze cell corresponds to a **2 × 2 tile footprint**
- the maze is built procedurally into VRAM by code, not copied from a prebuilt map
- the Godot maze JSON is derived by converting VRAM layout into per-cell wall bitmasks

## Related Timer Documentation

This document covers the static maze layout and the reconstruction of the logical maze
used by Godot.

The animated maze border is related visually, but it is a gameplay timer rather than part
of the static maze reconstruction.

See `gameplay_timers_reverse_engineering.md` for:

- `35E3` / `InitMazeBorderTimerForLevel`
- `39B1` / `UpdateMazeBorderTimer`
- `60AA` / `MazeBorderCountdown`
- `60AB` / `MazeBorderPeriod`
- level-dependent border cadence: 9, 6, then 3 ticks per border step
