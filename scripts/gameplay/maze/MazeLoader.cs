using System;
using System.Text.Json;
using Godot;

namespace LadyBug.Gameplay.Maze
{
    /// <summary>
    /// Utility class used to load a MazeGrid from a JSON file.
    /// </summary>
    public static class MazeLoader
    {
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
}