using Godot;

namespace LadyBug.Actors;

/// <summary>
/// Draws optional player debug visuals above the whole playfield.
/// </summary>
/// <remarks>
/// The player coordinates used to be drawn directly by <see cref="PlayerController"/>.
/// That made them share the player's normal canvas draw order, so rotating gates
/// could cover the text.
///
/// This overlay is created by the player controller at runtime, marked as
/// top-level, and rendered with a high absolute Z index. It therefore stays
/// visually above gates and maze objects without changing the player's gameplay
/// transform or sprite draw order.
/// </remarks>
internal partial class PlayerDebugOverlay : Node2D
{
    private const int OverlayZIndex = 4096;
    private const int OriginalDebugYMirror = 0xDD;

    private bool _drawAnchor;
    private bool _drawCoordinates;
    private Vector2 _coordinatesOffset;
    private Vector2I _arcadePixelPos;

    /// <summary>
    /// Configures the overlay so it is drawn independently from its parent order.
    /// </summary>
    public override void _Ready()
    {
        TopLevel = true;
        ZAsRelative = false;
        ZIndex = OverlayZIndex;
        Visible = false;
    }

    /// <summary>
    /// Updates the overlay state from the current player gameplay position.
    /// </summary>
    /// <param name="anchorGlobalPosition">Player gameplay anchor in global scene coordinates.</param>
    /// <param name="arcadePixelPos">Player gameplay position in arcade pixels.</param>
    /// <param name="drawAnchor">Whether to draw the cyan gameplay anchor.</param>
    /// <param name="drawCoordinates">Whether to draw hexadecimal debug coordinates.</param>
    /// <param name="coordinatesOffset">Text offset relative to the player anchor.</param>
    public void UpdateState(
        Vector2 anchorGlobalPosition,
        Vector2I arcadePixelPos,
        bool drawAnchor,
        bool drawCoordinates,
        Vector2 coordinatesOffset)
    {
        GlobalPosition = anchorGlobalPosition;
        _arcadePixelPos = arcadePixelPos;
        _drawAnchor = drawAnchor;
        _drawCoordinates = drawCoordinates;
        _coordinatesOffset = coordinatesOffset;
        Visible = _drawAnchor || _drawCoordinates;

        if (Visible)
            QueueRedraw();
    }

    /// <summary>
    /// Draws the requested debug information in overlay space.
    /// </summary>
    public override void _Draw()
    {
        if (_drawAnchor)
            DrawAnchor();

        if (_drawCoordinates)
            DrawCoordinates();
    }

    private void DrawAnchor()
    {
        DrawLine(new Vector2(-6, 0), new Vector2(6, 0), Colors.Cyan, 1.5f);
        DrawLine(new Vector2(0, -6), new Vector2(0, 6), Colors.Cyan, 1.5f);
        DrawCircle(Vector2.Zero, 2.0f, Colors.Cyan);
    }

    private void DrawCoordinates()
    {
        Font font = ThemeDB.FallbackFont;
        if (font == null)
            return;

        string text = FormatDebugCoordinates(_arcadePixelPos);
        Vector2 p = _coordinatesOffset;

        DrawString(font, p + new Vector2(-1, -1), text, HorizontalAlignment.Left, -1.0f, 16, Colors.Black);
        DrawString(font, p + new Vector2( 0, -1), text, HorizontalAlignment.Left, -1.0f, 16, Colors.Black);
        DrawString(font, p + new Vector2( 1, -1), text, HorizontalAlignment.Left, -1.0f, 16, Colors.Black);
        DrawString(font, p + new Vector2(-1,  0), text, HorizontalAlignment.Left, -1.0f, 16, Colors.Black);
        DrawString(font, p + new Vector2( 1,  0), text, HorizontalAlignment.Left, -1.0f, 16, Colors.Black);
        DrawString(font, p + new Vector2(-1,  1), text, HorizontalAlignment.Left, -1.0f, 16, Colors.Black);
        DrawString(font, p + new Vector2( 0,  1), text, HorizontalAlignment.Left, -1.0f, 16, Colors.Black);
        DrawString(font, p + new Vector2( 1,  1), text, HorizontalAlignment.Left, -1.0f, 16, Colors.Black);
        DrawString(font, p, text, HorizontalAlignment.Left, -1.0f, 16, Colors.White);
    }

    private static string FormatDebugCoordinates(Vector2I arcadePixelPos)
    {
        int originalX = arcadePixelPos.X;
        int originalY = OriginalDebugYMirror - arcadePixelPos.Y;
        return $"X={originalX:X2} Y={originalY:X2}";
    }
}
