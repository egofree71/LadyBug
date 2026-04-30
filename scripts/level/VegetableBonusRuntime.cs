using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using LadyBug.Gameplay.Collectibles;
using LadyBug.Gameplay.Enemies;

/// <summary>
/// Runtime-only vegetable bonus coordinator.
///
/// This direct-copy version is installed through Level.VegetableBonus.cs and does
/// not require editing Level.cs. It keeps enemy collision active while temporarily
/// disabling enemy movement during the vegetable freeze.
/// </summary>
public sealed partial class VegetableBonusRuntime : Node2D
{
    // Logical maze cell occupied by the central lair / vegetable pickup.
    private static readonly Vector2I LairLogicalCell = new(5, 5);

    // Reflection handle used to read EnemyRuntime's private monster slots without
    // replacing EnemyRuntime.cs in this direct-copy package.
    private static readonly FieldInfo? EnemyRuntimeMonstersField =
        typeof(EnemyRuntime).GetField("_monsters", BindingFlags.Instance | BindingFlags.NonPublic);

    // Stores each enemy's original MovementActive flag while the freeze is active.
    // CollisionActive is never changed, so frozen enemies remain fatal.
    private readonly Dictionary<int, bool> _movementActiveBeforeFreezeBySlot = new();

    // Owning Level. Used through small support methods exposed by Level.VegetableBonus.cs.
    private Level? _level;

    // Runtime visual node that shows/hides the current vegetable sprite.
    private VegetableBonusView? _view;

    // Fixed-tick accumulator, kept separate from Level's private accumulator.
    private double _simulationAccumulator;

    // Remaining freeze duration in fixed simulation ticks.
    private int _freezeTicksRemaining;

    // True once enemy movement has been disabled for the current freeze.
    private bool _freezeMovementApplied;

    // Tracks the transition from "not all enemies are out" to "all enemies are out".
    private bool _wasAllEnemiesInMaze;

    // Prevents the same vegetable from respawning until an enemy returns to the lair
    // and all four enemies leave the lair again.
    private bool _consumedDuringCurrentAllEnemiesOutCycle;

    // Local visibility flag used for pickup detection without querying the view node.
    private bool _isVisible;

    /// <summary>
    /// Fixed-tick freeze duration used for the first playable implementation.
    /// </summary>
    private const int FreezeDurationTicks = 300;

    /// <summary>
    /// Initializes the runtime after the owning Level has entered the scene tree.
    /// </summary>
    public void Initialize(Level level)
    {
        _level = level ?? throw new ArgumentNullException(nameof(level));
        ProcessPriority = -10_000;

        _view = new VegetableBonusView
        {
            Position = _level.VegetableSupport_GetLairScenePosition()
        };

        AddChild(_view);
        HideVegetable();
    }

    /// <summary>
    /// Runs before the Level default process by using a low ProcessPriority.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_level == null || Engine.IsEditorHint())
            return;

        if (_level.VegetableSupport_ShouldResetBonusRuntime())
        {
            ResetRuntimeState();
            return;
        }

        _simulationAccumulator += delta;

        while (_simulationAccumulator >= LadyBug.Actors.PlayerMovementTuning.TickDuration)
        {
            _simulationAccumulator -= LadyBug.Actors.PlayerMovementTuning.TickDuration;
            AdvanceOneSimulationTick();
        }
    }

    /// <summary>
    /// Advances the vegetable spawn, pickup, and freeze state by one fixed tick.
    /// </summary>
    private void AdvanceOneSimulationTick()
    {
        if (_level == null)
            return;

        bool allEnemiesInMaze = AreAllEnemiesInMaze();
        UpdateSpawnState(allEnemiesInMaze, _level.VegetableSupport_LevelNumber);

        if (_freezeTicksRemaining > 0)
        {
            ApplyFrozenMovementState();
            _freezeTicksRemaining--;
        }
        else
        {
            RestoreFrozenMovementState();
        }

        if (_level.VegetableSupport_IsPickupPopupActive)
            return;

        if (_isVisible && _level.VegetableSupport_PlayerLogicalCell == LairLogicalCell)
            ConsumeVegetable();
    }

    /// <summary>
    /// Updates whether the vegetable should currently be visible.
    /// </summary>
    private void UpdateSpawnState(bool allEnemiesInMaze, int levelNumber)
    {
        if (!allEnemiesInMaze)
        {
            _wasAllEnemiesInMaze = false;
            _consumedDuringCurrentAllEnemiesOutCycle = false;
            HideVegetable();
            return;
        }

        if (!_wasAllEnemiesInMaze)
        {
            _wasAllEnemiesInMaze = true;
            _consumedDuringCurrentAllEnemiesOutCycle = false;
        }

        if (!_consumedDuringCurrentAllEnemiesOutCycle)
            ShowForLevel(levelNumber);
    }

    /// <summary>
    /// Consumes the visible vegetable: add score, hide it, and start enemy freeze.
    /// </summary>
    private void ConsumeVegetable()
    {
        if (_level == null)
            return;

        _consumedDuringCurrentAllEnemiesOutCycle = true;
        HideVegetable();

        int score = VegetableBonusCatalog.GetScore(_level.VegetableSupport_LevelNumber);
        _level.VegetableSupport_AddScore(score);

        _freezeTicksRemaining = FreezeDurationTicks;
        ApplyFrozenMovementState();
    }

    /// <summary>
    /// Returns whether all four enemy slots are currently in the maze and fatal.
    /// MovementActive is deliberately ignored so the vegetable freeze does not look
    /// like an enemy returning to the lair.
    /// </summary>
    private bool AreAllEnemiesInMaze()
    {
        MonsterEntity[]? monsters = GetMonsters();

        if (monsters == null || monsters.Length == 0)
            return false;

        foreach (MonsterEntity monster in monsters)
        {
            if (monster.RuntimeState != MonsterRuntimeState.InMaze || !monster.CollisionActive)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Temporarily disables enemy movement while leaving collision active.
    /// </summary>
    private void ApplyFrozenMovementState()
    {
        MonsterEntity[]? monsters = GetMonsters();

        if (monsters == null)
            return;

        if (!_freezeMovementApplied)
        {
            _movementActiveBeforeFreezeBySlot.Clear();

            foreach (MonsterEntity monster in monsters)
                _movementActiveBeforeFreezeBySlot[monster.Id] = monster.MovementActive;

            _freezeMovementApplied = true;
        }

        foreach (MonsterEntity monster in monsters)
        {
            if (monster.RuntimeState == MonsterRuntimeState.InMaze && monster.CollisionActive)
                monster.MovementActive = false;
        }
    }

    /// <summary>
    /// Restores enemy movement after the freeze, without resurrecting enemies that
    /// are no longer in the maze.
    /// </summary>
    private void RestoreFrozenMovementState()
    {
        if (!_freezeMovementApplied)
            return;

        MonsterEntity[]? monsters = GetMonsters();

        if (monsters != null)
        {
            foreach (MonsterEntity monster in monsters)
            {
                if (monster.RuntimeState != MonsterRuntimeState.InMaze || !monster.CollisionActive)
                    continue;

                if (_movementActiveBeforeFreezeBySlot.TryGetValue(monster.Id, out bool wasMovementActive))
                    monster.MovementActive = wasMovementActive;
            }
        }

        _movementActiveBeforeFreezeBySlot.Clear();
        _freezeMovementApplied = false;
    }

    /// <summary>
    /// Gets the private EnemyRuntime monster array through reflection.
    /// This keeps this direct-copy package from replacing EnemyRuntime.cs.
    /// </summary>
    private MonsterEntity[]? GetMonsters()
    {
        if (_level == null || EnemyRuntimeMonstersField == null)
            return null;

        EnemyRuntime? enemyRuntime = _level.VegetableSupport_EnemyRuntime;
        return enemyRuntime == null
            ? null
            : EnemyRuntimeMonstersField.GetValue(enemyRuntime) as MonsterEntity[];
    }

    /// <summary>
    /// Shows the correct vegetable for the current level.
    /// </summary>
    private void ShowForLevel(int levelNumber)
    {
        if (_view == null)
            return;

        _isVisible = true;
        _view.ShowForLevel(levelNumber);
    }

    /// <summary>
    /// Hides the vegetable and marks it as unavailable for pickup.
    /// </summary>
    private void HideVegetable()
    {
        _isVisible = false;
        _view?.HideVegetable();
    }

    /// <summary>
    /// Clears vegetable and freeze state during death, transition, or game over pauses.
    /// </summary>
    private void ResetRuntimeState()
    {
        RestoreFrozenMovementState();
        _freezeTicksRemaining = 0;
        _simulationAccumulator = 0.0;
        _wasAllEnemiesInMaze = false;
        _consumedDuringCurrentAllEnemiesOutCycle = false;
        HideVegetable();
    }
}
