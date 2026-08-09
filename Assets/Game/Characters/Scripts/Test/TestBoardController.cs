using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    public sealed class TestBoardController : MonoBehaviour
    {
        public const int MinSize = 5;
        public const int MaxSize = 10;

        [Min(MinSize)]
        [SerializeField] private int rows = 6;
        [Min(MinSize)]
        [SerializeField] private int columns = 6;
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private TestBoardCellUI cellPrefab;
        [SerializeField] private List<Vector2Int> blockedPositions = new List<Vector2Int>();

        [Header("自适应格子大小")]
        [Min(24f)]
        [SerializeField] private float maxCellSize = 128f;
        [Min(0f)]
        [SerializeField] private float cellSpacing = 8f;
        [Min(24f)]
        [SerializeField] private float minCellSize = 40f;

        private readonly List<TestBoardCellUI> cells = new List<TestBoardCellUI>();

        public event Action<ICharacterPlacementCell> CellClicked;
        public event Action<CharacterData, ICharacterPlacementCell> CharacterDropped;

        /// <summary>棋盘重建完成后触发，参数为 (行数, 列数)。</summary>
        public event Action<int, int> GridGenerated;

        public IReadOnlyList<TestBoardCellUI> Cells => cells;

        public int Rows => rows;

        public int Columns => columns;

        public RectTransform GridRoot => gridRoot;

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
                Debug.LogWarning("TestBoardController is missing its grid root or cell prefab.", this);
                return;
            }

            ApplyLayoutForCurrentSize();

            HashSet<Vector2Int> blocked = new HashSet<Vector2Int>(blockedPositions);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    Vector2Int position = new Vector2Int(column, row);
                    TestBoardCellUI cell = Instantiate(cellPrefab, gridRoot);
                    cell.name = $"TestCell_{column}_{row}";
                    cell.Configure(position, !blocked.Contains(position));
                    cell.Clicked += HandleCellClicked;
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
            UnsubscribeFromCells();
            foreach (TestBoardCellUI cell in cells)
            {
                if (cell != null)
                {
                    Destroy(cell.gameObject);
                }
            }

            cells.Clear();
        }

        private void UnsubscribeFromCells()
        {
            foreach (TestBoardCellUI cell in cells)
            {
                if (cell != null)
                {
                    cell.Clicked -= HandleCellClicked;
                    cell.CharacterDropped -= HandleCharacterDropped;
                }
            }
        }

        private void HandleCellClicked(ICharacterPlacementCell cell)
        {
            CellClicked?.Invoke(cell);
        }

        private void HandleCharacterDropped(CharacterData character, ICharacterPlacementCell cell)
        {
            CharacterDropped?.Invoke(character, cell);
        }
    }
}
