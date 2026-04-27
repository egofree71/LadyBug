// scripts/level/Collectible.cs

using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Runtime visual view for a single collectible instance.
/// </summary>
/// <remarks>
/// Flowers and skulls use only the main sprite. Hearts use the main sprite for
/// the color-cycled outer ring and the overlay sprite for the fixed inner heart.
/// Letters use the main sprite tinted with the current collectible color cycle.
/// </remarks>
public partial class Collectible : Node2D
{
    private const int SkullFrame = 0;
    private const int FlowerFrame = 1;
    private const int HeartRingFrame = 2;
    private const int HeartCenterFrame = 3;

    // Current project spritesheet mapping, corrected from the captured sheet:
    // frame 4=E, 5=X, 6=T, 7=R, 8=A, 9=S, 10=P, 11=C, 12=I, 13=L.
    // Keep the semantic LetterKind -> visual-frame mapping here so the spawned
    // letter remains gameplay-correct even though the sheet order is arcade-like.
    private const int LetterEFrame = 4;
    private const int LetterXFrame = 5;
    private const int LetterTFrame = 6;
    private const int LetterRFrame = 7;
    private const int LetterAFrame = 8;
    private const int LetterSFrame = 9;
    private const int LetterPFrame = 10;
    private const int LetterCFrame = 11;
    private const int LetterIFrame = 12;
    private const int LetterLFrame = 13;

    // Arcade-like colors measured from the original screenshot.
    private static readonly Color ArcadeRed = Color.FromHtml("FF5100");
    private static readonly Color ArcadeYellow = Color.FromHtml("FFFF00");
    private static readonly Color ArcadeBlue = Color.FromHtml("00AEFF");

    private Sprite2D _mainSprite = default!;
    private Sprite2D? _overlaySprite;

    /// <summary>
    /// Caches the sprite nodes used to render this collectible.
    /// </summary>
    public override void _Ready()
    {
        _mainSprite = GetNode<Sprite2D>("MainSprite");
        _overlaySprite = GetNodeOrNull<Sprite2D>("OverlaySprite");
    }

    /// <summary>
    /// Displays this collectible as a flower.
    /// </summary>
    public void ShowFlower()
    {
        _mainSprite.Frame = FlowerFrame;
        _mainSprite.Modulate = Colors.White;
        HideOverlaySprite();
    }

    /// <summary>
    /// Displays this collectible as a skull.
    /// </summary>
    public void ShowSkull()
    {
        _mainSprite.Frame = SkullFrame;
        _mainSprite.Modulate = Colors.White;
        HideOverlaySprite();
    }

    /// <summary>
    /// Displays this collectible as a heart using the current color-cycle color.
    /// </summary>
    public void ShowHeart(CollectibleColor color)
    {
        _mainSprite.Frame = HeartRingFrame;
        _mainSprite.Modulate = ToGodotColor(color);

        if (_overlaySprite == null)
            return;

        _overlaySprite.Frame = HeartCenterFrame;
        _overlaySprite.Modulate = Colors.White;
        _overlaySprite.Visible = true;
    }

    /// <summary>
    /// Displays this collectible as a letter using the current color-cycle color.
    /// </summary>
    public void ShowLetter(LetterKind letter, CollectibleColor color)
    {
        _mainSprite.Frame = GetLetterFrame(letter);
        _mainSprite.Modulate = ToGodotColor(color);
        HideOverlaySprite();
    }

    /// <summary>
    /// Compatibility helper for older code paths that still request a red heart.
    /// </summary>
    public void ShowHeartRed()
    {
        ShowHeart(CollectibleColor.Red);
    }

    /// <summary>
    /// Compatibility helper for older code paths that still request a red letter.
    /// </summary>
    public void ShowLetterRed(LetterKind letter)
    {
        ShowLetter(letter, CollectibleColor.Red);
    }

    private void HideOverlaySprite()
    {
        if (_overlaySprite != null)
            _overlaySprite.Visible = false;
    }

    /// <summary>
    /// Returns the spritesheet frame used to display the specified letter.
    /// </summary>
    private static int GetLetterFrame(LetterKind letter)
    {
        return letter switch
        {
            LetterKind.A => LetterAFrame,
            LetterKind.C => LetterCFrame,
            LetterKind.E => LetterEFrame,
            LetterKind.I => LetterIFrame,
            LetterKind.L => LetterLFrame,
            LetterKind.P => LetterPFrame,
            LetterKind.R => LetterRFrame,
            LetterKind.S => LetterSFrame,
            LetterKind.T => LetterTFrame,
            LetterKind.X => LetterXFrame,
            _ => throw new System.ArgumentOutOfRangeException(nameof(letter), letter, null)
        };
    }

    /// <summary>
    /// Converts the logical collectible color to the tint applied to the sprite.
    /// </summary>
    private static Color ToGodotColor(CollectibleColor color)
    {
        return color switch
        {
            CollectibleColor.Blue => ArcadeBlue,
            CollectibleColor.Red => ArcadeRed,
            CollectibleColor.Yellow => ArcadeYellow,
            CollectibleColor.White => Colors.White,
            CollectibleColor.None => Colors.White,
            _ => Colors.White
        };
    }
}
