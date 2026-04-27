using Godot;

namespace LadyBug.Gameplay.Player;

/// <summary>
/// Tick-accurate high-level state machine for the arcade player death sequence.
/// </summary>
/// <remarks>
/// <para>
/// The original routine first displays red shrinking frames, then ghost appearance
/// frames, then moves the final ghost silhouette upward in a fixed zigzag. This
/// class stores only the semantic animation state: selected spritesheet, frame
/// index, and visual offset in arcade pixels.
/// </para>
/// <para>
/// Rendering remains owned by <c>PlayerController</c>, because it knows how to
/// convert arcade-pixel offsets to Godot scene coordinates.
/// </para>
/// </remarks>
public sealed class PlayerDeathSequenceState
{
    public const int SheetFrameCount = 7;

    private const int GhostPathSegmentTicks = 15;

    private static readonly DeathFrame[] RedFrames =
    {
        new(PlayerDeathVisualSheet.Red, 0, 30),
        new(PlayerDeathVisualSheet.Red, 1, 5),
        new(PlayerDeathVisualSheet.Red, 2, 5),
        new(PlayerDeathVisualSheet.Red, 3, 5),
        new(PlayerDeathVisualSheet.Red, 4, 5),
        new(PlayerDeathVisualSheet.Red, 5, 5),
        new(PlayerDeathVisualSheet.Red, 6, 5),
    };

    private static readonly DeathFrame[] GhostFrames =
    {
        new(PlayerDeathVisualSheet.Ghost, 0, 5),
        new(PlayerDeathVisualSheet.Ghost, 1, 5),
        new(PlayerDeathVisualSheet.Ghost, 2, 5),
        new(PlayerDeathVisualSheet.Ghost, 3, 5),
        new(PlayerDeathVisualSheet.Ghost, 4, 5),
        new(PlayerDeathVisualSheet.Ghost, 5, 5),
        new(PlayerDeathVisualSheet.Ghost, 6, 30),
    };

    private static readonly GhostMoveSegment[] GhostPath =
    {
        GhostMoveSegment.RightUp,
        GhostMoveSegment.LeftUp,
        GhostMoveSegment.LeftUp,
        GhostMoveSegment.RightUp,
        GhostMoveSegment.RightUp,
        GhostMoveSegment.LeftUp,
        GhostMoveSegment.LeftUp,
        GhostMoveSegment.RightUp,
    };

    private Phase _phase = Phase.Inactive;
    private int _frameIndex;
    private int _ticksRemainingInFrame;
    private int _ghostSegmentIndex;
    private int _ghostSegmentTick;

    /// <summary>
    /// Gets whether the sequence is currently running.
    /// </summary>
    public bool IsActive => _phase != Phase.Inactive && _phase != Phase.Complete;

    /// <summary>
    /// Gets whether the most recent sequence has completed.
    /// </summary>
    public bool IsComplete => _phase == Phase.Complete;

    /// <summary>
    /// Gets which spritesheet should currently be displayed.
    /// </summary>
    public PlayerDeathVisualSheet CurrentSheet { get; private set; } = PlayerDeathVisualSheet.None;

    /// <summary>
    /// Gets the current zero-based frame inside the selected death spritesheet.
    /// </summary>
    public int CurrentFrame { get; private set; }

    /// <summary>
    /// Gets the current visual offset in arcade pixels, using Godot visual Y direction.
    /// </summary>
    /// <remarks>
    /// The ghost path uses negative Y values because Godot scene coordinates grow
    /// downward while the ghost visually rises upward.
    /// </remarks>
    public Vector2I CurrentVisualOffsetArcade { get; private set; } = Vector2I.Zero;

    /// <summary>
    /// Starts the sequence from the first red frame.
    /// </summary>
    public void Start()
    {
        Reset();
        _phase = Phase.RedFrames;
        _frameIndex = 0;
        ApplyFrame(RedFrames[_frameIndex]);
    }

    /// <summary>
    /// Advances the sequence by one arcade simulation tick.
    /// </summary>
    /// <returns><see langword="true"/> when the sequence has just completed.</returns>
    public bool AdvanceOneTick()
    {
        if (!IsActive)
            return false;

        if (_phase == Phase.RedFrames)
            return AdvanceFrameSequence(RedFrames, Phase.GhostFrames);

        if (_phase == Phase.GhostFrames)
            return AdvanceFrameSequence(GhostFrames, Phase.GhostPath);

        if (_phase == Phase.GhostPath)
            return AdvanceGhostPathOneTick();

        return false;
    }

    /// <summary>
    /// Clears the sequence and hides all death visuals.
    /// </summary>
    public void Reset()
    {
        _phase = Phase.Inactive;
        _frameIndex = 0;
        _ticksRemainingInFrame = 0;
        _ghostSegmentIndex = 0;
        _ghostSegmentTick = 0;
        CurrentSheet = PlayerDeathVisualSheet.None;
        CurrentFrame = 0;
        CurrentVisualOffsetArcade = Vector2I.Zero;
    }

    private bool AdvanceFrameSequence(DeathFrame[] frames, Phase nextPhase)
    {
        _ticksRemainingInFrame--;

        if (_ticksRemainingInFrame > 0)
            return false;

        _frameIndex++;

        if (_frameIndex < frames.Length)
        {
            ApplyFrame(frames[_frameIndex]);
            return false;
        }

        if (nextPhase == Phase.GhostFrames)
        {
            _phase = Phase.GhostFrames;
            _frameIndex = 0;
            ApplyFrame(GhostFrames[_frameIndex]);
            return false;
        }

        StartGhostPath();
        return false;
    }

    private void ApplyFrame(DeathFrame frame)
    {
        CurrentSheet = frame.Sheet;
        CurrentFrame = frame.Frame;
        _ticksRemainingInFrame = frame.Ticks;
    }

    private void StartGhostPath()
    {
        _phase = Phase.GhostPath;
        _ghostSegmentIndex = 0;
        _ghostSegmentTick = 0;
        CurrentSheet = PlayerDeathVisualSheet.Ghost;
        CurrentFrame = 6;
    }

    private bool AdvanceGhostPathOneTick()
    {
        if (_ghostSegmentIndex >= GhostPath.Length)
        {
            Complete();
            return true;
        }

        CurrentVisualOffsetArcade += GetGhostPathDelta(
            GhostPath[_ghostSegmentIndex],
            _ghostSegmentTick);

        _ghostSegmentTick++;

        if (_ghostSegmentTick >= GhostPathSegmentTicks)
        {
            _ghostSegmentTick = 0;
            _ghostSegmentIndex++;
        }

        if (_ghostSegmentIndex < GhostPath.Length)
            return false;

        Complete();
        return true;
    }

    private void Complete()
    {
        _phase = Phase.Complete;
        CurrentSheet = PlayerDeathVisualSheet.Ghost;
        CurrentFrame = 6;
    }

    private static Vector2I GetGhostPathDelta(
        GhostMoveSegment segment,
        int tickWithinSegment)
    {
        int horizontalStep = segment == GhostMoveSegment.RightUp ? 1 : -1;

        if (tickWithinSegment < 2)
            return new Vector2I(horizontalStep, 0);

        if (tickWithinSegment < 6)
            return new Vector2I(horizontalStep, -1);

        if (tickWithinSegment < 10)
            return new Vector2I(0, -1);

        return Vector2I.Zero;
    }

    private readonly struct DeathFrame
    {
        public DeathFrame(PlayerDeathVisualSheet sheet, int frame, int ticks)
        {
            Sheet = sheet;
            Frame = frame;
            Ticks = ticks;
        }

        public PlayerDeathVisualSheet Sheet { get; }
        public int Frame { get; }
        public int Ticks { get; }
    }

    private enum Phase
    {
        Inactive,
        RedFrames,
        GhostFrames,
        GhostPath,
        Complete
    }

    private enum GhostMoveSegment
    {
        RightUp,
        LeftUp
    }
}
