using System;
using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Displays the prototype in-game HUD for one active level.
/// </summary>
/// <remarks>
/// <para>
/// The HUD currently shows the score, remaining lives, SPECIAL / EXTRA word
/// progress, and the blue-heart score multiplier indicators.
/// </para>
/// <para>
/// This script deliberately avoids hardcoding node placement. Positions, anchors,
/// margins, and most visual layout details remain authored in <c>Level.tscn</c>.
/// The script only finds the expected label nodes and updates their dynamic text.
/// </para>
/// <para>
/// SPECIAL, EXTRA, and multiplier indicators use <see cref="RichTextLabel"/> so
/// individual letters can be colored independently while the words remain in a
/// single logical HUD area.
/// </para>
/// </remarks>
public partial class Hud : CanvasLayer
{
    // Inactive letters in the arcade HUD are light grey rather than pure white.
    private const string GreyColor = "#C8C8C8";

    // SPECIAL uses the same red/orange color as red heart/letter collectibles.
    private const string SpecialActiveColor = "#FF5100";

    // EXTRA uses the same yellow color as yellow heart/letter collectibles.
    private const string ExtraActiveColor = "#FFFF00";

    // Multipliers use the same blue color as blue heart/letter collectibles.
    private const string MultiplierActiveColor = "#00AEFF";

    // The original HUD uses large tile-like letters. RichTextLabel text is kept
    // at the same visual size as the lower score/lives labels.
    private const int TopHudFontSize = 36;

    /// <summary>
    /// Gets or sets the path to the score label.
    /// </summary>
    /// <remarks>
    /// The fallback lookup still checks <c>Root/ScoreLabel</c> and <c>ScoreLabel</c>
    /// so the scene can be reorganized without immediately breaking the script.
    /// </remarks>
    [Export]
    public NodePath ScoreLabelPath { get; set; } = "Root/ScoreLabel";

    /// <summary>
    /// Gets or sets the path to the lives label.
    /// </summary>
    [Export]
    public NodePath LivesLabelPath { get; set; } = "Root/LivesLabel";

    /// <summary>
    /// Gets or sets the path to the RichTextLabel that displays SPECIAL.
    /// </summary>
    [Export]
    public NodePath SpecialWordLabelPath { get; set; } = "Root/SpecialWordLabel";

    /// <summary>
    /// Gets or sets the path to the RichTextLabel that displays EXTRA.
    /// </summary>
    [Export]
    public NodePath ExtraWordLabelPath { get; set; } = "Root/ExtraWordLabel";

    /// <summary>
    /// Gets or sets the path to the RichTextLabel that displays x2 / x3 / x5.
    /// </summary>
    [Export]
    public NodePath MultipliersLabelPath { get; set; } = "Root/MultipliersLabel";

    private Label? _scoreLabel;
    private Label? _livesLabel;
    private RichTextLabel? _specialWordLabel;
    private RichTextLabel? _extraWordLabel;
    private RichTextLabel? _multipliersLabel;

    // Last known values are cached so _Ready can safely reapply them if the HUD
    // enters the scene after Level has already called one of the setter methods.
    private int _lastScore;
    private int _lastLives = 3;
    private int _lastMultiplierStep;
    private string _lastSpecialWordText = BuildInactiveSpecialWordText();
    private string _lastExtraWordText = BuildInactiveExtraWordText();
    private string _lastMultipliersText = BuildMultipliersText(0);

    /// <summary>
    /// Resolves the HUD label nodes and applies the cached initial values.
    /// </summary>
    public override void _Ready()
    {
        _scoreLabel = FindScoreLabel();
        _livesLabel = FindLivesLabel();
        _specialWordLabel = FindRichTextLabel(SpecialWordLabelPath, "Root/SpecialWordLabel", "SpecialWordLabel");
        _extraWordLabel = FindRichTextLabel(ExtraWordLabelPath, "Root/ExtraWordLabel", "ExtraWordLabel");
        _multipliersLabel = FindRichTextLabel(MultipliersLabelPath, "Root/MultipliersLabel", "MultipliersLabel");

        if (_scoreLabel == null)
            GD.PushWarning("[Hud] Could not find ScoreLabel. Expected Root/ScoreLabel or ScoreLabel, or set ScoreLabelPath in the Inspector.");

        if (_livesLabel == null)
            GD.PushWarning("[Hud] Could not find LivesLabel. Expected Root/LivesLabel or LivesLabel, or set LivesLabelPath in the Inspector.");

        if (_specialWordLabel == null)
            GD.PushWarning("[Hud] Could not find SpecialWordLabel. Expected Root/SpecialWordLabel or SpecialWordLabel, or set SpecialWordLabelPath in the Inspector.");

        if (_extraWordLabel == null)
            GD.PushWarning("[Hud] Could not find ExtraWordLabel. Expected Root/ExtraWordLabel or ExtraWordLabel, or set ExtraWordLabelPath in the Inspector.");

        if (_multipliersLabel == null)
            GD.PushWarning("[Hud] Could not find MultipliersLabel. Expected Root/MultipliersLabel or MultipliersLabel, or set MultipliersLabelPath in the Inspector.");

        // Important: this script does not set screen positions or anchors.
        // Those are controlled in Level.tscn. It only controls dynamic text.
        SetScore(_lastScore);
        SetLives(_lastLives);
        ApplyRichText(_specialWordLabel, _lastSpecialWordText);
        ApplyRichText(_extraWordLabel, _lastExtraWordText);
        ApplyRichText(_multipliersLabel, _lastMultipliersText);
    }

    /// <summary>
    /// Updates the numeric score display.
    /// </summary>
    /// <param name="score">Current player score.</param>
    public void SetScore(int score)
    {
        _lastScore = score;

        if (_scoreLabel == null)
            return;

        _scoreLabel.Text = score.ToString();
    }

    /// <summary>
    /// Updates the remaining-lives display.
    /// </summary>
    /// <param name="lives">Current remaining life count.</param>
    public void SetLives(int lives)
    {
        _lastLives = lives;

        if (_livesLabel == null)
            return;

        _livesLabel.Text = $"LIVES {lives}";
    }

    /// <summary>
    /// Updates the SPECIAL and EXTRA word displays from the current word progress state.
    /// </summary>
    /// <param name="wordProgress">Semantic progress through both bonus words.</param>
    public void SetWordProgress(WordProgressState wordProgress)
    {
        _lastSpecialWordText = BuildSpecialWordText(wordProgress);
        _lastExtraWordText = BuildExtraWordText(wordProgress);

        ApplyRichText(_specialWordLabel, _lastSpecialWordText);
        ApplyRichText(_extraWordLabel, _lastExtraWordText);
    }

    /// <summary>
    /// Updates the x2 / x3 / x5 multiplier display from the blue-heart step.
    /// </summary>
    /// <remarks>
    /// Step 0 means no multiplier indicator is active. Step 1 lights x2, step 2
    /// lights x2 and x3, and step 3 lights x2, x3, and x5.
    /// </remarks>
    /// <param name="multiplierStep">Current blue-heart multiplier step.</param>
    public void SetMultiplierStep(int multiplierStep)
    {
        _lastMultiplierStep = Math.Clamp(multiplierStep, 0, 3);
        _lastMultipliersText = BuildMultipliersText(_lastMultiplierStep);
        ApplyRichText(_multipliersLabel, _lastMultipliersText);
    }

    /// <summary>
    /// Finds the score label using the exported path first, then scene-name fallbacks.
    /// </summary>
    private Label? FindScoreLabel()
    {
        if (!ScoreLabelPath.IsEmpty)
        {
            Label? exportedPathLabel = GetNodeOrNull<Label>(ScoreLabelPath);
            if (exportedPathLabel != null)
                return exportedPathLabel;
        }

        Label? rootChildLabel = GetNodeOrNull<Label>("Root/ScoreLabel");
        if (rootChildLabel != null)
            return rootChildLabel;

        return GetNodeOrNull<Label>("ScoreLabel");
    }

    /// <summary>
    /// Finds the lives label using the exported path first, then scene-name fallbacks.
    /// </summary>
    private Label? FindLivesLabel()
    {
        if (!LivesLabelPath.IsEmpty)
        {
            Label? exportedPathLabel = GetNodeOrNull<Label>(LivesLabelPath);
            if (exportedPathLabel != null)
                return exportedPathLabel;
        }

        Label? rootChildLabel = GetNodeOrNull<Label>("Root/LivesLabel");
        if (rootChildLabel != null)
            return rootChildLabel;

        return GetNodeOrNull<Label>("LivesLabel");
    }

    /// <summary>
    /// Finds one RichTextLabel using the exported path first, then stable fallback paths.
    /// </summary>
    /// <param name="exportedPath">Inspector-configurable node path.</param>
    /// <param name="rootPath">Expected path under the HUD root node.</param>
    /// <param name="fallbackPath">Fallback path for flatter HUD scene structures.</param>
    private RichTextLabel? FindRichTextLabel(
        NodePath exportedPath,
        string rootPath,
        string fallbackPath)
    {
        if (!exportedPath.IsEmpty)
        {
            RichTextLabel? exportedPathLabel = GetNodeOrNull<RichTextLabel>(exportedPath);
            if (exportedPathLabel != null)
                return exportedPathLabel;
        }

        RichTextLabel? rootChildLabel = GetNodeOrNull<RichTextLabel>(rootPath);
        if (rootChildLabel != null)
            return rootChildLabel;

        return GetNodeOrNull<RichTextLabel>(fallbackPath);
    }

    /// <summary>
    /// Applies rich text markup to a label if that label exists.
    /// </summary>
    private static void ApplyRichText(RichTextLabel? label, string text)
    {
        if (label == null)
            return;

        label.Text = text;
    }

    /// <summary>
    /// Builds the rich-text markup for the SPECIAL word.
    /// </summary>
    private static string BuildSpecialWordText(WordProgressState wordProgress)
    {
        string text = BuildColoredWord(
            WordProgressState.SpecialWordLetters,
            wordProgress.IsSpecialLetterActive,
            SpecialActiveColor);

        return TopHudText(text);
    }

    /// <summary>
    /// Builds the centered rich-text markup for the EXTRA word.
    /// </summary>
    private static string BuildExtraWordText(WordProgressState wordProgress)
    {
        string text = BuildColoredWord(
            WordProgressState.ExtraWordLetters,
            wordProgress.IsExtraLetterActive,
            ExtraActiveColor);

        return CenterText(TopHudText(text));
    }

    /// <summary>
    /// Builds one colored word by wrapping each letter in an individual color tag.
    /// </summary>
    /// <param name="letters">Ordered letters of the word to draw.</param>
    /// <param name="isLetterActive">Predicate indicating whether each letter is active.</param>
    /// <param name="activeColor">Color used for active letters.</param>
    private static string BuildColoredWord(
        LetterKind[] letters,
        Func<LetterKind, bool> isLetterActive,
        string activeColor)
    {
        string text = string.Empty;

        foreach (LetterKind letter in letters)
        {
            string color = isLetterActive(letter) ? activeColor : GreyColor;
            text += ColorToken(LetterToText(letter), color);
        }

        return text;
    }

    /// <summary>
    /// Builds the initial inactive SPECIAL display.
    /// </summary>
    private static string BuildInactiveSpecialWordText()
    {
        return TopHudText(BuildInactiveWordLetters("SPECIAL"));
    }

    /// <summary>
    /// Builds the initial inactive EXTRA display.
    /// </summary>
    private static string BuildInactiveExtraWordText()
    {
        return CenterText(TopHudText(BuildInactiveWordLetters("EXTRA")));
    }

    /// <summary>
    /// Builds the inactive grey markup for every character in one word.
    /// </summary>
    private static string BuildInactiveWordLetters(string word)
    {
        string text = string.Empty;

        foreach (char letter in word)
            text += ColorToken(letter.ToString(), GreyColor);

        return text;
    }

    /// <summary>
    /// Builds the right-aligned rich-text markup for x2 / x3 / x5.
    /// </summary>
    private static string BuildMultipliersText(int multiplierStep)
    {
        string x2Color = multiplierStep >= 1 ? MultiplierActiveColor : GreyColor;
        string x3Color = multiplierStep >= 2 ? MultiplierActiveColor : GreyColor;
        string x5Color = multiplierStep >= 3 ? MultiplierActiveColor : GreyColor;

        string text = string.Join(
            " ",
            ColorToken("x2", x2Color),
            ColorToken("x3", x3Color),
            ColorToken("x5", x5Color));

        return RightText(TopHudText(text));
    }

    /// <summary>
    /// Wraps top-HUD text in the font-size tag shared by SPECIAL, EXTRA, and multipliers.
    /// </summary>
    private static string TopHudText(string text)
    {
        return $"[font_size={TopHudFontSize}]{text}[/font_size]";
    }

    /// <summary>
    /// Wraps rich text in a center-alignment tag.
    /// </summary>
    private static string CenterText(string text)
    {
        return $"[center]{text}[/center]";
    }

    /// <summary>
    /// Wraps rich text in a right-alignment tag.
    /// </summary>
    private static string RightText(string text)
    {
        return $"[right]{text}[/right]";
    }

    /// <summary>
    /// Wraps text in a Godot rich-text color tag.
    /// </summary>
    private static string ColorToken(string text, string color)
    {
        return $"[color={color}]{text}[/color]";
    }

    /// <summary>
    /// Converts a letter enum into its HUD text representation.
    /// </summary>
    private static string LetterToText(LetterKind letter)
    {
        return letter switch
        {
            LetterKind.A => "A",
            LetterKind.C => "C",
            LetterKind.E => "E",
            LetterKind.I => "I",
            LetterKind.L => "L",
            LetterKind.P => "P",
            LetterKind.R => "R",
            LetterKind.S => "S",
            LetterKind.T => "T",
            LetterKind.X => "X",
            _ => string.Empty
        };
    }
}
