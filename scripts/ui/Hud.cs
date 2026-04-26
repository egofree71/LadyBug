using Godot;

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
            GD.PushWarning("[Hud] Could not find ScoreLabel. Expected Root/ScoreLabel or ScoreLabel, or set ScoreLabelPath in the Inspector.");
            return;
        }

        // Important: this script intentionally does not set position, size,
        // anchors, alignment or font size. Those belong in Level.tscn.
        SetScore(_lastScore);
    }

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
            Label? exportedPathLabel = GetNodeOrNull<Label>(ScoreLabelPath);
            if (exportedPathLabel != null)
                return exportedPathLabel;
        }

        Label? rootChildLabel = GetNodeOrNull<Label>("Root/ScoreLabel");
        if (rootChildLabel != null)
            return rootChildLabel;

        return GetNodeOrNull<Label>("ScoreLabel");
    }
}
