using Godot;
using LadyBug.Gameplay.Collectibles;

namespace LadyBug.Audio;

/// <summary>
/// Runtime helper for the short global gameplay sound effects used by one board.
/// </summary>
/// <remarks>
/// The node is created by <c>Level</c> at runtime, so audio-only changes do not
/// require editing Godot scene files. All effects use non-positional audio because
/// they behave like classic arcade board sounds rather than spatialized world audio.
/// </remarks>
public sealed partial class PickupSoundPlayer : Node
{
    private const string FlowerPickupSoundPath = "res://assets/audio/flower_pickup.wav";
    private const string CollectiblePickupSoundPath = "res://assets/audio/collectible_pickup.wav";
    private const string GateRotatedSoundPath = "res://assets/audio/gate_rotated.wav";
    private const string VegetablePickupSoundPath = "res://assets/audio/vegetable_pickup.wav";
    private const string EndLevelSoundPath = "res://assets/audio/end_level.wav";
    private const string TimerStepSoundPath = "res://assets/audio/timer.wav";
    private const string DeathSequenceSoundPath = "res://assets/audio/death_sequence.wav";
    private const string EnemyDeathSoundPath = "res://assets/audio/death_enemy.wav";
    private const string EnemyExitWarningSoundPath = "res://assets/audio/enemy_exit.wav";

    private const string DefaultAudioBus = "Master";

    // Pickups and gate pushes can happen close together, so keep a small overlap budget.
    private const int NormalEffectMaxPolyphony = 4;

    // The timer sound is deliberately single-voice because level 5+ advances faster
    // than timer.wav finishes. Restarting or throttling stays cleaner than stacking.
    private const int TimerEffectMaxPolyphony = 1;

    // Level-complete jingle should restart cleanly instead of stacking.
    private const int EndLevelEffectMaxPolyphony = 1;

    // Player death should not overlap with itself.
    private const int DeathEffectMaxPolyphony = 1;

    // Several enemies can theoretically hit skulls close together.
    private const int EnemyDeathEffectMaxPolyphony = 2;

    // The lair-exit warning is a single global alert effect.
    private const int EnemyExitWarningEffectMaxPolyphony = 1;

    private AudioStreamPlayer? _flowerPickupPlayer;
    private AudioStreamPlayer? _collectiblePickupPlayer;
    private AudioStreamPlayer? _gateRotatedPlayer;
    private AudioStreamPlayer? _vegetablePickupPlayer;
    private AudioStreamPlayer? _endLevelPlayer;
    private AudioStreamPlayer? _timerStepPlayer;
    private AudioStreamPlayer? _deathSequencePlayer;
    private AudioStreamPlayer? _enemyDeathPlayer;
    private AudioStreamPlayer? _enemyExitWarningPlayer;

    // Countdown for the audible border-timer cadence. It is intentionally allowed
    // to differ from the visual cadence on level 5+ so the sound stays regular
    // without changing the reverse-engineered border timer logic.
    private int _timerAudioCountdown;

    /// <summary>
    /// Creates the dedicated audio players once this helper enters the scene tree.
    /// </summary>
    public override void _Ready()
    {
        EnsurePlayers();
    }

    /// <summary>
    /// Plays the pickup sound associated with the consumed collectible kind.
    /// </summary>
    /// <remarks>
    /// Skulls deliberately do not use normal pickup sounds because touching a skull is
    /// handled as a lethal event rather than a score reward.
    /// </remarks>
    public void PlayForCollectible(CollectibleKind kind)
    {
        switch (kind)
        {
            case CollectibleKind.Flower:
                PlayFlowerPickup();
                break;

            case CollectibleKind.Heart:
            case CollectibleKind.Letter:
                PlayCollectiblePickup();
                break;
        }
    }

    /// <summary>
    /// Plays the short sound used when the player consumes a flower.
    /// </summary>
    public void PlayFlowerPickup()
    {
        EnsurePlayers();
        Play(_flowerPickupPlayer);
    }

    /// <summary>
    /// Plays the short sound used when the player consumes a heart or a letter.
    /// </summary>
    public void PlayCollectiblePickup()
    {
        EnsurePlayers();
        Play(_collectiblePickupPlayer);
    }

    /// <summary>
    /// Plays the short sound used when the player successfully rotates a gate.
    /// </summary>
    public void PlayGateRotated()
    {
        EnsurePlayers();
        Play(_gateRotatedPlayer);
    }

    /// <summary>
    /// Plays the short sound used when the player consumes the central vegetable bonus.
    /// </summary>
    public void PlayVegetablePickup()
    {
        EnsurePlayers();
        Play(_vegetablePickupPlayer);
    }

    /// <summary>
    /// Plays the sound used when a board has been completed and the between-level freeze begins.
    /// </summary>
    public void PlayEndLevel()
    {
        EnsurePlayers();
        Restart(_endLevelPlayer);
    }

    /// <summary>
    /// Resets the audible border-timer cadence, usually when a level or attempt starts.
    /// </summary>
    public void ResetTimerStepCadence(int levelNumber)
    {
        _timerAudioCountdown = GetTimerAudioPeriod(levelNumber);
    }

    /// <summary>
    /// Advances the independent audible border-timer cadence by one simulation tick.
    /// </summary>
    /// <remarks>
    /// The visual timer still advances at the reverse-engineered cadence. For levels 1-4,
    /// the sound cadence intentionally matches the visible border cadence. For level 5+,
    /// the sound uses a regular 4-tick cadence instead of following every 3-tick visual
    /// step. This keeps level 5+ clearly faster than levels 2-4, while avoiding the
    /// irregular rhythm produced by skipping selected visible steps.
    /// </remarks>
    public void AdvanceTimerSoundOneTick(int levelNumber)
    {
        EnsurePlayers();

        int period = GetTimerAudioPeriod(levelNumber);

        if (_timerAudioCountdown <= 0)
            _timerAudioCountdown = period;

        _timerAudioCountdown--;

        if (_timerAudioCountdown != 0)
            return;

        Restart(_timerStepPlayer);
        _timerAudioCountdown = period;
    }

    /// <summary>
    /// Plays the sound used when the player death sequence starts.
    /// </summary>
    public void PlayDeathSequenceStart()
    {
        EnsurePlayers();
        Restart(_deathSequencePlayer);
    }

    /// <summary>
    /// Plays the sound used when an enemy touches a skull and returns to the lair.
    /// </summary>
    public void PlayEnemyDeathFromSkull()
    {
        EnsurePlayers();
        Play(_enemyDeathPlayer);
    }

    /// <summary>
    /// Plays the warning sound shortly before a waiting enemy leaves the lair.
    /// </summary>
    public void PlayEnemyExitWarning()
    {
        EnsurePlayers();
        Restart(_enemyExitWarningPlayer);
    }

    /// <summary>
    /// Creates all effect players if they do not already exist.
    /// </summary>
    private void EnsurePlayers()
    {
        if (_flowerPickupPlayer != null && GodotObject.IsInstanceValid(_flowerPickupPlayer))
            return;

        _flowerPickupPlayer = CreatePlayer(
            "FlowerPickupAudio",
            FlowerPickupSoundPath,
            NormalEffectMaxPolyphony);

        _collectiblePickupPlayer = CreatePlayer(
            "CollectiblePickupAudio",
            CollectiblePickupSoundPath,
            NormalEffectMaxPolyphony);

        _gateRotatedPlayer = CreatePlayer(
            "GateRotatedAudio",
            GateRotatedSoundPath,
            NormalEffectMaxPolyphony);

        _vegetablePickupPlayer = CreatePlayer(
            "VegetablePickupAudio",
            VegetablePickupSoundPath,
            NormalEffectMaxPolyphony);

        _endLevelPlayer = CreatePlayer(
            "EndLevelAudio",
            EndLevelSoundPath,
            EndLevelEffectMaxPolyphony);

        _timerStepPlayer = CreatePlayer(
            "TimerStepAudio",
            TimerStepSoundPath,
            TimerEffectMaxPolyphony);

        _deathSequencePlayer = CreatePlayer(
            "DeathSequenceAudio",
            DeathSequenceSoundPath,
            DeathEffectMaxPolyphony);

        _enemyDeathPlayer = CreatePlayer(
            "EnemyDeathAudio",
            EnemyDeathSoundPath,
            EnemyDeathEffectMaxPolyphony);

        _enemyExitWarningPlayer = CreatePlayer(
            "EnemyExitWarningAudio",
            EnemyExitWarningSoundPath,
            EnemyExitWarningEffectMaxPolyphony);
    }

    /// <summary>
    /// Creates one audio player for a short effect stream.
    /// </summary>
    private AudioStreamPlayer CreatePlayer(
        string nodeName,
        string streamPath,
        int maxPolyphony)
    {
        AudioStream? stream = ResourceLoader.Load<AudioStream>(streamPath);

        if (stream == null)
            GD.PushWarning($"Could not load gameplay sound: {streamPath}");

        AudioStreamPlayer player = new()
        {
            Name = nodeName,
            Stream = stream,
            Bus = DefaultAudioBus
        };

        // Keep this compatible with Godot 4.x generated C# bindings.
        player.Set("max_polyphony", maxPolyphony);

        AddChild(player);
        return player;
    }

    /// <summary>
    /// Starts an effect player when its stream has loaded successfully.
    /// </summary>
    private static void Play(AudioStreamPlayer? player)
    {
        if (player == null || player.Stream == null)
            return;

        player.Play();
    }

    /// <summary>
    /// Restarts an effect player from the beginning without stacking voices.
    /// </summary>
    private static void Restart(AudioStreamPlayer? player)
    {
        if (player == null || player.Stream == null)
            return;

        player.Stop();
        player.Play();
    }

    /// <summary>
    /// Returns the audible timer period in fixed simulation ticks.
    /// </summary>
    private static int GetTimerAudioPeriod(int levelNumber)
    {
        // Visual / logical border cadence from the arcade:
        // level 1:      visible step every 9 simulation ticks
        // levels 2-4:   visible step every 6 simulation ticks
        // level 5+:     visible step every 3 simulation ticks
        //
        // Audible policy:
        // level 1:      match the visual cadence: 9 ticks
        // levels 2-4:   match the visual cadence: 6 ticks
        // level 5+:     use a regular 4-tick sound cadence.
        //
        // At 60.1145 Hz this gives roughly:
        // level 1:      150 ms
        // levels 2-4:   100 ms
        // level 5+:      67 ms
        if (levelNumber <= 1)
            return 9;

        if (levelNumber < 5)
            return 6;

        return 4;
    }
}
