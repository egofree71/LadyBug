# Enemy Movement

Project: Lady Bug remake in Godot 4.6.2 (.NET / C#)

Purpose of this document
------------------------

This document is a compact implementation reference for the monster/enemy system
in the Lady Bug Godot remake.

It summarizes the reverse-engineered arcade behavior, the important RAM/routine
anchors, and the runtime cases validated with MAME debugger logs.

The goal is **not** to translate the Z80 instruction-by-instruction. The goal is
to reproduce the arcade gameplay feel faithfully in clean Godot/C# code.

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

Confirmed.

Enemy movement is a hybrid system:

1. Each enemy has a base preferred direction.
2. If a temporary chase timer is active, a BFS direction toward Lady Bug can override that preference.
3. At decision centers, the preferred direction is validated against:
   - the logical maze map
   - local door / tile geometry
4. If the preferred direction fails, fallback logic searches another direction.
5. Outside decision centers, the enemy normally continues straight.
6. In special door-related cases, the enemy may be forced to reverse direction even outside a decision center.
7. Movement is pixel-by-pixel, not tile-by-tile.

Implementation consequence:

```text
monster movement = pixel step + center decisions + door edge cases + temporary BFS pressure
```

It should not be implemented as a modern pathfinding agent that constantly chases the player.

Key arcade routines
-------------------

```text
0x407E  Enemy_UpdateAll
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

Confirmed for the current first Godot implementation.

The initial playable enemy implementation is organized as a clean Godot/C#
runtime layer rather than as a literal RAM-layout port.

Current runtime classes:

```text
EnemyRuntime
	Coordinates the four enemy slots, enemy views, lair visibility, enemy
	release, enemy reset after player death, chase update, movement update,
	skull checks and player/enemy collision checks.

MonsterEntity
	Stores the gameplay state of one enemy slot: arcade-pixel position,
	direction, preferred direction, chase timer, runtime state, movement flag,
	collision flag and lair visibility.

EnemyController
	Owns only the Godot visual representation of one enemy. It loads the enemy
	spritesheet, selects the move_right / move_up animation, applies FlipH / FlipV,
	and synchronizes the scene position from arcade-pixel gameplay state.

EnemyNavigationGrid
	Builds the enemy navigation map from the static MazeGrid plus the dynamic
	GateSystem, then builds the BFS guidance map from Lady Bug's current cell.

EnemyMovementAi
    Applies one-pixel movement, decision-center direction choice, preferred
    direction validation, fallback direction selection and simplified forced
    reversal when a door/gate blocks the current path.

EnemyBasePreferenceSystem
    Prepares the non-chase preferred directions continuously before chase/BFS
    overrides. The current implementation follows the new B9-like two-mode
    finding: player-direction-derived preferences when the counter is above the
    threshold, pseudo-random per-enemy preferences below it.

EnemyChaseSystem
    Owns the arcade-inspired timing divider, B8-like activation counter,
    round-robin enemy selector and chase duration sequence.
```

The current implementation deliberately separates gameplay state from rendering:

```text
MonsterEntity = gameplay truth
EnemyController = visual sync only
```

This keeps future refinements possible without mixing movement rules with sprite
setup, visual offsets or scene-node visibility.

Pixel movement
--------------

Confirmed.

Routine `0x4224` moves by exactly one arcade pixel:

```text
left  -> X--
up    -> Y--
right -> X++
down  -> Y++
```

Godot implementation should use fixed tick integer arcade-pixel coordinates.

Do not use:

```csharp
Position += direction * speed * delta;
```

Use a fixed simulation tick and commit one-pixel steps.

Decision centers
----------------

Confirmed.

Enemies normally choose a new direction only at logical cell centers:

```text
X & 0x0F == 0x08
Y & 0x0F == 0x06
```

At any other pixel position, the enemy normally continues in its current direction,
except for the door-related forced reversal case described below.

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

Confirmed.

At a decision center, the preferred direction is checked in two stages:

1. Cell-level maze validation via `0x3911`.
2. Local door/tile validation via `0x4130`.

Practical Godot split:

```csharp
bool IsDirectionAllowedByMazeCell(MonsterCell cell, MonsterDir dir);
bool IsDirectionBlockedByLocalDoorGeometry(MonsterEntity monster, MonsterDir dir);
```

Keep them separate.

Do not collapse both into one generic collision check too early; the arcade code treats them as separate layers.

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

Confirmed for the level-1 stationary-player test; keep cadence and higher-level
patterns configurable until more traces are collected.

The arcade does not keep one fixed base preferred direction, and it is not pure
random movement. Routine `0x2E5C` compares the B9-like value at `0x61B9` with a
threshold table value. In the observed level-1 test, the threshold was `0x90`.

Observed level-1 rule:

```text
if 0x61B9 >= 0x90:
    derive the four preferred directions from the player's current direction
else:
    generate one pseudo-random preferred direction per enemy
```

Important implementation consequence:

```text
Base preferred directions are recalculated continuously, not only when an enemy
reaches an intersection. The enemy reads the current preferred direction when it
arrives at a decision center.
```

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

Confirmed for observed levels; partly open for advanced levels.

The chase system uses:

```text
61B6 = timing divider
61B7 = elapsed tick-like counter
61B8 = chase activation counter
61B9 = base preferred-direction mode / countdown-like value
61D2 = round-robin selector
```

About once per second-like tick:

- `61B8` increments
- active chase timers decrement
- a new chase activation may be loaded at a precise timing point

Round-robin behavior:

```text
61D2 selects Enemy0 -> Enemy1 -> Enemy2 -> Enemy3 -> repeat
```

If the selected enemy already has a nonzero chase timer, that activation opportunity is skipped/lost.

Measured chase activation tests
-------------------------------

Runtime logs measured the following early-level behavior with the current DIP state
observed as `DSW0=DF`.

### Level 1

Observed:

```text
first activation around B8=0x15
then every +0x08 B8 units
round-robin Enemy0, Enemy1, Enemy2, Enemy3...
```

Observed duration sequence:

```text
04, 04, 05, 05, 06, 06, 07, 07...
```

### Level 2

Observed with `CHASE_LOAD` breakpoint at `0x4752`:

```text
B8=0x0D -> Enemy0 / TIMER=61CE / VAL=03 / RR=01
B8=0x15 -> Enemy1 / TIMER=61CF / VAL=04 / RR=02
B8=0x1D -> Enemy2 / TIMER=61D0 / VAL=04 / RR=03
```

Observed spacing:

```text
+0x08 B8 units
```

### Level 5

Observed with `CHASE_LOAD` breakpoint at `0x4752`:

```text
B8=0x05 -> Enemy0 / TIMER=61CE / VAL=03 / RR=01
B8=0x0D -> Enemy1 / TIMER=61CF / VAL=03 / RR=02
B8=0x15 -> Enemy2 / TIMER=61D0 / VAL=04 / RR=03
B8=0x1D -> Enemy3 / TIMER=61D1 / VAL=04 / RR=04
```

Observed spacing:

```text
+0x08 B8 units
```

Implementation recommendation for first version:

```csharp
int firstActivationB8 =
	level == 1 ? 0x15 :
	level < 5  ? 0x0D :
				 0x05;

bool shouldActivate = b8 >= firstActivationB8
				   && ((b8 - firstActivationB8) % 0x08) == 0;
```

Use a table for durations rather than a guessed formula.

Open / caution:

The code contains tables suggesting more complex level/pattern behavior, and
possibly more frequent activation on later levels. Runtime tests above validated
levels 1, 2, and 5 only, and only early sequences. Do not claim all later levels
are `+0x08` unless tested.

Relevant routines/tables:

```text
0x46FB..0x4714 = activation window check against 61B8
0x471E..0x4731 = round-robin enemy selection and skip-if-active
0x4734..0x4752 = duration table selection and timer load
0x4788 = level-to-pattern table
0x47A6 = pattern translation table
0x47AE / 0x47CD = duration tables
```

Fallback behavior
-----------------

Confirmed.

If the preferred direction fails validation, the code searches for a fallback direction via `0x4241`.

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

Fallback candidate order
------------------------

Probable / needs final runtime confirmation with the correct log.

Code reading suggests that fallback candidates are scanned in this order:

```text
01, 02, 04, 08
```

and candidates already marked in `61C1` are skipped.

Suggested first implementation:

```csharp
private static readonly MonsterDir[] FallbackOrder =
{
	MonsterDir.Left,
	MonsterDir.Up,
	MonsterDir.Right,
	MonsterDir.Down,
};

MonsterDir FindFallbackDirection(MonsterEntity monster, MonsterDir rejectedMask)
{
	foreach (MonsterDir candidate in FallbackOrder)
	{
		if ((rejectedMask & candidate) != 0)
			continue;

		if (!IsDirectionAllowedByMazeCell(monster.Cell, candidate))
			continue;

		if (IsDirectionBlockedByLocalDoorGeometry(monster, candidate))
			continue;

		return candidate;
	}

	return monster.Direction; // safety fallback
}
```

Implementation note:

This simple fixed-order fallback is likely closer to the arcade than a clever
modern heuristic. Do not choose the "best" direction by distance here.

Forced reversal outside intersections
-------------------------------------

Confirmed.

Outside decision centers, the enemy normally keeps moving in the same direction.

However, `0x4189` can detect a special door/local-tile situation and trigger a forced reversal through `0x4347`.

Runtime validated case:

```text
FORCED REVERSAL HIT
PC=4347
TMP=02:68,4B
A=49
HL=D249
```

Observation:

- the enemy was between cell centers
- a pivoting door changed the local path state
- the enemy reversed direction immediately

Implementation helper:

```csharp
if (!IsMonsterDecisionCenter(monster.X, monster.Y))
{
	if (ShouldForceReverseBecauseOfDoor(monster))
		monster.Direction = Opposite(monster.Direction);

	MoveOnePixel(monster);
}
```

Opposite mapping:

```text
01 <-> 04
02 <-> 08
```

Do not restrict door handling to intersections only.

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
void RunGameplayTick()
{
	UpdateMazeBorderTimer();
	TickCollectibleColorCycle();

	if (EnemyFreezeTimer > 0)
	{
		EnemyFreezeTimer--;
	}
	else
	{
		UpdateEnemySystem();
	}

	UpdatePlayerMovement();

	CheckPlayerEnemyCollisions();
}
```

### Enemy system update

```csharp
void UpdateEnemySystem()
{
	UpdateDoorInfluenceInNavigationGrid();

	BuildBfsGuidanceFromPlayer();

	PrepareBasePreferredDirections();

	TickAndActivateChaseTimersIfNeeded();

	ApplyChaseBfsOverride();

	foreach (MonsterEntity monster in monsters)
	{
		if (!monster.MovementActive)
			continue;

		UpdateMonsterOnePixel(monster);
	}
}
```

### Per-monster update

```csharp
void UpdateMonsterOnePixel(MonsterEntity monster)
{
	MonsterDir dir = monster.Direction;
	int x = monster.X;
	int y = monster.Y;

	if (IsMonsterDecisionCenter(x, y))
	{
		MonsterDir preferred = monster.PreferredDirection;
		MonsterDir rejectedMask = MonsterDir.None;

		if (CanUseDirection(monster, preferred))
		{
			dir = preferred;
		}
		else
		{
			rejectedMask |= preferred;
			dir = FindFallbackDirection(monster, rejectedMask);
		}
	}
	else
	{
		if (ShouldForceReverseBecauseOfDoor(monster))
			dir = Opposite(dir);
	}

	MoveOnePixel(monster, dir);
}
```

### Direction validation

```csharp
bool CanUseDirection(MonsterEntity monster, MonsterDir dir)
{
	if (dir == MonsterDir.None)
		return false;

	if (!IsDirectionAllowedByMazeCell(monster.Cell, dir))
		return false;

	if (IsDirectionBlockedByLocalDoorGeometry(monster, dir))
		return false;

	return true;
}
```

Recommended Godot/C# architecture
---------------------------------

Suggested classes:

```text
MonsterEntity
MonsterRuntimeState
MonsterSystem
MonsterAi
MonsterPreferenceSystem
ChaseSystem
BfsNavigator
NavigationGrid
MovementValidator
DoorManager / LevelGateRuntime
MazeBorderTimer
```

Suggested responsibilities:

```text
MonsterEntity
	Owns per-enemy position, direction, preferred dir, chase timer, runtime state.

MonsterSystem
	Coordinates fixed-tick enemy update.

MonsterAi
	Implements per-enemy decision center, fallback, forced reversal, one-pixel movement.

MonsterPreferenceSystem
	Builds base preferred directions.

ChaseSystem
	Owns 61CE..61D1-like timers, 61D2-like round-robin, duration tables.

BfsNavigator
	Builds BFS guidance map from Lady Bug.

NavigationGrid
	Stores allowed directions and BFS directions.

MovementValidator
	Splits maze-cell validation from door-local validation.

MazeBorderTimer
	Handles enemy release timing and border animation synchronization.
```

Known limitations of the current Godot implementation
-----------------------------------------------------

The current enemy implementation is a first playable approximation. It is a good
commit point, but it is not yet arcade-perfect.

Implemented in the current Godot version:

```text
- four enemy slots
- one visible waiting enemy in the lair
- release from the maze-border timer
- one-pixel arcade movement
- decision-center based direction changes
- navigation from static maze + rotating gates
- two-mode B9-like base preferred-direction generation
- BFS chase pressure
- round-robin chase timers
- player/enemy collision
- enemy views hidden immediately during player death sequence
- enemy reset after player death without resetting collectibles or gates
- enemy killed by skull
- level-1 enemy spritesheet and visual offset
```

Still approximate / to refine:

```text
- exact B9 cadence / reload behavior and Z80 R-register pseudo-random details beyond the observed tests
- exact enemy release path from the lair into the maze
- exact behavior of enemies around rotating doors
- exact local door rejection and forced reversal semantics
- full chase activation tables for later levels / DIP settings
- vegetable bonus and enemy freeze behavior
- enemy type selection for later levels
- detailed visual/lair state progression around 0x81 / 0x82 transitions
```

Recommended implementation order
--------------------------------

1. MonsterEntity and fixed tick pixel movement.
2. Decision center test.
3. Logical navigation grid with allowed directions.
4. Dynamic door influence on navigation grid.
5. BFS guidance map from Lady Bug.
6. Chase timers and round-robin activation.
7. Preferred direction + BFS override.
8. Preferred direction validation and fallback.
9. Door-local rejection and forced reversal.
10. Skull death / reset to lair.
11. Normal release from lair / maze-border timer.
12. Vegetable freeze with collision still active.
13. Refine base preferred direction generation.

What is solid enough to implement now
-------------------------------------

Confirmed enough:

```text
- 5-byte enemy slots at 602B/6030/6035/603A
- enemy direction encoding 01/02/04/08
- pixel-by-pixel movement
- decision center X&0F=08, Y&0F=06
- preferred dirs at 61C4..61C7
- B9-like two-mode base preference behavior observed for level 1
- chase timers at 61CE..61D1
- round-robin chase selector 61D2
- BFS guidance in low nibble of 6200..62AF
- doors modify navigation
- local door validation can reject a direction
- forced reversal can occur outside intersections
- skull tile 63 kills enemies
- vegetable sets enemy freeze timer 61E1 and freezes movement
- frozen enemies remain fatal
- collision window is <9 pixels in both axes
```

Open / should remain configurable:

```text
- exact B9 reload/cadence and threshold tables outside observed level-1 behavior
- exact Z80 R-register pseudo-random distribution
- exact full chase activation table for high levels and all DIP difficulties
- exact semantics of bit0 in enemy state byte
- exact visual/lair state progression after 0x81 / 0x82 transitions
- exact fallback order runtime confirmation using the correct fallback-order log
- exact semantic names for all door-local tile values
```

Regression scenarios to preserve
--------------------------------

Movement:

```text
enemy straight movement all directions
enemy decision at X&0F=08 / Y&0F=06
preferred direction accepted
preferred direction rejected by maze -> fallback
preferred direction rejected by local door tile -> fallback
forced reversal outside decision center from door change
```

Base preference:

```text
B9 >= threshold derives four preferences from player current direction
player dir 01 -> 08,04,02,01
pseudo-random mode generates one direction per enemy
base preferences are prepared before BFS/chase override
```

Chase:

```text
BFS writes preferred direction for active chase timer
only one enemy selected by round-robin
selected enemy already chasing -> activation skipped
level 1 first activation around B8=15
level 2 first activation around B8=0D
level 5 first activation around B8=05
```

State:

```text
enemy skull death: active bit clears, position retained briefly
enemy reset to 82:58,86 via 0x3061
enemy waits/prepares in lair as 81:58,86
vegetable freeze sets 61E1=05
enemy movement skipped while 61E1>0
collision remains fatal while frozen
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
one-pixel enemy movement
decision only at cell centers
dynamic allowed directions from doors
B9-like base preferred-direction preparation
BFS guidance toward Lady Bug
temporary chase timers
round-robin activation
local door/tile rejection
door-forced reversal outside centers
stateful lair/release behavior
frozen but still fatal enemies
```

The result should feel close to the arcade while remaining readable and maintainable.
