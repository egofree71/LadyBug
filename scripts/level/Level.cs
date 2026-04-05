using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LadyBug.Actors;
using LadyBug.Gameplay;
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
/// and converting between logical cells, arcade-pixel gameplay coordinates,
/// and Godot scene coordinates.
///
/// The level intentionally separates:
/// - logical maze data used for gameplay
/// - dynamic rotating-gate state used as a movement overlay
/// - visual rendering provided by the maze background and gate views
///
/// The player controller uses gameplay anchors in arcade pixels,
/// while sprite-specific visual offsets are handled separately by the actor.
/// </remarks>
[Tool]
public partial class Level : Node2D
{
    private const string MazeJsonPath = "res://data/maze.json";
    private const string RotatingGateScenePath = "res://scenes/level/RotatingGate.tscn";

    // --- Constants ----------------------------------------------------------

    // Size of one logical maze cell in original arcade pixels.
    private const int CellSizeArcade = 16;

    // Scale factor used to render arcade pixels in the Godot scene.
    private const int RenderScale = 4;

    // Gameplay anchor used inside each logical 16x16 cell.
    // This anchor is used for movement and collision, not for visual sprite centering.
    private static readonly Vector2I GameplayAnchorArcade = new(8, 7);

    // --- Scene References ---------------------------------------------------

    private Node2D _gatesNode = null!;
    private readonly Dictionary<int, RotatingGateView> _gateViewsById = new();
    private readonly PackedScene _rotatingGateScene = GD.Load<PackedScene>(RotatingGateScenePath);

    // --- Exported Properties ------------------------------------------------

    private Vector2I _playerStartCell = Vector2I.Zero;

    /// <summary>
    /// Logical start cell used to place the player.
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
    /// In the editor, only the preview position is updated.
    /// At runtime, the logical maze is loaded, the rotating gates are created,
    /// and then the player controller is initialized.
    /// </remarks>
    public override void _Ready()
    {
        _gatesNode = GetNode<Node2D>("Gates");

        if (Engine.IsEditorHint())
        {
            UpdatePlayerPositionFromLogicalCell();
            return;
        }

        _mazeGrid = MazeLoader.LoadFromJsonFile(MazeJsonPath);

        MazeDataFile mazeData = LoadMazeDataFile();
        _gateSystem = GateSystem.FromDataFiles(mazeData.Gates);

        SpawnRotatingGates();

        PlayerController? player = GetNodeOrNull<PlayerController>("Player");
        if (player != null)
            player.Initialize(this);
    }

    // --- Rotating Gates -----------------------------------------------------

    /// <summary>
    /// Spawns all rotating-gate views from the current runtime gate system.
    /// </summary>
    /// <remarks>
    /// The runtime gate system is the source of truth.
    /// Views are just synchronized scene instances.
    /// </remarks>
    private void SpawnRotatingGates()
    {
        foreach (Node child in _gatesNode.GetChildren())
        {
            child.QueueFree();
        }

        _gateViewsById.Clear();

        foreach (RotatingGateRuntimeState gateState in _gateSystem.Gates)
        {
            RotatingGateView gateView = _rotatingGateScene.Instantiate<RotatingGateView>();
            gateView.Position = GetGateScenePosition(gateState.Pivot);
            _gatesNode.AddChild(gateView);
            gateView.SetOrientation(gateState.GetStableOrientation());
            _gateViewsById.Add(gateState.Id, gateView);
        }
    }

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
    /// <returns>
    /// True if the push is accepted; otherwise false.
    /// </returns>
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
    /// <param name="arcadePixelPos">Current gameplay position in arcade pixels.</param>
    /// <param name="direction">Attempted one-pixel movement direction.</param>
    /// <param name="collisionLead">Directional collision probe offset.</param>
    /// <param name="gateId">Returned blocking gate identifier if found.</param>
    /// <param name="contactHalf">
    /// Returned contacted gate half if one side is clearly touched;
    /// otherwise null when the probe hits the pivot dead zone.
    /// </param>
    /// <returns>True if a blocking gate is found; otherwise false.</returns>
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
    /// <param name="gate">Gate being tested.</param>
    /// <param name="probeStart">Probe position before the attempted step.</param>
    /// <param name="probeEnd">Probe position after the attempted step.</param>
    /// <param name="direction">Attempted one-pixel movement direction.</param>
    /// <param name="contactHalf">
    /// Returned contacted gate half if one side is clearly touched;
    /// otherwise null when the pivot itself is touched.
    /// </param>
    /// <returns>True if the probe intersects the gate; otherwise false.</returns>
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
    /// <param name="start">Start coordinate.</param>
    /// <param name="end">End coordinate.</param>
    /// <param name="target">Target coordinate.</param>
    /// <returns>True if the segment crosses or touches the target; otherwise false.</returns>
    private static bool CrossesCoordinate(int start, int end, int target)
    {
        return (start <= target && end >= target) ||
               (start >= target && end <= target);
    }

    /// <summary>
    /// Tries to find whether the evaluated step is blocked by one rotating gate
    /// when the probe crosses into another logical cell.
    /// </summary>
    /// <param name="mazeStep">Underlying static maze step result.</param>
    /// <param name="direction">Attempted one-pixel movement direction.</param>
    /// <param name="gateId">Returned blocking gate identifier if found.</param>
    /// <param name="contactHalf">Returned contacted gate half if found.</param>
    /// <returns>True if a blocking gate is found; otherwise false.</returns>
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

        int boundaryX = Math.Max(currentCell.X, nextCell.X);

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

        int boundaryY = Math.Max(currentCell.Y, nextCell.Y);

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

        if (_gateSystem.TryGetGateByPivot(pivot, out RotatingGateRuntimeState gate) &&
            gate.BlocksMovement(direction))
        {
            gateId = gate.Id;
            contactHalf = candidateHalf;
            return true;
        }

        return false;
    }

    // --- Data Loading -------------------------------------------------------

    /// <summary>
    /// Loads the raw serialized maze data file from JSON.
    /// </summary>
    /// <returns>The deserialized maze data structure.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the JSON file cannot be deserialized.
    /// </exception>
    private MazeDataFile LoadMazeDataFile()
    {
        string absolutePath = ProjectSettings.GlobalizePath(MazeJsonPath);
        string json = File.ReadAllText(absolutePath);

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        MazeDataFile? data = JsonSerializer.Deserialize<MazeDataFile>(json, options);

        if (data is null)
            throw new InvalidOperationException("Failed to deserialize maze.json.");

        return data;
    }

    // --- Gate Coordinate Helpers -------------------------------------------

    /// <summary>
    /// Converts one logical gate pivot into an arcade-pixel pivot position.
    /// </summary>
    /// <param name="pivot">Logical gate pivot.</param>
    /// <returns>Arcade-pixel position of that pivot.</returns>
    private static Vector2I GatePivotToArcadePixel(Vector2I pivot)
    {
        return new Vector2I(pivot.X * CellSizeArcade, pivot.Y * CellSizeArcade);
    }

    /// <summary>
    /// Converts one logical gate pivot into scene coordinates.
    /// </summary>
    /// <param name="pivot">Logical gate pivot.</param>
    /// <returns>Scene position of the gate pivot.</returns>
    private Vector2 GetGateScenePosition(Vector2I pivot)
    {
        return ArcadePixelToScenePosition(GatePivotToArcadePixel(pivot));
    }

    // --- Coordinate Conversion ---------------------------------------------

    /// <summary>
    /// Converts a logical maze cell into a gameplay arcade-pixel anchor.
    /// </summary>
    /// <param name="cell">Logical cell coordinates.</param>
    /// <returns>
    /// Arcade-pixel gameplay anchor corresponding to the logical cell.
    /// </returns>
    public Vector2I LogicalCellToArcadePixel(Vector2I cell)
    {
        return cell * CellSizeArcade + GameplayAnchorArcade;
    }

    /// <summary>
    /// Converts a gameplay arcade-pixel position back into a logical maze cell.
    /// </summary>
    /// <param name="arcadePixel">Gameplay position in arcade pixels.</param>
    /// <returns>
    /// Logical cell containing that gameplay position.
    /// </returns>
    /// <remarks>
    /// The cell changes at the midpoint between two anchors, not only once the
    /// next anchor itself is reached.
    /// </remarks>
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
    /// <param name="arcadePixel">Gameplay position in arcade pixels.</param>
    /// <returns>
    /// Scene position corresponding to that gameplay anchor.
    /// </returns>
    /// <remarks>
    /// This is a pure gameplay-to-scene conversion.
    /// No sprite-specific visual offset is applied here.
    /// </remarks>
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
    /// <param name="arcadeDelta">Delta expressed in arcade pixels.</param>
    /// <returns>
    /// Delta expressed in Godot scene pixels.
    /// </returns>
    /// <remarks>
    /// This is mainly used by gameplay actors to apply visual sprite offsets
    /// without changing their true gameplay anchor.
    /// </remarks>
    public Vector2 ArcadeDeltaToSceneDelta(Vector2I arcadeDelta)
    {
        return new Vector2(arcadeDelta.X * RenderScale, arcadeDelta.Y * RenderScale);
    }

    /// <summary>
    /// Converts a logical maze cell directly into a scene position.
    /// </summary>
    /// <param name="cell">Logical cell coordinates.</param>
    /// <returns>
    /// Scene position corresponding to the gameplay anchor of that cell.
    /// </returns>
    public Vector2 LogicalCellToScenePosition(Vector2I cell)
    {
        return ArcadePixelToScenePosition(LogicalCellToArcadePixel(cell));
    }

    // --- Helpers ------------------------------------------------------------

    /// <summary>
    /// Computes floor division for integers, including negative values.
    /// </summary>
    /// <param name="value">Dividend.</param>
    /// <param name="divisor">Divisor.</param>
    /// <returns>
    /// Mathematical floor of value / divisor.
    /// </returns>
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
    /// <remarks>
    /// This is mainly used in the editor so that changing <see cref="PlayerStartCell"/>
    /// immediately refreshes the visible placement.
    /// </remarks>
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
