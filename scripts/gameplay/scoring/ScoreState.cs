namespace LadyBug.Gameplay.Scoring;

/// <summary>
/// Runtime score state for one active game/level session.
///
/// Stage 1 deliberately keeps this small: it only stores the current score
/// and exposes controlled methods to reset and add points.
/// </summary>
public sealed class ScoreState
{
    public int Score { get; private set; }

    public void Reset()
    {
        Score = 0;
    }

    public void AddPoints(int points)
    {
        if (points <= 0)
            return;

        Score += points;
    }
}
