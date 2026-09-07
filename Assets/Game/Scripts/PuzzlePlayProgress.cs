using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Murdoku
{
    [Serializable]
    public sealed class PuzzlePlayProgressData
    {
        public string puzzleId;
        public List<PuzzlePlacementData> placements = new List<PuzzlePlacementData>();
        public List<PuzzleCellProgressData> cells = new List<PuzzleCellProgressData>();
    }

    [Serializable]
    public sealed class PuzzleCellProgressData
    {
        public int cellIndex;
        public bool playerMarked;
        public List<string> candidateCharacterIds = new List<string>();
    }

    /// <summary>玩家未完成推理的本机持久化存档；与关卡题面文件分开保存。</summary>
    public static class PuzzlePlayProgress
    {
        private const string FolderName = "PlayProgress";

        public static void Save(PuzzlePlayProgressData data)
        {
            if (data == null || string.IsNullOrEmpty(data.puzzleId) || !HasMeaningfulState(data))
            {
                if (data != null)
                {
                    Delete(data.puzzleId);
                }
                return;
            }

            string directory = Path.Combine(Application.persistentDataPath, FolderName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(GetPath(data.puzzleId), JsonUtility.ToJson(data, true));
        }

        public static PuzzlePlayProgressData Load(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId))
            {
                return null;
            }

            string path = GetPath(puzzleId);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                PuzzlePlayProgressData data = JsonUtility.FromJson<PuzzlePlayProgressData>(File.ReadAllText(path));
                return data != null && HasMeaningfulState(data) ? data : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("读取未完成推理存档失败：" + exception.Message);
                return null;
            }
        }

        public static void Delete(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId))
            {
                return;
            }

            string path = GetPath(puzzleId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static bool HasMeaningfulState(PuzzlePlayProgressData data)
        {
            return data != null &&
                   ((data.placements != null && data.placements.Count > 0) ||
                    (data.cells != null && data.cells.Count > 0));
        }

        private static string GetPath(string puzzleId)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                puzzleId = puzzleId.Replace(invalid, '_');
            }
            return Path.Combine(Application.persistentDataPath, FolderName, puzzleId + ".json");
        }
    }
}
