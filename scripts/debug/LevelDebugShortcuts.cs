using System;
using System.IO;
using Godot;

namespace LadyBug.DebugTools;

/// <summary>
/// Gameplay-only function-key debug shortcuts attached under the active Level instance.
///
/// Main deliberately does not handle these keys, so the title screen stays clean
/// and function-key debug actions exist only while a playable level is running.
/// </summary>
public partial class LevelDebugShortcuts : Node
{
    [Export]
    public bool Debug { get; set; } = true;

    private const string ScreenshotDirectory = "screenshots";

    private global::Level? _level;

    /// <summary>
    /// Caches the owning Level. This node is created as a runtime child of Level by Main.
    /// </summary>
    public override void _Ready()
    {
        _level = GetParent() as global::Level;
    }

    /// <summary>
    /// Handles only function-key shortcuts that are meaningful during gameplay.
    ///
    /// F1 advances through the normal next-level transition path.
    /// F12 saves the current viewport as a PNG screenshot.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (Engine.IsEditorHint() || !Debug)
            return;

        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        switch (keyEvent.Keycode)
        {
            case Key.F1:
                AdvanceToNextLevel();
                break;

            case Key.F12:
                SaveScreenshot();
                break;

            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// Delegates the debug level-advance action to Level, which owns board state.
    /// </summary>
    private void AdvanceToNextLevel()
    {
        if (_level == null)
        {
            GD.PushWarning("LevelDebugShortcuts could not find its owning Level node.");
            return;
        }

        _level.DebugAdvanceToNextLevel();
    }

    /// <summary>
    /// Saves the current viewport image to the local screenshots directory.
    /// </summary>
    private void SaveScreenshot()
    {
        string directoryPath = GetScreenshotDirectoryPath();

        try
        {
            Directory.CreateDirectory(directoryPath);
        }
        catch (Exception exception)
        {
            GD.PushError($"Could not create screenshot directory '{directoryPath}': {exception.Message}");
            return;
        }

        Image image = GetViewport().GetTexture().GetImage();
        string fileName = $"ladybug_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        string filePath = Path.Combine(directoryPath, fileName);

        Error error = image.SavePng(filePath);
        if (error != Error.Ok)
        {
            GD.PushError($"Could not save screenshot '{filePath}'. Godot error: {error}");
            return;
        }

        GD.Print($"Screenshot saved: {filePath}");
    }

    /// <summary>
    /// Uses a project-local folder in the editor and an executable-local folder in exports.
    /// </summary>
    private static string GetScreenshotDirectoryPath()
    {
        if (OS.HasFeature("editor"))
            return ProjectSettings.GlobalizePath($"res://{ScreenshotDirectory}");

        string executableDirectory = Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".";
        return Path.Combine(executableDirectory, ScreenshotDirectory);
    }
}
