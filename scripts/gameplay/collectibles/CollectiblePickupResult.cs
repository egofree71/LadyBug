namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Describes the gameplay result of consuming one collectible.
///
/// This is intentionally semantic: callers do not need to inspect the visual
/// collectible node to know what was eaten.
/// </summary>
public readonly struct CollectiblePickupResult
{
    public bool Consumed { get; }
    public CollectibleKind Kind { get; }
    public CollectibleColor Color { get; }
    public LetterKind Letter { get; }

    private CollectiblePickupResult(
        bool consumed,
        CollectibleKind kind,
        CollectibleColor color,
        LetterKind letter)
    {
        Consumed = consumed;
        Kind = kind;
        Color = color;
        Letter = letter;
    }

    public static CollectiblePickupResult None =>
        new(false, CollectibleKind.Flower, CollectibleColor.None, LetterKind.None);

    public static CollectiblePickupResult Collected(
        CollectibleKind kind,
        CollectibleColor color = CollectibleColor.None,
        LetterKind letter = LetterKind.None)
    {
        return new CollectiblePickupResult(true, kind, color, letter);
    }
}
