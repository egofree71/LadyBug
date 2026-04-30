using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Runtime-only visual node for the central vegetable bonus.
/// </summary>
public sealed partial class VegetableBonusView : Node2D
{
    private const string VegetablesTexturePath = "res://assets/sprites/props/vegetables.png";

    private readonly Sprite2D _sprite;

    public VegetableBonusView()
    {
        Name = "VegetableBonus";

        _sprite = new Sprite2D
        {
            Name = "Sprite",
            Texture = GD.Load<Texture2D>(VegetablesTexturePath),
            Hframes = VegetableBonusCatalog.FrameCount,
            Vframes = 1,
            Centered = true,
            Visible = false
        };

        AddChild(_sprite);
    }

    /// <summary>
    /// Displays the vegetable frame for the current level.
    /// </summary>
    public void ShowForLevel(int levelNumber)
    {
        _sprite.Frame = VegetableBonusCatalog.GetFrame(levelNumber);
        _sprite.Visible = true;
        Visible = true;
    }

    /// <summary>
    /// Hides the vegetable without freeing the view.
    /// </summary>
    public void HideVegetable()
    {
        _sprite.Visible = false;
        Visible = false;
    }
}
