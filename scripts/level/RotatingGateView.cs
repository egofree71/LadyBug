using Godot;
using LadyBug.Gameplay.Gates;

/// <summary>
/// Visual scene instance used to display one rotating gate in the level.
/// </summary>
/// <remarks>
/// For now, this class only controls the displayed stable orientation
/// of the gate sprite.
/// No gameplay interaction or runtime rotation animation is implemented yet.
/// </remarks>
public partial class RotatingGateView : Node2D
{
    private AnimatedSprite2D _sprite = null!;

    /// <summary>
    /// Caches the AnimatedSprite2D child used to display the gate.
    /// </summary>
    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }

    /// <summary>
    /// Displays the correct stable visual for the given orientation.
    /// </summary>
    /// <param name="orientation">
    /// Stable logical orientation to display.
    /// </param>
    public void SetOrientation(GateOrientation orientation)
    {
        string animationName = orientation == GateOrientation.Horizontal
            ? "horizontal"
            : "vertical";

        _sprite.Animation = animationName;
        _sprite.Frame = 0;
    }
}