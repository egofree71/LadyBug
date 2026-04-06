using Godot;

namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Visual scene instance used to display one rotating gate in the level.
/// </summary>
/// <remarks>
/// This class is purely visual at runtime, but it also exposes editor-facing
/// gate definition properties so that gates can be authored directly inside
/// <c>Level.tscn</c>.
///
/// The runtime gameplay state still lives in <see cref="GateSystem"/>.
/// </remarks>
[Tool]
public partial class RotatingGateView : Node2D
{
    private AnimatedSprite2D? _sprite;

    private int _gateId;
    private Vector2I _gatePivot = Vector2I.Zero;
    private GateOrientation _initialOrientation = GateOrientation.Horizontal;

    /// <summary>
    /// Gets or sets the unique gate identifier used at runtime.
    /// </summary>
    [Export]
    public int GateId
    {
        get => _gateId;
        set
        {
            if (_gateId == value)
                return;

            _gateId = value;
            RefreshFromDefinition();
        }
    }

    /// <summary>
    /// Gets or sets the logical pivot position of the gate.
    /// </summary>
    /// <remarks>
    /// This value is authored in the editor and is converted into a scene-space
    /// position through the owning <see cref="Level"/>.
    /// </remarks>
    [Export]
    public Vector2I GatePivot
    {
        get => _gatePivot;
        set
        {
            if (_gatePivot == value)
                return;

            _gatePivot = value;
            RefreshFromDefinition();
        }
    }

    /// <summary>
    /// Gets or sets the initial stable orientation authored in the editor.
    /// </summary>
    [Export]
    public GateOrientation InitialOrientation
    {
        get => _initialOrientation;
        set
        {
            if (_initialOrientation == value)
                return;

            _initialOrientation = value;
            RefreshFromDefinition();
        }
    }

    /// <summary>
    /// Caches the <see cref="AnimatedSprite2D"/> child used to display the gate.
    /// </summary>
    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        RefreshFromDefinition();
    }

    /// <summary>
    /// Reapplies the editor-authored definition to the current scene instance.
    /// </summary>
    /// <remarks>
    /// In the editor, this updates:
    /// - the stable sprite orientation
    /// - the scene-space position derived from <see cref="GatePivot"/>
    ///
    /// At runtime, the gate view is later driven by the gate runtime state.
    /// </remarks>
    public void RefreshFromDefinition()
    {
        if (!IsInsideTree())
            return;

        SetOrientation(_initialOrientation);

        if (Engine.IsEditorHint())
            UpdateScenePositionFromPivot();
    }

    /// <summary>
    /// Builds one initial runtime gate state from the current editor-authored definition.
    /// </summary>
    /// <returns>A new runtime gate state matching the editor definition.</returns>
    public RotatingGateRuntimeState CreateInitialRuntimeState()
    {
        return RotatingGateRuntimeState.FromInitialOrientation(
            _gateId,
            _gatePivot,
            _initialOrientation);
    }

    /// <summary>
    /// Displays the stable visual orientation of the gate.
    /// </summary>
    /// <param name="orientation">Stable orientation to display.</param>
    public void SetOrientation(GateOrientation orientation)
    {
        string animationName = orientation == GateOrientation.Horizontal
            ? "horizontal"
            : "vertical";

        SetAnimationFrame(animationName);
    }

    /// <summary>
    /// Displays the diagonal turning visual of the gate.
    /// </summary>
    /// <param name="turningVisual">Turning diagonal to display.</param>
    public void SetTurningVisual(GateTurningVisual turningVisual)
    {
        string animationName = turningVisual == GateTurningVisual.Slash
            ? "slash"
            : "backslash";

        SetAnimationFrame(animationName);
    }

    private void UpdateScenePositionFromPivot()
    {
        Level? level = GetOwningLevel();
        if (level == null)
            return;

        Position = level.GatePivotToScenePosition(_gatePivot);
    }

    private Level? GetOwningLevel()
    {
        Node? gatesNode = GetParent();
        if (gatesNode == null)
            return null;

        return gatesNode.GetParent() as Level;
    }

    private void SetAnimationFrame(string animationName)
    {
        _sprite ??= GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _sprite.Animation = animationName;
        _sprite.Frame = 0;
    }
}
