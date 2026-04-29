using System.Collections.Generic;
using Godot;
using LadyBug.Gameplay.Gates;
using LadyBug.Gameplay.Maze;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Runtime enemy navigation grid: allowed directions plus BFS guidance.
/// </summary>
/// <remarks>
/// The arcade stores allowed movement and BFS guidance in the same logical map.
/// This class keeps the same split conceptually: allowed directions are rebuilt
/// from the static maze and current gates, then BFS writes one guidance direction
/// toward Lady Bug into each reachable cell.
/// </remarks>
public sealed class EnemyNavigationGrid
{
    // Fixed scan order used by allowed-direction rebuild and BFS propagation.
    private static readonly MonsterDir[] DirectionOrder =
    {
        MonsterDir.Left,
        MonsterDir.Up,
        MonsterDir.Right,
        MonsterDir.Down
    };

    // Mutable logical cells containing current allowed directions and BFS guidance.
    private readonly EnemyNavigationCell[,] _cells;

    /// <summary>
    /// Creates an empty enemy-navigation grid with the given dimensions.
    /// </summary>
    /// <param name="width">Number of logical cells horizontally.</param>
    /// <param name="height">Number of logical cells vertically.</param>
    public EnemyNavigationGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new EnemyNavigationCell[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                _cells[x, y] = new EnemyNavigationCell();
        }
    }

    /// <summary>
    /// Gets the number of logical cells horizontally.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the number of logical cells vertically.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Returns whether a logical cell lies inside this navigation grid.
    /// </summary>
    /// <param name="cell">Cell to test.</param>
    public bool IsInside(Vector2I cell)
    {
        return cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;
    }

    /// <summary>
    /// Gets the navigation cell at the given logical position.
    /// </summary>
    /// <param name="cell">Cell coordinates to read.</param>
    public EnemyNavigationCell GetCell(Vector2I cell)
    {
        return _cells[cell.X, cell.Y];
    }

    /// <summary>
    /// Returns whether an enemy can currently leave the given cell in the requested direction.
    /// </summary>
    /// <param name="cell">Source logical cell.</param>
    /// <param name="dir">Enemy direction to test.</param>
    public bool IsDirectionAllowed(Vector2I cell, MonsterDir dir)
    {
        if (!IsInside(cell) || dir == MonsterDir.None)
            return false;

        return (GetCell(cell).AllowedDirections & dir) != 0;
    }

    /// <summary>
    /// Gets the current BFS guidance direction for one logical cell.
    /// </summary>
    /// <param name="cell">Logical cell to read.</param>
    /// <returns>The direction leading toward Lady Bug, or <see cref="MonsterDir.None"/>.</returns>
    public MonsterDir GetBfsDirection(Vector2I cell)
    {
        if (!IsInside(cell))
            return MonsterDir.None;

        return GetCell(cell).BfsDirection;
    }

    /// <summary>
    /// Rebuilds allowed enemy directions from the static maze and current gate states.
    /// </summary>
    /// <remarks>
    /// This should be called before BFS generation because pivoting gates can change
    /// which neighbor cells are reachable during the current tick.
    /// </remarks>
    public void RebuildAllowedDirections(MazeGrid mazeGrid, GateSystem gateSystem)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Vector2I cell = new(x, y);
                MonsterDir allowed = MonsterDir.None;

                foreach (MonsterDir dir in DirectionOrder)
                {
                    Vector2I vector = dir.ToVector();

                    if (!mazeGrid.CanMove(cell, vector))
                        continue;

                    if (IsBlockedByGateBoundary(cell, dir, gateSystem))
                        continue;

                    allowed |= dir;
                }

                _cells[x, y].AllowedDirections = allowed;
                _cells[x, y].BfsDirection = MonsterDir.None;
            }
        }
    }

    /// <summary>
    /// Builds a parent-direction BFS map outward from the player's current cell.
    /// </summary>
    /// <param name="playerCell">Logical cell currently occupied by Lady Bug.</param>
    /// <remarks>
    /// Each reached cell receives the direction an enemy should take from that cell
    /// to move one step closer to the player through the current maze/gate topology.
    /// </remarks>
    public void BuildBfsGuidanceFromPlayer(Vector2I playerCell)
    {
        if (!IsInside(playerCell))
            return;

        bool[,] visited = new bool[Width, Height];
        Queue<Vector2I> queue = new();

        visited[playerCell.X, playerCell.Y] = true;
        queue.Enqueue(playerCell);

        while (queue.Count > 0)
        {
            Vector2I current = queue.Dequeue();

            foreach (MonsterDir dir in DirectionOrder)
            {
                if (!IsDirectionAllowed(current, dir))
                    continue;

                Vector2I next = current + dir.ToVector();
                if (!IsInside(next) || visited[next.X, next.Y])
                    continue;

                MonsterDir returnDir = dir.Opposite();
                if (!IsDirectionAllowed(next, returnDir))
                    continue;

                visited[next.X, next.Y] = true;
                GetCell(next).BfsDirection = returnDir;
                queue.Enqueue(next);
            }
        }
    }

    /// <summary>
    /// Checks whether a gate on the crossed cell boundary blocks a direction.
    /// </summary>
    private static bool IsBlockedByGateBoundary(
        Vector2I cell,
        MonsterDir dir,
        GateSystem gateSystem)
    {
        Vector2I vector = dir.ToVector();

        if (vector.X != 0)
        {
            int boundaryX = vector.X > 0 ? cell.X + 1 : cell.X;
            return GateAtPivotBlocks(gateSystem, new Vector2I(boundaryX, cell.Y), vector) ||
                   GateAtPivotBlocks(gateSystem, new Vector2I(boundaryX, cell.Y + 1), vector);
        }

        if (vector.Y != 0)
        {
            int boundaryY = vector.Y > 0 ? cell.Y + 1 : cell.Y;
            return GateAtPivotBlocks(gateSystem, new Vector2I(cell.X, boundaryY), vector) ||
                   GateAtPivotBlocks(gateSystem, new Vector2I(cell.X + 1, boundaryY), vector);
        }

        return false;
    }

    /// <summary>
    /// Returns whether a gate at one pivot exists and blocks movement on the requested axis.
    /// </summary>
    private static bool GateAtPivotBlocks(
        GateSystem gateSystem,
        Vector2I pivot,
        Vector2I movement)
    {
        return gateSystem.TryGetGateByPivot(pivot, out RotatingGateRuntimeState gate) &&
               gate.BlocksMovement(movement);
    }
}
