using Godot;
using LadyBug.Actors;
using LadyBug.Gameplay;
using LadyBug.Gameplay.Collectibles;
using LadyBug.Gameplay.Gates;
using LadyBug.Gameplay.Maze;
using LadyBug.Gameplay.Player;
using LadyBug.Gameplay.Scoring;

/// <summary>
/// Represents one playable level of the game.
/// </summary>
/// <remarks>
/// <para>
/// The level coordinates the board-specific runtime systems: logical maze loading,
/// rotating-gate state, collectible state, playfield collision evaluation, player
/// initialization, and the minimal gameplay HUD.
/// </para>
/// <para>
/// Specialized work is delegated to smaller runtime classes. Coordinate conversion is
/// handled by <see cref="LevelCoordinateSystem"/>, rotating gates by
/// <see cref="LevelGateRuntime"/>, collectibles by <see cref="CollectibleFieldRuntime"/>,
/// and movement blocking by <see cref="PlayfieldCollisionResolver"/>.
/// </para>
/// <para>
/// Gameplay actors use arcade-pixel positions and logical cells. Visual placement is
/// converted at the level boundary so gameplay rules stay independent from scene-space
/// rendering details.
/// </para>
/// </remarks>
[Tool]
public partial class Level : Node2D
{
    [Export(PropertyHint.Range, "1,999,1")]
    private int _levelNumber = 1;

    // Godot RNG used by the start-of-level collectible spawn planner.
    private readonly RandomNumberGenerator _rng = new();

    // Score state currently owned by the level prototype.
    // Later this can move to a broader game/session state if score must persist across scenes.
    private readonly ScoreState _scoreState = new();

    // Global color cycle used by hearts and letters.
    private readonly CollectibleColorCycle _collectibleColorCycle = new();

    // Multiplier unlocked by collecting blue hearts.
    private readonly HeartMultiplierState _heartMultiplierState = new();

    // Short heart / letter pickup popup state. While active, normal gameplay is frozen.
    private readonly CollectiblePickupPopupState _pickupPopupState = new();

    // Current prototype life state. Later this can move to a broader game/session state.
    private readonly PlayerLifeState _lifeState = new();

    // Short freeze state used after the player touches a skull.
    private readonly PlayerDeathState _deathState = new();

    // Temporary high-level death duration. The exact arcade death animation can replace this later.
    private const int SkullDeathFreezeTicks = 60;

    // Minimal game-over guard until proper screen flow exists.
    private bool _isGameOver;

    // Data files loaded when the runtime level starts.
    private const string MazeJsonPath = "res://data/maze.json";
    private const string CollectiblesJsonPath = "res://data/collectibles_layout.json";

    // --- Coordinate System --------------------------------------------------

    private const int CellSizeArcade = 16;
    private const int RenderScale = 4;
    private static readonly Vector2I GameplayAnchorArcade = new(8, 7);

    // Central conversion helper between logical cells, arcade pixels, and Godot scene space.
    private readonly LevelCoordinateSystem _coordinateSystem =
        new(CellSizeArcade, RenderScale, GameplayAnchorArcade);

    // --- Scene References ---------------------------------------------------

    // Maze background sprite; its scene position is used as the playfield origin.
    private Sprite2D? _mazeSprite;

    // Parent node containing the editor-authored rotating gate views.
    private Node2D? _gatesRoot;

    // Parent node used by CollectibleFieldRuntime to spawn collectible views.
    private Node2D? _collectiblesRoot;

    // Runtime player controller owned by this level.
    private PlayerController? _player;

    // Optional HUD node. It currently displays score and lives.
    private Hud? _hud;

    // Runtime popup view displayed when collecting hearts and letters.
    private CollectiblePickupPopupView? _pickupPopupView;

    // --- Runtime State ------------------------------------------------------

    // Static logical maze loaded from maze.json.
    private MazeGrid _mazeGrid = null!;

    // Runtime owner for rotating gate state and gate view synchronization.
    private LevelGateRuntime _gateRuntime = null!;

    // Runtime lookup and view owner for flowers, hearts, letters, and skulls.
    private CollectibleFieldRuntime _collectibleField = null!;

    // Movement collision facade combining the static maze and dynamic rotating gates.
    private PlayfieldCollisionResolver _playfieldCollisionResolver = null!;

    // Accumulates frame time for the level-owned fixed simulation tick.
    private double _simulationAccumulator = 0.0;

    // --- Exported Properties ------------------------------------------------

    // Backing field for the exported player start cell.
    private Vector2I _playerStartCell = Vector2I.Zero;

    /// <summary>
    /// Gets or sets the logical start cell used to place the player.
    /// </summary>
    /// <remarks>
    /// In the editor, changing this value immediately updates the previewed player
    /// instance position.
    /// </remarks>
    [Export]
    public Vector2I PlayerStartCell
    {
        get => _playerStartCell;
        set
        {
            if (_playerStartCell == value)
                return;

            _playerStartCell = value;
            UpdatePlayerPositionFromLogicalCell();
        }
    }

    /// <summary>
    /// Gets the runtime logical maze used by gameplay actors.
    /// </summary>
    public MazeGrid MazeGrid => _mazeGrid;

    /// <summary>
    /// Gets the runtime rotating-gate system used by the active level.
    /// </summary>
    public GateSystem GateSystem => _gateRuntime.GateSystem;

    // --- Lifecycle ----------------------------------------------------------

    /// <summary>
    /// Initializes editor previews or builds the full runtime board.
    /// </summary>
    public override void _Ready()
    {
        _mazeSprite = GetNodeOrNull<Sprite2D>("Maze");
        _gatesRoot = GetNodeOrNull<Node2D>("Gates");
        _collectiblesRoot = GetNodeOrNull<Node2D>("Collectibles");

        if (_gatesRoot == null)
        {
            GD.PushError("Level is missing required child node: Gates");
            return;
        }

        _gateRuntime = new LevelGateRuntime(_gatesRoot);
        _gateRuntime.RefreshPlacedGateViewsFromDefinitions();

        if (Engine.IsEditorHint())
        {
            UpdatePlayerPositionFromLogicalCell();
            return;
        }

        if (_collectiblesRoot == null)
        {
            GD.PushError("Level is missing required child node: Collectibles");
            return;
        }

        InitializeRuntimeSystems();
        InitializeHud();
        InitializePlayer();
    }

    /// <summary>
    /// Owns the fixed gameplay tick for board-level systems and the player.
    /// </summary>
    /// <param name="delta">Frame delta time in seconds.</param>
    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() || _player == null)
            return;

        _simulationAccumulator += delta;

        bool ranSimulationTick = false;
        while (_simulationAccumulator >= PlayerMovementTuning.TickDuration)
        {
            _simulationAccumulator -= PlayerMovementTuning.TickDuration;
            RunOneSimulationTick();
            ranSimulationTick = true;
        }

        if (ranSimulationTick)
            _player.SynchronizeSceneFromGameplay();
    }

    /// <summary>
    /// Builds all runtime-only board systems after the scene nodes have been resolved.
    /// </summary>
    /// <remarks>
    /// This method keeps the startup order explicit: load the static maze first,
    /// build gate collision state, create collectible runtime state, then apply the
    /// start-of-level special collectible plan and initial color cycle.
    /// </remarks>
    private void InitializeRuntimeSystems()
    {
        _mazeGrid = MazeLoader.LoadFromJsonFile(MazeJsonPath);

        _gateRuntime.BuildRuntimeGateSystem();
        _gateRuntime.SyncGateViewsFromRuntimeState();

        _playfieldCollisionResolver = new PlayfieldCollisionResolver(
            _mazeGrid,
            _gateRuntime.GateSystem,
            ArcadePixelToLogicalCell,
            GatePivotToArcadePixel);

        _collectibleField = new CollectibleFieldRuntime(
            _collectiblesRoot!,
            LogicalCellToScenePosition);

        CollectibleLayoutFile collectibleLayout =
            CollectibleLoader.LoadFromJsonFile(CollectiblesJsonPath);

        _collectibleField.SpawnInitialFlowers(collectibleLayout);

        _rng.Randomize();
        CollectibleSpawnPlan spawnPlan =
            CollectibleSpawnPlanner.Generate(_levelNumber, _rng);

        _collectibleField.ApplySpecialCollectibleSpawnPlan(spawnPlan);

        _collectibleColorCycle.ResetToBlue();
        _collectibleField.ApplyColorCycle(_collectibleColorCycle.CurrentColor);
    }

    /// <summary>
    /// Initializes prototype session state that is currently displayed through the HUD.
    /// </summary>
    /// <remarks>
    /// Score, multiplier, lives, death state, and game-over state are still owned by
    /// <see cref="Level"/>. They can later move to a persistent game-session object
    /// when title screen, level transitions, and game-over flow are implemented.
    /// </remarks>
    private void InitializeHud()
    {
        _hud = GetNodeOrNull<Hud>("Hud");

        if (_hud == null)
            GD.PushWarning("Level could not find optional child node: Hud");

        _scoreState.Reset();
        _heartMultiplierState.Reset();
        _lifeState.Reset();
        _deathState.Reset();
        _isGameOver = false;
        _hud?.SetScore(_scoreState.Score);
        _hud?.SetLives(_lifeState.Lives);
    }

    /// <summary>
    /// Finds the player scene instance and initializes its movement state from this level.
    /// </summary>
    /// <remarks>
    /// The player must be initialized after the maze, gates, collectibles, HUD, and
    /// collision resolver exist, because the movement motor queries the level for
    /// maze geometry and playfield collision checks.
    /// </remarks>
    private void InitializePlayer()
    {
        _player = GetNodeOrNull<PlayerController>("Player");
        _player?.Initialize(this);
        _simulationAccumulator = 0.0;
    }

    // --- Simulation Tick ----------------------------------------------------

    /// <summary>
    /// Advances one full level simulation tick.
    /// </summary>
    /// <remarks>
    /// Board-level systems are advanced before the player, matching the previous
    /// order used by PlayerController while keeping global systems out of the
    /// player-specific class.
    /// </remarks>
    private void RunOneSimulationTick()
    {
        if (_isGameOver)
            return;

        if (_deathState.IsActive)
        {
            AdvancePlayerDeathOneTick();
            return;
        }

        if (_pickupPopupState.IsActive)
        {
            AdvancePickupPopupOneTick();
            return;
        }

        AdvanceBoardSimulationOneTick();
        _player?.AdvanceOneSimulationTick();
    }

    /// <summary>
    /// Advances board-level systems that are not owned by a specific actor.
    /// </summary>
    private void AdvanceBoardSimulationOneTick()
    {
        _gateRuntime.AdvanceOneTick();

        if (_collectibleColorCycle.AdvanceOneTick())
            _collectibleField.ApplyColorCycle(_collectibleColorCycle.CurrentColor);
    }

    // --- Collectibles -------------------------------------------------------

    /// <summary>
    /// Tries to consume the collectible currently present at the given logical cell.
    /// </summary>
    /// <param name="cell">Logical cell to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> if one collectible was found and consumed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryConsumeCollectible(Vector2I cell)
    {
        if (_pickupPopupState.IsActive ||
            _deathState.IsActive ||
            _isGameOver)
            return false;

        CollectiblePickupResult pickupResult = _collectibleField.TryConsume(cell);

        if (!pickupResult.Consumed)
            return false;

        ApplyCollectiblePickupResult(cell, pickupResult);
        return true;
    }

    /// <summary>
    /// Applies the gameplay consequences of one consumed collectible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CollectibleFieldRuntime"/> has already removed the collectible
    /// from the board before this method runs. This method is therefore responsible
    /// only for semantic consequences: score changes, multiplier changes, popup
    /// startup, or skull death.
    /// </para>
    /// <para>
    /// Skull handling is intentionally routed before score calculation because skulls
    /// are lethal objects with no score value and no pickup popup.
    /// </para>
    /// </remarks>
    /// <param name="cell">Logical cell where the collectible was consumed.</param>
    /// <param name="pickupResult">Semantic result returned by the collectible runtime.</param>
    private void ApplyCollectiblePickupResult(
        Vector2I cell,
        CollectiblePickupResult pickupResult)
    {
        if (pickupResult.Kind == CollectibleKind.Skull)
        {
            HandlePlayerDeathFromSkull();
            return;
        }

        CollectibleScoreCalculation scoreCalculation =
            CollectibleScoreService.Calculate(
                pickupResult.Kind,
                pickupResult.Color,
                _heartMultiplierState.CurrentMultiplier);

        if (scoreCalculation.HasScore)
        {
            _scoreState.AddPoints(scoreCalculation.ScoreDelta);
            _hud?.SetScore(_scoreState.Score);
        }

        bool needsPopup =
            pickupResult.Kind == CollectibleKind.Heart ||
            pickupResult.Kind == CollectibleKind.Letter;

        if (pickupResult.Kind == CollectibleKind.Heart &&
            pickupResult.Color == CollectibleColor.Blue)
        {
            _heartMultiplierState.AdvanceOneStep();
        }

        if (needsPopup && scoreCalculation.HasScore)
        {
            StartPickupPopup(cell, scoreCalculation);
        }
    }

    /// <summary>
    /// Starts the short score / multiplier popup shown after collecting a heart or letter.
    /// </summary>
    /// <remarks>
    /// While this state is active, normal gameplay ticks are frozen and only the popup
    /// timer advances. The player sprite is hidden to match the current high-level
    /// interpretation of the arcade pickup pause.
    /// </remarks>
    /// <param name="cell">Logical cell where the popup should be displayed.</param>
    /// <param name="scoreCalculation">Score information to display and track during the popup.</param>
    private void StartPickupPopup(
        Vector2I cell,
        CollectibleScoreCalculation scoreCalculation)
    {
        ClearPickupPopupView();

        _pickupPopupState.Start(
            cell,
            scoreCalculation.BaseScore,
            scoreCalculation.Multiplier,
            scoreCalculation.ScoreDelta);

        _pickupPopupView = new CollectiblePickupPopupView();
        AddChild(_pickupPopupView);
        _pickupPopupView.Position = LogicalCellToScenePosition(cell);
        _pickupPopupView.Configure(
            scoreCalculation.BaseScore,
            scoreCalculation.Multiplier);

        _player?.SetGameplaySpriteVisible(false);
    }

    /// <summary>
    /// Advances the active pickup popup by one fixed simulation tick.
    /// </summary>
    /// <remarks>
    /// When the popup completes, its temporary view is removed and the player sprite
    /// becomes visible again so normal gameplay can resume on the next tick.
    /// </remarks>
    private void AdvancePickupPopupOneTick()
    {
        if (!_pickupPopupState.AdvanceOneTick())
            return;

        ClearPickupPopupView();
        _player?.SetGameplaySpriteVisible(true);
    }

    /// <summary>
    /// Removes the temporary popup view if it currently exists.
    /// </summary>
    /// <remarks>
    /// The instance-valid check protects against cases where Godot has already queued
    /// or freed the node during scene teardown.
    /// </remarks>
    private void ClearPickupPopupView()
    {
        if (_pickupPopupView != null && GodotObject.IsInstanceValid(_pickupPopupView))
            _pickupPopupView.QueueFree();

        _pickupPopupView = null;
    }

    // --- Player Life / Death ----------------------------------------------

    /// <summary>
    /// Starts the simplified player-death sequence after touching a skull.
    /// </summary>
    /// <remarks>
    /// The current implementation deliberately keeps the arcade behavior high-level:
    /// the skull is removed, no score is awarded, one life is lost, gameplay freezes
    /// briefly, and the player either respawns or reaches the placeholder game-over
    /// state. The exact shrinking / halo animation can be added later without changing
    /// the life-state logic.
    /// </remarks>
    private void HandlePlayerDeathFromSkull()
    {
        // A skull has already been removed by CollectibleFieldRuntime.TryConsume.
        // It gives no score and immediately starts the player-death path.
        _pickupPopupState.Clear();
        ClearPickupPopupView();

        _lifeState.LoseLife();
        _hud?.SetLives(_lifeState.Lives);

        _player?.SetGameplaySpriteVisible(false);
        _deathState.Start(SkullDeathFreezeTicks);
    }

    /// <summary>
    /// Advances the temporary death freeze and performs respawn or game-over handling.
    /// </summary>
    /// <remarks>
    /// The simulation accumulator is cleared when the death pause ends so the player
    /// does not immediately process a backlog of fixed ticks after respawning.
    /// </remarks>
    private void AdvancePlayerDeathOneTick()
    {
        if (!_deathState.AdvanceOneTick())
            return;

        // Avoid consuming accumulated ticks immediately after respawn or game over.
        _simulationAccumulator = 0.0;

        if (_lifeState.IsGameOver)
        {
            _isGameOver = true;
            _player?.SetGameplaySpriteVisible(false);
            GD.Print("Game Over placeholder: proper game-over flow is not implemented yet.");
            return;
        }

        _player?.RespawnAtStartCell();
    }

    // --- Rotating Gates -----------------------------------------------------

    /// <summary>
    /// Attempts to push one rotating gate.
    /// </summary>
    /// <param name="gateId">Identifier of the gate to push.</param>
    /// <param name="moveDir">Attempted one-pixel gameplay movement direction.</param>
    /// <param name="contactHalf">Half of the gate being pushed.</param>
    /// <returns><see langword="true"/> if the push is accepted; otherwise, <see langword="false"/>.</returns>
    public bool TryPushGate(int gateId, Vector2I moveDir, GateContactHalf contactHalf)
    {
        return _gateRuntime.TryPushGate(gateId, moveDir, contactHalf);
    }

    // --- Playfield Step Evaluation -----------------------------------------

    /// <summary>
    /// Evaluates one attempted arcade-pixel step against the active playfield:
    /// static maze plus dynamic rotating gates.
    /// </summary>
    public PlayfieldStepResult EvaluateArcadePixelStepWithGates(
        Vector2I arcadePixelPos,
        Vector2I direction,
        Vector2I collisionLead)
    {
        return _playfieldCollisionResolver.EvaluateArcadePixelStep(
            arcadePixelPos,
            direction,
            collisionLead);
    }

    // --- Coordinate Conversion ---------------------------------------------

    /// <summary>
    /// Converts one logical maze cell into the corresponding arcade-pixel anchor.
    /// </summary>
    public Vector2I LogicalCellToArcadePixel(Vector2I cell)
    {
        return _coordinateSystem.LogicalCellToArcadePixel(cell);
    }

    /// <summary>
    /// Converts one arcade-pixel position back into a logical maze cell.
    /// </summary>
    public Vector2I ArcadePixelToLogicalCell(Vector2I arcadePixel)
    {
        return _coordinateSystem.ArcadePixelToLogicalCell(arcadePixel);
    }

    /// <summary>
    /// Converts one logical gate pivot into an arcade-pixel pivot position.
    /// </summary>
    public Vector2I GatePivotToArcadePixel(Vector2I pivot)
    {
        return _coordinateSystem.GatePivotToArcadePixel(pivot);
    }

    /// <summary>
    /// Converts one arcade-pixel position into scene coordinates.
    /// </summary>
    public Vector2 ArcadePixelToScenePosition(Vector2I arcadePixel)
    {
        return _coordinateSystem.ArcadePixelToScenePosition(
            arcadePixel,
            GetMazeSceneOrigin());
    }

    /// <summary>
    /// Converts an arcade-pixel delta into a scene-space delta.
    /// </summary>
    public Vector2 ArcadeDeltaToSceneDelta(Vector2I arcadeDelta)
    {
        return _coordinateSystem.ArcadeDeltaToSceneDelta(arcadeDelta);
    }

    /// <summary>
    /// Converts one logical maze cell directly into a scene position.
    /// </summary>
    public Vector2 LogicalCellToScenePosition(Vector2I cell)
    {
        return _coordinateSystem.LogicalCellToScenePosition(
            cell,
            GetMazeSceneOrigin());
    }

    /// <summary>
    /// Converts one logical gate pivot directly into a scene position.
    /// </summary>
    public Vector2 GatePivotToScenePosition(Vector2I pivot)
    {
        return _coordinateSystem.GatePivotToScenePosition(
            pivot,
            GetMazeSceneOrigin());
    }

    /// <summary>
    /// Gets the scene-space origin used by all playfield coordinate conversions.
    /// </summary>
    /// <remarks>
    /// The maze sprite position is treated as the visual origin of the arcade
    /// playfield. Returning <see cref="Vector2.Zero"/> keeps editor previews and
    /// runtime code safe if the node is temporarily unavailable.
    /// </remarks>
    private Vector2 GetMazeSceneOrigin()
    {
        _mazeSprite ??= GetNodeOrNull<Sprite2D>("Maze");
        return _mazeSprite?.Position ?? Vector2.Zero;
    }

    // --- Editor Preview -----------------------------------------------------

    /// <summary>
    /// Updates the editor player preview from the configured logical start cell.
    /// </summary>
    private void UpdatePlayerPositionFromLogicalCell()
    {
        Node2D? player = GetNodeOrNull<Node2D>("Player");
        if (player == null)
            return;

        player.Position = LogicalCellToScenePosition(_playerStartCell);

        if (!Engine.IsEditorHint())
            return;

        AnimatedSprite2D? animatedSprite =
            player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        if (animatedSprite != null)
        {
            Vector2 spriteOffset = ArcadeDeltaToSceneDelta(new Vector2I(5, 8));
            animatedSprite.Position = spriteOffset;
        }

        player.NotifyPropertyListChanged();
        QueueRedraw();
    }
}
