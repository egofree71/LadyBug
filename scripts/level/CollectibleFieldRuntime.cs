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
    // Runtime scene used for every visual collectible instance spawned on the board.
    private static readonly PackedScene CollectibleScene =
        GD.Load<PackedScene>("res://scenes/level/Collectible.tscn");

    // Scene node that owns the spawned collectible instances.
    private readonly Node2D _root;

    // Coordinate conversion supplied by Level so this runtime stays independent from scene layout.
    private readonly Func<Vector2I, Vector2> _logicalCellToScenePosition;

    // Main lookup from logical maze cell to the currently active collectible at that cell.
    private readonly Dictionary<Vector2I, RuntimeCollectible> _collectiblesByCell = new();

    /// <summary>
    /// Creates a collectible field runtime under the given scene root.
    /// </summary>
    /// <param name="root">Scene node used as parent for spawned collectible views.</param>
    /// <param name="logicalCellToScenePosition">Converter from logical cells to Godot scene coordinates.</param>
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
    /// Returns whether any level-completion collectible is still present on the board.
    /// </summary>
    /// <remarks>
    /// Flowers, hearts and letters must all be collected before the next level can start.
    /// Skulls do not block level completion.
    /// </remarks>
    public bool HasRemainingProgressCollectibles()
    {
        foreach (RuntimeCollectible runtimeCollectible in _collectiblesByCell.Values)
        {
            if (runtimeCollectible.Kind != CollectibleKind.Skull)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Counts the remaining flowers, hearts and letters still present on the board.
    /// </summary>
    public int CountRemainingProgressCollectibles()
    {
        int count = 0;

        foreach (RuntimeCollectible runtimeCollectible in _collectiblesByCell.Values)
        {
            if (runtimeCollectible.Kind != CollectibleKind.Skull)
                count++;
        }

        return count;
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
    /// Consumes the skull at the given logical cell when an enemy crosses it.
    /// </summary>
    /// <remarks>
    /// Player pickups still use <see cref="TryConsume"/> so the existing scoring,
    /// popup, and player-death path remains unchanged. Enemies only need the
    /// special skull case: the skull disappears and the enemy returns to the lair.
    /// </remarks>
    /// <param name="cell">Logical cell to test.</param>
    /// <returns><see langword="true"/> if a skull was found and removed.</returns>
    public bool TryConsumeSkullAt(Vector2I cell)
    {
        if (!_collectiblesByCell.TryGetValue(cell, out RuntimeCollectible? runtimeCollectible))
            return false;

        if (runtimeCollectible.Kind != CollectibleKind.Skull)
            return false;

        _collectiblesByCell.Remove(cell);

        if (GodotObject.IsInstanceValid(runtimeCollectible.View))
            runtimeCollectible.View.QueueFree();

        return true;
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

    /// <summary>
    /// Creates one flower collectible view and registers its semantic runtime state.
    /// </summary>
    /// <param name="cell">Logical maze cell where the flower should appear.</param>
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

    /// <summary>
    /// Refreshes the visual representation of one collectible from its semantic state.
    /// </summary>
    /// <remarks>
    /// The runtime state is the source of truth. The view only mirrors that state by
    /// choosing the correct sprite frame, letter, and color modulation.
    /// </remarks>
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

    /// <summary>
    /// Small semantic wrapper paired with a spawned collectible view.
    /// </summary>
    /// <remarks>
    /// Keeping kind, color and letter here avoids inferring gameplay meaning from
    /// visual sprite frames. That matters because hearts and letters share a global
    /// color cycle while flowers and skulls remain visually fixed.
    /// </remarks>
    private sealed class RuntimeCollectible
    {
        /// <summary>
        /// Creates one runtime collectible state object.
        /// </summary>
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

        /// <summary>
        /// Visual node currently representing this collectible.
        /// </summary>
        public Collectible View { get; }

        /// <summary>
        /// Gameplay category of the collectible.
        /// </summary>
        public CollectibleKind Kind { get; set; }

        /// <summary>
        /// Current gameplay color used by hearts and letters.
        /// </summary>
        public CollectibleColor Color { get; set; }

        /// <summary>
        /// Letter identity when <see cref="Kind"/> is <see cref="CollectibleKind.Letter"/>.
        /// </summary>
        public LetterKind Letter { get; set; }
    }
}
