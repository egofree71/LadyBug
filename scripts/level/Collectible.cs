using Godot;

/// <summary>
/// Represents one collectible view placed in the level scene.
/// </summary>
public partial class Collectible : Node2D
{
    private const int FlowerFrame = 1;

    private Sprite2D _mainSprite = default!;

    public override void _Ready()
    {
        _mainSprite = GetNode<Sprite2D>("MainSprite");
    }

    /// <summary>
    /// Displays the flower visual.
    /// </summary>
    public void ShowFlower()
    {
        _mainSprite.Frame = FlowerFrame;
    }
}