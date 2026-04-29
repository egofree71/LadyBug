using Godot;
using LadyBug.Gameplay.Enemies;

namespace LadyBug.Actors;

/// <summary>
/// Runtime visual controller for one enemy.
/// </summary>
/// <remarks>
/// The enemy node is created by <see cref="EnemyRuntime"/> so Level.tscn does not
/// need to be edited manually. The controller builds its SpriteFrames at runtime
/// from the six-frame level-1 enemy spritesheet.
/// </remarks>
public partial class EnemyController : Node2D
{
    // Runtime spritesheet currently used for the level-1 enemy.
    private const string EnemyTexturePath = "res://assets/sprites/enemies/enemy_level1.png";

    // Width and height of one enemy animation frame in the spritesheet.
    private const int FrameSize = 64;

    // Owning level used only for arcade-pixel to scene-space conversion.
    private Level _level = null!;

    // Runtime-created animated sprite; kept out of the scene file on purpose.
    private AnimatedSprite2D _animatedSprite = null!;

    // Cached enemy texture so SpriteFrames can be rebuilt without repeated loads.
    private Texture2D? _texture;

    // Prevents spamming the output if the enemy spritesheet is missing.
    private bool _warningShown;

    /// <summary>
    /// Ensures that the runtime animated sprite exists when the node enters the tree.
    /// </summary>
    public override void _Ready()
    {
        EnsureSprite();
    }

    /// <summary>
    /// Initializes this view from the owning level and initial enemy entity.
    /// </summary>
    /// <param name="level">Owning level used for coordinate conversion.</param>
    /// <param name="entity">Initial logical enemy state to render.</param>
    public void Initialize(Level level, MonsterEntity entity)
    {
        _level = level;
        EnsureSprite();
        SynchronizeFromEntity(entity);
    }

    /// <summary>
    /// Applies one logical enemy state to this Godot view.
    /// </summary>
    /// <param name="entity">Enemy slot state to display.</param>
    /// <param name="forceHidden">When true, suppresses the view regardless of slot visibility.</param>
    public void SynchronizeFromEntity(MonsterEntity entity, bool forceHidden = false)
    {
        if (_level == null || entity == null)
            return;

        EnsureSprite();

        bool shouldBeVisible = !forceHidden && entity.IsVisible;
        Visible = shouldBeVisible;
        _animatedSprite.Visible = shouldBeVisible;

        Position = _level.ArcadePixelToScenePosition(entity.ArcadePixelPos);
        _animatedSprite.Position =
            _level.ArcadeDeltaToSceneDelta(
                EnemyMovementTuning.GetSpriteRenderOffsetArcade(entity.Direction));

        ApplyVisualFacing(entity.Direction);
    }

    /// <summary>
    /// Creates the child <see cref="AnimatedSprite2D"/> and its sprite frames when needed.
    /// </summary>
    private void EnsureSprite()
    {
        if (_animatedSprite != null)
            return;

        _animatedSprite = new AnimatedSprite2D
        {
            Name = "AnimatedSprite2D"
        };

        AddChild(_animatedSprite);
        BuildSpriteFrames();
    }

    /// <summary>
    /// Builds right/up animations from the six-frame enemy spritesheet.
    /// </summary>
    private void BuildSpriteFrames()
    {
        _texture ??= ResourceLoader.Load<Texture2D>(EnemyTexturePath);

        if (_texture == null)
        {
            if (!_warningShown)
            {
                _warningShown = true;
                GD.PushWarning($"[EnemyController] Missing enemy spritesheet: {EnemyTexturePath}");
            }

            return;
        }

        SpriteFrames frames = new();
        AddAnimation(frames, "move_right", 0, 1, 2, speed: 6.0f);
        AddAnimation(frames, "move_up", 3, 4, 5, speed: 5.0f);

        _animatedSprite.SpriteFrames = frames;
        _animatedSprite.Play("move_right");
    }

    /// <summary>
    /// Adds one three-frame animation to a runtime <see cref="SpriteFrames"/> resource.
    /// </summary>
    private void AddAnimation(
        SpriteFrames frames,
        string animationName,
        int frame0,
        int frame1,
        int frame2,
        float speed)
    {
        frames.AddAnimation(animationName);
        frames.SetAnimationLoop(animationName, true);
        frames.SetAnimationSpeed(animationName, speed);
        frames.AddFrame(animationName, MakeAtlasTexture(frame0));
        frames.AddFrame(animationName, MakeAtlasTexture(frame1));
        frames.AddFrame(animationName, MakeAtlasTexture(frame2));
    }

    /// <summary>
    /// Creates an atlas frame pointing at one 64x64 tile inside the enemy spritesheet.
    /// </summary>
    private AtlasTexture MakeAtlasTexture(int frame)
    {
        return new AtlasTexture
        {
            Atlas = _texture!,
            Region = new Rect2(frame * FrameSize, 0, FrameSize, FrameSize)
        };
    }

    /// <summary>
    /// Selects animation and flips according to the current enemy direction.
    /// </summary>
    private void ApplyVisualFacing(MonsterDir direction)
    {
        if (_animatedSprite == null)
            return;

        _animatedSprite.FlipH = false;
        _animatedSprite.FlipV = false;

        switch (direction)
        {
            case MonsterDir.Left:
                _animatedSprite.Play("move_right");
                _animatedSprite.FlipH = true;
                break;

            case MonsterDir.Right:
                _animatedSprite.Play("move_right");
                break;

            case MonsterDir.Up:
                _animatedSprite.Play("move_up");
                break;

            case MonsterDir.Down:
                _animatedSprite.Play("move_up");
                _animatedSprite.FlipV = true;
                break;
        }
    }
}
