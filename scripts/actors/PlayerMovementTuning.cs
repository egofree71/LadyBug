using Godot;

/// <summary>
/// Stores the movement tuning values used by the player controller.
/// </summary>
/// <remarks>
/// This class centralizes all stable movement calibration values so that
/// <c>PlayerController</c> does not mix behavior logic with tuning data.
/// </remarks>
public static class PlayerMovementTuning
{
    // Fixed simulation frequency used by the current prototype.
    public const double TickRate = 60.1145;

    // Duration of one simulation tick.
    public const double TickDuration = 1.0 / TickRate;

    // Maximum vertical deviation tolerated when starting/resuming horizontal movement.
    public const int HorizontalRailSnapTolerance = 1;

    // Maximum horizontal deviation tolerated when starting/resuming vertical movement.
    public const int VerticalRailSnapTolerance = 1;

    // Render offset used while the player is effectively moving left.
    public static readonly Vector2I SpriteRenderOffsetLeftArcade = new(5, 8);

    // Render offset used while the player is effectively moving right.
    public static readonly Vector2I SpriteRenderOffsetRightArcade = new(4, 8);

    // Render offset used while the player is effectively moving vertically.
    public static readonly Vector2I SpriteRenderOffsetVerticalArcade = new(5, 7);

    // Forward probe distance used when moving left.
    public const int CollisionLeadLeft = 8;

    // Forward probe distance used when moving right.
    public const int CollisionLeadRight = 6;

    // Forward probe distance used when moving up.
    public const int CollisionLeadUp = 9;

    // Forward probe distance used when moving down.
    public const int CollisionLeadDown = 6;
}