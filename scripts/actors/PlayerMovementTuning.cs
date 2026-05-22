using Godot;
using LadyBug.Gameplay;

namespace LadyBug.Actors;

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
    public static readonly Vector2I SpriteRenderOffsetVerticalArcade = new(5, 8);

    // Forward probe distance used when moving left.
    public const int CollisionLeadLeft = 8;

    // Forward probe distance used when moving right.
    public const int CollisionLeadRight = 7;

    // Forward probe distance used when moving up.
    public const int CollisionLeadUp = 8;

    // Forward probe distance used when moving down.
    public const int CollisionLeadDown = 7;

    // Rotating-gate contact probe calibrated for the player sprite/motor.
    // The fixed-wall lead remains 7/8 px, but gates need a shorter contact
    // lead so they rotate on contact rather than on mere approach.
    public const int GateContactLeadLeft = 6;
    public const int GateContactLeadRight = 6;
    public const int GateContactLeadUp = 6;
    public const int GateContactLeadDown = 6;

    /// <summary>
    /// Gets the forward collision probe offset for one player direction.
    /// </summary>
    public static Vector2I GetStaticCollisionLead(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return new Vector2I(-CollisionLeadLeft, 0);

        if (direction == Vector2I.Right)
            return new Vector2I(CollisionLeadRight, 0);

        if (direction == Vector2I.Up)
            return new Vector2I(0, -CollisionLeadUp);

        if (direction == Vector2I.Down)
            return new Vector2I(0, CollisionLeadDown);

        return Vector2I.Zero;
    }

    /// <summary>
    /// Gets the rotating-gate contact probe offset for one player direction.
    /// </summary>
    public static Vector2I GetGateContactLead(Vector2I direction)
    {
        if (direction == Vector2I.Left)
            return new Vector2I(-GateContactLeadLeft, 0);

        if (direction == Vector2I.Right)
            return new Vector2I(GateContactLeadRight, 0);

        if (direction == Vector2I.Up)
            return new Vector2I(0, -GateContactLeadUp);

        if (direction == Vector2I.Down)
            return new Vector2I(0, GateContactLeadDown);

        return Vector2I.Zero;
    }

    /// <summary>
    /// Gets the complete playfield collision profile for one player step.
    /// </summary>
    public static PlayfieldCollisionProfile GetCollisionProfile(Vector2I direction)
    {
        return new PlayfieldCollisionProfile(
            GetStaticCollisionLead(direction),
            GetGateContactLead(direction));
    }
}