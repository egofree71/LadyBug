using System;
using Godot;
using LadyBug.Gameplay.Maze;

namespace LadyBug.Actors;

/// <summary>
/// Holds the player turn-lane maps used by <see cref="PlayerTurnWindowResolver"/>.
/// </summary>
/// <remarks>
/// The previous implementation stored reverse-engineered lane masks directly in
/// <see cref="PlayerTurnWindowResolver"/>. This version generates equivalent
/// turn-lane candidates from the logical maze loaded from <c>data/maze.json</c>.
///
/// The generated maps still use the same compact bit-mask representation as the
/// arcade-inspired resolver:
/// - vertical-turn maps are selected by row band and contain enabled X lanes;
/// - horizontal-turn maps are selected by column band and contain enabled Y lanes;
/// - Y lanes are stored in the original mirrored vertical order expected by the
///   resolver.
///
/// A logical cell is considered a turn-lane candidate when it has at least one
/// horizontal opening and at least one vertical opening. The final requested
/// direction is still validated later by the normal playfield collision path,
/// so this map only answers "is this an intersection-like lane?" rather than
/// "is the exact requested direction allowed?".
/// </remarks>
internal sealed class PlayerTurnWindowMaps
{
    /// <summary>
    /// First original-screen row band represented by the vertical-turn map.
    /// </summary>
    private const int VerticalBandOrigin = 0x36;

    /// <summary>
    /// First arcade X lane represented by bit 0 of the vertical-turn mask.
    /// </summary>
    private const int VerticalLaneOrigin = 0x08;

    /// <summary>
    /// First arcade X column band represented by the horizontal-turn map.
    /// </summary>
    private const int HorizontalBandOrigin = 0x08;

    /// <summary>
    /// First original-screen Y lane represented by bit 0 of the horizontal-turn mask.
    /// </summary>
    private const int HorizontalLaneOrigin = 0x36;

    /// <summary>
    /// Distance between two adjacent logical-cell lanes in arcade pixels.
    /// </summary>
    private const int LaneSpacing = 0x10;

    /// <summary>
    /// Gets the map used while moving left/right and requesting up/down.
    /// </summary>
    public TurnWindowMap VerticalTurnWindowsByRow { get; }

    /// <summary>
    /// Gets the map used while moving up/down and requesting left/right.
    /// </summary>
    public TurnWindowMap HorizontalTurnWindowsByColumn { get; }

    private PlayerTurnWindowMaps(
        TurnWindowMap verticalTurnWindowsByRow,
        TurnWindowMap horizontalTurnWindowsByColumn)
    {
        VerticalTurnWindowsByRow = verticalTurnWindowsByRow;
        HorizontalTurnWindowsByColumn = horizontalTurnWindowsByColumn;
    }

    /// <summary>
    /// Builds player turn-window maps from the current logical maze.
    /// </summary>
    /// <param name="mazeGrid">Runtime static maze loaded from <c>data/maze.json</c>.</param>
    /// <returns>Generated turn-window maps for the player movement resolver.</returns>
    public static PlayerTurnWindowMaps FromMazeGrid(MazeGrid mazeGrid)
    {
        if (mazeGrid == null)
            throw new ArgumentNullException(nameof(mazeGrid));

        if (mazeGrid.Width > 16)
            throw new ArgumentOutOfRangeException(nameof(mazeGrid), "Turn-window masks support at most 16 columns.");

        if (mazeGrid.Height > 16)
            throw new ArgumentOutOfRangeException(nameof(mazeGrid), "Turn-window masks support at most 16 rows.");

        return new PlayerTurnWindowMaps(
            new TurnWindowMap(
                masksByBand: BuildVerticalTurnMasksByOriginalRowBand(mazeGrid),
                bandOrigin: VerticalBandOrigin,
                laneOrigin: VerticalLaneOrigin,
                laneSpacing: LaneSpacing),
            new TurnWindowMap(
                masksByBand: BuildHorizontalTurnMasksByColumnBand(mazeGrid),
                bandOrigin: HorizontalBandOrigin,
                laneOrigin: HorizontalLaneOrigin,
                laneSpacing: LaneSpacing));
    }

    /// <summary>
    /// Builds row-selected masks for vertical turns: horizontal movement requesting up/down.
    /// </summary>
    private static ushort[] BuildVerticalTurnMasksByOriginalRowBand(MazeGrid mazeGrid)
    {
        ushort[] masks = new ushort[mazeGrid.Height];

        for (int y = 0; y < mazeGrid.Height; y++)
        {
            int originalRowBandIndex = mazeGrid.Height - 1 - y;

            for (int x = 0; x < mazeGrid.Width; x++)
            {
                Vector2I cell = new(x, y);
                if (!IsTurnCandidateCell(mazeGrid, cell))
                    continue;

                masks[originalRowBandIndex] |= (ushort)(1 << x);
            }
        }

        return masks;
    }

    /// <summary>
    /// Builds column-selected masks for horizontal turns: vertical movement requesting left/right.
    /// </summary>
    private static ushort[] BuildHorizontalTurnMasksByColumnBand(MazeGrid mazeGrid)
    {
        ushort[] masks = new ushort[mazeGrid.Width];

        for (int x = 0; x < mazeGrid.Width; x++)
        {
            for (int y = 0; y < mazeGrid.Height; y++)
            {
                Vector2I cell = new(x, y);
                if (!IsTurnCandidateCell(mazeGrid, cell))
                    continue;

                int originalScreenYBit = mazeGrid.Height - 1 - y;
                masks[x] |= (ushort)(1 << originalScreenYBit);
            }
        }

        return masks;
    }

    /// <summary>
    /// Returns true when the cell behaves like an intersection: it has at least
    /// one horizontal opening and at least one vertical opening.
    /// </summary>
    private static bool IsTurnCandidateCell(MazeGrid mazeGrid, Vector2I cell)
    {
        return HasHorizontalOpening(mazeGrid, cell) && HasVerticalOpening(mazeGrid, cell);
    }

    private static bool HasHorizontalOpening(MazeGrid mazeGrid, Vector2I cell)
    {
        return mazeGrid.CanMove(cell, Vector2I.Left) ||
               mazeGrid.CanMove(cell, Vector2I.Right);
    }

    private static bool HasVerticalOpening(MazeGrid mazeGrid, Vector2I cell)
    {
        return mazeGrid.CanMove(cell, Vector2I.Up) ||
               mazeGrid.CanMove(cell, Vector2I.Down);
    }

    /// <summary>
    /// Pair of enabled turn-lane coordinates surrounding the actor coordinate.
    /// </summary>
    /// <param name="NextLane">First valid lane at or after the actor coordinate, wrapping to the first lane when needed.</param>
    /// <param name="PreviousLane">Last valid lane before the actor coordinate, wrapping to the last lane when needed.</param>
    public readonly record struct TurnLanePair(int NextLane, int PreviousLane);

    /// <summary>
    /// Compact representation of one family of generated turn windows.
    /// </summary>
    /// <remarks>
    /// A turn-window map is selected in two stages:
    /// - the current row or column chooses one lane mask;
    /// - each set bit in that mask enables one lane coordinate.
    /// </remarks>
    public readonly struct TurnWindowMap
    {
        private const int CoordinateByteMask = 0xFF;

        private readonly ushort[] _masksByBand;
        private readonly int _bandOrigin;
        private readonly int _laneOrigin;
        private readonly int _laneSpacing;

        public TurnWindowMap(ushort[] masksByBand, int bandOrigin, int laneOrigin, int laneSpacing)
        {
            _masksByBand = masksByBand ?? throw new ArgumentNullException(nameof(masksByBand));
            _bandOrigin = bandOrigin;
            _laneOrigin = laneOrigin;
            _laneSpacing = laneSpacing;
        }

        /// <summary>
        /// Gets the lane mask for a given row or column coordinate.
        /// </summary>
        public ushort GetMaskForBand(int coordinate)
        {
            int index = ToWrappedCoordinate(coordinate - _bandOrigin) >> 4;
            index = Math.Clamp(index, 0, _masksByBand.Length - 1);
            return _masksByBand[index];
        }

        /// <summary>
        /// Finds the valid enabled lanes immediately around the actor coordinate.
        /// </summary>
        public TurnLanePair FindSurroundingLanes(ushort mask, int actorCoordinate)
        {
            int? previousLane = null;
            int? nextLane = null;
            int lastEnabledLane = _laneOrigin;
            bool foundAnyLane = false;

            for (int bit = 0; bit < 16; bit++)
            {
                if (((mask >> bit) & 1) == 0)
                    continue;

                int laneCoordinate = ToWrappedCoordinate(_laneOrigin + bit * _laneSpacing);
                lastEnabledLane = laneCoordinate;
                foundAnyLane = true;

                if (laneCoordinate < actorCoordinate)
                {
                    previousLane = laneCoordinate;
                }
                else if (nextLane == null)
                {
                    nextLane = laneCoordinate;
                }
            }

            if (!foundAnyLane)
                throw new InvalidOperationException($"Turn-window mask is empty: 0x{mask:X4}");

            previousLane ??= lastEnabledLane;
            nextLane ??= ToWrappedCoordinate(_laneOrigin);

            return new TurnLanePair(nextLane.Value, previousLane.Value);
        }

        private static int ToWrappedCoordinate(int value)
        {
            return value & CoordinateByteMask;
        }
    }
}
