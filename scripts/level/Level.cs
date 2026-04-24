using Godot;
using System.Collections.Generic;
using LadyBug.Actors;
using LadyBug.Gameplay;
using LadyBug.Gameplay.Collectibles;
using LadyBug.Gameplay.Gates;
using LadyBug.Gameplay.Maze;

/// <summary>
/// Represents one playable level of the game.
/// </summary>
/// <remarks>
/// This class is responsible for:
/// loading the logical maze,
/// owning the runtime rotating-gate system,
/// exposing playfield step evaluation,
/// spawning the base flower layout,
/// and converting between logical cells, arcade-pixel gameplay coordinates,
/// gate pivots, and Godot scene coordinates.
/// 
/// The level intentionally separates:
/// - logical maze data used for gameplay
/// - dynamic rotating-gate state used as a movement overlay
/// - base collectible layout used to spawn flowers
/// - visual rendering provided by the maze background and placed gate views
/// 
/// The player controller uses gameplay anchors in arcade pixels,
/// while sprite-specific visual offsets are handled separately by the actor.
/// </remarks>
[Tool]
public partial class Level : Node2D
{
    [Export(PropertyHint.Range, "1,999,1")]
    private int _levelNumber = 1;

    private readonly RandomNumberGenerator _rng = new();
    
    private const string MazeJsonPath = "res://data/maze.json";
    private const string CollectiblesJsonPath = "res://data/collectibles_layout.json";

    // --- Constants ----------------------------------------------------------

    private const int CellSizeArcade = 16;
    private const int RenderScale = 4;
    private static readonly Vector2I GameplayAnchorArcade = new(8, 7);

    private static readonly PackedScene CollectibleScene =
        GD.Load<PackedScene>("res://scenes/level/Collectible.tscn");

    // --- Scene References ---------------------------------------------------

    private Node2D _gatesNode = null!;
    private Node2D _collectiblesRoot = null!;

    private readonly Dictionary<int, RotatingGateView> _gateViewsById = new();

    // Lookup of runtime collectible views by logical cell.
    private readonly Dictionary<Vector2I, Collectible> _collectiblesByCell = new();

    // --- Exported Properties ------------------------------------------------

    private Vector2I _playerStartCell = Vector2I.Zero;

    /// <summary>
    /// Gets or sets the logical start cell used to place the player.
    /// </summary>
    /// <remarks>
    /// In the editor, changing this property immediately updates the previewed
    /// player instance position.
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

    // --- Runtime State ------------------------------------------------------

    private MazeGrid _mazeGrid = null!;
    private GateSystem _gateSystem = null!;
    private PlayfieldCollisionResolver _playfieldCollisionResolver = null!;

    /// <summary>
    /// Gets the runtime logical maze used by gameplay actors.
    /// </summary>
    public MazeGrid MazeGrid => _mazeGrid;

    /// <summary>
    /// Gets the runtime rotating-gate system used by the active level.
    /// </summary>
    public GateSystem GateSystem => _gateSystem;

    // --- Lifecycle ----------------------------------------------------------

    /// <summary>
    /// Initializes the level.
    /// </summary>
    /// <remarks>
    /// In the editor, gate views and the player preview are refreshed from their
    /// authored definitions.
    ///
    /// At runtime, the logical maze is loaded, the runtime gate system is built
    /// from the gate instances already placed under the Gates node, the base
    /// flower layout is loaded, and then the player controller is initialized.
    /// </remarks>
    public override void _Ready()
    {
        _gatesNode = GetNode<Node2D>("Gates");
        _collectiblesRoot = GetNode<Node2D>("Collectibles");

        CachePlacedGateViews();

        if (Engine.IsEditorHint())
        {
            UpdatePlayerPositionFromLogicalCell();
            RefreshPlacedGateViewsFromDefinitions();
            return;
        }

        _mazeGrid = MazeLoader.LoadFromJsonFile(MazeJsonPath);

        CollectibleLayoutFile collectibleLayout =
            CollectibleLoader.LoadFromJsonFile(CollectiblesJsonPath);

        RefreshPlacedGateViewsFromDefinitions();
        _gateSystem = BuildGateSystemFromPlacedViews();
        _playfieldCollisionResolver = new PlayfieldCollisionResolver(
            _mazeGrid,
            _gateSystem,
            ArcadePixelToLogicalCell,
            GatePivotToArcadePixel);
        SyncGateViewsFromRuntimeState();

        SpawnInitialFlowers(collectibleLayout);

        _rng.Randomize();

        CollectibleSpawnPlan spawnPlan =
            CollectibleSpawnPlanner.Generate(_levelNumber, _rng);

        ApplySpecialCollectibleSpawnPlan(spawnPlan);
        
        PlayerController? player = GetNodeOrNull<PlayerController>("Player");
        if (player != null)
            player.Initialize(this);
    }

    // --- Collectibles -------------------------------------------------------

    /// <summary>
    /// Spawns the base flower layout for the current level.
    /// </summary>
    /// <param name="layout">
    /// The deserialized collectible layout that defines which logical cells
    /// start with one flower.
    /// </param>
    /// <remarks>
    /// This method currently handles only the initial flower mask.
    /// Runtime replacements such as hearts, letters, and skulls should be
    /// applied later on top of this base layout.
    /// </remarks>
    private void SpawnInitialFlowers(CollectibleLayoutFile layout)
    {
        _collectiblesByCell.Clear();

        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                if (layout.Cells[y][x] != 1)
                {
                    continue;
                }

                SpawnFlower(new Vector2I(x, y));
            }
        }
    }

    /// <summary>
    /// Spawns one flower collectible at the given logical maze cell.
    /// </summary>
    /// <param name="cell">The logical maze cell where the flower should appear.</param>
    private void SpawnFlower(Vector2I cell)
    {
        var collectible = CollectibleScene.Instantiate<Collectible>();
        _collectiblesRoot.AddChild(collectible);

        collectible.Position = LogicalCellToScenePosition(cell);
        collectible.ShowFlower();

        _collectiblesByCell[cell] = collectible;
    }

    /// <summary>
    /// Removes the collectible currently present at the given logical cell.
    /// </summary>
    /// <param name="cell">The logical cell to clear.</param>
    /// <returns>
    /// <see langword="true"/> if one collectible was found and removed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private bool RemoveCollectible(Vector2I cell)
    {
        if (!_collectiblesByCell.TryGetValue(cell, out Collectible? collectible))
        {
            return false;
        }

        _collectiblesByCell.Remove(cell);
        collectible.QueueFree();
        return true;
    }

    /// <summary>
    /// Tries to consume the collectible currently present at the given logical cell.
    /// </summary>
    /// <param name="cell">The logical cell to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> if one collectible was found and consumed;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryConsumeCollectible(Vector2I cell)
    {
        return RemoveCollectible(cell);
    }

    /// <summary>
    /// Applies the generated start-of-level special collectible plan on top of the
    /// already spawned base flower layout.
    /// </summary>
    /// <param name="spawnPlan">Generated placements for letters, hearts, and skulls.</param>
    private void ApplySpecialCollectibleSpawnPlan(CollectibleSpawnPlan spawnPlan)
    {
        foreach (CollectiblePlacement placement in spawnPlan.Placements)
        {
            if (!_collectiblesByCell.TryGetValue(placement.Cell, out Collectible? collectible))
            {
                GD.PushWarning(
                    $"No base collectible found at logical cell {placement.Cell} " +
                    $"for special collectible placement.");
                continue;
            }

            ApplyCollectiblePlacement(collectible, placement);
        }
    }

    /// <summary>
    /// Applies one generated collectible placement to one existing collectible view.
    /// </summary>
    /// <param name="collectible">Existing collectible view at the target cell.</param>
    /// <param name="placement">Generated special collectible placement.</param>
    private static void ApplyCollectiblePlacement(
        Collectible collectible,
        CollectiblePlacement placement)
    {
        switch (placement.Kind)
        {
            case CollectibleKind.Heart:
                collectible.ShowHeartRed();
                break;

            case CollectibleKind.Letter:
                collectible.ShowLetterRed(placement.Letter);
                break;

            case CollectibleKind.Skull:
                collectible.ShowSkull();
                break;

            case CollectibleKind.Flower:
            default:
                collectible.ShowFlower();
                break;
        }
    }
    
    // --- Placed Gate Authoring ---------------------------------------------

    /// <summary>
    /// Rebuilds the internal lookup of gate views already placed under the Gates node.
    /// </summary>
    /// <remarks>
    /// The gate views are authored directly in the scene tree.
    /// Their editor definition is later converted into a separate runtime gate system.
    /// </remarks>
    private void CachePlacedGateViews()
    {
        _gateViewsById.Clear();

        foreach (Node child in _gatesNode.GetChildren())
        {
            if (child is not RotatingGateView gateView)
                continue;

            if (_gateViewsById.ContainsKey(gateView.GateId))
            {
                GD.PushError($"Duplicate rotating gate id '{gateView.GateId}' in Gates node.");
                continue;
            }

            _gateViewsById.Add(gateView.GateId, gateView);
        }
    }

    /// <summary>
    /// Reapplies the authored gate definitions to the placed gate views.
    /// </summary>
    private void RefreshPlacedGateViewsFromDefinitions()
    {
        foreach (RotatingGateView gateView in _gateViewsById.Values)
        {
            gateView.RefreshFromDefinition();
        }
    }

    /// <summary>
    /// Builds the runtime gate system from the placed gate views.
    /// </summary>
    private GateSystem BuildGateSystemFromPlacedViews()
    {
        List<RotatingGateRuntimeState> gateStates = new();

        foreach (RotatingGateView gateView in _gateViewsById.Values)
        {
            gateStates.Add(gateView.CreateInitialRuntimeState());
        }

        return GateSystem.FromRuntimeStates(gateStates);
    }

    // --- Rotating Gates -----------------------------------------------------

    /// <summary>
    /// Advances the rotating-gate runtime timers by one simulation tick
    /// and refreshes their current visual state.
    /// </summary>
    public void AdvanceGateSimulationOneTick()
    {
        _gateSystem.AdvanceOneTick();
        SyncGateViewsFromRuntimeState();
    }

    /// <summary>
    /// Attempts to push one rotating gate using the attempted gameplay direction
    /// and contacted half.
    /// </summary>
    /// <param name="gateId">Identifier of the gate to push.</param>
    /// <param name="moveDir">Attempted one-pixel gameplay movement direction.</param>
    /// <param name="contactHalf">Half of the gate that is being pushed.</param>
    /// <returns>True if the push is accepted; otherwise false.</returns>
    public bool TryPushGate(int gateId, Vector2I moveDir, GateContactHalf contactHalf)
    {
        bool pushed = _gateSystem.TryPush(gateId, moveDir, contactHalf);
        if (pushed)
            SyncGateViewsFromRuntimeState();
        return pushed;
    }

    /// <summary>
    /// Refreshes the gate views so they match the current runtime state.
    /// </summary>
    private void SyncGateViewsFromRuntimeState()
    {
        foreach (RotatingGateRuntimeState gateState in _gateSystem.Gates)
        {
            if (_gateViewsById.TryGetValue(gateState.Id, out RotatingGateView? gateView))
            {
                if (gateState.VisualState == GateVisualState.Turning)
                {
                    gateView.SetTurningVisual(gateState.TurningVisual);
                }
                else
                {
                    gateView.SetOrientation(gateState.GetStableOrientation());
                }
            }
        }
    }

    // --- Playfield Step Evaluation -----------------------------------------

    /// <summary>
    /// Evaluates one attempted arcade-pixel step against the active playfield:
    /// static maze plus dynamic rotating gates.
    /// </summary>
    /// <param name="arcadePixelPos">Current gameplay position in arcade pixels.</param>
    /// <param name="direction">Attempted one-pixel movement direction.</param>
    /// <param name="collisionLead">Directional collision probe offset.</param>
    /// <returns>
    /// A combined playfield result describing whether movement is allowed,
    /// blocked by a fixed wall, or blocked by a rotating gate.
    /// </returns>
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

    // --- Gate Coordinate Helpers -------------------------------------------

    /// <summary>
    /// Converts one logical gate pivot into an arcade-pixel pivot position.
    /// </summary>
    private static Vector2I GatePivotToArcadePixel(Vector2I pivot)
    {
        return new Vector2I(pivot.X * CellSizeArcade, pivot.Y * CellSizeArcade);
    }

    /// <summary>
    /// Converts one logical gate pivot into scene coordinates.
    /// </summary>
    /// <param name="pivot">Logical gate pivot.</param>
    /// <returns>Scene position of the gate pivot.</returns>
    public Vector2 GatePivotToScenePosition(Vector2I pivot)
    {
        return ArcadePixelToScenePosition(GatePivotToArcadePixel(pivot));
    }

    // --- Coordinate Conversion ---------------------------------------------

    /// <summary>
    /// Converts a logical maze cell into a gameplay arcade-pixel anchor.
    /// </summary>
    public Vector2I LogicalCellToArcadePixel(Vector2I cell)
    {
        return cell * CellSizeArcade + GameplayAnchorArcade;
    }

    /// <summary>
    /// Converts a gameplay arcade-pixel position back into a logical maze cell.
    /// </summary>
    public Vector2I ArcadePixelToLogicalCell(Vector2I arcadePixel)
    {
        int halfCell = CellSizeArcade / 2;

        int x = FloorDiv(arcadePixel.X - GameplayAnchorArcade.X + halfCell, CellSizeArcade);
        int y = FloorDiv(arcadePixel.Y - GameplayAnchorArcade.Y + halfCell, CellSizeArcade);

        return new Vector2I(x, y);
    }

    /// <summary>
    /// Converts a gameplay arcade-pixel position into scene coordinates.
    /// </summary>
    public Vector2 ArcadePixelToScenePosition(Vector2I arcadePixel)
    {
        Sprite2D? maze = GetNodeOrNull<Sprite2D>("Maze");
        if (maze == null)
            return Vector2.Zero;

        float x = maze.Position.X + arcadePixel.X * RenderScale;
        float y = maze.Position.Y + arcadePixel.Y * RenderScale;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Converts an arcade-pixel delta into a scene-space delta.
    /// </summary>
    public Vector2 ArcadeDeltaToSceneDelta(Vector2I arcadeDelta)
    {
        return new Vector2(arcadeDelta.X * RenderScale, arcadeDelta.Y * RenderScale);
    }

    /// <summary>
    /// Converts a logical maze cell directly into a scene position.
    /// </summary>
    public Vector2 LogicalCellToScenePosition(Vector2I cell)
    {
        return ArcadePixelToScenePosition(LogicalCellToArcadePixel(cell));
    }

    // --- Helpers ------------------------------------------------------------

    /// <summary>
    /// Computes floor division for integers, including negative values.
    /// </summary>
    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;

        if (remainder != 0 && ((value < 0) != (divisor < 0)))
            quotient--;

        return quotient;
    }

    // --- Editor Preview -----------------------------------------------------

    /// <summary>
    /// Updates the player instance preview position from the configured start cell.
    /// </summary>
    private void UpdatePlayerPositionFromLogicalCell()
    {
        Sprite2D? maze = GetNodeOrNull<Sprite2D>("Maze");
        Node2D? player = GetNodeOrNull<Node2D>("Player");
        if (maze == null || player == null)
            return;

        player.Position = LogicalCellToScenePosition(_playerStartCell);

        if (Engine.IsEditorHint())
        {
            AnimatedSprite2D? animatedSprite = player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            if (animatedSprite != null)
            {
                Vector2 spriteOffset = ArcadeDeltaToSceneDelta(new Vector2I(5, 8));
                animatedSprite.Position = spriteOffset;
            }

            player.NotifyPropertyListChanged();
            QueueRedraw();
        }
    }
}
