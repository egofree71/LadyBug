using Godot;
using LadyBug.Gameplay.Enemies;

public partial class Level
{
    /// <summary>
    /// Adds the runtime-only vegetable bonus system without modifying Level.cs.
    /// </summary>
    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint())
            CallDeferred(nameof(InstallVegetableBonusRuntimeNode));
    }

    /// <summary>
    /// Creates the vegetable runtime node once the normal Level scene is in the tree.
    /// </summary>
    private void InstallVegetableBonusRuntimeNode()
    {
        if (Engine.IsEditorHint() || GetNodeOrNull<VegetableBonusRuntime>("VegetableBonusRuntime") != null)
            return;

        VegetableBonusRuntime runtime = new()
        {
            Name = "VegetableBonusRuntime",
            ProcessPriority = -10_000
        };

        runtime.Initialize(this);
        AddChild(runtime);

        // Render the vegetable above the enemy/maze layers but below the player.
        Node2D? playerNode = GetNodeOrNull<Node2D>("Player");
        if (playerNode != null)
            MoveChild(runtime, playerNode.GetIndex());
    }

    // Current level number used to select the vegetable frame and score.
    internal int VegetableSupport_LevelNumber => _levelNumber;

    // Current enemy runtime, queried by the vegetable system to inspect enemy slots.
    internal EnemyRuntime? VegetableSupport_EnemyRuntime => _enemyRuntime;

    // Pickup popups freeze normal gameplay, so vegetable pickup should pause too.
    internal bool VegetableSupport_IsPickupPopupActive => _pickupPopupState.IsActive;

    // Current player logical cell used to detect pickup at the center lair.
    internal Vector2I VegetableSupport_PlayerLogicalCell =>
        _player == null
            ? new Vector2I(int.MinValue, int.MinValue)
            : _coordinateSystem.ArcadePixelToLogicalCell(_player.ArcadePixelPos);

    /// <summary>
    /// Gets the scene-space position for the vegetable, aligned with the lair enemy visual anchor.
    /// </summary>
    internal Vector2 VegetableSupport_GetLairScenePosition()
    {
        Vector2 mazeSceneOrigin = _mazeSprite?.Position ?? Vector2.Zero;
        return _coordinateSystem.ArcadePixelToScenePosition(
            EnemyMovementTuning.LairArcadePixelPos + EnemyMovementTuning.SpriteRenderOffsetVerticalArcade,
            mazeSceneOrigin);
    }

    /// <summary>
    /// Returns true when the vegetable runtime should clear its local state.
    /// </summary>
    internal bool VegetableSupport_ShouldResetBonusRuntime()
    {
        return _isGameOver || _isLevelTransitionScreenActive || _isPlayerDeathSequenceActive;
    }

    /// <summary>
    /// Adds vegetable score immediately, without popup and without heart multiplier.
    /// </summary>
    internal void VegetableSupport_AddScore(int score)
    {
        if (score <= 0)
            return;

        _scoreState.AddPoints(score);
        _hud?.SetScore(_scoreState.Score);
    }
}
