using Godot;
using LadyBug.Gameplay.Collectibles;

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

    public override void _Ready()
    {
        _mainSprite = GetNode<Sprite2D>("MainSprite");
        _overlaySprite = GetNodeOrNull<Sprite2D>("OverlaySprite");
    }

    public void ShowFlower()
    {
        _mainSprite.Frame = FlowerFrame;
        _mainSprite.Modulate = Colors.White;

        if (_overlaySprite != null)
        {
            _overlaySprite.Visible = false;
        }
    }

    public void ShowSkull()
    {
        _mainSprite.Frame = SkullFrame;
        _mainSprite.Modulate = Colors.White;

        if (_overlaySprite != null)
        {
            _overlaySprite.Visible = false;
        }
    }

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

    public void ShowLetterRed(LetterKind letter)
    {
        _mainSprite.Frame = GetLetterFrame(letter);
        _mainSprite.Modulate = Colors.Red;

        if (_overlaySprite != null)
        {
            _overlaySprite.Visible = false;
        }
    }

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