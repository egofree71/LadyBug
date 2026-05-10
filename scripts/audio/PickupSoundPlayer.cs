using Godot;
using LadyBug.Gameplay.Collectibles;

namespace LadyBug.Audio;

/// <summary>
/// Owns the short gameplay sound effects used by the current board.
/// </summary>
/// <remarks>
/// The node is created by <c>Level</c> at runtime so the package can be dropped into
/// the project without editing <c>Level.tscn</c>. It uses non-positional audio
/// because these arcade-style effects are global gameplay sounds rather than
/// spatialized world sounds.
/// </remarks>
public sealed partial class PickupSoundPlayer : Node
{
    // Sound played when the player consumes a normal flower.
    private const string FlowerPickupSoundPath = "res://assets/audio/flower_pickup.wav";

    // Sound played when the player consumes a heart or letter collectible.
    private const string CollectiblePickupSoundPath = "res://assets/audio/collectible_pickup.wav";

    // Sound played once when the player death sequence starts.
    private const string DeathSequenceSoundPath = "res://assets/audio/death_sequence.wav";

    // Output bus used by the simple gameplay effects. The default Master bus exists in Godot projects.
    private const string DefaultAudioBus = "Master";

    // Small polyphony budget to avoid cutting off the flower sound when pickups happen quickly.
    private const int PickupSoundMaxPolyphony = 4;

    // Death is not expected to overlap with itself, so one voice is enough.
    private const int DeathSoundMaxPolyphony = 1;

    // Runtime player dedicated to the flower pickup stream.
    private AudioStreamPlayer? _flowerPickupPlayer;

    // Runtime player dedicated to the heart / letter pickup stream.
    private AudioStreamPlayer? _collectiblePickupPlayer;

    // Runtime player dedicated to the death-sequence start stream.
    private AudioStreamPlayer? _deathSequencePlayer;

    /// <summary>
    /// Loads the pickup sound streams and creates the runtime audio players.
    /// </summary>
    public override void _Ready()
    {
        _flowerPickupPlayer = CreateGameplayAudioPlayer(
            "FlowerPickupAudioPlayer",
            FlowerPickupSoundPath,
            PickupSoundMaxPolyphony);

        _collectiblePickupPlayer = CreateGameplayAudioPlayer(
            "CollectiblePickupAudioPlayer",
            CollectiblePickupSoundPath,
            PickupSoundMaxPolyphony);

        _deathSequencePlayer = CreateGameplayAudioPlayer(
            "DeathSequenceAudioPlayer",
            DeathSequenceSoundPath,
            DeathSoundMaxPolyphony);
    }

    /// <summary>
    /// Plays the pickup sound associated with the consumed collectible kind.
    /// </summary>
    /// <remarks>
    /// Skulls deliberately do not use these pickup sounds because touching a skull is
    /// handled as a lethal event rather than a normal collectible reward.
    /// </remarks>
    /// <param name="kind">Semantic kind returned by the collectible runtime.</param>
    public void PlayForCollectible(CollectibleKind kind)
    {
        switch (kind)
        {
            case CollectibleKind.Flower:
                PlayIfAvailable(_flowerPickupPlayer);
                break;

            case CollectibleKind.Heart:
            case CollectibleKind.Letter:
                PlayIfAvailable(_collectiblePickupPlayer);
                break;
        }
    }

    /// <summary>
    /// Plays the sound used when the player death sequence starts.
    /// </summary>
    public void PlayDeathSequenceStart()
    {
        PlayIfAvailable(_deathSequencePlayer);
    }

    /// <summary>
    /// Creates one configured <see cref="AudioStreamPlayer"/> for a gameplay effect.
    /// </summary>
    /// <param name="nodeName">Runtime child node name used for debugging.</param>
    /// <param name="streamPath">Resource path of the WAV file to play.</param>
    /// <param name="maxPolyphony">Maximum number of simultaneous voices allowed for this effect.</param>
    /// <returns>The configured audio player, or <see langword="null"/> if the stream could not be loaded.</returns>
    private AudioStreamPlayer? CreateGameplayAudioPlayer(
        string nodeName,
        string streamPath,
        int maxPolyphony)
    {
        AudioStream? stream = ResourceLoader.Load<AudioStream>(streamPath);
        if (stream == null)
        {
            GD.PushWarning($"Gameplay sound stream could not be loaded: {streamPath}");
            return null;
        }

        AudioStreamPlayer player = new()
        {
            Name = nodeName,
            Stream = stream,
            Bus = DefaultAudioBus
        };

        // The C# wrapper follows Godot's snake_case property internally; Set keeps
        // this compatible with Godot 4.x even if generated property names change.
        player.Set("max_polyphony", maxPolyphony);

        AddChild(player);
        return player;
    }

    /// <summary>
    /// Starts playback on the given player when the sound stream was loaded correctly.
    /// </summary>
    /// <param name="player">Runtime audio player to trigger.</param>
    private static void PlayIfAvailable(AudioStreamPlayer? player)
    {
        player?.Play();
    }
}
