using System;
using System.IO;
using UnityEngine;

namespace Tower.Core
{
    // v0 checkpoint persistence: one JSON file at an injected path, so tests
    // can point it at a temp file and the runtime at persistentDataPath.
    public sealed class SaveRepository
    {
        private SaveRepository(string savePath)
        {
            SavePath = savePath;
        }

        public string SavePath { get; }

        public bool HasSave => File.Exists(SavePath);

        public static Result<SaveRepository> Create(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return Result<SaveRepository>.Failure("Save path is required.");
            }

            return Result<SaveRepository>.Success(new SaveRepository(savePath));
        }

        public Result Save(SaveGame game)
        {
            if (game == null)
            {
                return Result.Failure("Save game is required.");
            }

            try
            {
                var directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(SavePath, JsonUtility.ToJson(game, true));
                return Result.Success();
            }
            catch (Exception exception)
            {
                return Result.Failure($"Failed to write save file: {exception.Message}");
            }
        }

        public Result<SaveGame> Load()
        {
            if (!HasSave)
            {
                return Result<SaveGame>.Failure($"No save file at '{SavePath}'.");
            }

            try
            {
                var json = File.ReadAllText(SavePath);
                var game = JsonUtility.FromJson<SaveGame>(json);
                return game != null
                    ? Result<SaveGame>.Success(game)
                    : Result<SaveGame>.Failure("Save file could not be parsed.");
            }
            catch (Exception exception)
            {
                return Result<SaveGame>.Failure($"Failed to read save file: {exception.Message}");
            }
        }

        public Result Delete()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }

                return Result.Success();
            }
            catch (Exception exception)
            {
                return Result.Failure($"Failed to delete save file: {exception.Message}");
            }
        }
    }
}
