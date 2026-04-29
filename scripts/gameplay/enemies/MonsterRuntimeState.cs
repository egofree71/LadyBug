namespace LadyBug.Gameplay.Enemies;

/// <summary>
/// High-level runtime state of one enemy slot.
/// </summary>
public enum MonsterRuntimeState
{
    /// <summary>
    /// Slot is not currently represented by an active enemy.
    /// </summary>
    EmptyOrDead,

    /// <summary>
    /// Enemy is prepared in the central lair but has not been released yet.
    /// </summary>
    WaitingInLair,

    /// <summary>
    /// Reserved state for a future explicit lair-exit animation or scripted path.
    /// </summary>
    ExitingLair,

    /// <summary>
    /// Enemy is moving in the maze and can collide with the player.
    /// </summary>
    InMaze,

    /// <summary>
    /// Reserved state for a future vegetable-freeze implementation.
    /// </summary>
    FrozenInMaze
}
