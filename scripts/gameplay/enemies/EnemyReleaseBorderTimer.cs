using System;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Logical clockwise timer used by the maze border that announces enemy release.
/// </summary>
/// <remarks>
/// <para>
/// The timer does not know anything about Godot rendering. It only tracks which
/// border tiles should be considered green or white at the current simulation tick.
/// </para>
/// <para>
/// Reverse-engineered arcade timing:
/// level 1 uses one border step every 9 arcade ticks, levels 2-4 use 6 ticks,
/// and level 5 onward uses 3 ticks.
/// </para>
/// <para>
/// The arcade stores this as a countdown/reload pair at RAM 60AA/60AB. Both are
/// initialized to the period, so the first border step occurs after the full
/// period rather than immediately on level start.
/// </para>
/// </remarks>
public sealed class EnemyReleaseBorderTimer
{
    // Number of visible border tiles in the complete clockwise loop.
    private int _tileCount;

    // Number of tiles already processed in the current fill or clear phase.
    private int _progress;

    // Countdown equivalent of arcade RAM 60AA: ticks left before the next tile step.
    private int _ticksRemaining;

    // Reload period equivalent of arcade RAM 60AB.
    private int _ticksPerTile;

    /// <summary>
    /// Creates a border timer for a clockwise list of border tiles.
    /// </summary>
    /// <param name="tileCount">Number of tiles in the full border loop.</param>
    /// <param name="ticksPerTile">Countdown reload period, in fixed arcade ticks.</param>
    public EnemyReleaseBorderTimer(int tileCount, int ticksPerTile)
    {
        TileCount = tileCount;
        TicksPerTile = ticksPerTile;
        Reset();
    }

    /// <summary>
    /// Gets the current fill / clear phase of the border timer.
    /// </summary>
    public EnemyReleaseBorderTimerPhase Phase { get; private set; } =
        EnemyReleaseBorderTimerPhase.FillingGreen;

    /// <summary>
    /// Gets the number of border tiles already processed in the current phase.
    /// </summary>
    public int Progress => _progress;

    /// <summary>
    /// Gets or sets the number of active tiles in the border loop.
    /// </summary>
    public int TileCount
    {
        get => _tileCount;
        set
        {
            _tileCount = Math.Max(0, value);
            _progress = Math.Clamp(_progress, 0, _tileCount);
        }
    }

    /// <summary>
    /// Gets or sets the number of arcade simulation ticks before the next border tile changes color.
    /// </summary>
    /// <remarks>
    /// This is the reload value corresponding to the arcade RAM value at 60AB.
    /// </remarks>
    public int TicksPerTile
    {
        get => _ticksPerTile;
        set
        {
            _ticksPerTile = Math.Max(1, value);

            if (_ticksRemaining <= 0 || _ticksRemaining > _ticksPerTile)
                _ticksRemaining = _ticksPerTile;
        }
    }

    /// <summary>
    /// Gets the remaining countdown ticks before the next border tile step.
    /// </summary>
    /// <remarks>
    /// This corresponds to the arcade's current countdown at RAM 60AA.
    /// </remarks>
    public int TicksRemaining => _ticksRemaining;

    /// <summary>
    /// Returns the reverse-engineered border-step period for the visible level number.
    /// </summary>
    public static int GetTicksPerTileForLevel(int levelNumber)
    {
        if (levelNumber <= 1)
            return 9;

        if (levelNumber <= 4)
            return 6;

        return 3;
    }

    /// <summary>
    /// Resets the border to the initial all-white state.
    /// </summary>
    public void Reset()
    {
        Phase = EnemyReleaseBorderTimerPhase.FillingGreen;
        _progress = 0;
        _ticksRemaining = TicksPerTile;
    }

    /// <summary>
    /// Advances the timer by one fixed arcade simulation tick.
    /// </summary>
    public EnemyReleaseBorderTimerStepResult AdvanceOneTick()
    {
        if (_tileCount <= 0)
            return CurrentStepResult(visualChanged: false, shouldReleaseEnemy: false);

        _ticksRemaining--;

        if (_ticksRemaining > 0)
            return CurrentStepResult(visualChanged: false, shouldReleaseEnemy: false);

        _ticksRemaining = TicksPerTile;
        return AdvanceOneBorderTile();
    }

    /// <summary>
    /// Returns whether the tile at the given clockwise border index is currently green.
    /// </summary>
    public bool IsTileGreen(int tileIndex)
    {
        if (tileIndex < 0 || tileIndex >= _tileCount)
            return false;

        return Phase switch
        {
            EnemyReleaseBorderTimerPhase.FillingGreen => tileIndex < _progress,
            EnemyReleaseBorderTimerPhase.ClearingWhite => tileIndex >= _progress,
            _ => false
        };
    }

    /// <summary>
    /// Applies one visible tile step after the countdown reaches zero.
    /// </summary>
    private EnemyReleaseBorderTimerStepResult AdvanceOneBorderTile()
    {
        _progress++;

        if (_progress < _tileCount)
            return CurrentStepResult(visualChanged: true, shouldReleaseEnemy: false);

        if (Phase == EnemyReleaseBorderTimerPhase.FillingGreen)
        {
            Phase = EnemyReleaseBorderTimerPhase.ClearingWhite;
            _progress = 0;

            // The green pass has completed a full lap. Release one enemy now.
            return CurrentStepResult(visualChanged: true, shouldReleaseEnemy: true);
        }

        Phase = EnemyReleaseBorderTimerPhase.FillingGreen;
        _progress = 0;

        // The white pass has also completed a full lap. This is still a real
        // border-cycle completion, so it must release the next waiting enemy too.
        // Otherwise the game wastes every second visible cycle.
        return CurrentStepResult(visualChanged: true, shouldReleaseEnemy: true);
    }

    /// <summary>
    /// Builds a result snapshot from the current timer fields.
    /// </summary>
    private EnemyReleaseBorderTimerStepResult CurrentStepResult(
        bool visualChanged,
        bool shouldReleaseEnemy)
    {
        return new EnemyReleaseBorderTimerStepResult(
            Phase,
            _progress,
            _tileCount,
            TicksPerTile,
            _ticksRemaining,
            visualChanged,
            shouldReleaseEnemy);
    }
}

/// <summary>
/// Current high-level phase of the maze-border timer animation.
/// </summary>
public enum EnemyReleaseBorderTimerPhase
{
    /// <summary>
    /// Tiles are progressively turning from white to green.
    /// </summary>
    FillingGreen,

    /// <summary>
    /// Tiles are progressively turning from green back to white.
    /// </summary>
    ClearingWhite
}

/// <summary>
/// Snapshot returned after advancing the maze-border timer by one simulation tick.
/// </summary>
public readonly struct EnemyReleaseBorderTimerStepResult
{
    /// <summary>
    /// Creates an immutable snapshot of the border timer after one tick.
    /// </summary>
    public EnemyReleaseBorderTimerStepResult(
        EnemyReleaseBorderTimerPhase phase,
        int progress,
        int tileCount,
        int ticksPerTile,
        int ticksRemaining,
        bool visualChanged,
        bool shouldReleaseEnemy)
    {
        Phase = phase;
        Progress = progress;
        TileCount = tileCount;
        TicksPerTile = ticksPerTile;
        TicksRemaining = ticksRemaining;
        VisualChanged = visualChanged;
        ShouldReleaseEnemy = shouldReleaseEnemy;
    }

    /// <summary>
    /// Phase active after the tick was processed.
    /// </summary>
    public EnemyReleaseBorderTimerPhase Phase { get; }

    /// <summary>
    /// Number of tiles processed in the current phase after the tick was processed.
    /// </summary>
    public int Progress { get; }

    /// <summary>
    /// Number of tiles in the full border loop.
    /// </summary>
    public int TileCount { get; }

    /// <summary>
    /// Active countdown reload period, in fixed arcade ticks.
    /// </summary>
    public int TicksPerTile { get; }

    /// <summary>
    /// Remaining countdown ticks before the next border tile step.
    /// </summary>
    public int TicksRemaining { get; }

    /// <summary>
    /// True when at least one tile changed visual state during this tick.
    /// </summary>
    public bool VisualChanged { get; }

    /// <summary>
    /// True when a full visible border pass completed and the next enemy should be released.
    /// </summary>
    public bool ShouldReleaseEnemy { get; }
}
