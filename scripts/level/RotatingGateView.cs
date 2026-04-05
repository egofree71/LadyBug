using Godot;

namespace LadyBug.Gameplay.Gates;

/// <summary>
/// Visual scene instance used to display one rotating gate in the level.
/// </summary>
public partial class RotatingGateView : Node2D
{
    private AnimatedSprite2D? _sprite;

    /// <summary>
    /// Caches the AnimatedSprite2D child used to display the gate.
    /// </summary>
    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
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

    private void SetAnimationFrame(string animationName)
    {
        _sprite ??= GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _sprite.Animation = animationName;
        _sprite.Frame = 0;
    }
}