using System;
using System.Collections.Generic;
using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Runtime owner of the collectible views placed on one active level board.
///
/// This version stores semantic collectible state next to each visual node.
/// That lets gameplay systems know whether the player consumed a flower,
/// heart, letter or skull, without querying sprite frames.
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

            ApplyCollectiblePlacement(runtimeCollectible.View, placement);
        }
    }

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

    private static void ApplyCollectiblePlacement(
        Collectible collectible,
        CollectiblePlacement placement)
    {
        switch (placement.Kind)
        {
            case CollectibleKind.Heart:
                collectible.ShowHeartRed();
                break;

            case CollectibleKind.Letter:
                collectible.ShowLetterRed(placement.Letter);
                break;

            case CollectibleKind.Skull:
                collectible.ShowSkull();
                break;

            case CollectibleKind.Flower:
            default:
                collectible.ShowFlower();
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
