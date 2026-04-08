using Godot;

public partial class Collectible : Node2D
{
    private Sprite2D _mainSprite = default!;

    public override void _Ready()
    {
        _mainSprite = GetNode<Sprite2D>("MainSprite");
    }

    public void SetFrame(int frame)
    {
        _mainSprite.Frame = frame;
    }
}