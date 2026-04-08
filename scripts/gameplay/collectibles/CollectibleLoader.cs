using System;
using System.Text.Json;
using Godot;

namespace LadyBug.Gameplay.Collectibles;

public static class CollectibleLoader
{
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