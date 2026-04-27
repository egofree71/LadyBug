using System;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Identifies which arcade bonus word has just been completed.
/// </summary>
public enum WordCompletionKind
{
    None = 0,
    Special,
    Extra
}

/// <summary>
/// Describes the result of applying one collected letter to SPECIAL or EXTRA.
/// </summary>
/// <remarks>
/// A letter can be collected for points without changing a word: for example, a
/// blue letter, a red X, or a yellow S. This result lets <c>Level</c> distinguish
/// score-only pickups from pickups that should update the HUD or award a bonus.
/// </remarks>
public readonly struct LetterWordProgressResult
{
    /// <summary>
    /// Gets whether the collected letter activated a previously inactive word letter.
    /// </summary>
    public bool Changed { get; }

    /// <summary>
    /// Gets the word completed by this pickup, or <see cref="WordCompletionKind.None"/>.
    /// </summary>
    public WordCompletionKind CompletedWord { get; }

    /// <summary>
    /// Gets whether this pickup completed either SPECIAL or EXTRA.
    /// </summary>
    public bool Completed => CompletedWord != WordCompletionKind.None;

    private LetterWordProgressResult(bool changed, WordCompletionKind completedWord)
    {
        Changed = changed;
        CompletedWord = completedWord;
    }

    /// <summary>
    /// Result used when the collected letter has no SPECIAL / EXTRA effect.
    /// </summary>
    public static LetterWordProgressResult NoChange =>
        new(false, WordCompletionKind.None);

    /// <summary>
    /// Creates a result for a letter that progressed one word.
    /// </summary>
    /// <param name="completedWord">Completed word, or None if the word is not complete yet.</param>
    public static LetterWordProgressResult Progressed(WordCompletionKind completedWord) =>
        new(true, completedWord);
}

/// <summary>
/// Tracks the player's progress through the SPECIAL and EXTRA bonus words.
/// </summary>
/// <remarks>
/// <para>
/// The original arcade game stores this information as player-specific bitfields.
/// The remake keeps the same gameplay meaning but stores it semantically: one flag
/// per visible letter in each word.
/// </para>
/// <para>
/// The color rule is handled here because it belongs to word progression rather
/// than to rendering: red letters may progress SPECIAL, yellow letters may progress
/// EXTRA, and blue letters are score-only.
/// </para>
/// </remarks>
public sealed class WordProgressState
{
    /// <summary>
    /// Ordered letters displayed in the SPECIAL word.
    /// </summary>
    public static readonly LetterKind[] SpecialWordLetters =
    {
        LetterKind.S,
        LetterKind.P,
        LetterKind.E,
        LetterKind.C,
        LetterKind.I,
        LetterKind.A,
        LetterKind.L
    };

    /// <summary>
    /// Ordered letters displayed in the EXTRA word.
    /// </summary>
    public static readonly LetterKind[] ExtraWordLetters =
    {
        LetterKind.E,
        LetterKind.X,
        LetterKind.T,
        LetterKind.R,
        LetterKind.A
    };

    // These arrays mirror the display order above. Index 0 in _specialLetters
    // corresponds to S, index 1 to P, and so on.
    private readonly bool[] _specialLetters = new bool[SpecialWordLetters.Length];
    private readonly bool[] _extraLetters = new bool[ExtraWordLetters.Length];

    /// <summary>
    /// Gets whether every SPECIAL letter is active.
    /// </summary>
    public bool IsSpecialComplete => AreAllLettersActive(_specialLetters);

    /// <summary>
    /// Gets whether every EXTRA letter is active.
    /// </summary>
    public bool IsExtraComplete => AreAllLettersActive(_extraLetters);

    /// <summary>
    /// Clears all collected-letter progress.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_specialLetters);
        Array.Clear(_extraLetters);
    }

    /// <summary>
    /// Clears only the SPECIAL word progress.
    /// </summary>
    public void ResetSpecial()
    {
        Array.Clear(_specialLetters);
    }

    /// <summary>
    /// Clears only the EXTRA word progress.
    /// </summary>
    public void ResetExtra()
    {
        Array.Clear(_extraLetters);
    }

    /// <summary>
    /// Returns whether the given letter is active in the SPECIAL word.
    /// </summary>
    /// <param name="letter">Letter to inspect.</param>
    public bool IsSpecialLetterActive(LetterKind letter)
    {
        int index = IndexOf(SpecialWordLetters, letter);
        return index >= 0 && _specialLetters[index];
    }

    /// <summary>
    /// Returns whether the given letter is active in the EXTRA word.
    /// </summary>
    /// <param name="letter">Letter to inspect.</param>
    public bool IsExtraLetterActive(LetterKind letter)
    {
        int index = IndexOf(ExtraWordLetters, letter);
        return index >= 0 && _extraLetters[index];
    }

    /// <summary>
    /// Applies one collected letter according to the arcade color rules.
    /// </summary>
    /// <remarks>
    /// Red letters can progress SPECIAL. Yellow letters can progress EXTRA. Blue
    /// letters are score-only and do not affect either word. Letters outside the
    /// target word, or letters already active in that word, also make no progress.
    /// </remarks>
    /// <param name="letter">Collected letter.</param>
    /// <param name="color">Current collectible color at the moment of collection.</param>
    public LetterWordProgressResult TryApplyLetter(
        LetterKind letter,
        CollectibleColor color)
    {
        return color switch
        {
            CollectibleColor.Red => TryApplySpecialLetter(letter),
            CollectibleColor.Yellow => TryApplyExtraLetter(letter),
            _ => LetterWordProgressResult.NoChange
        };
    }

    /// <summary>
    /// Attempts to activate one SPECIAL letter.
    /// </summary>
    private LetterWordProgressResult TryApplySpecialLetter(LetterKind letter)
    {
        int index = IndexOf(SpecialWordLetters, letter);
        if (index < 0 || _specialLetters[index])
            return LetterWordProgressResult.NoChange;

        _specialLetters[index] = true;
        return LetterWordProgressResult.Progressed(
            IsSpecialComplete ? WordCompletionKind.Special : WordCompletionKind.None);
    }

    /// <summary>
    /// Attempts to activate one EXTRA letter.
    /// </summary>
    private LetterWordProgressResult TryApplyExtraLetter(LetterKind letter)
    {
        int index = IndexOf(ExtraWordLetters, letter);
        if (index < 0 || _extraLetters[index])
            return LetterWordProgressResult.NoChange;

        _extraLetters[index] = true;
        return LetterWordProgressResult.Progressed(
            IsExtraComplete ? WordCompletionKind.Extra : WordCompletionKind.None);
    }

    /// <summary>
    /// Returns true only when all flags in the supplied word state are active.
    /// </summary>
    private static bool AreAllLettersActive(bool[] letters)
    {
        for (int i = 0; i < letters.Length; i++)
        {
            if (!letters[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Finds the display index of a letter inside one ordered word definition.
    /// </summary>
    private static int IndexOf(LetterKind[] letters, LetterKind letter)
    {
        for (int i = 0; i < letters.Length; i++)
        {
            if (letters[i] == letter)
                return i;
        }

        return -1;
    }
}
