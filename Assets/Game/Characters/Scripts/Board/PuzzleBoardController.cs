using System;
using System.Collections.Generic;
using TMPro;
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
        private const float CoordinateGap = 30f;
        private const float CoordinateLabelSize = 46f;
        private RectTransform coordinateOverlay;
        private readonly List<GameObject> coordinateLabels = new List<GameObject>();
        private TMP_FontAsset coordinateFont;

        public event Action<ICharacterPlacementCell> CellClicked;
        public event Action<ICharacterPlacementCell> CellLongPressed;
        public event Action<ICharacterPlacementCell> CellRightClicked;
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
                    cell.RightClicked += HandleCellRightClicked;
                    cell.CharacterDropped += HandleCharacterDropped;
                    cells.Add(cell);
                }
            }

            RefreshCoordinateLabels();
            GridGenerated?.Invoke(rows, columns);
        }

        /// <summary>
        /// 棋盘行列标号：列标在棋盘上侧、行标在棋盘左侧，均从左上角以 1 起始，
        /// 方便玩家根据线索按“第几行/第几列”定位。
        /// </summary>
        private void RefreshCoordinateLabels()
        {
            ClearCoordinateLabels();

            if (gridRoot == null)
            {
                return;
            }

            GridLayoutGroup layout = gridRoot.GetComponent<GridLayoutGroup>();
            TMP_FontAsset font = GetCoordinateFont();
            if (layout == null || font == null || !EnsureCoordinateOverlay())
            {
                return;
            }

            float cellWidth = layout.cellSize.x;
            float cellHeight = layout.cellSize.y;
            float spacing = layout.spacing.x;
            int columnCount = Mathf.Max(1, layout.constraintCount);
            float totalWidth = columnCount * cellWidth + (columnCount - 1) * spacing;
            float totalHeight = rows * cellHeight + (rows - 1) * spacing;
            float originX = Mathf.Max(0f, (coordinateOverlay.rect.width - totalWidth) * 0.5f);
            float originY = Mathf.Max(0f, (coordinateOverlay.rect.height - totalHeight) * 0.5f);
            float fontSize = Mathf.Clamp(Mathf.Min(cellWidth, cellHeight) * 0.34f, 18f, 30f);

            // 列标：棋盘上侧，从左到右 1..N。
            for (int column = 0; column < columnCount; column++)
            {
                float x = originX + column * (cellWidth + spacing) + cellWidth * 0.5f;
                AddCoordinateLabel((column + 1).ToString(), x, originY - CoordinateGap, fontSize, font);
            }

            // 行标：棋盘左侧，从上到下 1..N。
            for (int row = 0; row < rows; row++)
            {
                float y = originY + row * (cellHeight + spacing) + cellHeight * 0.5f;
                AddCoordinateLabel((row + 1).ToString(), originX - CoordinateGap, y, fontSize, font);
            }
        }

        private TMP_FontAsset GetCoordinateFont()
        {
            if (coordinateFont == null)
            {
                coordinateFont = TMP_Settings.defaultFontAsset;
            }

            return coordinateFont;
        }

        private bool EnsureCoordinateOverlay()
        {
            if (coordinateOverlay != null)
            {
                return true;
            }

            if (gridRoot == null || gridRoot.parent == null)
            {
                return false;
            }

            GameObject overlayObject = new GameObject("CoordinateLabels", typeof(RectTransform));
            overlayObject.layer = LayerMask.NameToLayer("UI");
            coordinateOverlay = overlayObject.GetComponent<RectTransform>();
            coordinateOverlay.SetParent(gridRoot.parent, false);

            RectTransform gridRect = gridRoot;
            coordinateOverlay.anchorMin = gridRect.anchorMin;
            coordinateOverlay.anchorMax = gridRect.anchorMax;
            coordinateOverlay.pivot = gridRect.pivot;
            coordinateOverlay.anchoredPosition = gridRect.anchoredPosition;
            coordinateOverlay.sizeDelta = gridRect.sizeDelta;
            coordinateOverlay.SetAsLastSibling();
            return true;
        }

        private void AddCoordinateLabel(
            string text,
            float x,
            float y,
            float fontSize,
            TMP_FontAsset font)
        {
            GameObject labelObject = new GameObject(
                "CoordLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(coordinateOverlay, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CoordinateLabelSize, CoordinateLabelSize);
            rect.anchoredPosition = new Vector2(
                x - coordinateOverlay.rect.width * 0.5f,
                -(y - coordinateOverlay.rect.height * 0.5f));

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.86f, 0.90f, 0.96f, 1f);
            label.raycastTarget = false;
            coordinateLabels.Add(labelObject);
        }

        private void ClearCoordinateLabels()
        {
            foreach (GameObject labelObject in coordinateLabels)
            {
                if (labelObject != null)
                {
                    Destroy(labelObject);
                }
            }

            coordinateLabels.Clear();
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
                    cell.RightClicked -= HandleCellRightClicked;
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

        private void HandleCellRightClicked(ICharacterPlacementCell cell)
        {
            CellRightClicked?.Invoke(cell);
        }

        private void HandleCharacterDropped(CharacterData character, ICharacterPlacementCell cell)
        {
            CharacterDropped?.Invoke(character, cell);
        }
    }
}
