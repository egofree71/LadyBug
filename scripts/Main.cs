using Godot;
using LadyBug.DebugTools;
using LadyBug.UI;

/// <summary>
/// Application entry point.
///
/// Main owns only the coarse screen flow:
/// title screen first, then the playable level when the title screen requests it.
/// Gameplay-only shortcuts are attached to the Level instance instead of being handled here.
/// </summary>
public partial class Main : Node
{
    [Export]
    public bool Debug { get; set; } = true;

    private const string TitleScreenScenePath = "res://scenes/ui/TitleScreen.tscn";
    private const string LevelScenePath = "res://scenes/level/Level.tscn";

    // Keeps the current Level.tscn placement used by the previous Main.tscn.
    private static readonly Vector2 LevelScenePosition = new(27, -1);

    private Node? _currentScreen;
    private Node? _levelNode;

    /// <summary>
    /// Starts on the title screen instead of instantiating the gameplay level directly.
    /// </summary>
    public override void _Ready()
    {
        GD.Print("LadyBug project started.");
        ShowTitleScreen();
    }

    /// <summary>
    /// Instantiates the title screen and subscribes to its start signal.
    /// </summary>
    private void ShowTitleScreen()
    {
        ClearCurrentScreen();

        PackedScene? titleScene = ResourceLoader.Load<PackedScene>(TitleScreenScenePath);
        if (titleScene == null)
        {
            GD.PushError($"Could not load title screen scene: {TitleScreenScenePath}");
            StartGame();
            return;
        }

        TitleScreen titleScreen = titleScene.Instantiate<TitleScreen>();
        titleScreen.Name = "TitleScreen";
        titleScreen.Connect(TitleScreen.SignalName.StartRequested, Callable.From(StartGame));

        _currentScreen = titleScreen;
        AddChild(titleScreen);
    }

    /// <summary>
    /// Replaces the title screen with a fresh Level scene.
    /// </summary>
    private void StartGame()
    {
        if (_levelNode != null)
            return;

        ClearCurrentScreen();

        PackedScene? levelScene = ResourceLoader.Load<PackedScene>(LevelScenePath);
        if (levelScene == null)
        {
            GD.PushError($"Could not load level scene: {LevelScenePath}");
            return;
        }

        _levelNode = levelScene.Instantiate();
        _levelNode.Name = "Level";

        if (_levelNode is Node2D levelNode2D)
            levelNode2D.Position = LevelScenePosition;

        AttachGameplayDebugShortcuts(_levelNode);

        if (_levelNode is Level level)
            level.Connect(Level.SignalName.GameOverFinished, Callable.From(OnGameOverFinished));

        _currentScreen = _levelNode;
        AddChild(_levelNode);

        // The title screen starts a new game, but the arcade-style PART panel
        // should still be shown before the first playable board begins.
        // Deferred execution lets Level finish its own _Ready initialization first.
        if (_levelNode is Level initializedLevel)
            initializedLevel.CallDeferred(nameof(Level.StartInitialLevelTransition));
    }

    /// <summary>
    /// Returns to the title screen after the Level has displayed GAME OVER long enough.
    /// </summary>
    private void OnGameOverFinished()
    {
        ShowTitleScreen();
    }

    /// <summary>
    /// Adds gameplay-only debug shortcuts under the Level instance.
    ///
    /// This keeps Main free from F1 / F12 handling while preserving the useful
    /// screenshot shortcut once a playable level is active.
    /// </summary>
    private void AttachGameplayDebugShortcuts(Node levelNode)
    {
        if (levelNode.GetNodeOrNull<LevelDebugShortcuts>("LevelDebugShortcuts") != null)
            return;

        LevelDebugShortcuts shortcuts = new()
        {
            Name = "LevelDebugShortcuts",
            Debug = Debug
        };

        levelNode.AddChild(shortcuts);
    }

    /// <summary>
    /// Removes the current top-level screen before another one is displayed.
    /// </summary>
    private void ClearCurrentScreen()
    {
        if (_currentScreen == null)
        {
            _levelNode = null;
            return;
        }

        if (_currentScreen == _levelNode)
            _levelNode = null;

        _currentScreen.QueueFree();
        _currentScreen = null;
    }
}
