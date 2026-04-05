using System;
using System.Text.Json;
using Godot;

namespace LadyBug.Gameplay.Maze;

/// <summary>
/// Provides helper methods to load a <see cref="MazeGrid"/> from serialized JSON data.
/// </summary>
public static class MazeLoader
{
    /// <summary>
    /// Loads a <see cref="MazeGrid"/> from a JSON file.
    /// </summary>
    /// <param name="path">The resource or file path to the maze JSON file.</param>
    /// <returns>A <see cref="MazeGrid"/> instance created from the file content.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the file cannot be found or when the JSON content cannot be deserialized
    /// into valid maze data.
    /// </exception>
    public static MazeGrid LoadFromJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        if (!FileAccess.FileExists(path))
            throw new InvalidOperationException($"Maze JSON file not found: {path}");

        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        string json = file.GetAsText();

        MazeDataFile? data = JsonSerializer.Deserialize<MazeDataFile>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (data == null)
            throw new InvalidOperationException("Failed to deserialize maze JSON file.");

        return MazeGrid.FromDataFile(data);
    }
}
