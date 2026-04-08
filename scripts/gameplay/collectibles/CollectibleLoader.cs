using System;
using System.Text.Json;
using Godot;

namespace LadyBug.Gameplay.Collectibles;

/// <summary>
/// Provides helper methods to load a collectible layout from serialized JSON data.
/// </summary>
public static class CollectibleLoader
{
    /// <summary>
    /// Loads a <see cref="CollectibleLayoutFile"/> from a JSON file.
    /// </summary>
    /// <param name="path">
    /// The resource or file path to the collectible layout JSON file.
    /// </param>
    /// <returns>
    /// A <see cref="CollectibleLayoutFile"/> instance created from the file content.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="path"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the file cannot be found or when the JSON content cannot be
    /// deserialized into valid collectible layout data.
    /// </exception>
    public static CollectibleLayoutFile LoadFromJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        if (!FileAccess.FileExists(path))
        {
            throw new InvalidOperationException($"Collectible JSON file not found: {path}");
        }

        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        string json = file.GetAsText();

        CollectibleLayoutFile? data = JsonSerializer.Deserialize<CollectibleLayoutFile>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (data == null)
        {
            throw new InvalidOperationException("Failed to deserialize collectible JSON file.");
        }

        return data;
    }
}