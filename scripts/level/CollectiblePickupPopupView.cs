using Godot;

/// <summary>
/// Temporary scene-space view shown when collecting a heart or a letter.
/// </summary>
/// <remarks>
/// The arcade uses temporary sprite objects for this popup. The remake keeps the
/// behavior high-level: two labels are displayed near the collected object while
/// the level simulation is frozen.
/// </remarks>
public sealed partial class CollectiblePickupPopupView : Node2D
{
    private const int ScoreFontSize = 22;
    private const int MultiplierFontSize = 22;

    // Each label uses a fixed box so the popup can be tuned like a small sprite.
    // The score is centered in the upper half. The multiplier is right-aligned
    // in the lower half, matching the observed arcade layout more closely.
    private static readonly Vector2 PopupLineSize = new(48.0f, 26.0f);
    private static readonly Vector2 ScoreLabelPosition = new(-4.0f, 4.0f);
    private static readonly Vector2 MultiplierLabelPosition = new(-8.0f, 26.0f);

    private readonly Label _scoreLabel = CreateLabel(ScoreFontSize, HorizontalAlignment.Center);
    private readonly Label _multiplierLabel = CreateLabel(MultiplierFontSize, HorizontalAlignment.Right);

    public override void _Ready()
    {
        ZIndex = 100;

        _scoreLabel.Position = ScoreLabelPosition;
        _scoreLabel.Size = PopupLineSize;
        AddChild(_scoreLabel);

        _multiplierLabel.Position = MultiplierLabelPosition;
        _multiplierLabel.Size = PopupLineSize;
        AddChild(_multiplierLabel);
    }

    /// <summary>
    /// Updates the popup text for the collected heart or letter.
    /// </summary>
    /// <param name="baseScore">Unmultiplied score shown on the upper line.</param>
    /// <param name="multiplier">Current score multiplier shown on the lower line when greater than x1.</param>
    public void Configure(int baseScore, int multiplier)
    {
        _scoreLabel.Text = baseScore.ToString();

        if (multiplier > 1)
        {
            _multiplierLabel.Text = $"x{multiplier}";
            _multiplierLabel.Visible = true;
        }
        else
        {
            _multiplierLabel.Text = string.Empty;
            _multiplierLabel.Visible = false;
        }
    }

    private static Label CreateLabel(int fontSize, HorizontalAlignment horizontalAlignment)
    {
        Label label = new()
        {
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);

        return label;
    }
}
