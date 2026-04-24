using System;
using System.Collections.Generic;
using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Runtime owner of the collectible views placed on one active level board.
/// </summary>
/// <remarks>
/// This class currently manages the implemented visual/runtime collectible field:
/// - spawn the base flower layout;
/// - apply the start-of-level special collectible plan on top of that field;
/// - remove one collectible by logical cell when the player consumes it.
///
/// It intentionally still works with <see cref="Collectible"/> scene instances,
/// because the current collectible system is mostly visual/prototype runtime state.
/// Later gameplay rules such as score, colors, words, skull lethality and bonus
/// items can be layered on top without putting all collectible state back into
/// <c>Level</c>.
/// </remarks>
public sealed class CollectibleFieldRuntime
{
    private static readonly PackedScene CollectibleScene =
        GD.Load<PackedScene>("res://scenes/level/Collectible.tscn");

    private readonly Node2D _root;
    private readonly Func<Vector2I, Vector2> _logicalCellToScenePosition;
    private readonly Dictionary<Vector2I, Collectible> _collectiblesByCell = new();

    /// <summary>
    /// Creates the runtime collectible field for one active level.
    /// </summary>
    /// <param name="root">Scene node under which collectible views are spawned.</param>
    /// <param name="logicalCellToScenePosition">
    /// Conversion helper supplied by the owning level.
    /// </param>
    public CollectibleFieldRuntime(
        Node2D root,
        Func<Vector2I, Vector2> logicalCellToScenePosition)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _logicalCellToScenePosition = logicalCellToScenePosition
            ?? throw new ArgumentNullException(nameof(logicalCellToScenePosition));
    }

    /// <summary>
    /// Spawns the base flower layout for the current level.
    /// </summary>
    /// <param name="layout">
    /// Deserialized collectible layout defining which logical cells start with a flower.
    /// </param>
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
    /// Applies the generated start-of-level special collectible plan on top of the
    /// already spawned base flower field.
    /// </summary>
    /// <param name="spawnPlan">Generated placements for letters, hearts, and skulls.</param>
    public void ApplySpecialCollectibleSpawnPlan(CollectibleSpawnPlan spawnPlan)
    {
        if (spawnPlan == null)
            throw new ArgumentNullException(nameof(spawnPlan));

        foreach (CollectiblePlacement placement in spawnPlan.Placements)
        {
            if (!_collectiblesByCell.TryGetValue(placement.Cell, out Collectible? collectible))
            {
                GD.PushWarning(
                    $"No base collectible found at logical cell {placement.Cell} " +
                    $"for special collectible placement.");
                continue;
            }

            ApplyCollectiblePlacement(collectible, placement);
        }
    }

    /// <summary>
    /// Tries to consume the collectible currently present at the given logical cell.
    /// </summary>
    /// <param name="cell">Logical cell to clear.</param>
    /// <returns>True if one collectible was found and removed; otherwise false.</returns>
    public bool TryConsume(Vector2I cell)
    {
        if (!_collectiblesByCell.TryGetValue(cell, out Collectible? collectible))
            return false;

        _collectiblesByCell.Remove(cell);
        collectible.QueueFree();
        return true;
    }

    /// <summary>
    /// Clears all currently tracked collectible views.
    /// </summary>
    public void Clear()
    {
        foreach (Collectible collectible in _collectiblesByCell.Values)
        {
            if (GodotObject.IsInstanceValid(collectible))
                collectible.QueueFree();
        }

        _collectiblesByCell.Clear();
    }

    /// <summary>
    /// Spawns one flower collectible at the given logical maze cell.
    /// </summary>
    private void SpawnFlower(Vector2I cell)
    {
        var collectible = CollectibleScene.Instantiate<Collectible>();
        _root.AddChild(collectible);

        collectible.Position = _logicalCellToScenePosition(cell);
        collectible.ShowFlower();

        _collectiblesByCell[cell] = collectible;
    }

    /// <summary>
    /// Applies one generated collectible placement to one existing collectible view.
    /// </summary>
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
}
