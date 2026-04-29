using System;
using Godot;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Arcade enemy direction bits.
/// </summary>
/// <remarks>
/// These bits intentionally follow the enemy encoding documented from the ROM and
/// must not be mixed with the player-specific interpretation used by movement input.
/// </remarks>
[Flags]
public enum MonsterDir
{
    /// <summary>
    /// No direction or no valid direction selected.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// Arcade enemy bit for left movement.
    /// </summary>
    Left = 0x01,

    /// <summary>
    /// Arcade enemy bit for upward movement.
    /// </summary>
    Up = 0x02,

    /// <summary>
    /// Arcade enemy bit for right movement.
    /// </summary>
    Right = 0x04,

    /// <summary>
    /// Arcade enemy bit for downward movement.
    /// </summary>
    Down = 0x08
}

/// <summary>
/// Helper methods for converting and manipulating enemy direction bits.
/// </summary>
public static class MonsterDirExtensions
{
    /// <summary>
    /// Converts one enemy direction bit into a Godot unit vector.
    /// </summary>
    /// <param name="dir">Enemy direction to convert.</param>
    /// <returns>The corresponding one-pixel movement vector, or <see cref="Vector2I.Zero"/>.</returns>
    public static Vector2I ToVector(this MonsterDir dir)
    {
        return dir switch
        {
            MonsterDir.Left => Vector2I.Left,
            MonsterDir.Up => Vector2I.Up,
            MonsterDir.Right => Vector2I.Right,
            MonsterDir.Down => Vector2I.Down,
            _ => Vector2I.Zero
        };
    }

    /// <summary>
    /// Returns the opposite enemy direction.
    /// </summary>
    /// <param name="dir">Enemy direction to invert.</param>
    /// <returns>The opposite direction, or <see cref="MonsterDir.None"/>.</returns>
    public static MonsterDir Opposite(this MonsterDir dir)
    {
        return dir switch
        {
            MonsterDir.Left => MonsterDir.Right,
            MonsterDir.Right => MonsterDir.Left,
            MonsterDir.Up => MonsterDir.Down,
            MonsterDir.Down => MonsterDir.Up,
            _ => MonsterDir.None
        };
    }
}
