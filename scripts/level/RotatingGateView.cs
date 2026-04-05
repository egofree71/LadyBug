using Godot;

public partial class RotatingGateView : Node2D
{
    private AnimatedSprite2D _sprite = null!;

    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }

    public void SetOrientation(GateOrientation orientation)
    {
        string animationName = orientation == GateOrientation.Horizontal
            ? "horizontal"
            : "vertical";

        _sprite.Play(animationName);
        _sprite.Stop();
    }
}