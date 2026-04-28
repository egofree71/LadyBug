using System;
using System.Collections.Generic;
using Godot;
using LadyBug.Gameplay.Enemies;

/// <summary>
/// Visual layer for the animated maze-border timer.
/// </summary>
/// <remarks>
/// <para>
/// The original arcade video tiles mixed some border-timer graphics with the fixed
/// purple maze wall. This remake keeps the purple wall inside the existing maze PNG
/// and renders only the white/green timer layer on top as separate transparent sprites.
/// </para>
/// <para>
/// The view owns only rendering and visual placement. The actual clockwise timer state
/// is delegated to <see cref="EnemyReleaseBorderTimer"/> and is advanced by the owning
/// <see cref="Level"/> fixed simulation tick.
/// </para>
/// </remarks>
[Tool]
public partial class MazeBorderTimerView : Node2D
{
    private const int TileFrameCount = 6;

    // Generated Sprite2D instances, ordered clockwise in the same order as the timer loop.
    private readonly List<Sprite2D> _sprites = new();

    // Pure logical countdown / progress state. It is recreated when the tile loop is rebuilt.
    private EnemyReleaseBorderTimer _timer = new(
        0,
        EnemyReleaseBorderTimer.GetTicksPerTileForLevel(1));

    // --- Exported backing fields -------------------------------------------

    private Texture2D? _tilesTexture;
    private Rect2 _mazeOuterWallRectLocal = new(new Vector2(16, 64), new Vector2(704, 704));
    private int _tileSize = 32;
    private int _extraGapScenePixels;
    private int _ticksPerTile = EnemyReleaseBorderTimer.GetTicksPerTileForLevel(1);
    private int _levelNumber = 1;
    private bool _useArcadeLevelTiming = true;
    private int _topExtraGapScenePixels;
    private int _rightExtraGapScenePixels = 4;
    private int _bottomExtraGapScenePixels = 4;
    private int _leftExtraGapScenePixels;
    private bool _startCycleAtTopMiddle = true;
    private int _cycleStartOffsetTiles;
    private bool _drawDebugBounds;

    // Sprite index that is treated as timer index 0. This lets the cycle start at
    // the arcade-observed top-middle tile without physically reordering the sprites.
    private int _cycleStartSpriteIndex;

    /// <summary>
    /// Spritesheet containing 6 frames:
    /// 0 top-left corner, 1 top-right corner, 2 bottom-left corner,
    /// 3 bottom-right corner, 4 vertical edge, 5 horizontal edge.
    /// </summary>
    [Export]
    public Texture2D TilesTexture
    {
        get => _tilesTexture!;
        set
        {
            _tilesTexture = value;
            RebuildTiles();
        }
    }

    /// <summary>
    /// Rectangle of the fixed purple outer wall, in the local coordinates of this node.
    /// Keep this node at position (0, 0) under Level for the default values to apply.
    /// </summary>
    [Export]
    public Rect2 MazeOuterWallRectLocal
    {
        get => _mazeOuterWallRectLocal;
        set
        {
            if (_mazeOuterWallRectLocal == value)
                return;

            _mazeOuterWallRectLocal = value;
            RebuildTiles();
            QueueRedraw();
        }
    }

    /// <summary>
    /// Size of one timer tile in Godot scene pixels.
    /// </summary>
    [Export(PropertyHint.Range, "1,128,1")]
    public int TileSize
    {
        get => _tileSize;
        set
        {
            int clampedValue = Math.Max(1, value);
            if (_tileSize == clampedValue)
                return;

            _tileSize = clampedValue;
            RebuildTiles();
        }
    }

    /// <summary>
    /// Extra external gap between the purple wall rectangle and the timer tile cells.
    /// The provided tiles already include their own internal transparent margin, so the
    /// default is 0.
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public int ExtraGapScenePixels
    {
        get => _extraGapScenePixels;
        set
        {
            int clampedValue = Math.Max(0, value);
            if (_extraGapScenePixels == clampedValue)
                return;

            _extraGapScenePixels = clampedValue;
            RebuildTiles();
        }
    }

    /// <summary>
    /// Additional outward offset applied only to the top edge tiles.
    /// Useful if the visual tile artwork needs slightly more separation from the fixed purple wall.
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public int TopExtraGapScenePixels
    {
        get => _topExtraGapScenePixels;
        set
        {
            int clampedValue = Math.Max(0, value);
            if (_topExtraGapScenePixels == clampedValue)
                return;

            _topExtraGapScenePixels = clampedValue;
            RebuildTiles();
        }
    }

    /// <summary>
    /// Additional outward offset applied only to the right edge tiles.
    /// This compensates for the asymmetry of the simplified timer tileset.
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public int RightExtraGapScenePixels
    {
        get => _rightExtraGapScenePixels;
        set
        {
            int clampedValue = Math.Max(0, value);
            if (_rightExtraGapScenePixels == clampedValue)
                return;

            _rightExtraGapScenePixels = clampedValue;
            RebuildTiles();
        }
    }

    /// <summary>
    /// Additional outward offset applied only to the bottom edge tiles.
    /// This compensates for the asymmetry of the simplified timer tileset.
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public int BottomExtraGapScenePixels
    {
        get => _bottomExtraGapScenePixels;
        set
        {
            int clampedValue = Math.Max(0, value);
            if (_bottomExtraGapScenePixels == clampedValue)
                return;

            _bottomExtraGapScenePixels = clampedValue;
            RebuildTiles();
        }
    }

    /// <summary>
    /// Additional outward offset applied only to the left edge tiles.
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public int LeftExtraGapScenePixels
    {
        get => _leftExtraGapScenePixels;
        set
        {
            int clampedValue = Math.Max(0, value);
            if (_leftExtraGapScenePixels == clampedValue)
                return;

            _leftExtraGapScenePixels = clampedValue;
            RebuildTiles();
        }
    }

    /// <summary>
    /// Gets or sets whether the timer color sequence starts at the middle of the top edge.
    /// </summary>
    /// <remarks>
    /// The sprites are still generated from top-left clockwise. This setting only remaps
    /// logical timer index 0 to the arcade-observed starting position.
    /// </remarks>
    [Export]
    public bool StartCycleAtTopMiddle
    {
        get => _startCycleAtTopMiddle;
        set
        {
            if (_startCycleAtTopMiddle == value)
                return;

            _startCycleAtTopMiddle = value;
            RebuildTiles();
        }
    }

    /// <summary>
    /// Fine adjustment for the top-middle cycle start, expressed in top-edge tiles.
    /// </summary>
    /// <remarks>
    /// Use -1 or +1 if the first changing tile is one tile away from the desired
    /// arcade reference point.
    /// </remarks>
    [Export(PropertyHint.Range, "-32,32,1")]
    public int CycleStartOffsetTiles
    {
        get => _cycleStartOffsetTiles;
        set
        {
            if (_cycleStartOffsetTiles == value)
                return;

            _cycleStartOffsetTiles = value;
            RebuildTiles();
        }
    }

    /// <summary>
    /// Player-visible level number used to choose the arcade border timer cadence.
    /// </summary>
    /// <remarks>
    /// Reverse engineering found these periods: 9 ticks at level 1, 6 ticks at
    /// levels 2-4, and 3 ticks from level 5 onward.
    /// </remarks>
    [Export(PropertyHint.Range, "1,999,1")]
    public int LevelNumber
    {
        get => _levelNumber;
        set
        {
            int clampedValue = Math.Max(1, value);
            if (_levelNumber == clampedValue)
                return;

            _levelNumber = clampedValue;
            ApplyEffectiveTicksPerTile(resetTimer: true);
        }
    }

    /// <summary>
    /// Gets or sets whether the reverse-engineered arcade timing should be used.
    /// </summary>
    /// <remarks>
    /// When enabled, <see cref="TicksPerTile"/> is ignored at runtime and the period
    /// is derived from <see cref="LevelNumber"/>. Disable this only for visual tuning.
    /// </remarks>
    [Export]
    public bool UseArcadeLevelTiming
    {
        get => _useArcadeLevelTiming;
        set
        {
            if (_useArcadeLevelTiming == value)
                return;

            _useArcadeLevelTiming = value;
            ApplyEffectiveTicksPerTile(resetTimer: true);
        }
    }

    /// <summary>
    /// Manual number of simulation ticks before one more border tile changes color.
    /// </summary>
    /// <remarks>
    /// This is used only when <see cref="UseArcadeLevelTiming"/> is disabled.
    /// Smaller values are useful while tuning the visual alignment.
    /// </remarks>
    [Export(PropertyHint.Range, "1,120,1")]
    public int TicksPerTile
    {
        get => _ticksPerTile;
        set
        {
            _ticksPerTile = Math.Max(1, value);
            ApplyEffectiveTicksPerTile(resetTimer: true);
        }
    }

    /// <summary>
    /// Color used for timer tiles that are not filled yet.
    /// </summary>
    [Export]
    public Color WhiteColor { get; set; } = Colors.White;

    /// <summary>
    /// Color used for timer tiles that have been filled by the enemy-release timer.
    /// </summary>
    [Export]
    public Color GreenColor { get; set; } = new Color(0.318f, 1.0f, 0.318f, 1.0f);

    /// <summary>
    /// Z-index assigned to generated border-timer sprites.
    /// </summary>
    [Export(PropertyHint.Range, "-4096,4096,1")]
    public int TileZIndex { get; set; } = 5;

    /// <summary>
    /// Draws the reference purple-wall rectangle used to position the generated border tiles.
    /// </summary>
    [Export]
    public bool DrawDebugBounds
    {
        get => _drawDebugBounds;
        set
        {
            _drawDebugBounds = value;
            QueueRedraw();
        }
    }

    /// <summary>
    /// Builds the generated sprite loop and initializes the visible timer state.
    /// </summary>
    public override void _Ready()
    {
        RebuildTiles();
        ResetTimer();
    }

    /// <summary>
    /// Removes generated sprites when the view leaves the scene tree.
    /// </summary>
    public override void _ExitTree()
    {
        ClearGeneratedTiles();
    }

    public override void _Draw()
    {
        if (!DrawDebugBounds)
            return;

        DrawRect(MazeOuterWallRectLocal, Colors.Yellow, false, 2.0f);
    }

    /// <summary>
    /// Resets the timer to the all-white initial state and updates the sprites.
    /// </summary>
    public void ResetTimer()
    {
        ApplyEffectiveTicksPerTile(resetTimer: false);
        _timer.Reset();
        ApplyTimerVisualState();
    }

    /// <summary>
    /// Updates the visible level used by the arcade timing rule.
    /// </summary>
    /// <remarks>
    /// Call this from the owning level at level startup or after a future level transition.
    /// It resets the border to the all-white state, matching the arcade's timer setup.
    /// </remarks>
    public void ConfigureForLevel(int levelNumber)
    {
        _levelNumber = Math.Max(1, levelNumber);
        ResetTimer();
    }

    /// <summary>
    /// Advances the border timer by one fixed simulation tick.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the green fill completed and an enemy should be
    /// released by the future enemy runtime.
    /// </returns>
    public bool AdvanceOneSimulationTick()
    {
        EnemyReleaseBorderTimerStepResult stepResult = _timer.AdvanceOneTick();

        if (stepResult.VisualChanged)
            ApplyTimerVisualState();

        return stepResult.ShouldReleaseEnemy;
    }

    /// <summary>
    /// Recreates all generated sprites from the exported rectangle and tile tuning values.
    /// </summary>
    private void RebuildTiles()
    {
        if (!IsInsideTree())
            return;

        ClearGeneratedTiles();

        if (_tilesTexture == null)
        {
            if (!Engine.IsEditorHint())
                GD.PushWarning("MazeBorderTimerView has no TilesTexture assigned.");

            return;
        }

        int columns = Math.Max(1, Mathf.RoundToInt(MazeOuterWallRectLocal.Size.X / TileSize));
        int rows = Math.Max(1, Mathf.RoundToInt(MazeOuterWallRectLocal.Size.Y / TileSize));

        _cycleStartSpriteIndex = StartCycleAtTopMiddle
            ? 1 + Math.Clamp((columns / 2) + CycleStartOffsetTiles, 0, columns - 1)
            : 0;

        float left = MazeOuterWallRectLocal.Position.X;
        float top = MazeOuterWallRectLocal.Position.Y;
        float right = left + columns * TileSize;
        float bottom = top + rows * TileSize;
        float baseGap = ExtraGapScenePixels;
        float topEdgeGap = baseGap + TopExtraGapScenePixels;
        float rightEdgeGap = baseGap + RightExtraGapScenePixels;
        float bottomEdgeGap = baseGap + BottomExtraGapScenePixels;
        float leftEdgeGap = baseGap + LeftExtraGapScenePixels;

        // Corners stay on the unmodified perimeter grid. The side-specific offsets
        // below are visual corrections for the simplified edge frames only; applying
        // them to corners makes the corner pieces drift away from the purple maze wall.
        Vector2 topLeftCornerPosition = new(left - TileSize - baseGap, top - TileSize - baseGap);
        Vector2 topRightCornerPosition = new(right + baseGap, top - TileSize - baseGap);
        Vector2 bottomRightCornerPosition = new(right + baseGap, bottom + baseGap);
        Vector2 bottomLeftCornerPosition = new(left - TileSize - baseGap, bottom + baseGap);

        AddTile(BorderTimerTileRole.TopLeftCorner, topLeftCornerPosition);

        for (int x = 0; x < columns; x++)
            AddTile(BorderTimerTileRole.TopHorizontal, new Vector2(left + x * TileSize, top - TileSize - topEdgeGap));

        AddTile(BorderTimerTileRole.TopRightCorner, topRightCornerPosition);

        for (int y = 0; y < rows; y++)
            AddTile(BorderTimerTileRole.RightVertical, new Vector2(right + rightEdgeGap, top + y * TileSize));

        AddTile(BorderTimerTileRole.BottomRightCorner, bottomRightCornerPosition);

        for (int x = columns - 1; x >= 0; x--)
            AddTile(BorderTimerTileRole.BottomHorizontal, new Vector2(left + x * TileSize, bottom + bottomEdgeGap));

        AddTile(BorderTimerTileRole.BottomLeftCorner, bottomLeftCornerPosition);

        for (int y = rows - 1; y >= 0; y--)
            AddTile(BorderTimerTileRole.LeftVertical, new Vector2(left - TileSize - leftEdgeGap, top + y * TileSize));

        _timer = new EnemyReleaseBorderTimer(_sprites.Count, GetEffectiveTicksPerTile());
        ApplyTimerVisualState();
        QueueRedraw();
    }

    /// <summary>
    /// Gets the currently active period after applying the arcade-timing setting.
    /// </summary>
    private int GetEffectiveTicksPerTile()
    {
        return UseArcadeLevelTiming
            ? EnemyReleaseBorderTimer.GetTicksPerTileForLevel(LevelNumber)
            : TicksPerTile;
    }

    /// <summary>
    /// Applies the active ticks-per-tile value to the logical timer.
    /// </summary>
    /// <param name="resetTimer">
    /// When true, the border restarts from its initial all-white state. Runtime level
    /// changes and timing-mode edits should reset; plain initialization can preserve state.
    /// </param>
    private void ApplyEffectiveTicksPerTile(bool resetTimer)
    {
        _timer.TicksPerTile = GetEffectiveTicksPerTile();

        if (!resetTimer)
            return;

        _timer.Reset();
        ApplyTimerVisualState();
    }

    /// <summary>
    /// Adds one generated tile sprite to the clockwise sprite list.
    /// </summary>
    private void AddTile(BorderTimerTileRole role, Vector2 position)
    {
        if (_tilesTexture == null)
            return;

        Sprite2D sprite = new()
        {
            Name = $"BorderTimerTile_{_sprites.Count:000}",
            Texture = _tilesTexture,
            Hframes = TileFrameCount,
            Vframes = 1,
            Frame = GetFrame(role),
            Centered = false,
            Position = position,
            FlipH = role == BorderTimerTileRole.RightVertical,
            FlipV = role == BorderTimerTileRole.BottomHorizontal,
            ZIndex = TileZIndex,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };

        AddChild(sprite);
        _sprites.Add(sprite);
    }

    /// <summary>
    /// Applies the current logical timer state to every generated sprite color.
    /// </summary>
    private void ApplyTimerVisualState()
    {
        for (int i = 0; i < _sprites.Count; i++)
        {
            Sprite2D sprite = _sprites[i];
            if (!GodotObject.IsInstanceValid(sprite))
                continue;

            int timerIndex = PositiveModulo(i - _cycleStartSpriteIndex, _sprites.Count);
            sprite.Modulate = _timer.IsTileGreen(timerIndex) ? GreenColor : WhiteColor;
        }
    }

    /// <summary>
    /// Frees all generated sprites and clears the clockwise sprite list.
    /// </summary>
    private void ClearGeneratedTiles()
    {
        foreach (Sprite2D sprite in _sprites)
        {
            if (!GodotObject.IsInstanceValid(sprite))
                continue;

            RemoveChild(sprite);
            sprite.QueueFree();
        }

        _sprites.Clear();
    }

    /// <summary>
    /// Returns a non-negative modulo result, used when remapping sprite indices to timer indices.
    /// </summary>
    private static int PositiveModulo(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static int GetFrame(BorderTimerTileRole role)
    {
        return role switch
        {
            BorderTimerTileRole.TopLeftCorner => 0,
            BorderTimerTileRole.TopRightCorner => 1,
            BorderTimerTileRole.BottomLeftCorner => 2,
            BorderTimerTileRole.BottomRightCorner => 3,
            BorderTimerTileRole.LeftVertical => 4,
            BorderTimerTileRole.RightVertical => 4,
            BorderTimerTileRole.TopHorizontal => 5,
            BorderTimerTileRole.BottomHorizontal => 5,
            _ => 0
        };
    }

    private enum BorderTimerTileRole
    {
        TopLeftCorner,
        TopRightCorner,
        BottomLeftCorner,
        BottomRightCorner,
        TopHorizontal,
        BottomHorizontal,
        LeftVertical,
        RightVertical
    }
}
