using Godot;

/// <summary>
/// Adds screen-flow helpers to Level without touching the existing board,
/// movement, collectible, and enemy implementation.
/// </summary>
public partial class Level
{
    [Signal]
    public delegate void GameOverFinishedEventHandler();

    // Reverse-engineered GAME OVER timing:
    // 0x80 frames = 128 frames, about 2.13 seconds at ~60.1145 Hz.
    private const int GameOverArcadeDurationFrames = 0x80;
    private const double ArcadeRefreshRateHz = 60.1145;
    private const double GameOverReturnDelaySeconds = GameOverArcadeDurationFrames / ArcadeRefreshRateHz;

    private LevelGameOverOverlay? _gameOverOverlay;
    private GameOverOverlayDriver? _gameOverOverlayDriver;

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
        if (Engine.IsEditorHint())
            return;

        EnsureGameOverOverlayDriver();

        if (_isGameOver ||
            _isPlayerDeathSequenceActive ||
            _isEndLevelFreezeActive ||
            _isLevelTransitionScreenActive)
        {
            return;
        }

        _pickupPopupState.Clear();
        ClearPickupPopupView();
        _isNextLevelQueuedAfterPopup = false;

        // This is the pre-level PART screen shown after the title screen.
        // It must not use StartLevelTransitionScreen(...), because that method is
        // now the post-clear flow: sound + two-second frozen board + PART screen.
        // For a fresh game, there is no completed board to freeze, so show the
        // PART overlay immediately before the first playable level starts.
        _queuedNextLevelNumber = _levelNumber < 1 ? 1 : _levelNumber;
        _isEndLevelFreezeActive = false;
        _endLevelFreezeTicksRemaining = 0;
        _simulationAccumulator = 0.0;

        ShowLevelTransitionScreen();
    }

    /// <summary>
    /// Creates the GAME OVER overlay if it does not already exist.
    /// </summary>
    private void EnsureGameOverOverlay()
    {
        if (_gameOverOverlay != null && GodotObject.IsInstanceValid(_gameOverOverlay))
            return;

        _gameOverOverlay = new LevelGameOverOverlay
        {
            Name = "LevelGameOverOverlay"
        };
        AddChild(_gameOverOverlay);
    }

    /// <summary>
    /// Shows the GAME OVER overlay once the existing death/game-over state is reached.
    /// </summary>
    private void ShowGameOverOverlay()
    {
        EnsureGameOverOverlay();
        _gameOverOverlay?.ShowGameOver();
    }

    /// <summary>
    /// Hides the GAME OVER overlay when the level is no longer in game-over state.
    /// </summary>
    private void HideGameOverOverlay()
    {
        _gameOverOverlay?.HideOverlay();
    }

    /// <summary>
    /// Installs a tiny runtime driver that observes the existing game-over flag.
    ///
    /// The base Level.cs already owns the actual life/death logic and sets
    /// _isGameOver when the last life is lost. This driver only turns that
    /// existing state into a visible overlay and requests the return to the title
    /// screen after the measured arcade delay.
    /// </summary>
    private void EnsureGameOverOverlayDriver()
    {
        if (_gameOverOverlayDriver != null && GodotObject.IsInstanceValid(_gameOverOverlayDriver))
            return;

        EnsureGameOverOverlay();

        _gameOverOverlayDriver = new GameOverOverlayDriver
        {
            Name = "GameOverOverlayDriver"
        };
        _gameOverOverlayDriver.Configure(
            () => _isGameOver,
            ShowGameOverOverlay,
            HideGameOverOverlay,
            () => EmitSignal(SignalName.GameOverFinished));

        AddChild(_gameOverOverlayDriver);
    }

    /// <summary>
    /// Small child node used only to bridge the existing private game-over state
    /// to the visible overlay and to request a return to the title screen.
    /// </summary>
    private sealed partial class GameOverOverlayDriver : Node
    {
        private System.Func<bool>? _isGameOver;
        private System.Action? _showGameOver;
        private System.Action? _hideGameOver;
        private System.Action? _finishGameOver;
        private bool _wasGameOver;
        private bool _finishAlreadyRequested;
        private double _elapsedGameOverSeconds;

        public void Configure(
            System.Func<bool> isGameOver,
            System.Action showGameOver,
            System.Action hideGameOver,
            System.Action finishGameOver)
        {
            _isGameOver = isGameOver;
            _showGameOver = showGameOver;
            _hideGameOver = hideGameOver;
            _finishGameOver = finishGameOver;
        }

        public override void _Process(double delta)
        {
            bool isGameOver = _isGameOver?.Invoke() == true;

            if (isGameOver != _wasGameOver)
            {
                _wasGameOver = isGameOver;
                _elapsedGameOverSeconds = 0.0;
                _finishAlreadyRequested = false;

                if (isGameOver)
                    _showGameOver?.Invoke();
                else
                    _hideGameOver?.Invoke();
            }

            if (!isGameOver || _finishAlreadyRequested)
                return;

            _elapsedGameOverSeconds += delta;
            if (_elapsedGameOverSeconds < GameOverReturnDelaySeconds)
                return;

            _finishAlreadyRequested = true;
            _finishGameOver?.Invoke();
        }
    }
}
