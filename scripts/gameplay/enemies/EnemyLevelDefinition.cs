namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Visual and timing-facing data for the enemy set used by one visible level.
/// </summary>
/// <remarks>
/// Movement rules stay in <see cref="EnemyMovementAi"/> and timing rules stay in
/// <see cref="EnemyChaseSystem"/> / <see cref="EnemyReleaseBorderTimer"/>.
/// This definition only selects the spritesheet and frame layout used by the enemy
/// view for a level.
/// </remarks>
public sealed class EnemyLevelDefinition
{
    public EnemyLevelDefinition(
        int levelNumber,
        string spritesheetPath,
        int frameSize = 64,
        float moveRightAnimationSpeed = 6.0f,
        float moveUpAnimationSpeed = 5.0f)
    {
        LevelNumber = levelNumber;
        SpritesheetPath = spritesheetPath;
        FrameSize = frameSize;
        MoveRightAnimationSpeed = moveRightAnimationSpeed;
        MoveUpAnimationSpeed = moveUpAnimationSpeed;
    }

    /// <summary>
    /// Visible user-facing level number represented by this definition.
    /// </summary>
    public int LevelNumber { get; }

    /// <summary>
    /// Godot resource path to the six-frame enemy spritesheet.
    /// </summary>
    public string SpritesheetPath { get; }

    /// <summary>
    /// Width and height of one square enemy frame in the spritesheet.
    /// </summary>
    public int FrameSize { get; }

    /// <summary>
    /// Runtime animation speed for the right/left animation.
    /// </summary>
    public float MoveRightAnimationSpeed { get; }

    /// <summary>
    /// Runtime animation speed for the up/down animation.
    /// </summary>
    public float MoveUpAnimationSpeed { get; }
}
