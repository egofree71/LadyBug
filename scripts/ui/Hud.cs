using System;
using System.Collections.Generic;
using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Displays the prototype in-game HUD for one active level.
/// </summary>
/// <remarks>
/// <para>
/// The HUD currently shows the score, remaining lives, SPECIAL / EXTRA word
/// progress, and the blue-heart score multiplier indicators.
/// </para>
/// <para>
/// This script deliberately avoids hardcoding node placement. Positions, anchors,
/// margins, and most visual layout details remain authored in <c>Level.tscn</c>.
/// The script only finds the expected HUD nodes and updates their dynamic content.
/// </para>
/// <para>
/// SPECIAL, EXTRA, and multiplier indicators use <see cref="RichTextLabel"/> so
/// individual letters can be colored independently while the words remain in a
/// single logical HUD area.
/// </para>
/// </remarks>
public partial class Hud : CanvasLayer
{
    // Inactive letters in the arcade HUD are light grey rather than pure white.
    private const string GreyColor = "#C8C8C8";

    // SPECIAL uses the same red/orange color as red heart/letter collectibles.
    private const string SpecialActiveColor = "#FF5100";

    // EXTRA uses the same yellow color as yellow heart/letter collectibles.
    private const string ExtraActiveColor = "#FFFF00";

    // Multipliers use the same blue color as blue heart/letter collectibles.
    private const string MultiplierActiveColor = "#00AEFF";

    // The original HUD uses large tile-like letters. RichTextLabel text is kept
    // at the same visual size as the lower score label.
    private const int TopHudFontSize = 36;

    // The player spritesheet is built from 64x64 frames. The spare-life HUD uses
    // one static atlas frame from the same sheet, matching the arcade-style icon
    // display instead of drawing a numeric text value.
    private const string DefaultLifeIconTexturePath = "res://assets/sprites/player/ladybug_spritesheet.png";
    private const int LifeIconFrameSize = 64;

    // Runtime-only sprite drawn above the HUD while one life icon enters the maze.
    private const int LifeEntryAnimationZIndex = 300;

    // The entry path is driven by the Level fixed tick. The player moves one
    // arcade pixel per tick; with the current 4x render scale that is four scene
    // pixels per tick. This keeps the entry ladybug at the same movement speed as
    // the playable ladybug.
    private const float DefaultLifeEntryAnimationScenePixelsPerTick = 4.0f;

    private enum LifeEntryAnimationPhase
    {
        None,
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Gets or sets the path to the score label.
    /// </summary>
    /// <remarks>
    /// The fallback lookup still checks <c>Root/ScoreLabel</c> and <c>ScoreLabel</c>
    /// so the scene can be reorganized without immediately breaking the script.
    /// </remarks>
    [Export]
    public NodePath ScoreLabelPath { get; set; } = "Root/ScoreLabel";

    /// <summary>
    /// Gets or sets the path to the lives display anchor.
    /// </summary>
    /// <remarks>
    /// The node is still a Label in <c>Level.tscn</c> for compatibility with the
    /// previous HUD layout, but its text is cleared and TextureRect children are
    /// created under it to render one coccinelle sprite per visible life.
    /// </remarks>
    [Export]
    public NodePath LivesLabelPath { get; set; } = "Root/LivesLabel";

    /// <summary>
    /// Gets or sets the path to the RichTextLabel that displays SPECIAL.
    /// </summary>
    [Export]
    public NodePath SpecialWordLabelPath { get; set; } = "Root/SpecialWordLabel";

    /// <summary>
    /// Gets or sets the path to the RichTextLabel that displays EXTRA.
    /// </summary>
    [Export]
    public NodePath ExtraWordLabelPath { get; set; } = "Root/ExtraWordLabel";

    /// <summary>
    /// Gets or sets the path to the RichTextLabel that displays x2 / x3 / x5.
    /// </summary>
    [Export]
    public NodePath MultipliersLabelPath { get; set; } = "Root/MultipliersLabel";

    /// <summary>
    /// Gets or sets the spritesheet used for the spare-life icon.
    /// </summary>
    [Export]
    public string LifeIconTexturePath { get; set; } = DefaultLifeIconTexturePath;

    /// <summary>
    /// Gets or sets which 64x64 frame from <see cref="LifeIconTexturePath"/> is
    /// used as the static spare-life icon.
    /// </summary>
    [Export]
    public int LifeIconFrameIndex { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of lives that can be rendered in the HUD.
    /// </summary>
    /// <remarks>
    /// This is only a display cap. The semantic life counter itself is not capped:
    /// if the player has more lives than this value, the internal count remains
    /// higher but the HUD still draws only this many icons.
    /// </remarks>
    [Export]
    public int MaxVisibleLifeIcons { get; set; } = 5;

    /// <summary>
    /// Gets or sets the rendered size of each spare-life icon.
    /// </summary>
    [Export]
    public Vector2 LifeIconSize { get; set; } = new(64, 64);

    /// <summary>
    /// Gets or sets the horizontal spacing between spare-life icons.
    /// </summary>
    [Export]
    public float LifeIconSpacing { get; set; } = 64.0f;

    /// <summary>
    /// Gets or sets a local offset applied inside the lives display anchor.
    /// </summary>
    [Export]
    public Vector2 LifeIconOffset { get; set; } = Vector2.Zero;

    /// <summary>
    /// Gets or sets the distance travelled by the entering life icon on each fixed
    /// simulation tick, in rendered scene pixels.
    /// </summary>
    [Export]
    public float LifeEntryAnimationScenePixelsPerTick { get; set; } = DefaultLifeEntryAnimationScenePixelsPerTick;

    /// <summary>
    /// Gets or sets the animation speed used by the temporary entering ladybug sprite.
    /// </summary>
    [Export]
    public float LifeEntryAnimationFramesPerSecond { get; set; } = 12.0f;

    private Label? _scoreLabel;
    private Label? _livesLabel;
    private RichTextLabel? _specialWordLabel;
    private RichTextLabel? _extraWordLabel;
    private RichTextLabel? _multipliersLabel;

    private readonly List<TextureRect> _lifeIconViews = new();
    private Texture2D? _lifeIconTexture;
    private string _loadedLifeIconTexturePath = string.Empty;
    private int _loadedLifeIconFrameIndex = int.MinValue;
    private bool _lifeIconTextureWarningShown;

    private AnimatedSprite2D? _lifeEntrySprite;
    private LifeEntryAnimationPhase _lifeEntryAnimationPhase = LifeEntryAnimationPhase.None;
    private Vector2 _lifeEntryHorizontalTarget;
    private Vector2 _lifeEntryFinalTarget;
    private int _lifeEntryHiddenSourceIconIndex = -1;
    private bool _lifeEntryAnimationWarningShown;

    // True while the current playable life is represented by the player sprite in
    // the maze. In that state, the HUD displays only reserve lives. During PART
    // screens and after a death has consumed the active life, every remaining
    // available life is displayed in the HUD.
    private bool _isCurrentLifeInMaze = true;

    /// <summary>
    /// Gets whether a HUD life icon is currently travelling into the playfield.
    /// </summary>
    public bool IsLifeEntryAnimationActive => _lifeEntrySprite != null;

    // Last known values are cached so _Ready can safely reapply them if the HUD
    // enters the scene after Level has already called one of the setter methods.
    private int _lastScore;
    private int _lastLives = 3;
    private int _lastMultiplierStep;
    private string _lastSpecialWordText = BuildInactiveSpecialWordText();
    private string _lastExtraWordText = BuildInactiveExtraWordText();
    private string _lastMultipliersText = BuildMultipliersText(0);

    /// <summary>
    /// Resolves the HUD nodes and applies the cached initial values.
    /// </summary>
    public override void _Ready()
    {
        _scoreLabel = FindScoreLabel();
        _livesLabel = FindLivesLabel();
        _specialWordLabel = FindRichTextLabel(SpecialWordLabelPath, "Root/SpecialWordLabel", "SpecialWordLabel");
        _extraWordLabel = FindRichTextLabel(ExtraWordLabelPath, "Root/ExtraWordLabel", "ExtraWordLabel");
        _multipliersLabel = FindRichTextLabel(MultipliersLabelPath, "Root/MultipliersLabel", "MultipliersLabel");

        if (_scoreLabel == null)
            GD.PushWarning("[Hud] Could not find ScoreLabel. Expected Root/ScoreLabel or ScoreLabel, or set ScoreLabelPath in the Inspector.");

        if (_livesLabel == null)
            GD.PushWarning("[Hud] Could not find LivesLabel. Expected Root/LivesLabel or LivesLabel, or set LivesLabelPath in the Inspector.");

        if (_specialWordLabel == null)
            GD.PushWarning("[Hud] Could not find SpecialWordLabel. Expected Root/SpecialWordLabel or SpecialWordLabel, or set SpecialWordLabelPath in the Inspector.");

        if (_extraWordLabel == null)
            GD.PushWarning("[Hud] Could not find ExtraWordLabel. Expected Root/ExtraWordLabel or ExtraWordLabel, or set ExtraWordLabelPath in the Inspector.");

        if (_multipliersLabel == null)
            GD.PushWarning("[Hud] Could not find MultipliersLabel. Expected Root/MultipliersLabel or MultipliersLabel, or set MultipliersLabelPath in the Inspector.");

        // Important: this script does not set screen positions or anchors.
        // Those are controlled in Level.tscn. It only controls dynamic content.
        SetScore(_lastScore);
        SetLives(_lastLives);
        ApplyRichText(_specialWordLabel, _lastSpecialWordText);
        ApplyRichText(_extraWordLabel, _lastExtraWordText);
        ApplyRichText(_multipliersLabel, _lastMultipliersText);
    }

    /// <summary>
    /// Updates the numeric score display.
    /// </summary>
    /// <param name="score">Current player score.</param>
    public void SetScore(int score)
    {
        _lastScore = score;

        if (_scoreLabel == null)
            return;

        _scoreLabel.Text = score.ToString();
    }

    /// <summary>
    /// Updates the remaining-lives display.
    /// </summary>
    /// <param name="lives">Current remaining life count.</param>
    public void SetLives(int lives)
    {
        _lastLives = lives;
        UpdateLifeIconDisplay();
    }

    /// <summary>
    /// Controls whether the current life is already represented by the player sprite in the maze.
    /// </summary>
    /// <remarks>
    /// When <paramref name="isInMaze"/> is true, the HUD shows only reserve lives
    /// because one life is currently being played. When it is false, every remaining
    /// available life is shown in the HUD. This matches the arcade flow: PART screens
    /// show all available ladybugs, then the rightmost one leaves the HUD and becomes
    /// the playable character.
    /// </remarks>
    /// <param name="isInMaze">Whether the current available life is already in the maze.</param>
    public void SetCurrentLifeInMaze(bool isInMaze)
    {
        _isCurrentLifeInMaze = isInMaze;
        UpdateLifeIconDisplay();
    }

    /// <summary>
    /// Updates the SPECIAL and EXTRA word displays from the current word progress state.
    /// </summary>
    /// <param name="wordProgress">Semantic progress through both bonus words.</param>
    public void SetWordProgress(WordProgressState wordProgress)
    {
        _lastSpecialWordText = BuildSpecialWordText(wordProgress);
        _lastExtraWordText = BuildExtraWordText(wordProgress);

        ApplyRichText(_specialWordLabel, _lastSpecialWordText);
        ApplyRichText(_extraWordLabel, _lastExtraWordText);
    }

    /// <summary>
    /// Updates the x2 / x3 / x5 multiplier display from the blue-heart step.
    /// </summary>
    /// <remarks>
    /// Step 0 means no multiplier indicator is active. Step 1 lights x2, step 2
    /// lights x2 and x3, and step 3 lights x2, x3, and x5.
    /// </remarks>
    /// <param name="multiplierStep">Current blue-heart multiplier step.</param>
    public void SetMultiplierStep(int multiplierStep)
    {
        _lastMultiplierStep = Math.Clamp(multiplierStep, 0, 3);
        _lastMultipliersText = BuildMultipliersText(_lastMultiplierStep);
        ApplyRichText(_multipliersLabel, _lastMultipliersText);
    }

    /// <summary>
    /// Starts the arcade-style entry animation from the slot occupied by the
    /// current playable life.
    /// </summary>
    /// <remarks>
    /// <see cref="SetLives"/> receives the total semantic life count, including
    /// the active player life. The static HUD icons show only reserve lives, so
    /// the moving sprite starts from the next slot to the right of the reserves.
    /// Example: with 3 total lives, 2 static HUD icons remain and the 3rd icon
    /// enters the maze as the current player.
    /// </remarks>
    /// <param name="targetCenterScenePosition">Target sprite-center position in viewport / canvas coordinates.</param>
    /// <returns><see langword="true"/> when the animation was started.</returns>
    public bool TryStartLifeEntryAnimation(Vector2 targetCenterScenePosition)
    {
        CancelLifeEntryAnimation();

        if (_livesLabel == null)
            return false;

        int maxVisibleIcons = Math.Max(0, MaxVisibleLifeIcons);
        int totalLives = Math.Max(0, _lastLives);

        if (totalLives <= 0 || maxVisibleIcons <= 0)
            return false;

        // Before the entry starts, all remaining lives are available in the HUD.
        // The moving sprite is cloned from the rightmost available icon.
        _isCurrentLifeInMaze = false;
        UpdateLifeIconDisplay();

        int sourceIconIndex = Math.Clamp(totalLives - 1, 0, maxVisibleIcons - 1);
        if (sourceIconIndex < 0 || sourceIconIndex >= _lifeIconViews.Count)
            return false;

        Texture2D? sheetTexture = LoadLifeIconSheetTexture();
        if (sheetTexture == null)
            return false;

        TextureRect sourceIcon = _lifeIconViews[sourceIconIndex];
        Vector2 sourceCenter = GetLifeIconCenter(sourceIcon, sourceIconIndex);

        // Hide the exact HUD slot that visually becomes the travelling sprite.
        // This matters when the life counter is higher than MaxVisibleLifeIcons:
        // the reserve-life count is still capped at the maximum, so without this
        // explicit hidden slot the rightmost icon would remain visible underneath
        // its moving clone for the whole entry animation.
        _lifeEntryHiddenSourceIconIndex = sourceIconIndex;

        _lifeEntryHorizontalTarget = new Vector2(targetCenterScenePosition.X, sourceCenter.Y);
        _lifeEntryFinalTarget = targetCenterScenePosition;
        _lifeEntryAnimationPhase = LifeEntryAnimationPhase.Horizontal;

        _lifeEntrySprite = CreateLifeEntrySprite(sheetTexture);
        _lifeEntrySprite.Position = sourceCenter;
        AddChild(_lifeEntrySprite);

        // The rightmost HUD icon is now in transit, so the static HUD immediately
        // switches back to reserve-life display while the clone moves into the maze.
        _isCurrentLifeInMaze = true;

        ApplyLifeEntrySpriteFacing(_lifeEntryHorizontalTarget);
        UpdateLifeIconDisplay();
        return true;
    }

    /// <summary>
    /// Advances the active life-entry animation by one fixed gameplay tick.
    /// </summary>
    /// <returns><see langword="true"/> when the animation is finished or inactive.</returns>
    public bool AdvanceLifeEntryAnimationOneTick()
    {
        if (_lifeEntrySprite == null)
            return true;

        Vector2 target = _lifeEntryAnimationPhase == LifeEntryAnimationPhase.Horizontal
            ? _lifeEntryHorizontalTarget
            : _lifeEntryFinalTarget;

        float stepDistance = Math.Max(1.0f, LifeEntryAnimationScenePixelsPerTick);
        _lifeEntrySprite.Position = MoveTowards(_lifeEntrySprite.Position, target, stepDistance);

        if (!HasReached(_lifeEntrySprite.Position, target))
            return false;

        if (_lifeEntryAnimationPhase == LifeEntryAnimationPhase.Horizontal)
        {
            _lifeEntryAnimationPhase = LifeEntryAnimationPhase.Vertical;
            ApplyLifeEntrySpriteFacing(_lifeEntryFinalTarget);
            return false;
        }

        CancelLifeEntryAnimation();
        return true;
    }

    /// <summary>
    /// Stops the life-entry animation. The travelling icon is not restored as a
    /// reserve icon because it has become the active player life in the maze.
    /// </summary>
    public void CancelLifeEntryAnimation()
    {
        if (_lifeEntrySprite != null && GodotObject.IsInstanceValid(_lifeEntrySprite))
            _lifeEntrySprite.QueueFree();

        _lifeEntrySprite = null;
        _lifeEntryAnimationPhase = LifeEntryAnimationPhase.None;
        _lifeEntryHiddenSourceIconIndex = -1;
        UpdateLifeIconDisplay();
    }

    /// <summary>
    /// Finds the score label using the exported path first, then scene-name fallbacks.
    /// </summary>
    private Label? FindScoreLabel()
    {
        if (!ScoreLabelPath.IsEmpty)
        {
            Label? exportedPathLabel = GetNodeOrNull<Label>(ScoreLabelPath);
            if (exportedPathLabel != null)
                return exportedPathLabel;
        }

        Label? rootChildLabel = GetNodeOrNull<Label>("Root/ScoreLabel");
        if (rootChildLabel != null)
            return rootChildLabel;

        return GetNodeOrNull<Label>("ScoreLabel");
    }

    /// <summary>
    /// Finds the lives display anchor using the exported path first, then scene-name fallbacks.
    /// </summary>
    private Label? FindLivesLabel()
    {
        if (!LivesLabelPath.IsEmpty)
        {
            Label? exportedPathLabel = GetNodeOrNull<Label>(LivesLabelPath);
            if (exportedPathLabel != null)
                return exportedPathLabel;
        }

        Label? rootChildLabel = GetNodeOrNull<Label>("Root/LivesLabel");
        if (rootChildLabel != null)
            return rootChildLabel;

        return GetNodeOrNull<Label>("LivesLabel");
    }

    /// <summary>
    /// Finds one RichTextLabel using the exported path first, then stable fallback paths.
    /// </summary>
    /// <param name="exportedPath">Inspector-configurable node path.</param>
    /// <param name="rootPath">Expected path under the HUD root node.</param>
    /// <param name="fallbackPath">Fallback path for flatter HUD scene structures.</param>
    private RichTextLabel? FindRichTextLabel(
        NodePath exportedPath,
        string rootPath,
        string fallbackPath)
    {
        if (!exportedPath.IsEmpty)
        {
            RichTextLabel? exportedPathLabel = GetNodeOrNull<RichTextLabel>(exportedPath);
            if (exportedPathLabel != null)
                return exportedPathLabel;
        }

        RichTextLabel? rootChildLabel = GetNodeOrNull<RichTextLabel>(rootPath);
        if (rootChildLabel != null)
            return rootChildLabel;

        return GetNodeOrNull<RichTextLabel>(fallbackPath);
    }

    /// <summary>
    /// Renders the static coccinelle icons for the current HUD phase, capped to MaxVisibleLifeIcons.
    /// </summary>
    private void UpdateLifeIconDisplay()
    {
        if (_livesLabel == null)
            return;

        // The label remains as a scene-authored layout anchor, but it no longer
        // renders text. Its children carry the actual life icons.
        _livesLabel.Text = string.Empty;
        _livesLabel.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        int maxVisibleIcons = Math.Max(0, MaxVisibleLifeIcons);
        int visibleLives = GetVisibleLifeIconCount(maxVisibleIcons);

        EnsureLifeIconTexture();
        EnsureLifeIconViews(maxVisibleIcons);

        for (int i = 0; i < _lifeIconViews.Count; i++)
        {
            TextureRect icon = _lifeIconViews[i];
            bool visible = i < visibleLives
                && _lifeIconTexture != null
                && i != _lifeEntryHiddenSourceIconIndex;

            icon.Visible = visible;
            icon.Texture = _lifeIconTexture;
            icon.Position = LifeIconOffset + new Vector2(i * LifeIconSpacing, 0.0f);
            icon.Size = LifeIconSize;
            icon.CustomMinimumSize = LifeIconSize;
        }
    }


    /// <summary>
    /// Converts the semantic life counter to the number of icons visible in the
    /// current HUD phase.
    /// </summary>
    private int GetVisibleLifeIconCount(int maxVisibleIcons)
    {
        int visibleLives = _isCurrentLifeInMaze
            ? Math.Max(0, _lastLives - 1)
            : Math.Max(0, _lastLives);

        return Math.Clamp(visibleLives, 0, maxVisibleIcons);
    }

    /// <summary>
    /// Loads the configured player spritesheet frame used by the life icons.
    /// </summary>
    private void EnsureLifeIconTexture()
    {
        string texturePath = string.IsNullOrWhiteSpace(LifeIconTexturePath)
            ? DefaultLifeIconTexturePath
            : LifeIconTexturePath;

        int frameIndex = Math.Max(0, LifeIconFrameIndex);

        if (_lifeIconTexture != null
            && _loadedLifeIconTexturePath == texturePath
            && _loadedLifeIconFrameIndex == frameIndex)
        {
            return;
        }

        Texture2D? sheetTexture = GD.Load<Texture2D>(texturePath);
        if (sheetTexture == null)
        {
            _lifeIconTexture = null;
            _loadedLifeIconTexturePath = string.Empty;
            _loadedLifeIconFrameIndex = int.MinValue;

            if (!_lifeIconTextureWarningShown)
            {
                GD.PushWarning($"[Hud] Could not load life icon texture at '{texturePath}'.");
                _lifeIconTextureWarningShown = true;
            }

            return;
        }

        int maxFrameIndex = Math.Max(0, (sheetTexture.GetWidth() / LifeIconFrameSize) - 1);
        int clampedFrameIndex = Math.Clamp(frameIndex, 0, maxFrameIndex);

        if (clampedFrameIndex != frameIndex && !_lifeIconTextureWarningShown)
        {
            GD.PushWarning($"[Hud] LifeIconFrameIndex {frameIndex} is outside '{texturePath}'. Using frame {clampedFrameIndex} instead.");
            _lifeIconTextureWarningShown = true;
        }

        _lifeIconTexture = new AtlasTexture
        {
            Atlas = sheetTexture,
            Region = new Rect2(
                clampedFrameIndex * LifeIconFrameSize,
                0,
                LifeIconFrameSize,
                Math.Min(LifeIconFrameSize, sheetTexture.GetHeight()))
        };

        _loadedLifeIconTexturePath = texturePath;
        _loadedLifeIconFrameIndex = frameIndex;
    }

    /// <summary>
    /// Ensures the HUD owns exactly one reusable TextureRect per possible visible life.
    /// </summary>
    /// <param name="desiredCount">Number of icon views to keep alive.</param>
    private void EnsureLifeIconViews(int desiredCount)
    {
        if (_livesLabel == null)
            return;

        while (_lifeIconViews.Count > desiredCount)
        {
            int lastIndex = _lifeIconViews.Count - 1;
            TextureRect icon = _lifeIconViews[lastIndex];
            _lifeIconViews.RemoveAt(lastIndex);
            icon.QueueFree();
        }

        while (_lifeIconViews.Count < desiredCount)
        {
            int iconNumber = _lifeIconViews.Count + 1;
            TextureRect icon = new()
            {
                Name = $"LifeIcon{iconNumber}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };

            _livesLabel.AddChild(icon);
            _lifeIconViews.Add(icon);
        }
    }

    /// <summary>
    /// Loads the full player spritesheet used by the temporary entry animation.
    /// </summary>
    private Texture2D? LoadLifeIconSheetTexture()
    {
        string texturePath = string.IsNullOrWhiteSpace(LifeIconTexturePath)
            ? DefaultLifeIconTexturePath
            : LifeIconTexturePath;

        Texture2D? sheetTexture = ResourceLoader.Load<Texture2D>(texturePath);
        if (sheetTexture != null)
            return sheetTexture;

        if (!_lifeEntryAnimationWarningShown)
        {
            GD.PushWarning($"[Hud] Could not start life-entry animation because '{texturePath}' could not be loaded.");
            _lifeEntryAnimationWarningShown = true;
        }

        return null;
    }

    /// <summary>
    /// Computes the center of one HUD life icon in canvas coordinates.
    /// </summary>
    private Vector2 GetLifeIconCenter(TextureRect icon, int iconIndex)
    {
        if (icon.Size != Vector2.Zero)
            return icon.GlobalPosition + icon.Size * 0.5f;

        if (_livesLabel != null)
        {
            return _livesLabel.GlobalPosition +
                   LifeIconOffset +
                   new Vector2(iconIndex * LifeIconSpacing, 0.0f) +
                   LifeIconSize * 0.5f;
        }

        return LifeIconOffset + LifeIconSize * 0.5f;
    }

    /// <summary>
    /// Creates the temporary animated ladybug sprite used only during entry.
    /// </summary>
    private AnimatedSprite2D CreateLifeEntrySprite(Texture2D texture)
    {
        AnimatedSprite2D sprite = new()
        {
            Name = "LifeEntrySprite",
            Centered = true,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZAsRelative = false,
            ZIndex = LifeEntryAnimationZIndex
        };

        float animationSpeed = Math.Max(1.0f, LifeEntryAnimationFramesPerSecond);
        SpriteFrames frames = new();
        AddAnimation(frames, "move_right", texture, animationSpeed, 1, 0, 2);
        AddAnimation(frames, "move_up", texture, animationSpeed, 3, 4, 5);

        sprite.SpriteFrames = frames;
        sprite.Animation = "move_right";
        return sprite;
    }

    /// <summary>
    /// Selects the temporary sprite animation and mirroring for the current segment.
    /// </summary>
    private void ApplyLifeEntrySpriteFacing(Vector2 target)
    {
        if (_lifeEntrySprite == null)
            return;

        Vector2 delta = target - _lifeEntrySprite.Position;
        _lifeEntrySprite.FlipH = false;
        _lifeEntrySprite.FlipV = false;

        if (Math.Abs(delta.X) >= Math.Abs(delta.Y) && Math.Abs(delta.X) > 0.01f)
        {
            _lifeEntrySprite.Play("move_right");
            _lifeEntrySprite.FlipH = delta.X < 0.0f;
        }
        else
        {
            _lifeEntrySprite.Play("move_up");
            _lifeEntrySprite.FlipV = delta.Y > 0.0f;
        }
    }

    /// <summary>
    /// Adds one looping animation from three frame indexes in the player spritesheet.
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
    /// Returns one 64x64 atlas frame from the horizontal player spritesheet.
    /// </summary>
    private static AtlasTexture MakeAtlasTexture(Texture2D texture, int frameIndex)
    {
        return new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2(frameIndex * LifeIconFrameSize, 0, LifeIconFrameSize, LifeIconFrameSize)
        };
    }

    /// <summary>
    /// Moves a point toward a target without overshooting.
    /// </summary>
    private static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
    {
        Vector2 delta = target - current;
        float distance = delta.Length();

        if (distance <= maxDistanceDelta || distance <= 0.0001f)
            return target;

        return current + delta / distance * maxDistanceDelta;
    }

    /// <summary>
    /// Checks whether a moving point has reached its current target.
    /// </summary>
    private static bool HasReached(Vector2 current, Vector2 target)
    {
        return current.DistanceSquaredTo(target) <= 0.01f;
    }

    /// <summary>
    /// Applies rich text markup to a label if that label exists.
    /// </summary>
    private static void ApplyRichText(RichTextLabel? label, string text)
    {
        if (label == null)
            return;

        label.Text = text;
    }

    /// <summary>
    /// Builds the rich-text markup for the SPECIAL word.
    /// </summary>
    private static string BuildSpecialWordText(WordProgressState wordProgress)
    {
        string text = BuildColoredWord(
            WordProgressState.SpecialWordLetters,
            wordProgress.IsSpecialLetterActive,
            SpecialActiveColor);

        return TopHudText(text);
    }

    /// <summary>
    /// Builds the centered rich-text markup for the EXTRA word.
    /// </summary>
    private static string BuildExtraWordText(WordProgressState wordProgress)
    {
        string text = BuildColoredWord(
            WordProgressState.ExtraWordLetters,
            wordProgress.IsExtraLetterActive,
            ExtraActiveColor);

        return CenterText(TopHudText(text));
    }

    /// <summary>
    /// Builds one colored word by wrapping each letter in an individual color tag.
    /// </summary>
    /// <param name="letters">Ordered letters of the word to draw.</param>
    /// <param name="isLetterActive">Predicate indicating whether each letter is active.</param>
    /// <param name="activeColor">Color used for active letters.</param>
    private static string BuildColoredWord(
        LetterKind[] letters,
        Func<LetterKind, bool> isLetterActive,
        string activeColor)
    {
        string text = string.Empty;

        foreach (LetterKind letter in letters)
        {
            string color = isLetterActive(letter) ? activeColor : GreyColor;
            text += ColorToken(LetterToText(letter), color);
        }

        return text;
    }

    /// <summary>
    /// Builds the initial inactive SPECIAL display.
    /// </summary>
    private static string BuildInactiveSpecialWordText()
    {
        return TopHudText(BuildInactiveWordLetters("SPECIAL"));
    }

    /// <summary>
    /// Builds the initial inactive EXTRA display.
    /// </summary>
    private static string BuildInactiveExtraWordText()
    {
        return CenterText(TopHudText(BuildInactiveWordLetters("EXTRA")));
    }

    /// <summary>
    /// Builds the inactive grey markup for every character in one word.
    /// </summary>
    private static string BuildInactiveWordLetters(string word)
    {
        string text = string.Empty;

        foreach (char letter in word)
            text += ColorToken(letter.ToString(), GreyColor);

        return text;
    }

    /// <summary>
    /// Builds the right-aligned rich-text markup for x2 / x3 / x5.
    /// </summary>
    private static string BuildMultipliersText(int multiplierStep)
    {
        string x2Color = multiplierStep >= 1 ? MultiplierActiveColor : GreyColor;
        string x3Color = multiplierStep >= 2 ? MultiplierActiveColor : GreyColor;
        string x5Color = multiplierStep >= 3 ? MultiplierActiveColor : GreyColor;

        string text = string.Join(
            " ",
            ColorToken("x2", x2Color),
            ColorToken("x3", x3Color),
            ColorToken("x5", x5Color));

        return RightText(TopHudText(text));
    }

    /// <summary>
    /// Wraps top-HUD text in the font-size tag shared by SPECIAL, EXTRA, and multipliers.
    /// </summary>
    private static string TopHudText(string text)
    {
        return $"[font_size={TopHudFontSize}]{text}[/font_size]";
    }

    /// <summary>
    /// Wraps rich text in a center-alignment tag.
    /// </summary>
    private static string CenterText(string text)
    {
        return $"[center]{text}[/center]";
    }

    /// <summary>
    /// Wraps rich text in a right-alignment tag.
    /// </summary>
    private static string RightText(string text)
    {
        return $"[right]{text}[/right]";
    }

    /// <summary>
    /// Wraps text in a Godot rich-text color tag.
    /// </summary>
    private static string ColorToken(string text, string color)
    {
        return $"[color={color}]{text}[/color]";
    }

    /// <summary>
    /// Converts a letter enum into its HUD text representation.
    /// </summary>
    private static string LetterToText(LetterKind letter)
    {
        return letter switch
        {
            LetterKind.A => "A",
            LetterKind.C => "C",
            LetterKind.E => "E",
            LetterKind.I => "I",
            LetterKind.L => "L",
            LetterKind.P => "P",
            LetterKind.R => "R",
            LetterKind.S => "S",
            LetterKind.T => "T",
            LetterKind.X => "X",
            _ => string.Empty
        };
    }
}
