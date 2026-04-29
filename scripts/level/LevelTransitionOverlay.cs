using System;
using Godot;

/// <summary>
/// Simple full-screen overlay shown between levels.
/// </summary>
/// <remarks>
/// The original arcade game shows a richer intermission-style panel. For now this
/// overlay intentionally keeps only the upcoming part number, for example PART 2,
/// PART 3, and so on.
/// </remarks>
public partial class LevelTransitionOverlay : CanvasLayer
{
    // Full-screen black background that hides the board while the next part is announced.
    private ColorRect? _background;

    // Layout helper used to keep the PART label centered independently of the viewport size.
    private CenterContainer? _centerContainer;

    // Main text label displaying the upcoming level number, for example "PART 2".
    private Label? _partLabel;

    /// <summary>
    /// Builds the runtime-only UI tree.
    /// </summary>
    public override void _Ready()
    {
        Layer = 100;
        EnsureUi();
        HideOverlay();
    }

    /// <summary>
    /// Shows the overlay for the upcoming playable level.
    /// </summary>
    /// <param name="upcomingLevelNumber">Visible level number that will start next.</param>
    public void ShowForUpcomingLevel(int upcomingLevelNumber)
    {
        EnsureUi();

        int partNumber = Math.Max(1, upcomingLevelNumber);
        _partLabel!.Text = $"PART {partNumber}";
        Visible = true;
    }

    /// <summary>
    /// Hides the overlay.
    /// </summary>
    public void HideOverlay()
    {
        Visible = false;
    }

    /// <summary>
    /// Creates the child controls once.
    /// </summary>
    /// <remarks>
    /// The overlay is built from code so the current Level.tscn does not need a new
    /// editor-authored UI branch. This keeps the transition screen easy to remove,
    /// replace, or expand later when the arcade intermission is reproduced more closely.
    /// </remarks>
    private void EnsureUi()
    {
        if (_background != null)
            return;

        _background = new ColorRect
        {
            Name = "Background",
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _background.AnchorRight = 1.0f;
        _background.AnchorBottom = 1.0f;
        AddChild(_background);

        _centerContainer = new CenterContainer
        {
            Name = "CenterContainer",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _centerContainer.AnchorRight = 1.0f;
        _centerContainer.AnchorBottom = 1.0f;
        AddChild(_centerContainer);

        _partLabel = new Label
        {
            Name = "PartLabel",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off
        };

        _partLabel.AddThemeFontSizeOverride("font_size", 52);
        _partLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.62f, 0.12f, 1.0f));
        _centerContainer.AddChild(_partLabel);
    }
}
