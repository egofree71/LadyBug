// scripts/level/Collectible.cs

using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Runtime visual view for a single collectible instance.
/// </summary>
/// <remarks>
/// This node renders the collectible states used by the current level setup:
/// flowers, skulls, hearts and letters.
///
/// Hearts use two sprite layers: the main sprite is the color-cycled outer ring,
/// while the overlay sprite keeps the inner heart color from the spritesheet.
/// Letters use only the main sprite and are tinted by the same color cycle.
/// </remarks>
public partial class Collectible : Node2D
{
    private const int SkullFrame = 0;
    private const int FlowerFrame = 1;
    private const int HeartRingFrame = 2;
    private const int HeartCenterFrame = 3;

    // Current project spritesheet mapping. Do not change unless the spritesheet is changed too.
    private const int LetterAFrame = 4;
    private const int LetterCFrame = 5;
    private const int LetterEFrame = 6;
    private const int LetterIFrame = 7;
    private const int LetterLFrame = 8;
    private const int LetterPFrame = 9;
    private const int LetterRFrame = 10;
    private const int LetterSFrame = 11;
    private const int LetterTFrame = 12;
    private const int LetterXFrame = 13;

    // Arcade-like tints measured from the original game screenshot.
    private static readonly Color ArcadeRed = new(1.0f, 81.0f / 255.0f, 0.0f);      // #FF5100
    private static readonly Color ArcadeYellow = new(1.0f, 1.0f, 0.0f);             // #FFFF00
    private static readonly Color ArcadeBlue = new(0.0f, 174.0f / 255.0f, 1.0f);    // #00AEFF

    private Sprite2D _mainSprite = null!;
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

        if (_overlaySprite != null)
            _overlaySprite.Visible = false;
    }

    /// <summary>
    /// Displays this collectible as a skull.
    /// </summary>
    public void ShowSkull()
    {
        _mainSprite.Frame = SkullFrame;
        _mainSprite.Modulate = Colors.White;

        if (_overlaySprite != null)
            _overlaySprite.Visible = false;
    }

    /// <summary>
    /// Displays this collectible as a heart using the current global collectible color.
    /// </summary>
    public void ShowHeart(CollectibleColor color)
    {
        _mainSprite.Frame = HeartRingFrame;
        _mainSprite.Modulate = ToGodotColor(color);

        if (_overlaySprite != null)
        {
            _overlaySprite.Frame = HeartCenterFrame;
            _overlaySprite.Modulate = Colors.White;
            _overlaySprite.Visible = true;
        }
    }

    /// <summary>
    /// Displays this collectible as a letter using the current global collectible color.
    /// </summary>
    public void ShowLetter(LetterKind letter, CollectibleColor color)
    {
        _mainSprite.Frame = GetLetterFrame(letter);
        _mainSprite.Modulate = ToGodotColor(color);

        if (_overlaySprite != null)
            _overlaySprite.Visible = false;
    }

    /// <summary>
    /// Backward-compatible helper for older call sites.
    /// </summary>
    public void ShowHeartRed()
    {
        ShowHeart(CollectibleColor.Red);
    }

    /// <summary>
    /// Backward-compatible helper for older call sites.
    /// </summary>
    public void ShowLetterRed(LetterKind letter)
    {
        ShowLetter(letter, CollectibleColor.Red);
    }

    /// <summary>
    /// Converts a gameplay collectible color into the tint used for rendering.
    /// </summary>
    private static Color ToGodotColor(CollectibleColor color)
    {
        return color switch
        {
            CollectibleColor.Red => ArcadeRed,
            CollectibleColor.Yellow => ArcadeYellow,
            CollectibleColor.Blue => ArcadeBlue,
            CollectibleColor.White => Colors.White,
            CollectibleColor.None => Colors.White,
            _ => Colors.White
        };
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
}
