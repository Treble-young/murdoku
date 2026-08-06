namespace Murdoku
{
    /// <summary>
    /// 跨场景传递当前选中的关卡 ID（选关场景 -> 出题/游戏场景）。
    /// </summary>
    public static class PuzzleSession
    {
        public static string SelectedPuzzleId { get; set; }
    }
}
