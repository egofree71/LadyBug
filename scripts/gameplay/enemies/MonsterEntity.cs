using Godot;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Mutable gameplay state for one enemy slot.
/// </summary>
/// <remarks>
/// This mirrors the useful parts of the arcade enemy slots without copying the
/// original RAM layout literally. Positions are stored in integer arcade pixels.
/// </remarks>
public sealed class MonsterEntity
{
    /// <summary>
    /// Creates one enemy slot state.
    /// </summary>
    /// <param name="id">Stable slot identifier in the 0..3 enemy array.</param>
    public MonsterEntity(int id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the stable slot identifier.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets or sets the enemy gameplay position in integer arcade pixels.
    /// </summary>
    public Vector2I ArcadePixelPos { get; set; }

    /// <summary>
    /// Gets or sets the currently committed movement direction.
    /// </summary>
    public MonsterDir Direction { get; set; }

    /// <summary>
    /// Gets or sets the direction preferred by base AI or temporary BFS chase pressure.
    /// </summary>
    public MonsterDir PreferredDirection { get; set; }

    /// <summary>
    /// Gets or sets the remaining temporary chase duration for this enemy.
    /// </summary>
    public int ChaseTimer { get; set; }

    /// <summary>
    /// Gets or sets the arcade sprite code assigned to this slot.
    /// </summary>
    /// <remarks>
    /// This corresponds to the original enemy slot byte at offset +3. The Godot view
    /// uses a spritesheet path, but keeping the original value helps validate the
    /// implementation against reverse-engineered traces.
    /// </remarks>
    public byte SpriteCode { get; set; }

    /// <summary>
    /// Gets or sets the arcade attribute / palette value assigned to this slot.
    /// </summary>
    /// <remarks>
    /// This corresponds to the original enemy slot byte at offset +4.
    /// </remarks>
    public byte SpriteAttribute { get; set; }

    /// <summary>
    /// Gets the zero-based natural visual index derived from <see cref="SpriteAttribute"/>.
    /// </summary>
    public int NaturalVisualIndex => SpriteAttribute <= 0 ? 0 : SpriteAttribute - 1;

    /// <summary>
    /// Gets or sets the high-level slot state.
    /// </summary>
    public MonsterRuntimeState RuntimeState { get; set; }

    /// <summary>
    /// Gets or sets whether this enemy can currently kill the player on contact.
    /// </summary>
    public bool CollisionActive { get; set; }

    /// <summary>
    /// Gets or sets whether this enemy is updated by the one-pixel movement AI.
    /// </summary>
    public bool MovementActive { get; set; }

    /// <summary>
    /// Gets or sets whether this non-moving enemy slot should be rendered in the central lair.
    /// </summary>
    public bool VisibleInLair { get; set; }

    /// <summary>
    /// Gets whether this enemy slot should currently have a visible view.
    /// </summary>
    public bool IsVisible => MovementActive || CollisionActive || VisibleInLair;
}
