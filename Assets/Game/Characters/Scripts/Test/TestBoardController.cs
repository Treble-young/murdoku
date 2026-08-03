using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    public sealed class TestBoardController : MonoBehaviour
    {
        [Min(1)]
        [SerializeField] private int rows = 6;
        [Min(1)]
        [SerializeField] private int columns = 6;
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private TestBoardCellUI cellPrefab;
        [SerializeField] private List<Vector2Int> blockedPositions = new List<Vector2Int>();

        private readonly List<TestBoardCellUI> cells = new List<TestBoardCellUI>();

        public event Action<ICharacterPlacementCell> CellClicked;
        public event Action<CharacterData, ICharacterPlacementCell> CharacterDropped;

        public IReadOnlyList<TestBoardCellUI> Cells => cells;

        private void Start()
        {
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

            GridLayoutGroup layout = gridRoot.GetComponent<GridLayoutGroup>();
            if (layout != null)
            {
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = columns;
            }

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
