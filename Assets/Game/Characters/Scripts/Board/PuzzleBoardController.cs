using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    public sealed class PuzzleBoardController : MonoBehaviour
    {
        public const int MinSize = 5;
        public const int MaxSize = 10;

        [Min(MinSize)]
        [SerializeField] private int rows = 6;
        [Min(MinSize)]
        [SerializeField] private int columns = 6;
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private PuzzleBoardCellUI cellPrefab;
        [SerializeField] private List<Vector2Int> blockedPositions = new List<Vector2Int>();

        [Header("自适应格子大小")]
        [Min(24f)]
        [SerializeField] private float maxCellSize = 128f;
        [Min(0f)]
        [SerializeField] private float cellSpacing = 4f;
        [Min(24f)]
        [SerializeField] private float minCellSize = 40f;

        private readonly List<PuzzleBoardCellUI> cells = new List<PuzzleBoardCellUI>();

        public event Action<ICharacterPlacementCell> CellClicked;
        public event Action<ICharacterPlacementCell> CellLongPressed;
        public event Action<CharacterData, ICharacterPlacementCell> CharacterDropped;

        /// <summary>棋盘重建完成后触发，参数为 (行数, 列数)。</summary>
        public event Action<int, int> GridGenerated;

        public IReadOnlyList<PuzzleBoardCellUI> Cells => cells;

        public int Rows => rows;

        public int Columns => columns;

        public RectTransform GridRoot => gridRoot;

        /// <summary>
        /// 行列高亮已取消（放置人物后改用黑叉禁用标记，湖泊等蓝色格子上更醒目），
        /// 此方法现在只负责清除所有行列高亮。
        /// </summary>
        public void RefreshRowColumnHighlights()
        {
            foreach (PuzzleBoardCellUI cell in cells)
            {
                if (cell != null)
                {
                    cell.SetRowColumnHighlight(false);
                }
            }
        }

        /// <summary>
        /// 放置人物后：给该人物所在行/列的空格打上玩家禁用标记（黑叉，推理辅助）。
        /// 返回被打标记的格子列表（撤销时清除）。
        /// </summary>
        public List<PuzzleBoardCellUI> DisableRowColumnCells(CharacterData placed, ICharacterPlacementCell atCell)
        {
            List<PuzzleBoardCellUI> disabled = new List<PuzzleBoardCellUI>();
            if (placed == null || atCell == null)
            {
                return disabled;
            }

            // 人物可能是从别处移动过来的：先移除它在旧行列留下的自动来源。
            ClearRowColumnCells(placed);

            int row = atCell.GridPosition.y;
            int column = atCell.GridPosition.x;
            foreach (PuzzleBoardCellUI cell in cells)
            {
                if (cell == null || cell.IsOccupied)
                {
                    continue;
                }

                Vector2Int pos = cell.GridPosition;
                if (pos.y == row || pos.x == column)
                {
                    if (cell.SetAutomaticPlayerMark(placed, true))
                    {
                        disabled.Add(cell);
                    }
                }
            }

            return disabled;
        }

        /// <summary>只移除指定人物产生的行列自动禁放来源，保留玩家手动叉号和其他人物来源。</summary>
        public void ClearRowColumnCells(CharacterData placed)
        {
            if (placed == null)
            {
                return;
            }

            foreach (PuzzleBoardCellUI cell in cells)
            {
                if (cell != null)
                {
                    cell.SetAutomaticPlayerMark(placed, false);
                }
            }
        }

        /// <summary>
        /// 放置人物后：清除该人物所在行/列上其他角色的候选标记（该行列已被占用）。
        /// 返回被清除的 (格子, 角色) 记录列表（撤销时恢复）。
        /// </summary>
        public List<(PuzzleBoardCellUI, CharacterData)> ClearOtherMarksInRowColumn(
            CharacterData placed,
            ICharacterPlacementCell atCell)
        {
            List<(PuzzleBoardCellUI, CharacterData)> cleared = new List<(PuzzleBoardCellUI, CharacterData)>();
            if (placed == null || atCell == null)
            {
                return cleared;
            }

            int row = atCell.GridPosition.y;
            int column = atCell.GridPosition.x;
            foreach (PuzzleBoardCellUI cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                Vector2Int pos = cell.GridPosition;
                if (pos.y != row && pos.x != column)
                {
                    continue;
                }

                List<CharacterData> removed = cell.RemoveCandidateMarksExcept(placed);
                foreach (CharacterData character in removed)
                {
                    if (character != null)
                    {
                        cleared.Add((cell, character));
                    }
                }
            }

            return cleared;
        }

        /// <summary>
        /// 清除提交失败时标记的红色错误格（行/列高亮由 RefreshRowColumnHighlights 统一刷新）。
        /// </summary>
        public void ClearErrorHighlights()
        {
            foreach (PuzzleBoardCellUI cell in cells)
            {
                if (cell != null)
                {
                    cell.SetErrorHighlight(false);
                }
            }
        }

        private void Start()
        {
            // Canvas 第一帧布局未完成时 gridRoot.rect 为 0，会导致格子被缩到最小值。
            // 延迟一帧等布局完成后再生成棋盘，保证 rect 有效。
            StartCoroutine(GenerateAfterLayout());
        }

        private System.Collections.IEnumerator GenerateAfterLayout()
        {
            yield return null;
            GenerateGrid();
        }

        private void OnDestroy()
        {
            UnsubscribeFromCells();
        }

        public void GenerateGrid()
        {
            ClearGrid();

            if (gridRoot == null || cellPrefab == null)
            {
                Debug.LogWarning("PuzzleBoardController is missing its grid root or cell prefab.", this);
                return;
            }

            ApplyLayoutForCurrentSize();

            HashSet<Vector2Int> blocked = new HashSet<Vector2Int>(blockedPositions);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    Vector2Int position = new Vector2Int(column, row);
                    PuzzleBoardCellUI cell = Instantiate(cellPrefab, gridRoot);
                    cell.name = $"PuzzleCell_{column}_{row}";
                    cell.Configure(position, !blocked.Contains(position));
                    cell.Clicked += HandleCellClicked;
                    cell.LongPressed += HandleCellLongPressed;
                    cell.CharacterDropped += HandleCharacterDropped;
                    cells.Add(cell);
                }
            }

            GridGenerated?.Invoke(rows, columns);
        }

        /// <summary>
        /// 运行时重新设置棋盘行列数并重建棋盘（尺寸会被限制在 MinSize~MaxSize）。
        /// </summary>
        public void SetGridSize(int newRows, int newColumns)
        {
            rows = Mathf.Clamp(newRows, MinSize, MaxSize);
            columns = Mathf.Clamp(newColumns, MinSize, MaxSize);
            GenerateGrid();
        }

        /// <summary>
        /// 根据列数自动计算格子大小，保证棋盘始终适配 gridRoot 的固定区域。
        /// </summary>
        private void ApplyLayoutForCurrentSize()
        {
            GridLayoutGroup layout = gridRoot.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                return;
            }

            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;

            float spacing = Mathf.Max(0f, cellSpacing);
            float availableWidth = Mathf.Max(1f, gridRoot.rect.width - spacing * (columns - 1));
            float cellSize = Mathf.Clamp(availableWidth / columns, minCellSize, maxCellSize);
            layout.cellSize = new Vector2(cellSize, cellSize);
            layout.spacing = new Vector2(spacing, spacing);
        }

        private void ClearGrid()
        {
            var generatedCells = new HashSet<PuzzleBoardCellUI>();
            foreach (PuzzleBoardCellUI cell in cells)
            {
                if (cell != null)
                {
                    generatedCells.Add(cell);
                }
            }

            // 域重载或编辑器验证中断后，cells 列表可能为空，但旧格子仍留在 PuzzleGrid 下。
            if (gridRoot != null)
            {
                foreach (PuzzleBoardCellUI cell in gridRoot.GetComponentsInChildren<PuzzleBoardCellUI>(true))
                {
                    if (cell != null)
                    {
                        generatedCells.Add(cell);
                    }
                }
            }

            foreach (PuzzleBoardCellUI cell in generatedCells)
            {
                cell.Clicked -= HandleCellClicked;
                cell.CharacterDropped -= HandleCharacterDropped;
                DestroyGeneratedCell(cell.gameObject);
            }

            cells.Clear();
        }

        private static void DestroyGeneratedCell(GameObject generatedCell)
        {
            if (generatedCell == null)
            {
                return;
            }

            // 先脱离 GridLayoutGroup，避免 Play Mode 同一帧重建时旧格子仍参与布局。
            generatedCell.transform.SetParent(null, false);
            if (Application.isPlaying)
            {
                Destroy(generatedCell);
            }
            else
            {
                DestroyImmediate(generatedCell);
            }
        }

        private void UnsubscribeFromCells()
        {
            foreach (PuzzleBoardCellUI cell in cells)
            {
                if (cell != null)
                {
                    cell.Clicked -= HandleCellClicked;
                    cell.LongPressed -= HandleCellLongPressed;
                    cell.CharacterDropped -= HandleCharacterDropped;
                }
            }
        }

        /// <summary>
        /// 人物放置后清空该人物在整张棋盘上的候选标记，避免残留干扰推理。
        /// 返回实际清除了标记的格子列表（供撤销放置时恢复标记）。
        /// </summary>
        public List<PuzzleBoardCellUI> ClearCandidateMarksFor(CharacterData character)
        {
            List<PuzzleBoardCellUI> cleared = new List<PuzzleBoardCellUI>();
            foreach (PuzzleBoardCellUI cell in cells)
            {
                if (cell != null && cell.RemoveCandidateMark(character))
                {
                    cleared.Add(cell);
                }
            }

            return cleared;
        }

        private void HandleCellClicked(ICharacterPlacementCell cell)
        {
            CellClicked?.Invoke(cell);
        }

        private void HandleCellLongPressed(ICharacterPlacementCell cell)
        {
            CellLongPressed?.Invoke(cell);
        }

        private void HandleCharacterDropped(CharacterData character, ICharacterPlacementCell cell)
        {
            CharacterDropped?.Invoke(character, cell);
        }
    }
}
