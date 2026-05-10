using Godot;

/// <summary>
/// Adds the first-level PART transition entry point to Level without touching the
/// existing between-level transition implementation.
/// </summary>
public partial class Level
{
    /// <summary>
    /// Shows the normal PART transition for the current level before gameplay starts.
    ///
    /// The playable board is already initialized by Level._Ready(), but this call
    /// freezes simulation through the existing transition state, hides the player and
    /// enemy views, displays the same preview panel used between later levels, and
    /// then rebuilds the board when the transition completes. That final rebuild lets
    /// the first level consume the spawn plan previewed by the PART screen.
    /// </summary>
    public void StartInitialLevelTransition()
    {
        if (Engine.IsEditorHint() || _isGameOver || _isPlayerDeathSequenceActive || _isLevelTransitionScreenActive)
            return;

        _pickupPopupState.Clear();
        ClearPickupPopupView();
        _isNextLevelQueuedAfterPopup = false;
        StartLevelTransitionScreen(_levelNumber);
    }
}
