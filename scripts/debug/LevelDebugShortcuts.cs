using System;
using System.IO;
using Godot;

namespace LadyBug.DebugTools;

/// <summary>
/// Gameplay-only debug shortcuts attached under the active Level instance.
///
/// Main deliberately does not handle these keys anymore, so the title screen
/// stays clean and debug actions exist only while a playable level is running.
/// </summary>
public partial class LevelDebugShortcuts : Node
{
    [Export]
    public bool Debug { get; set; } = true;

    private const string ScreenshotDirectory = "screenshots";

    /// <summary>
    /// Handles only shortcuts that are meaningful during gameplay.
    ///
    /// F1 is not handled here because Level.cs already owns the debug shortcut
    /// used to start the next-level transition.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (Engine.IsEditorHint() || !Debug)
            return;

        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        if (keyEvent.Keycode != Key.F12)
            return;

        SaveScreenshot();
        GetViewport().SetInputAsHandled();
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
