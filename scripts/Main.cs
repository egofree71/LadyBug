using System;
using System.IO;
using Godot;

public partial class Main : Node
{
    [Export]
    public bool Debug { get; set; } = true;

    private const string ScreenshotDirectory = "screenshots";

    public override void _Ready()
    {
        GD.Print("LadyBug project started.");
    }

    public override void _Input(InputEvent @event)
    {
        if (Engine.IsEditorHint())
            return;

        if (@event is not InputEventKey keyEvent ||
            !keyEvent.Pressed ||
            keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.Keycode != Key.F1 && keyEvent.Keycode != Key.F12)
            return;

        if (!Debug)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.Keycode == Key.F12)
        {
            SaveScreenshot();
            GetViewport().SetInputAsHandled();
        }

        // When Debug is enabled, F1 is intentionally left unhandled here.
        // Level.cs already owns the F1 shortcut that starts the next-level transition.
    }

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

    private static string GetScreenshotDirectoryPath()
    {
        if (OS.HasFeature("editor"))
            return ProjectSettings.GlobalizePath($"res://{ScreenshotDirectory}");

        string executableDirectory = Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".";
        return Path.Combine(executableDirectory, ScreenshotDirectory);
    }
}
