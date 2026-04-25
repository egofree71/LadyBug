namespace LadyBug.Gameplay.Scoring;

/// <summary>
/// Stores the current score for one active play session.
/// </summary>
public sealed class ScoreState
{
    /// <summary>
    /// Gets the current player score.
    /// </summary>
    public int Score { get; private set; }

    /// <summary>
    /// Resets the score to zero.
    /// </summary>
    public void Reset()
    {
        Score = 0;
    }

    /// <summary>
    /// Adds a positive number of points to the current score.
    /// </summary>
    public void AddPoints(int points)
    {
        if (points <= 0)
            return;

        Score += points;
    }
}
