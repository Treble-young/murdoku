using UnityEngine;

namespace Murdoku
{
    /// <summary>保存在本机的关卡通关状态。</summary>
    public static class PuzzleProgress
    {
        private const string SolvedPrefix = "Murdoku.PuzzleSolved.";

        public static bool IsSolved(string puzzleId)
        {
            return !string.IsNullOrEmpty(puzzleId) && PlayerPrefs.GetInt(SolvedPrefix + puzzleId, 0) == 1;
        }

        public static void MarkSolved(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId))
            {
                return;
            }

            PlayerPrefs.SetInt(SolvedPrefix + puzzleId, 1);
            PlayerPrefs.Save();
        }

        public static void ClearSolved(string puzzleId)
        {
            if (!string.IsNullOrEmpty(puzzleId))
            {
                PlayerPrefs.DeleteKey(SolvedPrefix + puzzleId);
            }
        }
    }
}
