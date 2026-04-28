# Gameplay Timers Reverse Engineering

Project: Lady Bug remake in Godot 4.6.1 / 4.6.2 (.NET) with C#

## Purpose

This document keeps the reverse-engineering notes for gameplay timers that affect more
than one subsystem.

It was created to avoid mixing two different mechanisms:

- the heart / letter color cycle used by special collectibles
- the animated maze-border timer, which also acts as the enemy-release timer

The most important conclusion is:

```text
The collectible color cycle and the maze-border timer are updated from the same gameplay
loop, but they do not use the same counter.
```

## Fixed Arcade Tick Rate

For the Godot remake, these timers should advance once per fixed arcade simulation tick.

The current project uses:

```text
60.1145 Hz
```

Approximate tick duration:

```text
1 / 60.1145 = 0.016635 s
```

## Gameplay Loop Order

In the gameplay loop, the two timer routines are called one after the other:

```text
079C CALL UpdateMazeBorderTimer
079F CALL TickCollectibleColorCycle
```

This is why the two effects can appear globally synchronized at the frame level.
However, they are separate routines with separate RAM state.

## Heart / Letter Color Cycle

### Purpose

This timer controls the gameplay color classification of special collectibles:

- hearts
- letters

It does not affect:

- flowers
- skulls
- the maze-border animation cadence

### Relevant Ghidra Names

```text
3956  TickCollectibleColorCycle
3D9F  ClassifyCollectibleColorOrType
6199  CollectibleColorCycleCounter
```

### RAM

```text
6199/619A = CollectibleColorCycleCounter
```

This is a 16-bit little-endian counter.

The code loads and stores it as a word:

```text
LD HL,(6199)
INC HL
LD (6199),HL
```

So in Ghidra, it is best represented as a 16-bit value starting at `6199`, not as
two unrelated bytes.

### Reset

At the start of gameplay, the counter is reset to zero:

```text
0770 LD DE,0x0000
0773 LD (6199),DE
```

### Period

The counter wraps at:

```text
0x0258 = 600 ticks
```

### Gameplay Color Classification

`ClassifyCollectibleColorOrType` reads `6199/619A` and classifies the current special
collectible color as follows:

| Counter range | Mode | Gameplay meaning |
|---:|---|---|
| `< 0x001F` | Red | red letters can progress SPECIAL |
| `< 0x00B4` | Yellow | yellow letters can progress EXTRA |
| `>= 0x00B4` | Blue | blue hearts increase the score multiplier |
| `0x0258` | Reset | cycle wraps to zero |

### Durations

At 60.1145 Hz:

| Mode | Ticks | Approx. duration |
|---|---:|---:|
| Red | 31 | 0.516 s |
| Yellow | 149 | 2.479 s |
| Blue | 420 | 6.986 s |
| Total | 600 | 9.981 s |

### Godot Implementation Rule

The visible color and the pickup effect must use the same current mode.

Recommended structure:

```csharp
public enum CollectibleColorMode
{
    Red,
    Yellow,
    Blue
}

public sealed class CollectibleColorCycle
{
    private int _counter;

    public CollectibleColorMode CurrentMode => _counter switch
    {
        < 0x001F => CollectibleColorMode.Red,
        < 0x00B4 => CollectibleColorMode.Yellow,
        _ => CollectibleColorMode.Blue
    };

    public void Tick()
    {
        _counter++;
        if (_counter >= 0x0258)
            _counter = 0;
    }
}
```

The remake may choose a different initial visual phase if desired, but the gameplay rule
should remain internally consistent: displayed color and collection effect must agree.

## Maze-Border / Enemy-Release Timer

### Purpose

The maze border is not only decoration. It acts as the visible timer for enemy release.
The general game description says that each completed tour of the border corresponds to a
new enemy appearing from the central area, and that the apparition speed increases at
levels 2 and 5.

The disassembly confirms a level-dependent border cadence:

```text
level < 2  -> 9 frames
level < 5  -> 6 frames
level >= 5 -> 3 frames
```

Using the normal 1-based level numbering, this means:

| Level | Border step period | Approx. duration per border step |
|---:|---:|---:|
| 1 | 9 ticks | 0.150 s |
| 2 to 4 | 6 ticks | 0.100 s |
| 5+ | 3 ticks | 0.050 s |

### Relevant Ghidra Names

```text
35E3  InitMazeBorderTimerForLevel
39B1  UpdateMazeBorderTimer
60AA  MazeBorderCountdown
60AB  MazeBorderPeriod
```

### RAM

```text
60AA = current countdown
60AB = reload period
```

These are byte-sized values, not a 16-bit word.

### Initialization Routine

`InitMazeBorderTimerForLevel` calls the current-level lookup routine, then initializes the
border countdown and reload period:

```text
35E3 CALL current-level lookup
35E6 LD HL,60AA
35E9 CP 02
35ED LD (60AA),09   ; level < 2
35F1 CP 05
35F5 LD (60AA),06   ; level < 5
35F9 LD (60AA),03   ; level >= 5
35FB LD A,(60AA)
35FD LD (60AB),A
```

### Update Routine

`UpdateMazeBorderTimer` decrements `60AA` each gameplay tick:

```text
39B1 LD HL,60AA
39B4 DEC (HL)
39B5 JP NZ,3A48
```

When the countdown reaches zero, the routine updates one border position and then reloads
`60AA` from `60AB`:

```text
3A01 LD A,(60AB)
3A04 LD (60AA),A
```

Therefore the first border step after initialization happens after exactly `period` ticks.

### Border State Variables Observed

The same routine also uses several surrounding variables:

| RAM | Probable meaning |
|---:|---|
| `6063/6064` | current VRAM pointer for the border position |
| `6062` | border tile phase / toggle |
| `6061` | position index along current side; compared against `0x17` |
| `6060` | side index; cycles through 0..3 |

These names are less certain than `60AA/60AB`, but they are useful when continuing the
border reverse engineering.

### Enemy Release Hooks Inside The Border Routine

The border routine contains checks around `6060` and `6061` that interact with enemy
state.

Observed behavior:

```text
side index 6060 must be 0
position index 6061 is checked against 1 and 0x0C
```

The code then scans the four enemy structures at `602B`, `6030`, `6035`, `603A`, each
5 bytes apart, and toggles bits in the enemy state byte.

Practical interpretation:

```text
The maze-border animation is the visible countdown, and specific border positions trigger
enemy-release state changes.
```

This is why the border timer should be implemented near the enemy-release system, even
though it is visually part of the maze.

### Important Caveat About Routine Naming

`UpdateMazeBorderTimer` is a practical name for the part we currently care about, but the
routine also updates other gameplay counters after the border block, including the
second-like timing path used by enemy chase timers.

So a more verbose Ghidra name could be:

```text
UpdateMazeBorderAndGameplayTimers
```

However, keeping the shorter name is acceptable if comments explain that the tail of the
routine also updates enemy/chase counters.

## Important Distinction Between The Two Timers

| System | Routine | RAM | Period |
|---|---|---|---|
| Heart / letter colors | `3956` | `6199/619A` | 600 ticks |
| Maze border / enemy release | `35E3`, `39B1` | `60AA/60AB` | 9, 6, or 3 ticks per border step |

These systems are called from the same gameplay loop, but they are not the same timer.

## Recommended Godot Architecture

Suggested split:

```text
CollectibleColorCycle
MazeBorderTimer
EnemyReleaseSystem
```

`CollectibleColorCycle` should own only the 600-tick red/yellow/blue state.

`MazeBorderTimer` should own:

- level-dependent period: 9 / 6 / 3
- countdown
- border step advancement
- current border side/index/phase if reproduced visually

`EnemyReleaseSystem` should listen to border-step events or share the same border phase,
so enemy release stays synchronized with the visible border.

### Pseudocode

```csharp
public sealed class MazeBorderTimer
{
    private int _period;
    private int _countdown;

    public void ResetForLevel(int level)
    {
        _period = level switch
        {
            < 2 => 9,
            < 5 => 6,
            _ => 3
        };

        _countdown = _period;
    }

    public bool Tick()
    {
        _countdown--;

        if (_countdown != 0)
            return false;

        _countdown = _period;
        return true; // advance one visible border step
    }
}
```

If the project uses user-facing level numbers starting at 1, the thresholds above produce:

```text
level 1     -> 9 ticks
levels 2-4  -> 6 ticks
level 5+    -> 3 ticks
```

## Documentation Cross-References

- `collectibles_reverse_engineering.md` should keep the detailed heart/letter color-cycle
  behavior.
- `enemy_movement.md` should mention that enemy movement starts after release, while the
  release cadence itself comes from the maze-border timer.
- `maze_reverse_engineering.md` should remain focused on static maze reconstruction; the
  animated border timer belongs here instead.

## Current Confidence

Confirmed:

- `6199/619A` is a 16-bit collectible color-cycle counter.
- The color cycle wraps at 600 ticks.
- Red/yellow/blue thresholds are `0x001F` and `0x00B4`.
- `60AA` is the maze-border countdown.
- `60AB` is the maze-border reload period.
- Border period is level-dependent: 9, 6, then 3 ticks.
- The two timers are called from the same gameplay loop but use separate RAM.

Probable / still worth refining later:

- exact semantic names of `6060`, `6061`, `6062`, and `6063`
- exact mapping between every border position and enemy-release trigger
- whether the Godot remake should reproduce the border visual path byte-for-byte or only
  preserve timing and release behavior
