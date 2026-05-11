using System;
using System.Collections.Generic;
using Godot;
using LadyBug.Gameplay.Collectibles;

/// <summary>
/// Arcade-style PART screen shown between two playable levels.
/// </summary>
/// <remarks>
/// The panel is deliberately drawn only inside the purple maze frame so the HUD,
/// maze-border timer tiles, and maze frame remain visible during the transition.
/// It previews the upcoming level number, vegetable bonus, skull count, letters,
/// and hearts using the same spawn plan that will be consumed by the next board.
/// </remarks>
public partial class LevelTransitionOverlay : CanvasLayer
{
    // The transition panel is positioned against the current rendered viewport.
    // Runtime captures are 800x880, including the upper and lower HUD strips.
    private const float ReferenceViewportWidth = 800.0f;
    private const float ReferenceViewportHeight = 880.0f;

    // Inner area of the purple maze frame in the current 4x render. The black
    // panel must stay within this rectangle so the purple frame remains visible.
    private static readonly Vector2 ReferencePanelPosition = new(51.0f, 72.0f);
    private static readonly Vector2 ReferencePanelSize = new(696.0f, 696.0f);

    private const string VegetableSpriteSheetPath = "res://assets/sprites/props/vegetables.png";
    private const string ArcadeFontPath = "res://assets/fonts/PressStart2P-Regular.ttf";
    private const string CollectiblesSpriteSheetPath = "res://assets/sprites/props/collectibles.png";
    private const float VegetableFrameSize = 64.0f;
    private const float CollectibleFrameSize = 64.0f;
    private const int SkullFrameIndex = 0;
    private const int HeartMainFrameIndex = 2;
    private const int HeartOverlayFrameIndex = 3;
    private static readonly Vector2 VegetableIconDisplaySize = new(52.0f, 52.0f);
    private static readonly Vector2 SkullIconDisplaySize = new(64.0f, 64.0f);
    private static readonly Vector2 LetterIconDisplaySize = new(64.0f, 64.0f);
    private static readonly Vector2 HeartIconDisplaySize = new(64.0f, 64.0f);
    // Collectible.tscn uses MainSprite offset (-16, -4) and OverlaySprite position (-14, -4),
    // so the overlay is effectively 2 px to the right of the main frame and not shifted vertically.
    // Keep this separate so it is easy to tune manually from screenshots.
    private static readonly Vector2 HeartOverlayOffset = new(2.0f, 0.0f);

    private static readonly Color ArcadeBlack = new(0.0f, 0.0f, 0.0f, 1.0f);
    private static readonly Color ArcadeCyan = new(0.0f, 0.75f, 1.0f, 1.0f);
    private static readonly Color ArcadeBlue = new(0.0f, 0.68f, 1.0f, 1.0f);
    private static readonly Color ArcadeGreen = new(0.1f, 1.0f, 0.1f, 1.0f);
    private static readonly Color ArcadeYellow = new(1.0f, 1.0f, 0.0f, 1.0f);
    private static readonly Color ArcadeRed = Color.FromHtml("FF5100");
    private static readonly Color ArcadeWhite = new(1.0f, 1.0f, 1.0f, 1.0f);

    private const int PartFontSize = 28;
    private const int BonusFormulaFontSize = 24;
    private const int VegetableNameFontSize = 26;
    private const int GoodLuckFontSize = 24;

    // TTF arcade font used only for transition-screen text. Sprites keep their original assets.
    private Font? _arcadeFont;

    // Full-screen root used only to anchor the transition panel to the viewport.
    private Control? _root;

    // Black rectangle drawn inside the purple maze frame.
    private ColorRect? _panel;

    // Vertical container that stacks all preview rows inside the black panel.
    private VBoxContainer? _content;

    // Main line displaying the upcoming part number.
    private Label? _partLabel;

    // Row containing the vegetable sprite and its bonus value.
    private HBoxContainer? _vegetableBonusRow;

    // Vegetable icon shown on the bonus row.
    private TextureRect? _vegetableIcon;

    // Label that renders the bonus formula, for example "= 1500".
    private Label? _bonusFormulaLabel;

    // Text label showing the vegetable name on the next line.
    private Label? _vegetableNameLabel;

    // Preview row for the skull icons that will appear in the next level.
    private HBoxContainer? _skullRow;

    // Preview row for the three upcoming letters.
    private HBoxContainer? _letterRow;

    // Preview row for the three upcoming hearts.
    private HBoxContainer? _heartRow;

    // Final encouragement line shown at the bottom of the panel.
    private Label? _goodLuckLabel;

    // Runtime RNG used only to build the preview spawn plan shown on the PART screen.
    private readonly RandomNumberGenerator _previewRng = new();

    public override void _Ready()
    {
        Layer = 100;
        LoadArcadeFont();
        EnsureUi();
        HideOverlay();
    }

    /// <summary>
    /// Shows the transition panel for the upcoming playable level.
    /// </summary>
    /// <param name="upcomingLevelNumber">Visible level number that will start after the transition.</param>
    public void ShowForUpcomingLevel(int upcomingLevelNumber)
    {
        EnsureUi();

        int partNumber = Math.Max(1, upcomingLevelNumber);
        VegetableInfo vegetable = GetVegetableInfo(partNumber);
        int skullCount = CollectibleSpawnPlanner.ComputeSkullCount(partNumber);

        _previewRng.Randomize();
        CollectibleSpawnPlan previewSpawnPlan =
            CollectibleSpawnPlanner.GeneratePreviewForTransition(partNumber, _previewRng);

        _partLabel!.Text = $"PART {partNumber}";
        _bonusFormulaLabel!.Text = $"= {vegetable.Points}";
        _vegetableNameLabel!.Text = vegetable.Name;
        UpdateVegetableIcon(vegetable.FrameIndex);
        UpdateSkullRow(skullCount);
        UpdateLetterRow(previewSpawnPlan);
        UpdateHeartRow(previewSpawnPlan);
        _goodLuckLabel!.Text = "GOOD LUCK";

        UpdatePanelLayout();
        Visible = true;
    }

    /// <summary>
    /// Hides the transition panel.
    /// </summary>
    public void HideOverlay()
    {
        Visible = false;
    }

    /// <summary>
    /// Loads the shared arcade TTF used by transition-screen labels.
    /// If it is missing, Godot will fall back to the default UI font.
    /// </summary>
    private void LoadArcadeFont()
    {
        _arcadeFont = ResourceLoader.Load<Font>(ArcadeFontPath);
        if (_arcadeFont == null)
            GD.PushWarning($"[LevelTransitionOverlay] Missing arcade font: {ArcadeFontPath}");
    }

    /// <summary>
    /// Creates the runtime-only UI tree once.
    /// </summary>
    private void EnsureUi()
    {
        if (_root != null)
        {
            return;
        }

        _root = new Control
        {
            Name = "Root",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.AnchorRight = 1.0f;
        _root.AnchorBottom = 1.0f;
        AddChild(_root);

        _panel = new ColorRect
        {
            Name = "MazeInnerPanel",
            Color = ArcadeBlack,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.AddChild(_panel);

        _content = new VBoxContainer
        {
            Name = "Content",
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _content.AnchorRight = 1.0f;
        _content.AnchorBottom = 1.0f;
        _content.AddThemeConstantOverride("separation", 2);
        _panel.AddChild(_content);

        _partLabel = CreateCenteredLabel("PartLabel", PartFontSize, ArcadeCyan);
        _vegetableBonusRow = CreateVegetableBonusRow();
        _vegetableNameLabel = CreateCenteredLabel("VegetableNameLabel", VegetableNameFontSize, ArcadeYellow);
        _skullRow = CreateIconRow("SkullRow", 66.0f, 14);
        _letterRow = CreateIconRow("LetterRow", 66.0f, 14);
        _heartRow = CreateIconRow("HeartRow", 66.0f, 12);
        _goodLuckLabel = CreateCenteredLabel("GoodLuckLabel", GoodLuckFontSize, ArcadeRed);

        _content.AddChild(CreateSpacer(42));
        _content.AddChild(_partLabel);
        _content.AddChild(CreateSpacer(30));
        _content.AddChild(_vegetableBonusRow);
        _content.AddChild(_vegetableNameLabel);
        _content.AddChild(CreateSpacer(22));
        _content.AddChild(_skullRow);
        _content.AddChild(CreateSpacer(16));
        _content.AddChild(_letterRow);
        _content.AddChild(CreateSpacer(6));
        _content.AddChild(_heartRow);
        _content.AddChild(CreateSpacer(28));
        _content.AddChild(_goodLuckLabel);
        _content.AddChild(CreateSpacer(42));

        UpdatePanelLayout();
    }

    private Label CreateCenteredLabel(string name, int fontSize, Color fontColor)
    {
        Label label = new()
        {
            Name = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
            CustomMinimumSize = new Vector2(0.0f, fontSize + 12.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        if (_arcadeFont != null)
            label.AddThemeFontOverride("font", _arcadeFont);

        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", fontColor);
        return label;
    }

    private HBoxContainer CreateVegetableBonusRow()
    {
        HBoxContainer row = new()
        {
            Name = "VegetableBonusRow",
            Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(0.0f, 58.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 10);

        _vegetableIcon = new TextureRect
        {
            Name = "VegetableIcon",
            CustomMinimumSize = VegetableIconDisplaySize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _bonusFormulaLabel = CreateCenteredLabel("BonusFormulaLabel", BonusFormulaFontSize, ArcadeGreen);
        // Do not reserve a fixed width here: the HBoxContainer must center the
        // visible icon + formula as a single group, like the arcade screen.
        // A wide label box would make the formula look centered mathematically
        // but shifted visually.
        _bonusFormulaLabel.CustomMinimumSize = new Vector2(0.0f, 50.0f);
        _bonusFormulaLabel.HorizontalAlignment = HorizontalAlignment.Left;

        row.AddChild(_vegetableIcon);
        row.AddChild(_bonusFormulaLabel);
        return row;
    }


    private static HBoxContainer CreateIconRow(string name, float height, int separation)
    {
        HBoxContainer row = new()
        {
            Name = name,
            Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(0.0f, height),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    private static Control CreateSpacer(float height)
    {
        return new Control
        {
            Name = "Spacer",
            CustomMinimumSize = new Vector2(1.0f, height),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    /// <summary>
    /// Recomputes the panel rectangle from the current viewport size.
    /// </summary>
    private void UpdatePanelLayout()
    {
        if (_panel == null || _content == null)
        {
            return;
        }

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;

        float sx = Mathf.Max(0.1f, viewportSize.X / ReferenceViewportWidth);
        float sy = Mathf.Max(0.1f, viewportSize.Y / ReferenceViewportHeight);

        _panel.Position = new Vector2(ReferencePanelPosition.X * sx, ReferencePanelPosition.Y * sy);
        _panel.Size = new Vector2(ReferencePanelSize.X * sx, ReferencePanelSize.Y * sy);

        _content.Position = Vector2.Zero;
        _content.Size = _panel.Size;
    }


    /// <summary>
    /// Rebuilds the skull preview row from the upcoming level skull count.
    /// </summary>
    private void UpdateSkullRow(int skullCount)
    {
        if (_skullRow == null)
        {
            return;
        }

        foreach (Node child in _skullRow.GetChildren())
        {
            child.QueueFree();
        }

        Texture2D? collectiblesSheet = GD.Load<Texture2D>(CollectiblesSpriteSheetPath);
        if (collectiblesSheet == null)
        {
            _skullRow.Visible = false;
            return;
        }

        skullCount = Math.Max(0, skullCount);

        for (int i = 0; i < skullCount; i++)
        {
            TextureRect icon = CreateCollectibleIcon(
                collectiblesSheet,
                SkullFrameIndex,
                SkullIconDisplaySize,
                ArcadeWhite,
                $"SkullIcon{i}");

            _skullRow.AddChild(icon);
        }

        _skullRow.Visible = skullCount > 0;
    }


    /// <summary>
    /// Rebuilds the letter preview row from the cached upcoming level spawn plan.
    /// </summary>
    private void UpdateLetterRow(CollectibleSpawnPlan previewSpawnPlan)
    {
        if (_letterRow == null)
        {
            return;
        }

        foreach (Node child in _letterRow.GetChildren())
        {
            child.QueueFree();
        }

        Texture2D? collectiblesSheet = GD.Load<Texture2D>(CollectiblesSpriteSheetPath);
        if (collectiblesSheet == null)
        {
            _letterRow.Visible = false;
            return;
        }

        int shownCount = 0;
        IReadOnlyList<LetterKind> previewLetters = previewSpawnPlan.TransitionPreviewLetters;

        if (previewLetters.Count > 0)
        {
            foreach (LetterKind letter in previewLetters)
            {
                if (!TryAddLetterIcon(collectiblesSheet, letter, shownCount))
                    continue;

                shownCount++;
            }
        }
        else
        {
            // Compatibility fallback for spawn plans created before the explicit
            // transition-preview order existed. New plans should always use
            // TransitionPreviewLetters so the screen shows EXTRA, SPECIAL, A/E.
            foreach (CollectiblePlacement placement in previewSpawnPlan.Placements)
            {
                if (placement.Kind != CollectibleKind.Letter || placement.Letter == LetterKind.None)
                {
                    continue;
                }

                if (!TryAddLetterIcon(collectiblesSheet, placement.Letter, shownCount))
                    continue;

                shownCount++;
            }
        }

        _letterRow.Visible = shownCount > 0;
    }

    /// <summary>
    /// Adds one transition-screen letter icon when the logical letter is displayable.
    /// </summary>
    private bool TryAddLetterIcon(Texture2D collectiblesSheet, LetterKind letter, int shownCount)
    {
        if (_letterRow == null || letter == LetterKind.None)
            return false;

        int frameIndex = GetLetterFrameIndex(letter);
        if (frameIndex < 0)
            return false;

        TextureRect icon = CreateCollectibleIcon(
            collectiblesSheet,
            frameIndex,
            LetterIconDisplaySize,
            ArcadeBlue,
            $"LetterIcon{shownCount}");

        _letterRow.AddChild(icon);
        return true;
    }


    /// <summary>
    /// Rebuilds the heart preview row from the cached upcoming level spawn plan.
    /// </summary>
    private void UpdateHeartRow(CollectibleSpawnPlan previewSpawnPlan)
    {
        if (_heartRow == null)
        {
            return;
        }

        foreach (Node child in _heartRow.GetChildren())
        {
            child.QueueFree();
        }

        Texture2D? collectiblesSheet = GD.Load<Texture2D>(CollectiblesSpriteSheetPath);
        if (collectiblesSheet == null)
        {
            _heartRow.Visible = false;
            return;
        }

        int shownCount = 0;
        foreach (CollectiblePlacement placement in previewSpawnPlan.Placements)
        {
            if (placement.Kind != CollectibleKind.Heart)
            {
                continue;
            }

            Control icon = CreateHeartIcon(collectiblesSheet, $"HeartIcon{shownCount}");
            _heartRow.AddChild(icon);
            shownCount++;
        }

        _heartRow.Visible = shownCount > 0;
    }

    /// <summary>
    /// Creates one composite heart icon from the ring frame and the fixed center overlay frame.
    /// </summary>
    private static Control CreateHeartIcon(Texture2D collectiblesSheet, string name)
    {
        Control root = new()
        {
            Name = name,
            CustomMinimumSize = HeartIconDisplaySize,
            Size = HeartIconDisplaySize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        TextureRect main = CreateCollectibleIcon(
            collectiblesSheet,
            HeartMainFrameIndex,
            HeartIconDisplaySize,
            ArcadeBlue,
            $"{name}Main");
        main.Position = Vector2.Zero;
        main.Size = HeartIconDisplaySize;

        TextureRect overlay = CreateCollectibleIcon(
            collectiblesSheet,
            HeartOverlayFrameIndex,
            HeartIconDisplaySize,
            Colors.White,
            $"{name}Overlay");
        overlay.Position = HeartOverlayOffset;
        overlay.Size = HeartIconDisplaySize;

        root.AddChild(main);
        root.AddChild(overlay);
        return root;
    }

    /// <summary>
    /// Creates a nearest-filtered icon from one frame of the collectibles sprite sheet.
    /// </summary>
    private static TextureRect CreateCollectibleIcon(
        Texture2D collectiblesSheet,
        int frameIndex,
        Vector2 displaySize,
        Color modulate,
        string name)
    {
        AtlasTexture atlasTexture = new()
        {
            Atlas = collectiblesSheet,
            Region = new Rect2(frameIndex * CollectibleFrameSize, 0.0f, CollectibleFrameSize, CollectibleFrameSize)
        };

        return new TextureRect
        {
            Name = name,
            Texture = atlasTexture,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            CustomMinimumSize = displaySize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = modulate,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    /// <summary>
    /// Maps a logical letter to its current frame index in collectibles.png.
    /// </summary>
    private static int GetLetterFrameIndex(LetterKind letter)
    {
        return letter switch
        {
            LetterKind.E => 4,
            LetterKind.X => 5,
            LetterKind.T => 6,
            LetterKind.R => 7,
            LetterKind.A => 8,
            LetterKind.S => 9,
            LetterKind.P => 10,
            LetterKind.C => 11,
            LetterKind.I => 12,
            LetterKind.L => 13,
            _ => -1
        };
    }

    /// <summary>
    /// Updates the vegetable icon shown next to the bonus value.
    /// </summary>
    private void UpdateVegetableIcon(int frameIndex)
    {
        if (_vegetableIcon == null)
        {
            return;
        }

        Texture2D? vegetableSheet = GD.Load<Texture2D>(VegetableSpriteSheetPath);
        if (vegetableSheet == null)
        {
            _vegetableIcon.Visible = false;
            return;
        }

        AtlasTexture atlasTexture = new()
        {
            Atlas = vegetableSheet,
            Region = new Rect2(frameIndex * VegetableFrameSize, 0.0f, VegetableFrameSize, VegetableFrameSize)
        };

        _vegetableIcon.Texture = atlasTexture;
        _vegetableIcon.Visible = true;
    }

    /// <summary>
    /// Returns the vegetable and bonus value for the given level number.
    /// </summary>
    private static VegetableInfo GetVegetableInfo(int levelNumber)
    {
        // The original caps the vegetable at horseradish / 9500 points after level 18.
        int index = Math.Clamp(levelNumber, 1, VegetableTable.Length) - 1;
        return VegetableTable[index];
    }

    private readonly record struct VegetableInfo(string Name, int Points, int FrameIndex);

    private static readonly VegetableInfo[] VegetableTable =
    {
        new("CUCUMBER", 1000, 0),
        new("EGGPLANT", 1500, 1),
        new("CARROT", 2000, 2),
        new("RADISH", 2500, 3),
        new("PARSLEY", 3000, 4),
        new("TOMATO", 3500, 5),
        new("PUMPKIN", 4000, 6),
        new("BAMBOO SHOOT", 4500, 7),
        new("JAPANESE RADISH", 5000, 8),
        new("MUSHROOM", 5500, 9),
        new("POTATO", 6000, 10),
        new("ONION", 6500, 11),
        new("CHINESE CABBAGE", 7000, 12),
        new("TURNIP", 7500, 13),
        new("RED PEPPER", 8000, 14),
        new("CELERY", 8500, 15),
        new("SWEET POTATO", 9000, 16),
        new("HORSERADISH", 9500, 17)
    };
}
