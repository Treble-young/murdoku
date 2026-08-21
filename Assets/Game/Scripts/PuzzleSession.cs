namespace Murdoku
{
    /// <summary>
    /// 跨场景传递当前选中的关卡 ID（选关场景 -> 出题/游戏场景）。
    /// </summary>
    public static class PuzzleSession
    {
        public static string SelectedPuzzleId { get; set; }

        /// <summary>
        /// 是否以编辑模式载入（谜题列表点「编辑」：载入关卡进入出题界面，
        /// 在原有基础上修改而非游玩）；游玩模式载入为 false。
        /// </summary>
        public static bool EditMode { get; set; }
    }
}
