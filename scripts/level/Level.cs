using Godot;
using System;
using System.IO;
using System.Text.Json;
using LadyBug.Gameplay.Maze;
using LadyBug.Gameplay.Gates;
using LadyBug.Actors;
using LadyBug.Gameplay;
using System.Collections.Generic;

/// <summary>
/// Represents one playable level of the game.
/// </summary>
/// <remarks>
/// This class is responsible for:
/// loading the logical maze,
/// exposing it to gameplay actors,
/// and converting between logical cells, arcade-pixel gameplay coordinates,
/// and Godot scene coordinates.
///
/// The level intentionally separates:
/// - logical maze data used for gameplay
/// - visual maze rendering provided by the background sprite
///
/// The player controller uses gameplay anchors in arcade pixels,
/// while sprite-specific visual offsets are handled separately by the actor.
/// </remarks>
[Tool]
public partial class Level : Node2D
{
    private GateSystem _gateSystem = null!;

    /// <summary>
    /// Gets the runtime rotating gate system used by the level.
    /// </summary>
    public GateSystem GateSystem => _gateSystem;
    
    private const string MazeJsonPath = "res://data/maze.json";
    private const string RotatingGateScenePath = "res://scenes/level/RotatingGate.tscn";

    private Node2D _gatesNode = null!;
    private readonly PackedScene _rotatingGateScene = GD.Load<PackedScene>(RotatingGateScenePath);

    private readonly Dictionary<int, RotatingGateView> _gateViewsById = new();
    
    // --- Constants ----------------------------------------------------------

    // Size of one logical maze cell in original arcade pixels.
    private const int CellSizeArcade = 16;

    // Scale factor used to render arcade pixels in the Godot scene.
    private const int RenderScale = 4;

    // Gameplay anchor used inside each logical 16x16 cell.
    // This anchor is used for movement and collision, not for visual sprite centering.
    private static readonly Vector2I GameplayAnchorArcade = new(8, 7);

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

    // Runtime logical maze loaded from JSON.
    private MazeGrid _mazeGrid = null!;

    /// <summary>
    /// Gets the runtime logical maze used by gameplay actors.
    /// </summary>
    public MazeGrid MazeGrid => _mazeGrid;

    // --- Lifecycle ----------------------------------------------------------

    /// <summary>
    /// Initializes the level.
    /// </summary>
    /// <remarks>
    /// In the editor, only the preview position is updated.
    /// At runtime, the logical maze is loaded, the rotating gates are spawned,
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

        PlayerController player = GetNodeOrNull<PlayerController>("Player");
        if (player != null)
            player.Initialize(this);
    }

    // --- Rotating Gates -----------------------------------------------------

    /// <summary>
    /// Spawns all rotating gate visuals defined in the maze JSON file.
    /// </summary>
    /// <remarks>
    /// For now, gates are display-only objects.
    /// Their interaction with the player and their runtime logical state
    /// are not implemented yet.
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

    /// <summary>
    /// Converts a gate pivot read from JSON into a scene position.
    /// </summary>
    /// <param name="pivot">Gate pivot coordinates from the JSON file.</param>
    /// <returns>Scene position of the gate pivot.</returns>
    /// <remarks>
    /// Gate pivot coordinates are expressed on a grid aligned with 16 arcade-pixel steps.
    /// The visual sprite offset is handled locally inside RotatingGate.tscn.
    /// </remarks>
    private Vector2 GetGateScenePosition(Vector2I pivot)
    {
        Vector2I pivotArcade = new(pivot.X * 16, pivot.Y * 16);
        return ArcadePixelToScenePosition(pivotArcade);
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
        Sprite2D maze = GetNodeOrNull<Sprite2D>("Maze");
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
        Sprite2D maze = GetNodeOrNull<Sprite2D>("Maze");
        Node2D player = GetNodeOrNull<Node2D>("Player");

        if (maze == null || player == null)
            return;

        // Position gameplay de l'instance Player.
        player.Position = LogicalCellToScenePosition(_playerStartCell);

        // En mode éditeur, on applique aussi un offset visuel au sprite
        // pour que l'aperçu corresponde au runtime.
        if (Engine.IsEditorHint())
        {
            AnimatedSprite2D animatedSprite = player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            if (animatedSprite != null)
            {
                // Même offset visuel vertical que celui utilisé au runtime au démarrage.
                Vector2 spriteOffset = ArcadeDeltaToSceneDelta(new Vector2I(5, 7));
                animatedSprite.Position = spriteOffset;
            }

            player.NotifyPropertyListChanged();
            QueueRedraw();
        }
    }
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

        if (mazeStep.NextCell == mazeStep.CurrentCell)
            return PlayfieldStepResult.AllowedStep(mazeStep);

        if (TryGetBlockingGateIdForStep(mazeStep, direction, out int gateId))
            return PlayfieldStepResult.BlockedByGate(mazeStep, gateId);

        return PlayfieldStepResult.AllowedStep(mazeStep);
    }
    
    /// <summary>
    /// Tries to find whether the evaluated step is blocked by one rotating gate.
    /// </summary>
    /// <param name="mazeStep">Underlying static maze step result.</param>
    /// <param name="direction">Attempted one-pixel movement direction.</param>
    /// <param name="gateId">Returned blocking gate identifier if found.</param>
    /// <returns>True if a blocking gate is found; otherwise false.</returns>
    private bool TryGetBlockingGateIdForStep(
        MazeStepResult mazeStep,
        Vector2I direction,
        out int gateId)
    {
        gateId = -1;

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
                out gateId);
        }

        if (direction.Y != 0)
        {
            return TryGetBlockingGateIdAcrossHorizontalBoundary(
                mazeStep.CurrentCell,
                mazeStep.NextCell,
                direction,
                out gateId);
        }

        return false;
    }
    
    /// <summary>
    /// Tries to find a blocking gate when movement crosses a vertical cell boundary,
    /// that is, during a left/right movement step.
    /// </summary>
    /// <param name="currentCell">Logical cell containing the current position.</param>
    /// <param name="nextCell">Logical cell reached by the forward probe.</param>
    /// <param name="direction">Attempted one-pixel movement direction.</param>
    /// <param name="gateId">Returned blocking gate identifier if found.</param>
    /// <returns>True if a blocking gate is found; otherwise false.</returns>
    private bool TryGetBlockingGateIdAcrossVerticalBoundary(
        Vector2I currentCell,
        Vector2I nextCell,
        Vector2I direction,
        out int gateId)
    {
        gateId = -1;

        int boundaryX = Math.Max(currentCell.X, nextCell.X);

        Vector2I pivotA = new(boundaryX, currentCell.Y);
        Vector2I pivotB = new(boundaryX, currentCell.Y + 1);

        return TryGetBlockingGateIdAtCandidatePivots(direction, pivotA, pivotB, out gateId);
    }
    
    /// <summary>
    /// Tries to find a blocking gate when movement crosses a horizontal cell boundary,
    /// that is, during an up/down movement step.
    /// </summary>
    /// <param name="currentCell">Logical cell containing the current position.</param>
    /// <param name="nextCell">Logical cell reached by the forward probe.</param>
    /// <param name="direction">Attempted one-pixel movement direction.</param>
    /// <param name="gateId">Returned blocking gate identifier if found.</param>
    /// <returns>True if a blocking gate is found; otherwise false.</returns>
    private bool TryGetBlockingGateIdAcrossHorizontalBoundary(
        Vector2I currentCell,
        Vector2I nextCell,
        Vector2I direction,
        out int gateId)
    {
        gateId = -1;

        int boundaryY = Math.Max(currentCell.Y, nextCell.Y);

        Vector2I pivotA = new(currentCell.X, boundaryY);
        Vector2I pivotB = new(currentCell.X + 1, boundaryY);

        return TryGetBlockingGateIdAtCandidatePivots(direction, pivotA, pivotB, out gateId);
    }
    
    /// <summary>
    /// Tests two candidate gate pivots and returns the first gate that blocks
    /// the attempted movement direction.
    /// </summary>
    /// <param name="direction">Attempted one-pixel movement direction.</param>
    /// <param name="pivotA">First candidate pivot.</param>
    /// <param name="pivotB">Second candidate pivot.</param>
    /// <param name="gateId">Returned blocking gate identifier if found.</param>
    /// <returns>True if a blocking gate is found; otherwise false.</returns>
    private bool TryGetBlockingGateIdAtCandidatePivots(
        Vector2I direction,
        Vector2I pivotA,
        Vector2I pivotB,
        out int gateId)
    {
        gateId = -1;

        if (_gateSystem.TryGetGateByPivot(pivotA, out RotatingGateRuntimeState gateA) &&
            gateA.BlocksMovement(direction))
        {
            gateId = gateA.Id;
            return true;
        }

        if (_gateSystem.TryGetGateByPivot(pivotB, out RotatingGateRuntimeState gateB) &&
            gateB.BlocksMovement(direction))
        {
            gateId = gateB.Id;
            return true;
        }

        return false;
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
    /// Attempts to push one rotating gate using the attempted gameplay direction.
    /// </summary>
    /// <param name="gateId">Identifier of the gate to push.</param>
    /// <param name="moveDir">Attempted one-pixel gameplay movement direction.</param>
    /// <returns>
    /// True if the push is accepted; otherwise false.
    /// </returns>
    public bool TryPushGate(int gateId, Vector2I moveDir)
    {
        bool pushed = _gateSystem.TryPush(gateId, moveDir);

        if (pushed)
            SyncGateViewsFromRuntimeState();

        return pushed;
    }

    /// <summary>
    /// Refreshes the gate views so they match the current runtime logical state.
    /// </summary>
    /// <remarks>
    /// For now, this keeps the visual model intentionally simple:
    /// the view jumps directly to the new stable orientation.
    /// The short Turning state already exists in runtime and will be used later
    /// once the diagonal transition visuals are wired.
    /// </remarks>
    private void SyncGateViewsFromRuntimeState()
    {
        foreach (RotatingGateRuntimeState gateState in _gateSystem.Gates)
        {
            if (_gateViewsById.TryGetValue(gateState.Id, out RotatingGateView gateView))
            {
                gateView.SetOrientation(gateState.GetStableOrientation());
            }
        }
    }
}