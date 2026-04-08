using System;

namespace LadyBug.Gameplay.Collectibles;

[Serializable]
public partial class CollectibleLayoutFile
{
    public int Width { get; set; }

    public int Height { get; set; }

    public int[][] Cells { get; set; } = Array.Empty<int[]>();
}