# Enemy Movement

Project: Lady Bug remake in Godot 4.6.1 (.NET / C#)

Purpose of this document
------------------------

This document is a compact implementation reference for the monster/enemy system
in the Lady Bug Godot remake.

It summarizes the reverse-engineered arcade behavior, the important RAM/routine
anchors, and the runtime cases validated with MAME debugger logs.

The goal is **not** to translate the Z80 instruction-by-instruction. The goal is
to reproduce the arcade gameplay feel faithfully in clean Godot/C# code.

Update note
-----------

This version incorporates the later MAME/Fable fidelity pass and the matching
Godot implementation:

- B9 reload and odd/even thresholds are now resolved (`0xB3`, `0x90` / `0x24`);
- chase activation uses the ROM tables at `0x4788` / `0x47A6`;
- chase duration uses the elapsed-life table at `0x47AE`, with the alternative
  `0x47CD` table kept configurable;
- waiting lair enemies can receive chase timers;
- enemy speed uses the `0x0EA6` base-index table, the `0x0ED8` / `0x0EE8` speed
  tables and one global fractional accumulator;
- each enemy can execute multiple one-pixel sub-steps in one fixed tick;
- precise near/far rotating-gate reversal probes from `0x4189` are implemented;
- the existing skull -> lair -> normal release cycle remains intentionally
  preserved so all four slots and the vegetable condition continue to work.

The goal remains a readable high-level C# implementation of validated gameplay
behavior, not an instruction-by-instruction port of the Z80 program.

Main source material
--------------------

- `LadyBug_Ghidra.txt`
- `LadyBug_enemy_management_extract.txt`
- runtime MAME debugger logs collected during enemy movement tests
- `ladybug.cpp` for hardware mapping / DIP switch context
- `Description.txt` for high-level gameplay behavior

Confidence levels
-----------------

This document uses three levels:

- **Confirmed**: backed by code paths and/or runtime logs.
- **Probable**: strongly supported by code reading, but not fully tested in every situation.
- **Open**: still unclear; implement cautiously or keep configurable.

Important direction warning
---------------------------

Enemy direction bits and player direction bits must not be mixed.

Enemy direction encoding:

```text
01 = left
02 = up
04 = right
08 = down
```

Player movement analysis uses a different effective interpretation in some contexts.
Keep separate enums in Godot.

Recommended C# enums:

```csharp
[Flags]
public enum MonsterDir
{
	None  = 0x00,
	Left  = 0x01,
	Up    = 0x02,
	Right = 0x04,
	Down  = 0x08,
}
```

High-level behavior
-------------------

Confirmed enough for implementation.

Enemy movement is a hybrid system:

1. Each enemy has a base preferred direction.
2. If a temporary chase timer is active, a BFS direction toward Lady Bug can override that preference.
3. At decision centers, the preferred direction is validated against two distinct layers:
   - static/logical maze permissions
   - local door / tile / playfield geometry
4. If either validation layer rejects the preferred direction, that direction is added to a `61C1`-like rejected-direction mask.
5. Before fallback, the enemy tries to keep its current direction if that direction is still valid.
6. Only if the current direction is also rejected does fallback search another direction in the fixed arcade order `01, 02, 04, 08`.
7. Outside decision centers, the enemy normally continues straight.
8. Precise near/far rotating-gate probes can force an immediate reversal outside a decision center; broad gate or boundary rejection is not used as the trigger.
9. Normal local movement probes use simulator-derived directional offsets: left = X-1,Y; up = X,Y-7; right = X+8,Y; down = X,Y+2. Forced reversal uses a separate two-probe table.
10. Movement is built from one-pixel sub-steps, with a ROM-table speed system that can execute more than one sub-step in a fixed tick.

Implementation consequence:

```text
monster movement = pixel step
                 + center decisions
                 + preferred direction validation
                 + current-direction preservation
                 + rejected mask
                 + fixed-order fallback
                 + straight outside-center movement
                 + normal local probes and precise gate-reversal probes
                 + ROM-table speed / shared fractional accumulator
                 + temporary BFS pressure
```

It should not be implemented as a modern pathfinding agent that constantly chases the player.

Important algorithmic warning:

```text
A visible 180-degree turn at a decision center is not necessarily a forced reversal.
It can be the normal result of:

preferred direction
-> static maze allowed
-> local door/tile rejected
-> rejected direction added to 61C1-like mask
-> current direction also rejected
-> fallback chooses the opposite direction
```

Reserve the term "forced reversal" for the specific outside-center door/local-tile path
corresponding to `0x4189 -> 0x4347`.

Key arcade routines
-------------------

```text
0x407E  Enemy_UpdateAll
0x40CC  Enemy_ComputeSpeedSubsteps
0x40F8  Enemy_SelectSpeedByte
0x42BA  Enemy_UpdateOne
0x4224  Enemy_MoveTempOnePixel
0x427E  Enemy_IsAtDecisionCenter
0x42E6  Enemy_TryPreferredDirection
0x4241  Enemy_FindFallbackDirection
0x4130  Enemy_CheckLocalDoorBlock
0x4189  Enemy_CheckDoorForcedReversal
0x4347  Enemy_ReverseTempDirection
0x43CE  Enemy_CommitTempState
0x2E5C  Enemy_PrepareBasePreferredDirections
0x447D  Bfs_BuildGuidanceFromPlayer
0x46D8  Enemy_ApplyChaseBfsOverride
0x3061  Enemy_InitSlot
0x05AE  Enemy_FindFreeSlotAndInit helper
```

Enemy data structure
--------------------

Confirmed.

The four enemy slots start at `0x602B`, with 5 bytes per enemy.

```text
Enemy0 = 0x602B
Enemy1 = 0x6030
Enemy2 = 0x6035
Enemy3 = 0x603A
```

Layout:

```text
+0 = direction / flags byte
+1 = X pixel coordinate
+2 = Y pixel coordinate
+3 = sprite-related byte
+4 = attribute-related byte
```

The high nibble of `+0` stores the direction. Bit 1 (`0x02`) is the active /
collision-enabled bit used by gameplay checks.

Examples:

```text
0x12 = direction 01 + active bit -> moving left, active
0x22 = direction 02 + active bit -> moving up, active
0x42 = direction 04 + active bit -> moving right, active
0x82 = direction 08 + active bit -> moving down, active / initialized
```

Observed non-active / lair / intermediate examples:

```text
0x10 = direction 01 retained, active bit cleared
0x81 = direction 08 plus bit0, active bit not set; observed as prepared/waiting-like state
```

Implementation note
-------------------

Do not model monsters with only a single boolean. Use a richer state.

Suggested model:

```csharp
public enum MonsterRuntimeState
{
	EmptyOrDead,
	WaitingInLair,
	ExitingLair,
	InMaze,
	FrozenInMaze,
}

public sealed class MonsterEntity
{
	public int Id;
	public int X;
	public int Y;
	public MonsterDir Direction;
	public MonsterDir PreferredDirection;
	public int ChaseTimer;

	public MonsterRuntimeState RuntimeState;

	public bool CollisionActive;
	public bool MovementActive;
}
```

Current Godot implementation notes
----------------------------------

Confirmed for the current Godot implementation.

The enemy code is organized as a clean gameplay runtime rather than as a literal
RAM-layout port.

Current runtime classes:

```text
EnemyRuntime
    Coordinates the four enemy slots, views, lair visibility, release and reset.
    Rebuilds navigation/BFS, prepares preferences, advances chase timing, asks
    EnemySpeedSystem for one global sub-step count, then processes Enemy0..Enemy3
    in stable slot order. Skull collision is checked after every sub-step.

MonsterEntity
    Stores the gameplay state of one enemy slot: arcade-pixel position,
    direction, preferred direction, chase timer, runtime state, movement flag,
    collision flag and lair visibility.

EnemyController
    Owns only the Godot visual representation and synchronizes it from
    MonsterEntity.

EnemyNavigationGrid
    Builds allowed directions from MazeGrid + GateSystem and stores BFS guidance
    toward Lady Bug separately.

EnemyMovementAi
    Performs one one-pixel sub-step. At centers it applies preferred -> current ->
    fallback. On every sub-step it applies the precise near/far rotating-gate
    reversal probes through Level.IsGateBlockingEnemyProbe(...).

EnemyMovementTuning
    Centralizes normal local movement probes and the separate gate-reversal
    probe offsets.

EnemyBasePreferenceSystem
    Owns the B9 two-mode cycle. B9 starts at 0xB3, decrements once per gameplay
    tick, wraps as a byte, and uses 0x90 on odd levels or 0x24 on even levels.

EnemyChaseSystem
    Owns the 60-tick divider, capped elapsed-life counter, ROM-table activation
    windows, round-robin slot selection and ROM-table chase durations. Waiting
    lair enemies can be armed.

EnemySpeedSystem
    Selects a speed from the level/time ROM tables and converts it into a shared
    one- or two-sub-step count through one global 8-bit fractional accumulator.

VegetableBonusRuntime / Level
    Freeze enemy movement while preserving collision-active flags, so frozen
    enemies remain fatal.
```

Gameplay and rendering remain separate:

```text
MonsterEntity = gameplay truth
EnemyController = visual sync only
```

Pixel movement and speed
------------------------

Confirmed.

Routine `0x4224` still commits exactly one arcade pixel per movement sub-step:

```text
left  -> X--
up    -> Y--
right -> X++
down  -> Y++
```

The arcade can execute more than one sub-step in a video frame. The speed chain
uses:

```text
0x61C3 = encoded speed byte
0x61B5 = global 8-bit fractional accumulator
```

The high nibble of the speed byte gives whole sub-steps. The supported low-nibble
encodings add a fraction to the global accumulator:

```text
0x10 = 1.0 px/tick
0x12 = 1.2 px/tick  -> +0x33
0x15 = 1.5 px/tick  -> +0x80
0x18 = 1.8 px/tick  -> +0xCC
0x20 = 2.0 px/tick
```

A carry from the accumulator grants one extra one-pixel sub-step. The speed table
index is:

```text
min(0x0EA6[level] + (lifeSeconds >> 4), 15)
```

The default table is at `0x0ED8`; the alternative difficulty table is at
`0x0EE8`. The current Godot implementation computes the sub-step count once per
fixed tick and shares it across all four enemies. Each enemy completes all of its
sub-steps before the next slot is processed.

Use fixed-tick integer arcade-pixel coordinates. Do not replace this with
continuous `delta`-scaled movement.

Decision centers
----------------

Confirmed.

Enemies normally choose a new direction only at logical cell centers:

```text
X & 0x0F == 0x08
Y & 0x0F == 0x06
```

At any other pixel position, the enemy normally continues in its current direction,
except when the precise near/far rotating-gate probes trigger an immediate forced
reversal. Broad gate/boundary rejection is not used as the reversal trigger.

Implementation helper:

```csharp
static bool IsMonsterDecisionCenter(int x, int y)
{
    return (x & 0x0F) == 0x08
        && (y & 0x0F) == 0x06;
}
```

Godot lair placement note
-------------------------

Confirmed for the current first Godot implementation.

In the current Godot coordinate system, the visible waiting enemy is placed at
logical cell `(5, 5)`, using the enemy decision-center anchor:

```text
X = 5 * 16 + 8
Y = 5 * 16 + 6
```

This is intentionally not the same anchor used by the player / collectibles,
because enemy decision centers use:

```text
X & 0x0F == 0x08
Y & 0x0F == 0x06
```

One enemy should be visible in the lair before the first maze-border release.
The waiting enemy is shown facing upward.

Visual note:

The level-1 enemy spritesheet is aligned through a render-only offset. The
movement / collision anchor remains the arcade-pixel enemy anchor above; the
sprite can be shifted slightly for visual alignment without changing gameplay
coordinates.

Temporary enemy work state
--------------------------

Confirmed.

Important RAM:

```text
61BD = EnemyTemp_Dir
61BE = EnemyTemp_X
61BF = EnemyTemp_Y
61C1 = EnemyRejectedDirMask
61C2 = fallback helper / work mask
61C4 = Enemy0_PreferredDir
61C5 = Enemy1_PreferredDir
61C6 = Enemy2_PreferredDir
61C7 = Enemy3_PreferredDir
61CE = Enemy0_ChaseTimer
61CF = Enemy1_ChaseTimer
61D0 = Enemy2_ChaseTimer
61D1 = Enemy3_ChaseTimer
61D2 = EnemyChase_RoundRobinIndex
61E1 = enemy freeze timer after vegetable bonus
6200..62AF = logical maze map, 11 x 16 cells
```

Logical maze map
----------------

Confirmed.

`0x6200..0x62AF` is an 11 x 16 logical maze map.

Each cell stores:

```text
high nibble = allowed directions
low nibble  = BFS guidance direction toward Lady Bug
```

Direction bits are the enemy direction bits:

```text
01 = left
02 = up
04 = right
08 = down
```

Important correction:

The high nibble represents **allowed** directions, not blocked directions.

Doors dynamically modify the allowed-direction high nibbles.

Godot model:

```csharp
public sealed class NavigationCell
{
    public MonsterDir AllowedDirections;  // high nibble equivalent
    public MonsterDir BfsDirection;       // low nibble equivalent
}
```

Door influence on navigation
----------------------------

Confirmed.

Doors are part of enemy navigation, not just rendering.

Door orientation changes:

- the logical maze allowed directions
- BFS propagation
- which enemy direction choices are legal
- local door/tile checks
- forced reversal edge cases

Relevant arcade routines:

```text
0x463A  initializes door influence in the logical maze map
0x467B  updates door influence dynamically
0x46C4  table of 20 special door cell indices
0x0D1D  table used to locate relevant video/door tiles
```

Observed central door tile states:

```text
0x36 = horizontal opening
0x3E = vertical opening
```

Observed local door / special tiles involved in tests:

```text
0x3F = local door/tile rejection at decision time
0x49 = forced reversal case outside decision center
```

These tile names are still implementation-level observations. In Godot, prefer semantic door state checks rather than hardcoding the tile IDs everywhere.

Validation at decision centers
------------------------------

Confirmed enough for implementation.

At a decision center, the preferred direction is checked in two distinct stages:

1. Static/logical maze validation, equivalent to `0x3911`.
2. Local door/tile/playfield validation, equivalent to `0x4130`.

A direction can be allowed by the static/logical maze map and still be rejected
by local door/tile geometry. In that case, the rejected direction contributes to
the `61C1` rejected-direction mask, and fallback must search another direction.

Practical Godot split:

```csharp
bool IsDirectionAllowedByMazeCell(Vector2I cell, MonsterDir dir);
bool IsDirectionBlockedByLocalDoorGeometry(MonsterEntity monster, MonsterDir dir);
```

Better implementation shape:

```csharp
public enum MonsterDirectionRejectReason
{
	None,
	NoDirection,
	StaticMazeBlocked,
	LocalDoorBlocked,
}

public readonly record struct MonsterDirectionValidation(
	bool Accepted,
	MonsterDirectionRejectReason RejectReason,
	PlayfieldStepKind? LocalBlockKind = null
);
```

Recommended validation flow:

```csharp
MonsterDirectionValidation ValidateCandidateDirection(
	MonsterEntity monster,
	MonsterDir candidate,
	EnemyNavigationGrid navigationGrid)
{
	if (candidate == MonsterDir.None)
		return new(false, MonsterDirectionRejectReason.NoDirection);

	Vector2I cell = ArcadePixelToLogicalCell(monster.ArcadePixelPos);

	if (!navigationGrid.IsDirectionAllowed(cell, candidate))
		return new(false, MonsterDirectionRejectReason.StaticMazeBlocked);

	PlayfieldStepResult step = EvaluateStep(monster, candidate);
	if (!step.Allowed)
		return new(false, MonsterDirectionRejectReason.LocalDoorBlocked, step.Kind);

	return new(true, MonsterDirectionRejectReason.None);
}
```

Keep these two validation layers conceptually separate even if the first Godot
implementation uses the shared `PlayfieldCollisionResolver` for the local layer.
The arcade has both concepts:

```text
0x3911 = static/logical maze direction validation
0x4130 = local door/tile validation
```

Do not collapse them into one opaque collision check too early. It makes logs
harder to interpret and can hide the difference between a real maze rejection
and a local door rejection.

Preferred direction
-------------------

Confirmed / Probable.

Each enemy has a preferred direction in `61C4..61C7`.

Preferred directions are prepared globally, then used by per-enemy decision logic.

Sources:

1. Base behavior from routines including `0x2E5C`, `0x40F8`, `0x40CC`.
2. Temporary BFS chase override from `0x46D8`.

Safe wording:

```text
Outside BFS chase phases, enemies receive a preferred direction from global
gameplay-state routines. These are influenced by level, elapsed time, difficulty,
and sometimes the Z80 R register pseudo-random source.
```

Do not describe non-chase enemy movement as purely random.

Base preferred direction two-mode behavior
------------------------------------------

Confirmed for the implemented mechanism.

The arcade alternates between a player-direction-derived mode and a per-enemy
pseudo-random mode. Routine `0x2E5C` compares B9 (`0x61B9`) with a two-entry
threshold table selected by level parity.

Current rule:

```text
B9 reload at life start = 0xB3
B9 decrements every gameplay tick and wraps over 256 values
odd level threshold     = 0x90
even level threshold    = 0x24

if B9 >= threshold:
    derive the four preferred directions from the player's current direction
else:
    generate one pseudo-random preferred direction per enemy
```

The `0x90` and `0x24` values were validated in MAME traces; the odd/even selection
comes directly from the Z80 branch. The current Godot pseudo-random branch uses a
small deterministic generator rather than the real Z80 R register.

Base preferences are recalculated continuously before the chase/BFS override.
Enemies consume the current preferred direction when they reach a decision center.

Player-direction-derived mode
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Enemy direction bits are rotated through the four one-bit directions:

```text
01 -> 08 -> 04 -> 02 -> 01
```

Observed with player current direction `01`:

```text
Enemy0 preferred dir = 08
Enemy1 preferred dir = 04
Enemy2 preferred dir = 02
Enemy3 preferred dir = 01
```

Expected examples:

```text
player dir 01 -> 08,04,02,01
player dir 02 -> 01,08,04,02
player dir 04 -> 02,01,08,04
player dir 08 -> 04,02,01,08
```

Use the player's current/effective direction, not only the currently held input.
When the player is standing still, the arcade still keeps a current direction.

Pseudo-random mode
~~~~~~~~~~~~~~~~~~

The pseudo-random branch generates one preferred direction per enemy. It does
not choose one shared random direction for all enemies. The arcade uses the Z80
`R` register as part of this behavior; the current Godot version uses a small
deterministic PRNG approximation so the behavior is varied and reproducible.

Current Godot implementation note:

```text
PrepareBasePreferredDirections();
TickAndActivateChaseTimersIfNeeded();
ApplyChaseBfsOverride();
UpdateEnemies();
```

The important rule is that base preferences are prepared before the BFS/chase
override. Chase remains authoritative for enemies with active chase timers.

BFS chase system
----------------

Confirmed.

The game builds a BFS guidance map from Lady Bug’s position.

BFS source:

```text
6027 = player X
6028 = player Y
```

Routine `0x447D` builds the map.

The low nibble of each logical maze cell stores the direction an enemy should take
from that cell to move toward Lady Bug.

This is a parent-direction map, not just a distance map.

Example:

```text
cell.BfsDirection = Left
```

means:

```text
from this cell, moving left leads toward Lady Bug
```

Chase timers and BFS override
-----------------------------

Confirmed.

`61CE..61D1` are per-enemy chase timers.

If an enemy's chase timer is nonzero:

1. convert its pixel position to a logical cell
2. read the cell's BFS direction
3. if nonzero, overwrite that enemy's preferred direction in `61C4..61C7`

Validated runtime case:

```text
Breakpoint 0x477D:
HL=61CE, IY=61C4, A=08, CH=04,00,00,00
```

Interpretation:

```text
Enemy0 chase timer was active.
BFS direction 08 was written into Enemy0_PreferredDir at 61C4.
```

Another runtime sequence confirmed that the same enemy later reached a decision
center and committed a direction derived from the active BFS preference.

Important nuance:

The BFS override is dynamic. It may write one value while the enemy is between
centers and a different value by the time it reaches the next decision center.

Godot implementation:

```csharp
foreach (MonsterEntity monster in monsters)
{
	if (monster.ChaseTimer <= 0)
		continue;

	MonsterDir bfsDir = navigationGrid.GetBfsDirection(monster.X, monster.Y);
	if (bfsDir != MonsterDir.None)
		monster.PreferredDirection = bfsDir;
}
```

Chase activation pattern
------------------------

Confirmed for the implemented table mechanism and the measured level examples.

The chase system uses:

```text
61B6 = 60-tick divider
61B7 / 61B8 = capped elapsed-life / activation counters
61D2 = round-robin selector
61CE..61D1 = per-enemy chase timers
```

Once per 60 simulation ticks:

- elapsed life time advances, capped at `0xF0`;
- active chase timers decrement;
- the current level's activation window is evaluated;
- if the window is open, `61D2` selects Enemy0 -> Enemy1 -> Enemy2 -> Enemy3;
- an already-running timer causes that opportunity to be skipped;
- an inactive enemy still waiting in the lair may be armed.

Activation window:

```text
patternIndex = 0x4788[min(level, 30) - 1]
V            = 0x47A6[patternIndex]
window open  = B8 mod 2^bitlength(V) == V
```

Validated examples:

```text
level 2  -> B8 mod 8 == 5  (13, 21, 29...)
level 4  -> B8 mod 4 == 3  (11, 15, 19, 23...)
level 15 -> B8 mod 2 == 1  (5, 7, 9, 11...)
```

Chase duration is not indexed by activation rank. It is read from the duration
table using elapsed life time:

```text
index = min(lifeSeconds >> 3, 30)
default duration = 0x47AE[index]  // 3..18 seconds
alternative      = 0x47CD[index]  // 10..40 seconds
```

The default table matches the factory-switch traces used during validation. The
alternative table remains configurable because the end-to-end UI label mapping
was not validated in the remake.

Fallback behavior
-----------------

Confirmed.

If the preferred direction fails validation, the enemy first tries to keep its
current direction. Fallback via `0x4241` is used only after both the preferred
direction and the current direction are rejected.

The enemy does not stop merely because the preferred direction is invalid.

Runtime validated generic fallback:

```text
FALLBACK HIT at 0x4241
TMP=02:58,86
C1=02
COMMIT after fallback: FINAL=08:58,87
```

Interpretation:

```text
Enemy at decision center 58,86.
Preferred/current candidate 02 was rejected.
Fallback selected 08.
```

Runtime validated door-local fallback:

```text
DOOR_LOCAL_REJECT at 0x4187
RET=4309
POS=68,66
TILE=3F
PROBE_DE=7066
PREF=04,04,04,04

Then fallback at 0x4241 with C1=06.
```

Interpretation:

```text
A direction that looked valid at the preferred-direction stage was rejected by
local door/tile geometry, then fallback logic searched another direction.
```

Important implementation consequence:

```text
center 180-degree turn != automatically forced reversal
```

At a decision center, an apparent reversal should usually be modeled as:

```text
preferred rejected
-> rejectedMask updated
-> current direction tested
-> current rejected if blocked
-> fallback order scans directions
-> fallback may choose the opposite direction
```

Do not add coordinate-specific reversal rules to reproduce individual traces.
Those rules may match one log while encoding the wrong mechanism.

Fallback candidate order
------------------------

Confirmed enough for implementation.

When the preferred direction fails validation, the arcade fallback routine scans
directions in this order:

```text
01, 02, 04, 08
```

Using the Godot enemy enum:

```csharp
private static readonly MonsterDir[] FallbackOrder =
{
	MonsterDir.Left,   // 01
	MonsterDir.Up,     // 02
	MonsterDir.Right,  // 04
	MonsterDir.Down,   // 08
};
```

Fallback must:

1. start only after the preferred direction and current direction have both failed;
2. skip any direction already present in the local `61C1`-like rejected-direction mask;
3. validate each candidate against the static/logical maze layer;
4. validate each candidate against the local door/tile/playfield layer;
5. return the first candidate accepted by both layers.

Recommended implementation:

```csharp
MonsterDir FindFallbackDirection(
	MonsterEntity monster,
	MonsterDir rejectedMask,
	EnemyNavigationGrid navigationGrid)
{
	foreach (MonsterDir candidate in FallbackOrder)
	{
		if ((rejectedMask & candidate) != 0)
			continue;

		MonsterDirectionValidation validation =
			ValidateCandidateDirection(monster, candidate, navigationGrid);

		if (validation.Accepted)
			return candidate;

		rejectedMask |= candidate;
	}

	return MonsterDir.None;
}
```

Implementation notes:

```text
- Do not choose the "best" fallback direction by distance to the player.
- Do not skip the current-direction test between preferred rejection and fallback.
- Do not treat fallback rejections as persistent enemy state beyond the current decision.
- If the safety fallback is needed, make it visible in debug logs.
```

The fixed order is likely closer to the arcade than a clever modern heuristic.

Forced reversal outside intersections
-------------------------------------

Confirmed in arcade traces and implemented through precise semantic gate probes.

Outside decision centers, the normal behavior is still to preserve the current
direction. However, routine `0x4189 -> 0x4347` can force an immediate reversal
when a rotating-gate arm blocks one of two direction-specific pixels ahead.

Implemented probe offsets:

```text
left  -> X-1 and X-3
up    -> Y-1 and Y-7
right -> X+2 and X+8
down  -> Y+2 and Y+4
```

On every one-pixel sub-step, including immediately after a decision-center choice,
Godot asks `Level.IsGateBlockingEnemyProbe(...)` whether either probe is blocked
by a rotating gate on the current movement axis. If so, the direction is reversed
without re-validating the opposite direction, then the enemy advances one pixel
back into the corridor it came from.

This is intentionally different from the removed broad rule that reversed on any
high-level gate or boundary rejection. Fixed walls and unrelated nearby gates do
not trigger this forced-reversal path.

A visible 180-degree turn at a decision center can still be a normal fallback
result rather than a forced reversal.

Skull death / enemy killed by skull
-----------------------------------

Confirmed.

A skull tile uses tile value `0x63`.

Runtime validated case:

```text
ENEMY_SKULL PC=4384
TILE=63
TILEHL=D19A
TMP=01:3D,D6
E0=12:3D,D6
CH=02,00,00,00
```

After skull collision:

```text
E0=10:3D,D6
```

Interpretation:

- enemy 0 was active at `3D,D6`
- skull hit cleared the active bit
- direction high nibble remained
- position was retained temporarily
- chase timer was not immediately cleared; it naturally decremented from `02 -> 01 -> 00`

Later reset:

```text
ENEMY_INIT_BEGIN PC=3061 SLOT_C=00
ENEMY_INIT_END IX=602B
E0=82:58,86
```

Implementation guidance:

```csharp
void KillMonsterBySkull(MonsterEntity monster)
{
	monster.MovementActive = false;
	monster.CollisionActive = false;
	monster.RuntimeState = MonsterRuntimeState.EmptyOrDead;

	// Arcade-like behavior:
	// position may remain briefly; chase timer can be allowed to decrement naturally.
}
```

Then later:

```csharp
void InitMonsterSlot(MonsterEntity monster)
{
	monster.Direction = MonsterDir.Down;
	monster.X = 0x58;
	monster.Y = 0x86;
	monster.RuntimeState = MonsterRuntimeState.WaitingInLair;
}
```

Normal release / lair initialization
------------------------------------

Confirmed / still partially open.

The helper at `0x05AE` scans enemy slots from `0x602B` by steps of five bytes.
It looks for a slot where `(state & 0x03) == 0`, then calls `0x3061`.

Runtime validated startup/helper path:

```text
RELEASE_SCAN PC=05AE
RELEASE_FREE_SLOT PC=05C3 SLOT_C=00 HL=602B
ENEMY_INIT_BEGIN PC=3061 RET=05C6 SLOT_C=00
ENEMY_INIT_END PC=3086 IX=602B
E0=82:58,86
RELEASE_RETURN ...
E0=81:58,86
```

But during normal round progression, many initializations were observed through
another caller:

```text
ENEMY_INIT_BEGIN PC=3061 RET=4474 SLOT_C=01 -> IX=6030
ENEMY_INIT_BEGIN PC=3061 RET=4474 SLOT_C=00 -> IX=602B
ENEMY_INIT_BEGIN PC=3061 RET=4474 SLOT_C=02 -> IX=6035
ENEMY_INIT_BEGIN PC=3061 RET=4474 SLOT_C=03 -> IX=603A
```

Interpretation:

- `0x3061` is the central enemy slot initialization routine.
- `0x05AE` is a slot-scan/helper path, clearly used in some setup/release contexts.
- Normal in-round release/reinitialization often reaches `0x3061` through a path returning to `0x4474`.
- A slot may be initialized as `0x82:58,86`, then appear as `0x81:58,86` while waiting/prepared in the lair.

Godot implementation recommendation:

Separate:

```text
PrepareMonsterInLair
ReleaseMonsterFromLair
MonsterInMaze movement
```

instead of a single "spawn active monster" call.

Maze-border timer / release cadence
-----------------------------------

Confirmed from code reading; release path still being mapped.

Relevant RAM:

```text
60AA = MazeBorderCountdown
60AB = MazeBorderPeriod
```

Relevant routines:

```text
0x35E3 / 0x35FE = border timer initialization path
0x39B1 = border timer update
```

Known period table from earlier analysis:

```text
Level 1     -> 9 ticks per border step
Level 2-4   -> 6 ticks per border step
Level 5+    -> 3 ticks per border step
```

Runtime release test at level 1 observed:

```text
BORDER_INIT_DONE 60AA=09 60AB=09
```

Implementation note:

Enemy release should be synchronized with the maze-border timer, not with the
letter/heart color cycle.

Maze-border release cadence Godot note
--------------------------------------

Confirmed for the current first Godot implementation; still worth validating
against additional arcade traces if exact visual phase semantics become important.

The enemy release signal should occur after each full visible border cycle that
represents an enemy-release lap.

Do not accidentally model the border as two gameplay release cycles where:

```text
white -> green = release enemy
green -> white = no release
```

That creates an incorrect skipped release opportunity: one lap releases an enemy,
the next lap does nothing, then the following lap releases another enemy.

For the Godot implementation, the external behavior should be:

```text
each completed release lap -> one enemy-release opportunity
```

If the renderer internally uses fill / clear visual phases, hide that detail
inside the timer view/runtime and expose only the intended gameplay cadence to
`EnemyRuntime`.

Vegetable bonus / enemy freeze
------------------------------

Confirmed.

When Lady Bug eats the central vegetable bonus, enemies freeze but remain fatal.

Runtime validated:

```text
VEGETABLE_COLLECT PC=0898 P=58,86 6021=82:58,86 61E1=00
FREEZE_SET PC=08B4 61E1=05
```

During freeze:

```text
FREEZE_TICK 61E1=04
enemy positions unchanged
```

Main loop behavior:

- `61E1` is tested before `Enemy_UpdateAll`.
- If `61E1 != 0`, enemy movement update is skipped.
- Collision checks still run.

Runtime validated fatal collision during freeze:

```text
COLLISION_DURING_FREEZE HL=6035 P=58,B5 61E1=04
E2=22:58,BD

PLAYER_FATAL P=58,B5 61E1=04
```

Distance:

```text
dx = 0
dy = 8
```

This matches the normal collision window `< 9` pixels.

Implementation:

```csharp
if (EnemyFreezeTimer > 0)
{
	EnemyFreezeTimer--;

	// Do not update enemy AI or enemy movement.
}
else
{
	UpdateEnemies();
}

// Always check collision, even while frozen.
CheckPlayerEnemyCollisions();
```

Do not disable enemy hitboxes during freeze.

Player death caused by enemy
----------------------------

Confirmed / implementation-observed.

When Lady Bug collides with an enemy, enemy views should disappear immediately
before the player death animation begins.

The player death sequence then runs normally:

```text
red shrink / ball phase -> ghost apparition / movement phase
```

After the death sequence completes, if lives remain, the current attempt is
reset without fully reloading the level:

```text
- Lady Bug respawns at the normal start cell.
- All enemies that were active in the maze are cleared.
- The enemy system returns to a fresh attempt state.
- One enemy is visible again in the lair, waiting for release.
- The maze-border release timer is reset.
- Already consumed collectibles remain consumed.
- Rotating gate states are preserved.
- Score, multiplier, lives and word progress are preserved.
```

Implementation consequence:

```csharp
HideEnemyViewsImmediately();
StartPlayerDeathSequence();

// Later, when the death sequence ends and lives remain:
ResetEnemiesForNewAttempt();
ResetMazeBorderTimer();
RespawnPlayerAtStartCell();
```

This is a partial attempt reset, not a full level restart.
Do not rebuild the collectible field and do not reset rotating gates when the
player dies from touching an enemy.

Player/enemy collision timing
-----------------------------

Confirmed.

In the main gameplay loop, the ordering is:

```text
Enemy_UpdateAll
other update path
Player_UpdateMovement
player-related update
Player/enemy collision check
```

Runtime validated death case:

```text
Enemy moved first.
Player moved after.
Collision checked after both.
```

Collision window:

```text
abs(playerX - enemyX) < 9
abs(playerY - enemyY) < 9
```

If both are true, the player dies.

Implementation consequence:

```csharp
UpdateEnemiesIfNotFrozen();
UpdatePlayer();
CheckPlayerEnemyCollisions();
```

Do not check player/enemy collision before movement if you want the arcade edge cases.

Current Godot tick-order note
-----------------------------

For the current implementation, the most important ordering constraint remains:

```text
Update enemies
Update player
Check player/enemy collision
```

This matches the validated arcade behavior where the enemy update runs before
player movement, and collision is checked after both have moved.

The current Godot board tick can be summarized as:

```text
Advance gates
Advance maze-border timer / possibly release enemy
Update enemy system
Update player movement
Check player/enemy collision
Advance collectible color cycle
```

The exact placement of the collectible color-cycle tick is less critical than
preserving the enemy -> player -> collision ordering.

Implementation-ready algorithm
------------------------------

### Per fixed tick

```csharp
void UpdateEnemySystem()
{
    navigationGrid.Rebuild(staticMaze, gateSystem);
    navigationGrid.BuildBfsGuidance(playerCell);

    basePreferenceSystem.PrepareBasePreferredDirections(monsters, playerDirection);
    chaseSystem.AdvanceOneTick(monsters);
    chaseSystem.ApplyBfsOverride(monsters, navigationGrid, ToLogicalCell);

    int subSteps = speedSystem.ComputeStepsForThisTick(
        levelNumber,
        chaseSystem.LifeSecondsCapped);

    foreach (MonsterEntity monster in monstersInSlotOrder)
    {
        if (!monster.MovementActive)
            continue;

        for (int step = 0; step < subSteps; step++)
        {
            movementAi.UpdateMonsterOnePixel(monster, navigationGrid);
            TryHandleSkullCollision(monster);

            if (!monster.MovementActive)
                break;
        }
    }
}
```

Important ordering:

```text
base preferences
-> chase countdown / activation
-> BFS override
-> one global speed calculation
-> Enemy0 all sub-steps
-> Enemy1 all sub-steps
-> Enemy2 all sub-steps
-> Enemy3 all sub-steps
```

### Per one-pixel sub-step

```csharp
void UpdateMonsterOnePixel(MonsterEntity monster)
{
    MonsterDir chosen = monster.Direction;

    if (IsMonsterDecisionCenter(monster.X, monster.Y))
        chosen = ChoosePreferredCurrentOrFallback(monster);

    if (IsBlockingGateAtNearOrFarProbe(monster.Position, chosen))
        chosen = chosen.Opposite();

    monster.Direction = chosen;
    monster.Position += chosen.ToVector();
}
```

The decision-center order remains:

```text
validate preferred
-> validate current
-> fallback in order 01,02,04,08
```

The preferred direction from BFS never bypasses static maze validation, local
playfield validation, the rejected mask or fallback.

### Local probes

Normal direction validation uses the enemy collision profile:

```text
left  X-1,Y
up    X,Y-7
right X+8,Y
down  X,Y+2
```

Forced gate reversal uses the separate near/far offsets:

```text
left  X-1 / X-3
up    Y-1 / Y-7
right X+2 / X+8
down  Y+2 / Y+4
```

The current Godot implementation maps these probes to semantic rotating-gate
state through the high-level collision resolver rather than reproducing the
original tile bytes literally.

Recommended Godot/C# architecture
---------------------------------

Current classes and responsibilities:

```text
MonsterEntity
    Owns per-enemy position, directions, chase timer and runtime flags.

EnemyRuntime
    Coordinates fixed-tick update order, four slots, views, release, skulls,
    reset, chase and speed sub-steps.

EnemyMovementAi
    Implements center decisions, fallback, precise gate reversal and one-pixel movement.

EnemyMovementTuning
    Owns normal collision probes and gate-reversal probe tables.

EnemyBasePreferenceSystem
    Owns B9 and non-chase preferred directions.

EnemyChaseSystem
    Owns elapsed-life timing, activation tables, round-robin and duration tables.

EnemySpeedSystem
    Owns level/time speed tables and the shared fractional accumulator.

EnemyNavigationGrid
    Stores allowed directions and BFS directions.

Level / PlayfieldCollisionResolver / LevelGateRuntime
    Expose semantic static-wall and rotating-gate validation.

EnemyReleaseBorderTimer
    Handles release cadence and border animation synchronization.

EnemyController
    Owns rendering and animation only.
```

Known limitations of the current Godot implementation
-----------------------------------------------------

The current implementation includes the major validated movement mechanisms and
is no longer merely the original first-playable approximation. It is still a
high-level remake, not a verified instruction-exact port.

Implemented:

```text
- four enemy slots and normal skull -> lair -> release cycle
- level/slot visual selection
- fixed-tick integer movement with one-pixel sub-steps
- ROM-table speed ramp with one global fractional accumulator
- decision-center preferred -> current -> fallback logic
- local rejected-direction mask and fixed fallback order 01,02,04,08
- static maze + dynamic gate navigation
- B9 reload 0xB3 and odd/even thresholds 0x90/0x24
- BFS chase override
- ROM-table chase activation windows and durations
- chase arming for waiting lair enemies
- precise near/far gate forced-reversal probes
- player/enemy collision, vegetable freeze and skull deaths
- attempt reset while preserving collectibles and gate orientations
```

Remaining approximations / open points:

```text
- deterministic C# PRNG instead of the Z80 R-register source
- alternative difficulty-table UI label mapping not validated end-to-end
- speed accumulator reset phase at life restart is a documented assumption
- rejected-direction mask does not persist across multiple sub-steps of one enemy frame
- semantic Godot gate model replaces literal local tile-code filtering
- exact lair/release visual and low-level state transitions remain simplified
- exact arcade vegetable duration / visual timing remains approximate
- no automated full-project enemy regression suite
```

Recommended implementation order
--------------------------------

The major movement-fidelity work is now implemented. Future work should be
problem-driven rather than a new broad reverse-engineering cycle:

1. Keep the current implementation as a stable checkpoint.
2. Preserve four-slot, skull-return, vegetable and player-collision invariants.
3. Add lightweight regression scenarios before changing chase, speed or gate probes.
4. Refine lair visuals, PRNG behavior or difficulty mapping only when new evidence
   or a visible gameplay problem justifies it.
5. Avoid unrelated movement refactors merely to mirror the original RAM layout.

What is solid enough to implement now
-------------------------------------

Confirmed / implemented enough:

```text
- four 5-byte arcade enemy slots represented by four MonsterEntity instances
- direction encoding 01/02/04/08
- one-pixel sub-steps and stable slot processing order
- global fractional speed accumulator and ROM speed tables
- decision center X&0F=08, Y&0F=06
- preferred directions at 61C4..61C7
- B9 reload 0xB3 and odd/even thresholds 0x90/0x24
- chase timers 61CE..61D1 and round-robin selector 61D2
- activation tables 0x4788/0x47A6 and duration table 0x47AE
- BFS guidance in the low nibble of 6200..62AF
- preferred -> current -> fallback validation order
- fallback order 01,02,04,08
- normal enemy collision probes and separate near/far gate reversal probes
- skull tile 63 kills enemies and the remake returns them to the lair/release cycle
- frozen enemies remain fatal
- player/enemy collision window is <9 pixels on both axes
```

Open / configurable:

```text
- exact Z80 R-register pseudo-random distribution
- UI label mapping for the alternative chase/speed difficulty tables
- fractional accumulator phase across every possible life-reset edge case
- persistent 61C1-like mask semantics across multiple sub-steps
- literal local tile-code filtering around every rotating-gate geometry
- exact visual/lair state progression after 0x81 / 0x82 transitions
- exact arcade vegetable duration and low-level visual cadence
```

Regression scenarios to preserve
--------------------------------

Core slot / lair cycle:

```text
exactly four logical enemy slots always exist
all four enemies can become active
an enemy killed by a skull returns to the lair and can be released again
repeated skull deaths never permanently reduce the enemy count
the vegetable can still appear once its four-active-enemy condition is met
```

Movement and gates:

```text
one-pixel movement works in all four directions
speed sub-step count is computed once per tick and shared by all slots
one enemy completes all sub-steps before the next slot
level 1 begins at 1.0 px/tick on the default table
higher levels / elapsed life time can produce fractional and multi-pixel ticks
preferred -> current -> fallback remains the center decision order
fallback scans 01,02,04,08 and validates every candidate
a center 180-degree turn can be fallback rather than forced reversal
outside centers, no broad boundary result causes reversal
precise near/far gate probes reverse only when a gate blocks the movement axis
no gate causes stable straight movement without ping-pong reversals
skull collision is checked after every sub-step
```

Preferences and chase:

```text
B9 resets to 0xB3 and wraps as an 8-bit counter
odd/even thresholds are 0x90 / 0x24
player dir 01 produces 08,04,02,01 in deterministic mode
pseudo-random mode produces one direction per enemy
base preferences run before chase/BFS override
level 2 activation satisfies B8 mod 8 == 5
level 4 activation satisfies B8 mod 4 == 3
level 15 activation satisfies B8 mod 2 == 1
chase duration uses table[lifeSeconds >> 3]
a waiting lair enemy can receive and count down a chase timer
BFS override still passes through normal validation and fallback
```

Collision / freeze / reset:

```text
player/enemy collision is checked after enemy and player movement
collision window remains abs(dx)<9 and abs(dy)<9
vegetable freeze skips enemy movement but enemies remain fatal
player death resets chase and speed attempt state
collectibles and rotating-gate orientations remain preserved after player death
```

Debugging anchors
-----------------

Useful breakpoints:

```text
0x2E5C = base preferred-direction preparation
0x43D4 = enemy commit final dir/x/y
0x42E6 = preferred direction decision
0x4241 = fallback start
0x4187 = local door rejection return
0x4189 = forced reversal test
0x4347 = forced reversal direction inversion
0x477D = BFS preferred direction write
0x4752 = chase timer load
0x4384 = enemy skull hit
0x3061 = enemy slot initialization
0x0898 = vegetable collected
0x08B4 = freeze set
0x088B = player/enemy collision
0x0AF3 = player fatal collision handler
```

Final implementation philosophy
-------------------------------

Do not copy the RAM layout literally into Godot.

Preserve the gameplay principles:

```text
integer arcade pixels
fixed tick update
one-pixel enemy sub-steps with ROM-table speed
stable slot-order processing
decision only at cell centers
dynamic allowed directions from doors
B9 base-preference cycle with validated reload / thresholds
BFS guidance toward Lady Bug
ROM-table chase activation and duration
round-robin activation, including waiting lair slots
local door/tile rejection
precise near/far rotating-gate forced reversal without broad boundary reversal
stateful lair/release behavior
frozen but still fatal enemies
```

The result should feel close to the arcade while remaining readable and maintainable.
