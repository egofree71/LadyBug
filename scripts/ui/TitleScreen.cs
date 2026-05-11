using Godot;

namespace LadyBug.UI;

/// <summary>
/// Arcade-style title screen shown before the first playable level.
///
/// This package intentionally keeps the title screen independent from the
/// gameplay runtime: enemies and ladybug are visual-only AnimatedSprite2D nodes.
/// No enemy AI, maze logic, score state, or session state is touched here.
/// </summary>
public partial class TitleScreen : Node2D
{
    [Signal]
    public delegate void StartRequestedEventHandler();

    private const string LogoPath = "res://assets/images/title_lady_bug_logo.png";
    private const string PlayerSpritesheetPath = "res://assets/sprites/player/ladybug_spritesheet.png";
    private const string ArcadeFontPath = "res://assets/fonts/PressStart2P-Regular.ttf";
    private const string EnemySpritesheetPattern = "res://assets/sprites/enemies/enemy_level{0}.png";

    private const int FrameSize = 64;
    private const float EnemyAnimationSpeed = 6.0f;
    private const float PlayerAnimationSpeed = 6.0f;

    // The provided title logo is 700x176 and is displayed centered at LogoCenterY.
    // These values let the enemy cluster be centered in the free area above the logo.
    private const float LogoCenterY = 485.0f;
    private const float LogoPixelHeight = 176.0f;

    private readonly Color _black = new(0.0f, 0.0f, 0.0f, 1.0f);
    private readonly Color _white = new(1.0f, 1.0f, 1.0f, 1.0f);

    // Pulse between pure white and a light grey instead of blinking or using pink.
    private const float PromptPulseMinimumBrightness = 0.55f;
    private const float PromptPulseSpeed = 4.0f;

    private Font? _arcadeFont;
    private Label? _pressAnyKeyLabel;
    private bool _startAlreadyRequested;
    private double _blinkTimer;

    /// <summary>
    /// Builds the whole screen from code so the scene file can stay minimal.
    /// </summary>
    public override void _Ready()
    {
        LoadArcadeFont();
        BuildBackground();
        BuildAnimatedEnemies();
        BuildLogo();
        BuildBottomPrompt();
    }

    /// <summary>
    /// Gently pulses the start prompt between white and light grey.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_pressAnyKeyLabel == null)
            return;

        _blinkTimer += delta;

        float wave = 0.5f + (0.5f * Mathf.Sin((float)_blinkTimer * PromptPulseSpeed));
        float brightness = Mathf.Lerp(PromptPulseMinimumBrightness, 1.0f, wave);
        _pressAnyKeyLabel.Modulate = new Color(brightness, brightness, brightness, 1.0f);
    }

    /// <summary>
    /// Starts the game on any non-debug key or joypad button.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_startAlreadyRequested)
            return;

        if (IsStartInput(@event))
        {
            _startAlreadyRequested = true;
            EmitSignal(SignalName.StartRequested);
            GetViewport().SetInputAsHandled();
        }
    }


    /// <summary>
    /// Loads the arcade TTF used by the title-screen prompt.
    /// If the font is missing, the title screen still works with Godot's fallback font.
    /// </summary>
    private void LoadArcadeFont()
    {
        _arcadeFont = ResourceLoader.Load<Font>(ArcadeFontPath);
        if (_arcadeFont == null)
            GD.PushWarning($"[TitleScreen] Missing arcade font: {ArcadeFontPath}");
    }

    /// <summary>
    /// Creates a solid black background that covers the current viewport.
    /// </summary>
    private void BuildBackground()
    {
        Vector2 viewportSize = GetViewportRect().Size;

        ColorRect background = new()
        {
            Name = "Background",
            Color = _black,
            Position = Vector2.Zero,
            Size = viewportSize
        };

        AddChild(background);
    }

    /// <summary>
    /// Places the four animated enemies above the logo, matching the requested order:
    /// level 5, then level 1 to the right, level 2 to the left, and level 3 near the middle.
    /// </summary>
    private void BuildAnimatedEnemies()
    {
        Vector2 viewportSize = GetViewportRect().Size;

        // Imagine a rectangle around the four 64x64 enemy sprites.
        // Its center is placed at the center of the free area above the logo,
        // rather than centering each enemy independently.
        float logoTopY = LogoCenterY - (LogoPixelHeight * 0.5f);
        Vector2 groupCenter = new(viewportSize.X * 0.5f, logoTopY * 0.5f);

        AddEnemy(5, groupCenter + new Vector2(-95.0f, -107.5f), "move_up");
        AddEnemy(1, groupCenter + new Vector2(265.0f, -52.5f), "move_right", flipHorizontally: true);
        AddEnemy(2, groupCenter + new Vector2(-265.0f, 67.5f), "move_right");
        AddEnemy(3, groupCenter + new Vector2(40.0f, 107.5f), "move_up");
    }

    /// <summary>
    /// Adds the provided Lady Bug title logo, centered horizontally.
    /// </summary>
    private void BuildLogo()
    {
        Texture2D? logoTexture = ResourceLoader.Load<Texture2D>(LogoPath);
        if (logoTexture == null)
        {
            GD.PushWarning($"[TitleScreen] Missing logo texture: {LogoPath}");
            return;
        }

        Sprite2D logo = new()
        {
            Name = "Logo",
            Texture = logoTexture,
            Centered = true,
            Position = new Vector2(GetViewportRect().Size.X * 0.5f, LogoCenterY)
        };

        AddChild(logo);
    }

    /// <summary>
    /// Adds the ladybug and the single start prompt below the logo.
    /// The prompt itself is centered horizontally on screen and vertically inside
    /// the free area between the bottom of the logo and the bottom of the viewport.
    /// </summary>
    private void BuildBottomPrompt()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        float logoBottomY = LogoCenterY + (LogoPixelHeight * 0.5f);
        float bottomAreaCenterY = logoBottomY + ((viewportSize.Y - logoBottomY) * 0.5f);

        Vector2 labelSize = new(620.0f, 56.0f);
        Vector2 labelPosition = new(
            (viewportSize.X * 0.5f) - (labelSize.X * 0.5f),
            bottomAreaCenterY - (labelSize.Y * 0.5f));

        // Press Start 2P is much wider than Godot's default font.
        // The prompt label is centered on the screen, but the visible text occupies only
        // the middle of that label. Keep the ladybug in the left gap between the screen
        // edge and the text, instead of anchoring it outside the enlarged label.
        AddLadybug(new Vector2(labelPosition.X + 70.0f, bottomAreaCenterY));
        _pressAnyKeyLabel = AddLabel("PRESS ANY KEY", labelPosition, labelSize, 26, _white, "PressAnyKeyLabel");
    }

    /// <summary>
    /// Adds one visual-only animated enemy from the level spritesheet.
    /// </summary>
    private void AddEnemy(int levelNumber, Vector2 position, string animationName, bool flipHorizontally = false)
    {
        string path = string.Format(EnemySpritesheetPattern, levelNumber);
        AnimatedSprite2D sprite = CreateAnimatedSpriteFromSixFrameSheet(path, EnemyAnimationSpeed, EnemyAnimationSpeed);
        sprite.Name = $"EnemyLevel{levelNumber}";
        sprite.Position = position;
        sprite.FlipH = flipHorizontally;
        sprite.Play(animationName);
        AddChild(sprite);
    }

    /// <summary>
    /// Adds the visual-only ladybug sprite used as the start prompt marker.
    /// </summary>
    private void AddLadybug(Vector2 position)
    {
        AnimatedSprite2D sprite = CreateAnimatedSpriteFromSixFrameSheet(PlayerSpritesheetPath, PlayerAnimationSpeed, PlayerAnimationSpeed);
        sprite.Name = "LadybugPrompt";
        sprite.Position = position;
        sprite.Play("move_right");
        AddChild(sprite);
    }

    /// <summary>
    /// Creates an AnimatedSprite2D from the project's existing six-frame spritesheet layout:
    /// three right-moving frames, then three upward-moving frames.
    /// </summary>
    private AnimatedSprite2D CreateAnimatedSpriteFromSixFrameSheet(string path, float moveRightSpeed, float moveUpSpeed)
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
        AnimatedSprite2D sprite = new()
        {
            Centered = true
        };

        if (texture == null)
        {
            GD.PushWarning($"[TitleScreen] Missing spritesheet: {path}");
            return sprite;
        }

        SpriteFrames frames = new();
        AddAnimation(frames, "move_right", texture, moveRightSpeed, 0, 1, 2);
        AddAnimation(frames, "move_up", texture, moveUpSpeed, 3, 4, 5);

        sprite.SpriteFrames = frames;
        sprite.Animation = "move_right";
        return sprite;
    }

    /// <summary>
    /// Adds one looping animation from three frame indexes in a horizontal spritesheet.
    /// </summary>
    private static void AddAnimation(SpriteFrames frames, string animationName, Texture2D texture, float speed, int frame0, int frame1, int frame2)
    {
        frames.AddAnimation(animationName);
        frames.SetAnimationLoop(animationName, true);
        frames.SetAnimationSpeed(animationName, speed);
        frames.AddFrame(animationName, MakeAtlasTexture(texture, frame0));
        frames.AddFrame(animationName, MakeAtlasTexture(texture, frame1));
        frames.AddFrame(animationName, MakeAtlasTexture(texture, frame2));
    }

    /// <summary>
    /// Returns one 64x64 atlas frame from a horizontal spritesheet.
    /// </summary>
    private static AtlasTexture MakeAtlasTexture(Texture2D texture, int frameIndex)
    {
        return new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2(frameIndex * FrameSize, 0, FrameSize, FrameSize)
        };
    }

    /// <summary>
    /// Creates a simple arcade-coloured label at a fixed screen position.
    /// </summary>
    private Label AddLabel(string text, Vector2 position, Vector2 size, int fontSize, Color fontColor, string nodeName)
    {
        Label label = new()
        {
            Name = nodeName,
            Text = text,
            Position = position,
            Size = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (_arcadeFont != null)
            label.AddThemeFontOverride("font", _arcadeFont);

        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", fontColor);
        AddChild(label);

        return label;
    }

    /// <summary>
    /// Accepts normal start inputs while preserving function keys for gameplay debug usage.
    /// </summary>
    private static bool IsStartInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            if (!keyEvent.Pressed || keyEvent.Echo)
                return false;

            return keyEvent.Keycode != Key.F1 && keyEvent.Keycode != Key.F2 && keyEvent.Keycode != Key.F12;
        }

        if (@event is InputEventJoypadButton joypadButton)
            return joypadButton.Pressed;

        return false;
    }
}
