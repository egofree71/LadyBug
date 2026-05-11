using Godot;

/// <summary>
/// Arcade-style GAME OVER overlay shown inside the same maze-inner panel used
/// by the PART transition screen.
///
/// The overlay deliberately keeps the HUD, border timer, and purple maze frame
/// visible by drawing only over the black playfield area.
/// </summary>
public partial class LevelGameOverOverlay : CanvasLayer
{
    // Same reference coordinates as LevelTransitionOverlay so both screens use
    // the same black panel inside the purple maze frame.
    private const float ReferenceViewportWidth = 800.0f;
    private const float ReferenceViewportHeight = 880.0f;

    private static readonly Vector2 ReferencePanelPosition = new(51.0f, 72.0f);
    private static readonly Vector2 ReferencePanelSize = new(696.0f, 696.0f);

    private const string ArcadeFontPath = "res://assets/fonts/PressStart2P-Regular.ttf";
    private const int GameOverFontSize = 28;

    private static readonly Color ArcadeBlack = new(0.0f, 0.0f, 0.0f, 1.0f);
    private static readonly Color ArcadeRed = Color.FromHtml("FF5100");

    private Control? _root;
    private ColorRect? _panel;
    private Label? _gameOverLabel;
    private Font? _arcadeFont;

    public override void _Ready()
    {
        Layer = 120;
        LoadArcadeFont();
        EnsureUi();
        HideOverlay();
    }

    /// <summary>
    /// Shows the GAME OVER message centered in the maze-inner panel.
    /// The font size matches the PART title used by LevelTransitionOverlay.
    /// </summary>
    public void ShowGameOver()
    {
        EnsureUi();
        UpdatePanelLayout();
        Visible = true;
    }

    /// <summary>
    /// Hides the game-over panel.
    /// </summary>
    public void HideOverlay()
    {
        Visible = false;
    }

    /// <summary>
    /// Loads the project-wide arcade font used by the game-over text.
    /// </summary>
    private void LoadArcadeFont()
    {
        _arcadeFont = ResourceLoader.Load<Font>(ArcadeFontPath);
        if (_arcadeFont == null)
            GD.PushWarning($"[LevelGameOverOverlay] Missing arcade font: {ArcadeFontPath}");
    }

    /// <summary>
    /// Creates the runtime-only UI tree once.
    /// </summary>
    private void EnsureUi()
    {
        if (_root != null)
            return;

        _root = new Control
        {
            Name = "Root",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.AnchorRight = 1.0f;
        _root.AnchorBottom = 1.0f;
        AddChild(_root);

        _panel = new ColorRect
        {
            Name = "MazeInnerPanel",
            Color = ArcadeBlack,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.AddChild(_panel);

        _gameOverLabel = new Label
        {
            Name = "GameOverLabel",
            Text = "GAME OVER",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _gameOverLabel.AnchorRight = 1.0f;
        _gameOverLabel.AnchorBottom = 1.0f;
        if (_arcadeFont != null)
            _gameOverLabel.AddThemeFontOverride("font", _arcadeFont);

        _gameOverLabel.AddThemeFontSizeOverride("font_size", GameOverFontSize);
        _gameOverLabel.AddThemeColorOverride("font_color", ArcadeRed);
        _panel.AddChild(_gameOverLabel);

        UpdatePanelLayout();
    }

    /// <summary>
    /// Recomputes the panel rectangle from the current viewport size.
    /// </summary>
    private void UpdatePanelLayout()
    {
        if (_panel == null || _gameOverLabel == null)
            return;

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        float sx = Mathf.Max(0.1f, viewportSize.X / ReferenceViewportWidth);
        float sy = Mathf.Max(0.1f, viewportSize.Y / ReferenceViewportHeight);

        _panel.Position = new Vector2(ReferencePanelPosition.X * sx, ReferencePanelPosition.Y * sy);
        _panel.Size = new Vector2(ReferencePanelSize.X * sx, ReferencePanelSize.Y * sy);

        _gameOverLabel.Position = Vector2.Zero;
        _gameOverLabel.Size = _panel.Size;
    }
}
