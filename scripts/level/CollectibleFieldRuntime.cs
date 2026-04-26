using System;
using System.Collections.Generic;
using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Runtime owner of the collectible views placed on one active level board.
///
/// This class stores semantic collectible state next to each visual node so
/// gameplay systems can know whether the player consumed a flower, heart,
/// letter or skull without querying sprite frames.
/// </summary>
public sealed class CollectibleFieldRuntime
{
    private static readonly PackedScene CollectibleScene =
        GD.Load<PackedScene>("res://scenes/level/Collectible.tscn");

    private readonly Node2D _root;
    private readonly Func<Vector2I, Vector2> _logicalCellToScenePosition;
    private readonly Dictionary<Vector2I, RuntimeCollectible> _collectiblesByCell = new();

    public CollectibleFieldRuntime(
        Node2D root,
        Func<Vector2I, Vector2> logicalCellToScenePosition)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _logicalCellToScenePosition = logicalCellToScenePosition
            ?? throw new ArgumentNullException(nameof(logicalCellToScenePosition));
    }

    /// <summary>
    /// Spawns the base flower field from the serialized logical layout.
    /// </summary>
    public void SpawnInitialFlowers(CollectibleLayoutFile layout)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        Clear();

        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                if (layout.Cells[y][x] != 1)
                    continue;

                SpawnFlower(new Vector2I(x, y));
            }
        }
    }

    /// <summary>
    /// Replaces selected base flowers with level-start hearts, letters and skulls.
    /// </summary>
    public void ApplySpecialCollectibleSpawnPlan(CollectibleSpawnPlan spawnPlan)
    {
        if (spawnPlan == null)
            throw new ArgumentNullException(nameof(spawnPlan));

        foreach (CollectiblePlacement placement in spawnPlan.Placements)
        {
            if (!_collectiblesByCell.TryGetValue(placement.Cell, out RuntimeCollectible? runtimeCollectible))
            {
                GD.PushWarning(
                    $"No base collectible found at logical cell {placement.Cell} " +
                    $"for special collectible placement.");
                continue;
            }

            runtimeCollectible.Kind = placement.Kind;
            runtimeCollectible.Color = placement.Color;
            runtimeCollectible.Letter = placement.Letter;

            ApplyCollectibleVisual(runtimeCollectible);
        }
    }

    /// <summary>
    /// Applies the current global color cycle to all active hearts and letters.
    /// </summary>
    public void ApplyColorCycle(CollectibleColor color)
    {
        foreach (RuntimeCollectible runtimeCollectible in _collectiblesByCell.Values)
        {
            if (runtimeCollectible.Kind != CollectibleKind.Heart &&
                runtimeCollectible.Kind != CollectibleKind.Letter)
            {
                continue;
            }

            runtimeCollectible.Color = color;
            ApplyCollectibleVisual(runtimeCollectible);
        }
    }

    /// <summary>
    /// Removes the collectible at the given logical cell and returns its semantic result.
    /// </summary>
    /// <remarks>
    /// The caller receives the collectible kind, current color-cycle color and letter kind
    /// before the visual node is freed. This lets scoring and popup logic stay independent
    /// from sprite frames.
    /// </remarks>
    public CollectiblePickupResult TryConsume(Vector2I cell)
    {
        if (!_collectiblesByCell.TryGetValue(cell, out RuntimeCollectible? runtimeCollectible))
            return CollectiblePickupResult.None;

        _collectiblesByCell.Remove(cell);

        if (GodotObject.IsInstanceValid(runtimeCollectible.View))
            runtimeCollectible.View.QueueFree();

        return CollectiblePickupResult.Collected(
            runtimeCollectible.Kind,
            runtimeCollectible.Color,
            runtimeCollectible.Letter);
    }

    /// <summary>
    /// Removes all active collectible views and clears the runtime lookup.
    /// </summary>
    public void Clear()
    {
        foreach (RuntimeCollectible runtimeCollectible in _collectiblesByCell.Values)
        {
            if (GodotObject.IsInstanceValid(runtimeCollectible.View))
                runtimeCollectible.View.QueueFree();
        }

        _collectiblesByCell.Clear();
    }

    private void SpawnFlower(Vector2I cell)
    {
        Collectible collectible = CollectibleScene.Instantiate<Collectible>();
        _root.AddChild(collectible);
        collectible.Position = _logicalCellToScenePosition(cell);
        collectible.ShowFlower();

        _collectiblesByCell[cell] = new RuntimeCollectible(
            collectible,
            CollectibleKind.Flower,
            CollectibleColor.None,
            LetterKind.None);
    }

    private static void ApplyCollectibleVisual(RuntimeCollectible runtimeCollectible)
    {
        if (!GodotObject.IsInstanceValid(runtimeCollectible.View))
            return;

        switch (runtimeCollectible.Kind)
        {
            case CollectibleKind.Heart:
                runtimeCollectible.View.ShowHeart(runtimeCollectible.Color);
                break;

            case CollectibleKind.Letter:
                runtimeCollectible.View.ShowLetter(
                    runtimeCollectible.Letter,
                    runtimeCollectible.Color);
                break;

            case CollectibleKind.Skull:
                runtimeCollectible.View.ShowSkull();
                break;

            case CollectibleKind.Flower:
            default:
                runtimeCollectible.View.ShowFlower();
                break;
        }
    }

    private sealed class RuntimeCollectible
    {
        public RuntimeCollectible(
            Collectible view,
            CollectibleKind kind,
            CollectibleColor color,
            LetterKind letter)
        {
            View = view;
            Kind = kind;
            Color = color;
            Letter = letter;
        }

        public Collectible View { get; }
        public CollectibleKind Kind { get; set; }
        public CollectibleColor Color { get; set; }
        public LetterKind Letter { get; set; }
    }
}
