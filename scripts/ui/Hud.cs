using Godot;

/// <summary>
/// Displays the gameplay HUD for one active board.
/// </summary>
/// <remarks>
/// This script only updates HUD values. Layout, anchors, font size, colors and
/// alignment are authored in the scene so the UI remains easy to adjust in the
/// Godot editor.
/// </remarks>
public partial class Hud : CanvasLayer
{
    [Export]
    public NodePath ScoreLabelPath { get; set; } = "Root/ScoreLabel";

    private Label? _scoreLabel;
    private int _lastScore;

    public override void _Ready()
    {
        _scoreLabel = FindScoreLabel();

        if (_scoreLabel == null)
        {
            GD.PushWarning("Hud could not find a ScoreLabel node.");
            return;
        }

        SetScore(_lastScore);
    }

    /// <summary>
    /// Updates the displayed current score.
    /// </summary>
    public void SetScore(int score)
    {
        _lastScore = score;

        if (_scoreLabel == null)
            return;

        _scoreLabel.Text = score.ToString();
    }

    private Label? FindScoreLabel()
    {
        if (!ScoreLabelPath.IsEmpty)
        {
            Label? configuredLabel = GetNodeOrNull<Label>(ScoreLabelPath);
            if (configuredLabel != null)
                return configuredLabel;
        }

        Label? rootChildLabel = GetNodeOrNull<Label>("Root/ScoreLabel");
        if (rootChildLabel != null)
            return rootChildLabel;

        return GetNodeOrNull<Label>("ScoreLabel");
    }
}
