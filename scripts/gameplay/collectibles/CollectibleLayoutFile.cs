using System;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Represents the serialized JSON structure of the base collectible layout.
/// </summary>
/// <remarks>
/// This file currently stores only the initial flower placement mask.
///
/// A value of:
/// - 1 means one flower is present in that logical cell
/// - 0 means the cell starts empty
///
/// Runtime replacements such as hearts, letters, and skulls are not stored
/// here and should be applied later by gameplay logic.
/// </remarks>
[Serializable]
public partial class CollectibleLayoutFile
{
    /// <summary>
    /// Gets or sets the logical layout width in cells.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the logical layout height in cells.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the 2D collectible layout mask.
    /// </summary>
    /// <remarks>
    /// The first index is the logical row (Y), and the second index is the
    /// logical column (X).
    /// </remarks>
    public int[][] Cells { get; set; } = Array.Empty<int[]>();
}