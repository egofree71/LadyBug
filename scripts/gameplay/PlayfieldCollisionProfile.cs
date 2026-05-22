using Godot;

namespace LadyBug.Gameplay;

/// <summary>
/// Directional collision probes used to evaluate one arcade-pixel movement step.
/// </summary>
/// <remarks>
/// Static maze walls and rotating gates are intentionally separate. A fixed wall
/// usually needs the actor's full forward body lead so the sprite does not enter
/// the wall visually. A rotating gate can require a different contact probe,
/// because it is a dynamic object centered on a pivot rather than a full tile wall.
/// </remarks>
public readonly struct PlayfieldCollisionProfile
{
    /// <summary>
    /// Creates a collision profile for one attempted movement direction.
    /// </summary>
    /// <param name="staticCollisionLead">Probe offset used for fixed maze walls.</param>
    /// <param name="gateContactLead">Probe offset used for rotating-gate contact.</param>
    public PlayfieldCollisionProfile(Vector2I staticCollisionLead, Vector2I gateContactLead)
    {
        StaticCollisionLead = staticCollisionLead;
        GateContactLead = gateContactLead;
    }

    /// <summary>
    /// Probe offset used for fixed maze walls.
    /// </summary>
    public Vector2I StaticCollisionLead { get; }

    /// <summary>
    /// Probe offset used for rotating-gate contact.
    /// </summary>
    public Vector2I GateContactLead { get; }

    /// <summary>
    /// Creates a profile where fixed walls and gates use the same probe.
    /// </summary>
    public static PlayfieldCollisionProfile SameLead(Vector2I collisionLead)
    {
        return new PlayfieldCollisionProfile(collisionLead, collisionLead);
    }
}
