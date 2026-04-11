// scripts/level/Collectible.cs

using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Runtime visual view for a single collectible instance.
/// </summary>
/// <remarks>
/// This node renders the currently implemented collectible states used by the
/// prototype level setup:
/// - flower
/// - skull
/// - red heart
/// - red letter
///
/// The main sprite displays the shared collectible spritesheet frame.
/// The optional overlay sprite is currently used by hearts so the inner shape
/// can stay fixed while the outer ring is tinted.
/// </remarks>
public partial class Collectible : Node2D
{
    private const int SkullFrame = 0;
    private const int FlowerFrame = 1;
    private const int HeartRingFrame = 2;
    private const int HeartCenterFrame = 3;
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

    private Sprite2D _mainSprite = default!;
    private Sprite2D _overlaySprite = default!;

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
        {
            _overlaySprite.Visible = false;
        }
    }

    /// <summary>
    /// Displays this collectible as a skull.
    /// </summary>
    public void ShowSkull()
    {
        _mainSprite.Frame = SkullFrame;
        _mainSprite.Modulate = Colors.White;

        if (_overlaySprite != null)
        {
            _overlaySprite.Visible = false;
        }
    }

    /// <summary>
    /// Displays this collectible as a red heart.
    /// </summary>
    public void ShowHeartRed()
    {
        _mainSprite.Frame = HeartRingFrame;
        _mainSprite.Modulate = Colors.Red;

        if (_overlaySprite != null)
        {
            _overlaySprite.Frame = HeartCenterFrame;
            _overlaySprite.Modulate = Colors.White;
            _overlaySprite.Visible = true;
        }
    }

    /// <summary>
    /// Displays this collectible as a red letter.
    /// </summary>
    /// <param name="letter">The letter to render.</param>
    public void ShowLetterRed(LetterKind letter)
    {
        _mainSprite.Frame = GetLetterFrame(letter);
        _mainSprite.Modulate = Colors.Red;

        if (_overlaySprite != null)
        {
            _overlaySprite.Visible = false;
        }
    }

    /// <summary>
    /// Returns the spritesheet frame used to display the specified letter.
    /// </summary>
    /// <param name="letter">The letter to convert to a frame index.</param>
    /// <returns>The frame index in the collectible spritesheet.</returns>
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