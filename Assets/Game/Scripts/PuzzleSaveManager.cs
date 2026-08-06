using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Murdoku
{
    /// <summary>
    /// 关卡存档管理：把玩家创建的出题保存为 JSON 文件（persistentDataPath/Puzzles/）。
    /// </summary>
    public static class PuzzleSaveManager
    {
        private static string PuzzlesDirectory
        {
            get { return Path.Combine(Application.persistentDataPath, "Puzzles"); }
        }

        public static string GenerateId()
        {
            return "puzzle_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        }

        public static void SavePuzzle(PuzzleData data)
        {
            if (data == null)
            {
                Debug.LogError("PuzzleSaveManager.SavePuzzle: data is null.");
                return;
            }

            if (string.IsNullOrEmpty(data.id))
            {
                data.id = GenerateId();
            }

            Directory.CreateDirectory(PuzzlesDirectory);
            string path = Path.Combine(PuzzlesDirectory, data.id + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        public static PuzzleData LoadPuzzle(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            string path = Path.Combine(PuzzlesDirectory, id + ".json");
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<PuzzleData>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogError("PuzzleSaveManager.LoadPuzzle failed: " + exception);
                return null;
            }
        }

        public static List<PuzzleData> ListPuzzles()
        {
            List<PuzzleData> result = new List<PuzzleData>();
            if (!Directory.Exists(PuzzlesDirectory))
            {
                return result;
            }

            foreach (string file in Directory.GetFiles(PuzzlesDirectory, "*.json"))
            {
                try
                {
                    PuzzleData data = JsonUtility.FromJson<PuzzleData>(File.ReadAllText(file));
                    if (data != null && !string.IsNullOrEmpty(data.id))
                    {
                        result.Add(data);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("PuzzleSaveManager skipped an unreadable save: " + exception);
                }
            }

            // id 含时间戳，按 id 倒序 = 最近创建的排前面。
            result.Sort((left, right) => string.Compare(right.id, left.id, StringComparison.Ordinal));
            return result;
        }

        public static bool DeletePuzzle(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            string path = Path.Combine(PuzzlesDirectory, id + ".json");
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
    }
}
