using System;
using System.Collections.Generic;
using Godot;
using LadyBug.Actors;
using LadyBug.Gameplay.Gates;
using LadyBug.Gameplay.Maze;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Coordinates the four enemy slots for one active level.
/// </summary>
/// <remarks>
/// This class is the bridge between the level tick and the lower-level enemy
/// helpers. It owns the logical slots, view nodes, navigation grid, chase timers,
/// lair visibility, skull handling, and death/reset behavior.
/// </remarks>
public sealed class EnemyRuntime
{
    // Parent node that contains all runtime-created enemy view nodes.
    private readonly Node2D _root;

    // Owning level, used as the source of coordinate conversion and collision helpers.
    private readonly Level _level;

    // Per-tick navigation map rebuilt from the static maze plus current rotating gates.
    private readonly EnemyNavigationGrid _navigationGrid;

    // One-pixel movement and direction-decision helper.
    private readonly EnemyMovementAi _movementAi;

    // Arcade-inspired non-chase base preference generator.
    private readonly EnemyBasePreferenceSystem _basePreferenceSystem;

    // Temporary chase timer system with round-robin enemy activation.
    private readonly EnemyChaseSystem _chaseSystem;

    // Four arcade-like logical enemy slots.
    private readonly MonsterEntity[] _monsters = new MonsterEntity[EnemyMovementTuning.MaxEnemyCount];

    // Godot view node paired with each logical enemy slot.
    private readonly EnemyController[] _views = new EnemyController[EnemyMovementTuning.MaxEnemyCount];

    // True while the player death animation is running after an enemy collision.
    // During that pause the enemy logical slots are intentionally left untouched,
    // but all enemy rendering must be suppressed until the board attempt restarts.
    private bool _suppressEnemyViewsDuringPlayerDeath;

    /// <summary>
    /// Creates the runtime enemy system and its four enemy view nodes.
    /// </summary>
    /// <param name="root">Scene parent used for enemy views.</param>
    /// <param name="level">Owning level.</param>
    /// <param name="mazeGrid">Static logical maze.</param>
    /// <param name="gateSystem">Current rotating-gate system.</param>
    /// <param name="levelNumber">Current level number used by chase timing.</param>
    public EnemyRuntime(
        Node2D root,
        Level level,
        MazeGrid mazeGrid,
        GateSystem gateSystem,
        int levelNumber)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _level = level ?? throw new ArgumentNullException(nameof(level));
        MazeGrid = mazeGrid ?? throw new ArgumentNullException(nameof(mazeGrid));
        GateSystem = gateSystem ?? throw new ArgumentNullException(nameof(gateSystem));

        _navigationGrid = new EnemyNavigationGrid(mazeGrid.Width, mazeGrid.Height);
        _movementAi = new EnemyMovementAi(level);
        _basePreferenceSystem = new EnemyBasePreferenceSystem(levelNumber);
        _chaseSystem = new EnemyChaseSystem(levelNumber);

        CreateSlotsAndViews();
        UpdateWaitingLairVisibility();
        SynchronizeViewsFromEntities();
    }

    /// <summary>
    /// Gets the static logical maze used to rebuild enemy navigation.
    /// </summary>
    private MazeGrid MazeGrid { get; }

    /// <summary>
    /// Gets the rotating-gate system used to rebuild enemy navigation.
    /// </summary>
    private GateSystem GateSystem { get; }

    /// <summary>
    /// Enumerates enemies that can currently kill the player on contact.
    /// </summary>
    public IEnumerable<MonsterEntity> CollisionActiveMonsters
    {
        get
        {
            foreach (MonsterEntity monster in _monsters)
            {
                if (monster.CollisionActive)
                    yield return monster;
            }
        }
    }

    /// <summary>
    /// Advances all enemy systems for one fixed simulation tick.
    /// </summary>
    /// <param name="playerArcadePixelPos">Current player position in arcade pixels.</param>
    /// <param name="playerCurrentDirection">Player effective direction, preserved while input is idle.</param>
    /// <param name="tryConsumeSkullAt">Callback used when an enemy reaches a skull cell.</param>
    public void AdvanceOneSimulationTick(
        Vector2I playerArcadePixelPos,
        Vector2I playerCurrentDirection,
        Func<Vector2I, bool> tryConsumeSkullAt)
    {
        _navigationGrid.RebuildAllowedDirections(MazeGrid, GateSystem);
        _navigationGrid.BuildBfsGuidanceFromPlayer(_level.ArcadePixelToLogicalCell(playerArcadePixelPos));

        _basePreferenceSystem.PrepareBasePreferredDirections(_monsters, playerCurrentDirection);

        _chaseSystem.AdvanceOneTick(_monsters);
        _chaseSystem.ApplyBfsOverride(
            _monsters,
            _navigationGrid,
            _level.ArcadePixelToLogicalCell);

        foreach (MonsterEntity monster in _monsters)
        {
            if (!monster.MovementActive)
                continue;

            _movementAi.UpdateMonsterOnePixel(monster, _navigationGrid);
            TryHandleSkullCollision(monster, tryConsumeSkullAt);
        }

        UpdateWaitingLairVisibility();
        SynchronizeViewsFromEntities();
    }

    /// <summary>
    /// Resets enemy slots after the player has lost a life.
    /// </summary>
    /// <remarks>
    /// Collectibles and gate states are intentionally not touched here. This only
    /// clears enemies that had already entered the maze, restarts chase timing,
    /// and puts one waiting enemy back in the central lair, matching the observed
    /// arcade-level restart behavior after a death.
    /// </remarks>
    public void ResetAfterPlayerDeath()
    {
        _basePreferenceSystem.Reset();
        _chaseSystem.Reset();
        _suppressEnemyViewsDuringPlayerDeath = false;

        _root.Visible = true;

        foreach (MonsterEntity monster in _monsters)
            PrepareMonsterInLair(monster);

        UpdateWaitingLairVisibility();
        SynchronizeViewsFromEntities();
    }

    /// <summary>
    /// Hides all enemy views immediately when an enemy-triggered player death starts.
    /// </summary>
    /// <remarks>
    /// The player death sequence freezes normal board simulation, so this cannot be
    /// a one-shot view.Visible change that might later be overwritten by a view
    /// synchronization. A death-suppression flag is kept until the next-life reset.
    /// The root node is also hidden as a belt-and-braces visual guarantee.
    /// </remarks>
    public void HideAllViewsForPlayerDeathSequence()
    {
        _suppressEnemyViewsDuringPlayerDeath = true;
        _root.Visible = false;

        SynchronizeViewsFromEntities();
    }

    /// <summary>
    /// Releases the currently waiting lair enemy, or the next available free slot.
    /// </summary>
    /// <returns><see langword="true"/> when one enemy was released into the maze.</returns>
    public bool TryReleaseNextEnemy()
    {
        MonsterEntity? candidate = null;

        foreach (MonsterEntity monster in _monsters)
        {
            if (monster.MovementActive || monster.CollisionActive)
                continue;

            if (monster.VisibleInLair)
            {
                candidate = monster;
                break;
            }

            candidate ??= monster;
        }

        if (candidate == null)
            return false;

        ReleaseMonsterFromLair(candidate);
        UpdateWaitingLairVisibility();
        SynchronizeViewsFromEntities();
        return true;
    }

    /// <summary>
    /// Synchronizes all enemy views from their logical slot state.
    /// </summary>
    public void SynchronizeViewsFromEntities()
    {
        for (int i = 0; i < _monsters.Length; i++)
            _views[i].SynchronizeFromEntity(
                _monsters[i],
                forceHidden: _suppressEnemyViewsDuringPlayerDeath);
    }

    /// <summary>
    /// Creates four logical enemy slots and their runtime view nodes.
    /// </summary>
    private void CreateSlotsAndViews()
    {
        for (int i = 0; i < EnemyMovementTuning.MaxEnemyCount; i++)
        {
            MonsterEntity monster = new(i);
            PrepareMonsterInLair(monster);
            _monsters[i] = monster;

            EnemyController view = new()
            {
                Name = $"Enemy{i}"
            };

            _root.AddChild(view);
            view.Initialize(_level, monster);
            _views[i] = view;
        }
    }

    /// <summary>
    /// Resets one enemy slot to the inactive waiting-lair state.
    /// </summary>
    private static void PrepareMonsterInLair(MonsterEntity monster)
    {
        monster.ArcadePixelPos = EnemyMovementTuning.LairArcadePixelPos;
        monster.Direction = MonsterDir.Up;
        monster.PreferredDirection = MonsterDir.Up;
        monster.ChaseTimer = 0;
        monster.RuntimeState = MonsterRuntimeState.WaitingInLair;
        monster.MovementActive = false;
        monster.CollisionActive = false;
        monster.VisibleInLair = false;
    }

    /// <summary>
    /// Converts one waiting enemy slot into an active maze enemy.
    /// </summary>
    private static void ReleaseMonsterFromLair(MonsterEntity monster)
    {
        monster.ArcadePixelPos = EnemyMovementTuning.LairArcadePixelPos;
        monster.Direction = MonsterDir.Up;
        monster.PreferredDirection = MonsterDir.Up;
        monster.RuntimeState = MonsterRuntimeState.InMaze;
        monster.MovementActive = true;
        monster.CollisionActive = true;
        monster.VisibleInLair = false;
    }

    /// <summary>
    /// Ensures that at most one inactive waiting enemy is shown in the central lair.
    /// </summary>
    private void UpdateWaitingLairVisibility()
    {
        bool selectedWaitingEnemy = false;

        foreach (MonsterEntity monster in _monsters)
        {
            monster.VisibleInLair = false;

            if (selectedWaitingEnemy ||
                monster.MovementActive ||
                monster.CollisionActive ||
                monster.RuntimeState != MonsterRuntimeState.WaitingInLair)
            {
                continue;
            }

            monster.VisibleInLair = true;
            selectedWaitingEnemy = true;
        }
    }

    /// <summary>
    /// Handles enemy contact with a skull at the current decision center.
    /// </summary>
    private void TryHandleSkullCollision(
        MonsterEntity monster,
        Func<Vector2I, bool> tryConsumeSkullAt)
    {
        if (!EnemyMovementTuning.IsDecisionCenter(monster.ArcadePixelPos))
            return;

        Vector2I cell = _level.ArcadePixelToLogicalCell(monster.ArcadePixelPos);

        if (!tryConsumeSkullAt(cell))
            return;

        PrepareMonsterInLair(monster);
        UpdateWaitingLairVisibility();
    }
}
