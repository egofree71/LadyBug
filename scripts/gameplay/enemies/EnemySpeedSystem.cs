using System;

namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// Computes how many one-pixel enemy sub-steps run during one simulation tick.
/// </summary>
/// <remarks>
/// This reproduces the arcade speed chain (routines 0x40F8 / 0x40CC):
/// one speed byte per frame, computed from a per-level base index table
/// (ROM 0x0EA6) plus a slow in-life time ramp (life seconds >> 4, index capped
/// at 15), looked up in a 16-entry speed table (ROM 0x0ED8 factory-default /
/// 0x0EE8 hard side). High nibble = whole pixels per frame; low-nibble bits
/// select a fractional add of 0x33 (~0.2), 0x80 (0.5) or 0xCC (~0.8) into one
/// GLOBAL 8-bit accumulator (RAM 0x61B5) shared by all four enemies; an
/// accumulator carry grants one extra pixel that frame. Encoded speeds:
/// 0x10=1.0, 0x12=1.2, 0x15=1.5, 0x18=1.8, 0x20=2.0 pixels per frame.
/// </remarks>
public sealed class EnemySpeedSystem
{
    // ROM 0x0EA6: visible level (1-based, capped) -> base speed index.
    private static readonly byte[] BaseIndexTable =
    {
        0, 0, 2, 4, 1, 3, 5, 6, 8, 5, 7, 9, 10, 11, 8, 12, 13,
        14, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15
    };

    // ROM 0x0ED8: factory-default (easy-side) speed bytes.
    private static readonly byte[] SpeedEasyTable =
    {
        0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x12, 0x12,
        0x12, 0x12, 0x12, 0x12, 0x15, 0x15, 0x15, 0x18
    };

    // ROM 0x0EE8: hard-side speed bytes (up to 2.0 px/frame).
    private static readonly byte[] SpeedHardTable =
    {
        0x10, 0x10, 0x10, 0x12, 0x12, 0x12, 0x15, 0x15,
        0x15, 0x15, 0x18, 0x18, 0x18, 0x18, 0x18, 0x20
    };

    /// <summary>
    /// Selects the hard-difficulty speed table (arcade DIP hard side).
    /// </summary>
    /// <remarks>
    /// Default false = factory-default arcade switches, matching the traces used
    /// for validation (0x61C3 == 0x10 measured at level-1 start).
    /// </remarks>
    public static bool UseHardSpeedTable { get; set; }

    // Global fractional accumulator shared by all enemies (arcade RAM 0x61B5).
    private int _accumulator;

    /// <summary>
    /// Resets the shared fractional accumulator.
    /// </summary>
    /// <remarks>
    /// Called on each board attempt. The arcade per-life init clears the enemy
    /// work RAM block; an explicit reset keeps runs reproducible either way
    /// (worst case, the phase of one fractional pixel differs).
    /// </remarks>
    public void Reset()
    {
        _accumulator = 0;
    }

    /// <summary>
    /// Computes the number of one-pixel sub-steps for the current tick.
    /// </summary>
    /// <param name="levelNumber">Visible level number (1-based).</param>
    /// <param name="lifeSecondsCapped">Elapsed board-attempt seconds, arcade-capped.</param>
    /// <returns>Whole pixels every active enemy advances this tick.</returns>
    public int ComputeStepsForThisTick(int levelNumber, int lifeSecondsCapped)
    {
        int levelIndex = Math.Min(Math.Max(levelNumber, 1), BaseIndexTable.Length) - 1;
        int speedIndex = Math.Min(BaseIndexTable[levelIndex] + (lifeSecondsCapped >> 4), 15);
        byte[] table = UseHardSpeedTable ? SpeedHardTable : SpeedEasyTable;
        byte speed = table[speedIndex];

        int steps = speed >> 4;
        int fractionalAdd = (speed & 0x02) != 0 ? 0x33
            : (speed & 0x04) != 0 ? 0x80
            : (speed & 0x08) != 0 ? 0xCC
            : 0;

        if (fractionalAdd != 0)
        {
            _accumulator += fractionalAdd;

            if (_accumulator > 0xFF)
            {
                _accumulator &= 0xFF;
                steps++;
            }
        }

        return steps;
    }
}
