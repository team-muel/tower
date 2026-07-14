using System;
using System.IO;
using UnityEngine;

namespace Tower.Core
{
    // Persists MetaProgress apart from the run save: platinum and unlocks
    // pierce retreat, the great regression, and run-save deletion.
    public sealed class MetaProgressRepository
    {
        private MetaProgressRepository(string savePath)
        {
            SavePath = savePath;
        }

        public string SavePath { get; }
        public bool HasSave => File.Exists(SavePath);

        public static Result<MetaProgressRepository> Create(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return Result<MetaProgressRepository>.Failure("Meta save path is required.");
            }

            return Result<MetaProgressRepository>.Success(new MetaProgressRepository(savePath));
        }

        public Result Save(MetaProgressSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return Result.Failure("Meta progress snapshot is required.");
            }

            try
            {
                var directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(SavePath, JsonUtility.ToJson(snapshot, true));
                return Result.Success();
            }
            catch (Exception exception)
            {
                return Result.Failure($"Failed to write meta save: {exception.Message}");
            }
        }

        public Result<MetaProgressSnapshot> Load()
        {
            if (!HasSave)
            {
                return Result<MetaProgressSnapshot>.Failure($"No meta save at '{SavePath}'.");
            }

            try
            {
                var snapshot = JsonUtility.FromJson<MetaProgressSnapshot>(File.ReadAllText(SavePath));
                return snapshot != null
                    ? Result<MetaProgressSnapshot>.Success(snapshot)
                    : Result<MetaProgressSnapshot>.Failure("Meta save could not be parsed.");
            }
            catch (Exception exception)
            {
                return Result<MetaProgressSnapshot>.Failure($"Failed to read meta save: {exception.Message}");
            }
        }
    }
}
