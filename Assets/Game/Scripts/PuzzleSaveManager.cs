using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Murdoku
{
    /// <summary>
    /// 关卡存档管理：把玩家创建的出题保存为 JSON 文件。
    /// 编辑器（开发/出题）存到项目内 Saves/Puzzles/，随 git 同步到云端仓库；
    /// 构建版（打包后的游戏）存到 persistentDataPath/Puzzles/（可写）。
    /// </summary>
    public static class PuzzleSaveManager
    {
        private const string BundledPuzzlesFolderName = "Puzzles";

        private static string PuzzlesDirectory
        {
            get
            {
                if (Application.isEditor)
                {
                    string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                    return Path.Combine(projectRoot, "Saves", "Puzzles");
                }

                return Path.Combine(Application.persistentDataPath, "Puzzles");
            }
        }

        /// <summary>
        /// 构建版首次读取关卡时，把随安装包发布的预制关卡安装到玩家的可写存档目录。
        /// 同一游戏版本只执行一次，因此玩家删除或编辑预制关卡后不会在下次启动时被还原。
        /// </summary>
        private static void EnsureBundledPuzzlesInstalled()
        {
            if (Application.isEditor)
            {
                return;
            }

            string bundledDirectory = Path.Combine(Application.streamingAssetsPath, BundledPuzzlesFolderName);
            if (!Directory.Exists(bundledDirectory))
            {
                return;
            }

            Directory.CreateDirectory(PuzzlesDirectory);
            string markerName = ".bundled-puzzles-" + SanitizeFileName(Application.version) + ".installed";
            string markerPath = Path.Combine(PuzzlesDirectory, markerName);
            if (File.Exists(markerPath))
            {
                return;
            }

            try
            {
                foreach (string sourcePath in Directory.GetFiles(bundledDirectory, "*.json"))
                {
                    string destinationPath = Path.Combine(PuzzlesDirectory, Path.GetFileName(sourcePath));
                    if (!File.Exists(destinationPath))
                    {
                        File.Copy(sourcePath, destinationPath);
                    }
                }

                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            }
            catch (Exception exception)
            {
                // 安装失败时不写 marker，下次读取关卡时会自动重试。
                Debug.LogError("PuzzleSaveManager failed to install bundled puzzles: " + exception);
            }
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrEmpty(value) ? "unknown" : value;
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalidCharacter, '_');
            }

            return result;
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

            NormalizeLegacyFontSubstitutions(data);

            Directory.CreateDirectory(PuzzlesDirectory);
            string path = Path.Combine(PuzzlesDirectory, data.id + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        public static PuzzleData LoadPuzzle(string id)
        {
            EnsureBundledPuzzlesInstalled();

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
                PuzzleData data = JsonUtility.FromJson<PuzzleData>(File.ReadAllText(path));
                NormalizeLegacyFontSubstitutions(data);
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogError("PuzzleSaveManager.LoadPuzzle failed: " + exception);
                return null;
            }
        }

        public static List<PuzzleData> ListPuzzles()
        {
            EnsureBundledPuzzlesInstalled();

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
                    NormalizeLegacyFontSubstitutions(data);
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

        /// <summary>
        /// 早期字体的字形映射有误，部分关卡曾用其他汉字代替目标汉字。
        /// 这里只迁移已经核实的词组，避免把正常出现的“三”等文字误改。
        /// </summary>
        private static void NormalizeLegacyFontSubstitutions(PuzzleData data)
        {
            if (data == null)
            {
                return;
            }

            data.name = NormalizeLegacyFontText(data.name);
            data.globalClue = NormalizeLegacyFontText(data.globalClue);
            if (data.clues == null)
            {
                return;
            }

            foreach (PuzzleClueData clue in data.clues)
            {
                if (clue == null)
                {
                    continue;
                }

                clue.name = NormalizeLegacyFontText(clue.name);
                clue.clue = NormalizeLegacyFontText(clue.clue);
            }
        }

        private static string NormalizeLegacyFontText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return text
                .Replace("人物卡三", "人物卡上")
                .Replace("格子三", "格子上")
                .Replace("椅子三", "椅子上")
                .Replace("长椅三", "长椅上")
                .Replace("座位三", "座位上")
                .Replace("雪地三", "雪地上")
                .Replace("地毯三", "地毯上")
                .Replace("贝壳三", "贝壳上")
                .Replace("游泳圈三", "游泳圈上")
                .Replace("床三", "床上")
                .Replace("三面", "上面")
                .Replace("三方", "上方")
                .Replace("第丈", "第三");
        }

        public static bool NameExists(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            List<PuzzleData> puzzles = ListPuzzles();
            for (int i = 0; i < puzzles.Count; i++)
            {
                if (puzzles[i] != null && string.Equals(puzzles[i].name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool DeletePuzzle(string id)
        {
            EnsureBundledPuzzlesInstalled();

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
