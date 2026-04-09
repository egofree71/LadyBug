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
        SyncGateViewsFromRuntimeState();

        SpawnInitialFlowers(collectibleLayout);

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
    /// A combined playfield result describing whether the step is allowed,
    /// blocked by a fixed wall, or blocked by a rotating gate.
    /// </returns>
    public PlayfieldStepResult EvaluateArcadePixelStepWithGates(
        Vector2I arcadePixelPos,
        Vector2I direction,
        Vector2I collisionLead)
    {
        MazeStepResult mazeStep = _mazeGrid.EvaluateArcadePixelStep(
            arcadePixelPos,
            direction,
            collisionLead,
            ArcadePixelToLogicalCell);

        if (!mazeStep.Allowed)
            return PlayfieldStepResult.BlockedByFixedWall(mazeStep);

        if (TryGetBlockingGateIdAtProbe(
                arcadePixelPos,
                direction,
                collisionLead,
                out int gateId,
                out GateContactHalf? contactHalf))
        {
            return PlayfieldStepResult.BlockedByGate(mazeStep, gateId, contactHalf);
        }

        if (mazeStep.NextCell == mazeStep.CurrentCell)
            return PlayfieldStepResult.AllowedStep(mazeStep);

        if (TryGetBlockingGateIdForStep(
                mazeStep,
                direction,
                out gateId,
                out GateContactHalf boundaryContactHalf))
        {
            return PlayfieldStepResult.BlockedByGate(mazeStep, gateId, boundaryContactHalf);
        }

        return PlayfieldStepResult.AllowedStep(mazeStep);
    }

    /// <summary>
    /// Tries to detect a blocking gate directly from the pixel probe motion,
    /// even when the probe does not cross into a different logical cell yet.
    /// </summary>
    private bool TryGetBlockingGateIdAtProbe(
        Vector2I arcadePixelPos,
        Vector2I direction,
        Vector2I collisionLead,
        out int gateId,
        out GateContactHalf? contactHalf)
    {
        gateId = -1;
        contactHalf = null;

        if (direction == Vector2I.Zero)
            return false;

        Vector2I currentCell = ArcadePixelToLogicalCell(arcadePixelPos);
        Vector2I probeStart = arcadePixelPos + collisionLead;
        Vector2I probeEnd = arcadePixelPos + direction + collisionLead;

        Vector2I[] candidatePivots =
        {
            currentCell,
            new Vector2I(currentCell.X + 1, currentCell.Y),
            new Vector2I(currentCell.X, currentCell.Y + 1),
            new Vector2I(currentCell.X + 1, currentCell.Y + 1)
        };

        foreach (Vector2I pivot in candidatePivots)
        {
            if (!_gateSystem.TryGetGateByPivot(pivot, out RotatingGateRuntimeState gate))
                continue;

            if (!gate.BlocksMovement(direction))
                continue;

            if (TryGetGateBlockAtProbe(
                    gate,
                    probeStart,
                    probeEnd,
                    direction,
                    out GateContactHalf? candidateHalf))
            {
                gateId = gate.Id;
                contactHalf = candidateHalf;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tests whether the current probe motion intersects the blocking geometry
    /// of one specific gate.
    /// </summary>
    private bool TryGetGateBlockAtProbe(
        RotatingGateRuntimeState gate,
        Vector2I probeStart,
        Vector2I probeEnd,
        Vector2I direction,
        out GateContactHalf? contactHalf)
    {
        contactHalf = null;

        Vector2I pivotArcade = GatePivotToArcadePixel(gate.Pivot);

        if (gate.LogicalState == GateLogicalState.BlocksVertical)
        {
            if (direction.Y == 0)
                return false;

            if (!CrossesCoordinate(probeStart.Y, probeEnd.Y, pivotArcade.Y))
                return false;

            int localX = probeEnd.X - pivotArcade.X;
            if (Mathf.Abs(localX) > 8)
                return false;

            if (localX < 0)
                contactHalf = GateContactHalf.Left;
            else if (localX > 0)
                contactHalf = GateContactHalf.Right;
            else
                contactHalf = null;

            return true;
        }

        if (gate.LogicalState == GateLogicalState.BlocksHorizontal)
        {
            if (direction.X == 0)
                return false;

            if (!CrossesCoordinate(probeStart.X, probeEnd.X, pivotArcade.X))
                return false;

            int localY = probeEnd.Y - pivotArcade.Y;
            if (Mathf.Abs(localY) > 8)
                return false;

            if (localY < 0)
                contactHalf = GateContactHalf.Top;
            else if (localY > 0)
                contactHalf = GateContactHalf.Bottom;
            else
                contactHalf = null;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether a 1D segment crosses or touches a target coordinate.
    /// </summary>
    private static bool CrossesCoordinate(int start, int end, int target)
    {
        return (start <= target && end >= target) || (start >= target && end <= target);
    }

    /// <summary>
    /// Tries to find whether the evaluated step is blocked by one rotating gate
    /// when the probe crosses into another logical cell.
    /// </summary>
    private bool TryGetBlockingGateIdForStep(
        MazeStepResult mazeStep,
        Vector2I direction,
        out int gateId,
        out GateContactHalf contactHalf)
    {
        gateId = -1;
        contactHalf = GateContactHalf.Left;

        if (direction == Vector2I.Zero)
            return false;

        if (mazeStep.NextCell == mazeStep.CurrentCell)
            return false;

        if (direction.X != 0)
        {
            return TryGetBlockingGateIdAcrossVerticalBoundary(
                mazeStep.CurrentCell,
                mazeStep.NextCell,
                direction,
                out gateId,
                out contactHalf);
        }

        if (direction.Y != 0)
        {
            return TryGetBlockingGateIdAcrossHorizontalBoundary(
                mazeStep.CurrentCell,
                mazeStep.NextCell,
                direction,
                out gateId,
                out contactHalf);
        }

        return false;
    }

    /// <summary>
    /// Tries to find a blocking gate when movement crosses a vertical cell boundary,
    /// that is, during a left/right movement step.
    /// </summary>
    private bool TryGetBlockingGateIdAcrossVerticalBoundary(
        Vector2I currentCell,
        Vector2I nextCell,
        Vector2I direction,
        out int gateId,
        out GateContactHalf contactHalf)
    {
        gateId = -1;
        contactHalf = GateContactHalf.Left;

        int boundaryX = System.Math.Max(currentCell.X, nextCell.X);

        Vector2I pivotTop = new(boundaryX, currentCell.Y);
        if (TryGetBlockingGateAtPivot(direction, pivotTop, GateContactHalf.Bottom, out gateId, out contactHalf))
            return true;

        Vector2I pivotBottom = new(boundaryX, currentCell.Y + 1);
        if (TryGetBlockingGateAtPivot(direction, pivotBottom, GateContactHalf.Top, out gateId, out contactHalf))
            return true;

        return false;
    }

    /// <summary>
    /// Tries to find a blocking gate when movement crosses a horizontal cell boundary,
    /// that is, during an up/down movement step.
    /// </summary>
    private bool TryGetBlockingGateIdAcrossHorizontalBoundary(
        Vector2I currentCell,
        Vector2I nextCell,
        Vector2I direction,
        out int gateId,
        out GateContactHalf contactHalf)
    {
        gateId = -1;
        contactHalf = GateContactHalf.Left;

        int boundaryY = System.Math.Max(currentCell.Y, nextCell.Y);

        Vector2I pivotLeft = new(currentCell.X, boundaryY);
        if (TryGetBlockingGateAtPivot(direction, pivotLeft, GateContactHalf.Right, out gateId, out contactHalf))
            return true;

        Vector2I pivotRight = new(currentCell.X + 1, boundaryY);
        if (TryGetBlockingGateAtPivot(direction, pivotRight, GateContactHalf.Left, out gateId, out contactHalf))
            return true;

        return false;
    }

    /// <summary>
    /// Tests whether one gate at a candidate pivot blocks the attempted movement.
    /// </summary>
    private bool TryGetBlockingGateAtPivot(
        Vector2I direction,
        Vector2I pivot,
        GateContactHalf candidateHalf,
        out int gateId,
        out GateContactHalf contactHalf)
    {
        gateId = -1;
        contactHalf = GateContactHalf.Left;

        if (_gateSystem.TryGetGateByPivot(pivot, out RotatingGateRuntimeState gate) && gate.BlocksMovement(direction))
        {
            gateId = gate.Id;
            contactHalf = candidateHalf;
            return true;
        }

        return false;
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
                Vector2 spriteOffset = ArcadeDeltaToSceneDelta(new Vector2I(5, 7));
                animatedSprite.Position = spriteOffset;
            }

            player.NotifyPropertyListChanged();
            QueueRedraw();
        }
    }
}
