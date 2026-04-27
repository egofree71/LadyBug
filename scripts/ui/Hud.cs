using Godot;

public partial class Hud : CanvasLayer
{
    [Export]
    public NodePath ScoreLabelPath { get; set; } = "Root/ScoreLabel";

    [Export]
    public NodePath LivesLabelPath { get; set; } = "Root/LivesLabel";

    private Label? _scoreLabel;
    private Label? _livesLabel;
    private int _lastScore;
    private int _lastLives = 3;

    public override void _Ready()
    {
        _scoreLabel = FindScoreLabel();
        _livesLabel = FindLivesLabel();

        if (_scoreLabel == null)
        {
            GD.PushWarning("[Hud] Could not find ScoreLabel. Expected Root/ScoreLabel or ScoreLabel, or set ScoreLabelPath in the Inspector.");
            return;
        }

        // Important: this script intentionally does not set position, size,
        // anchors, alignment or font size. Those belong in Level.tscn.
        SetScore(_lastScore);
        SetLives(_lastLives);
    }

    public void SetScore(int score)
    {
        _lastScore = score;

        if (_scoreLabel == null)
            return;

        _scoreLabel.Text = score.ToString();
    }

    public void SetLives(int lives)
    {
        _lastLives = lives;

        if (_livesLabel == null)
            return;

        _livesLabel.Text = $"LIVES {lives}";
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
}
