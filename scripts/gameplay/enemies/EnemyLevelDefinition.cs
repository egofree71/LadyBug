namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Arcade-facing sprite and attribute values assigned to one enemy slot.
/// </summary>
/// <remarks>
/// In the original arcade RAM enemy slot, these correspond to:
/// slot + 3 = sprite code, slot + 4 = attribute / palette value.
/// Keeping both values makes the Godot implementation easier to compare with
/// reverse-engineered traces, even though the view ultimately loads a spritesheet.
/// </remarks>
public readonly record struct EnemySpriteInfo(byte SpriteCode, byte Attr)
{
    /// <summary>
    /// Gets the zero-based visual index in the natural arcade order.
    /// </summary>
    public int NaturalVisualIndex => Attr <= 0 ? 0 : Attr - 1;
}

/// <summary>
/// Visual and timing-facing data for the enemy set used by one enemy slot.
/// </summary>
/// <remarks>
/// Movement rules stay in <see cref="EnemyMovementAi"/> and timing rules stay in
/// <see cref="EnemyChaseSystem"/> / <see cref="EnemyReleaseBorderTimer"/>.
/// This definition selects the spritesheet and frame layout used by one enemy
/// view. From level 9 onward, different enemy slots can intentionally receive
/// different definitions.
/// </remarks>
public sealed class EnemyLevelDefinition
{
    public EnemyLevelDefinition(
        int levelNumber,
        string spritesheetPath,
        int frameSize = 64,
        float moveRightAnimationSpeed = 6.0f,
        float moveUpAnimationSpeed = 5.0f)
        : this(levelNumber, default, spritesheetPath, frameSize, moveRightAnimationSpeed, moveUpAnimationSpeed)
    {
    }

    public EnemyLevelDefinition(
        int levelNumber,
        EnemySpriteInfo spriteInfo,
        string spritesheetPath,
        int frameSize = 64,
        float moveRightAnimationSpeed = 6.0f,
        float moveUpAnimationSpeed = 5.0f)
    {
        LevelNumber = levelNumber;
        SpriteInfo = spriteInfo;
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
    /// Arcade sprite code assigned by the original enemy initialization routine.
    /// </summary>
    public byte SpriteCode => SpriteInfo.SpriteCode;

    /// <summary>
    /// Arcade attribute / palette value assigned by the original enemy initialization routine.
    /// </summary>
    public byte Attr => SpriteInfo.Attr;

    /// <summary>
    /// Combined arcade sprite information for this enemy slot.
    /// </summary>
    public EnemySpriteInfo SpriteInfo { get; }

    /// <summary>
    /// Zero-based visual index in the natural arcade sprite order.
    /// </summary>
    public int NaturalVisualIndex => SpriteInfo.NaturalVisualIndex;

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
