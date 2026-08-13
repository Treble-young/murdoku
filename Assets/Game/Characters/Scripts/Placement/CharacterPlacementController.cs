using System.Collections.Generic;
using Murdoku;
using UnityEngine;

namespace Murdoku.Characters
{
    public enum CharacterPlacementResult
    {
        NoCharacterSelected,
        InvalidCell,
        CellNotPlaceable,
        CellOccupied,
        AlreadyInCell,
        RejectedByCell,
        RowColumnConflict,
        Placed,
        Moved
    }

    public sealed class CharacterPlacementController : MonoBehaviour
    {
        [SerializeField] private CharacterPanelUI selectionSource;

        /// <summary>一步放置/移动操作的撤销/重做记录。</summary>
        private sealed class PlacementUndoEntry
        {
            public CharacterData Character;
            public ICharacterPlacementCell FromCell; // 操作前所在格子；首次放置为 null（面板）
            public ICharacterPlacementCell ToCell;   // 操作后所在格子（撤销前的当前位置）
        }

        private readonly Dictionary<CharacterData, ICharacterPlacementCell> placements =
            new Dictionary<CharacterData, ICharacterPlacementCell>();
        private readonly List<PlacementUndoEntry> undoHistory = new List<PlacementUndoEntry>();
        private readonly List<PlacementUndoEntry> redoHistory = new List<PlacementUndoEntry>();

        public CharacterData SelectedCharacter { get; private set; }

        public CharacterPanelUI SelectionSource => selectionSource;

        public int UndoHistoryCount => undoHistory.Count;

        public int RedoHistoryCount => redoHistory.Count;

        private void OnEnable()
        {
            SubscribeToSelectionSource();
        }

        private void OnDisable()
        {
            UnsubscribeFromSelectionSource();
        }

        public void SetSelectionSource(CharacterPanelUI source)
        {
            if (selectionSource == source)
            {
                return;
            }

            UnsubscribeFromSelectionSource();
            selectionSource = source;

            if (isActiveAndEnabled)
            {
                SubscribeToSelectionSource();
            }
        }

        public CharacterPlacementResult HandleCellClicked(ICharacterPlacementCell cell)
        {
            if (SelectedCharacter == null)
            {
                return CharacterPlacementResult.NoCharacterSelected;
            }

            return HandleCharacterDropped(SelectedCharacter, cell);
        }

        public CharacterPlacementResult HandleCharacterDropped(
            CharacterData character,
            ICharacterPlacementCell cell)
        {
            if (character == null)
            {
                return CharacterPlacementResult.NoCharacterSelected;
            }

            if (cell == null)
            {
                return CharacterPlacementResult.InvalidCell;
            }

            if (placements.TryGetValue(character, out ICharacterPlacementCell currentCell) &&
                ReferenceEquals(currentCell, cell))
            {
                return CharacterPlacementResult.AlreadyInCell;
            }

            if (!cell.IsPlaceable)
            {
                return CharacterPlacementResult.CellNotPlaceable;
            }

            if (cell.IsOccupied)
            {
                return CharacterPlacementResult.CellOccupied;
            }

            if (HasRowOrColumnConflict(character, cell))
            {
                return CharacterPlacementResult.RowColumnConflict;
            }

            if (!cell.TryPlaceCharacter(character))
            {
                return CharacterPlacementResult.RejectedByCell;
            }

            bool moved = currentCell != null;
            if (moved)
            {
                currentCell.RemoveCharacter();
            }

            placements[character] = cell;

            // 记录撤销历史：移动记录原格子，首次放置记录 null；新操作清空重做栈。
            undoHistory.Add(new PlacementUndoEntry
            {
                Character = character,
                FromCell = moved ? currentCell : null,
                ToCell = cell
            });
            redoHistory.Clear();

            return moved ? CharacterPlacementResult.Moved : CharacterPlacementResult.Placed;
        }

        /// <summary>
        /// 撤销最近一步放置/移动操作（多步可连续撤销）。
        /// 移动：人物回到原格子；首次放置：人物移除回面板。
        /// </summary>
        public bool UndoLastPlacement()
        {
            if (undoHistory.Count == 0)
            {
                return false;
            }

            PlacementUndoEntry entry = undoHistory[undoHistory.Count - 1];
            undoHistory.RemoveAt(undoHistory.Count - 1);

            if (entry.Character == null)
            {
                return true;
            }

            // 从当前位置（ToCell）移除。
            ICharacterPlacementCell fromAfterUndo = null;
            if (placements.TryGetValue(entry.Character, out ICharacterPlacementCell currentCell) &&
                currentCell != null)
            {
                currentCell.RemoveCharacter();
                placements.Remove(entry.Character);
            }

            // 移动操作：放回原格子（若仍可放置且未被占用）。
            if (entry.FromCell != null &&
                entry.FromCell.IsPlaceable && !entry.FromCell.IsOccupied &&
                entry.FromCell.TryPlaceCharacter(entry.Character))
            {
                placements[entry.Character] = entry.FromCell;
                fromAfterUndo = entry.FromCell;
            }

            // 记录重做信息：撤销后人物所在位置 + 重做目标（ToCell）。
            redoHistory.Add(new PlacementUndoEntry
            {
                Character = entry.Character,
                FromCell = fromAfterUndo,
                ToCell = entry.ToCell
            });

            return true;
        }

        /// <summary>
        /// 恢复（重做）最近一次被撤销的放置/移动操作（多步可连续恢复）。
        /// </summary>
        public bool RedoLastPlacement()
        {
            if (redoHistory.Count == 0)
            {
                return false;
            }

            PlacementUndoEntry entry = redoHistory[redoHistory.Count - 1];
            redoHistory.RemoveAt(redoHistory.Count - 1);

            if (entry.Character == null)
            {
                return true;
            }

            // 从当前位置移除。
            if (placements.TryGetValue(entry.Character, out ICharacterPlacementCell currentCell) &&
                currentCell != null)
            {
                currentCell.RemoveCharacter();
                placements.Remove(entry.Character);
            }

            // 放回 ToCell（若仍可放置且未被占用）。
            bool placed = entry.ToCell != null &&
                          entry.ToCell.IsPlaceable && !entry.ToCell.IsOccupied &&
                          entry.ToCell.TryPlaceCharacter(entry.Character);
            if (placed)
            {
                placements[entry.Character] = entry.ToCell;
            }

            // 记录撤销信息：重做后人物所在位置。
            undoHistory.Add(new PlacementUndoEntry
            {
                Character = entry.Character,
                FromCell = entry.FromCell,
                ToCell = placed ? entry.ToCell : null
            });

            return true;
        }

        /// <summary>
        /// 清空撤销与重做历史（载入关卡/重建棋盘时调用）。
        /// </summary>
        public void ClearUndoHistory()
        {
            undoHistory.Clear();
            redoHistory.Clear();
        }

        /// <summary>
        /// Murdoku 核心规则：每行、每列最多站一个人。
        /// 目标格所在行/列已被其他角色占用时拒绝放置（移动时跳过自身旧位置）。
        /// </summary>
        private bool HasRowOrColumnConflict(CharacterData character, ICharacterPlacementCell target)
        {
            foreach (KeyValuePair<CharacterData, ICharacterPlacementCell> pair in placements)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                if (ReferenceEquals(pair.Key, character))
                {
                    continue;
                }

                if (pair.Value.GridPosition.x == target.GridPosition.x ||
                    pair.Value.GridPosition.y == target.GridPosition.y)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 统计指定角色中尚未放置到棋盘的人数。
        /// </summary>
        public int CountMissingCharacters(IReadOnlyList<CharacterData> characters)
        {
            if (characters == null)
            {
                return 0;
            }

            int missing = 0;
            foreach (CharacterData character in characters)
            {
                if (character == null)
                {
                    continue;
                }

                if (!placements.TryGetValue(character, out ICharacterPlacementCell cell) || cell == null)
                {
                    missing++;
                }
            }

            return missing;
        }

        /// <summary>
        /// 是否存在两个角色占用同一行或同一列（用于提交时兜底校验旧存档）。
        /// </summary>
        public bool HasRowColumnConflict()
        {
            return GetRowColumnConflictCells().Count > 0;
        }

        /// <summary>
        /// 返回所有参与同行/同列冲突的角色所在格子。
        /// </summary>
        public List<ICharacterPlacementCell> GetRowColumnConflictCells()
        {
            List<ICharacterPlacementCell> result = new List<ICharacterPlacementCell>();
            List<ICharacterPlacementCell> cellsList = new List<ICharacterPlacementCell>(placements.Values);
            for (int i = 0; i < cellsList.Count; i++)
            {
                for (int j = i + 1; j < cellsList.Count; j++)
                {
                    if (cellsList[i] == null || cellsList[j] == null)
                    {
                        continue;
                    }

                    if (cellsList[i].GridPosition.x == cellsList[j].GridPosition.x ||
                        cellsList[i].GridPosition.y == cellsList[j].GridPosition.y)
                    {
                        if (!result.Contains(cellsList[i]))
                        {
                            result.Add(cellsList[i]);
                        }

                        if (!result.Contains(cellsList[j]))
                        {
                            result.Add(cellsList[j]);
                        }
                    }
                }
            }

            return result;
        }

        public bool TryGetPlacement(CharacterData character, out ICharacterPlacementCell cell)
        {
            return placements.TryGetValue(character, out cell);
        }

        /// <summary>
        /// 导出当前角色放置，供关卡存档使用（cellIndex = row * columns + col）。
        /// </summary>
        public List<PuzzlePlacementData> ExportPlacements(int columns)
        {
            List<PuzzlePlacementData> result = new List<PuzzlePlacementData>();
            foreach (KeyValuePair<CharacterData, ICharacterPlacementCell> pair in placements)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                string id = string.IsNullOrEmpty(pair.Key.CharacterId)
                    ? pair.Key.DisplayName
                    : pair.Key.CharacterId;
                Vector2Int position = pair.Value.GridPosition;
                result.Add(new PuzzlePlacementData
                {
                    characterId = id,
                    cellIndex = position.y * columns + position.x
                });
            }

            return result;
        }

        /// <summary>
        /// 按 CharacterId（或显示名）在角色面板数据中查找角色。
        /// </summary>
        public CharacterData FindCharacterById(string characterId)
        {
            if (selectionSource == null || string.IsNullOrEmpty(characterId))
            {
                return null;
            }

            foreach (CharacterData character in selectionSource.Characters)
            {
                if (character == null)
                {
                    continue;
                }

                if (string.Equals(character.CharacterId, characterId, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(character.DisplayName, characterId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return character;
                }
            }

            return null;
        }

        private void SubscribeToSelectionSource()
        {
            if (selectionSource == null)
            {
                return;
            }

            selectionSource.SelectionChanged -= HandleSelectionChanged;
            selectionSource.SelectionChanged += HandleSelectionChanged;
            SelectedCharacter = selectionSource.SelectedCharacter;
        }

        private void UnsubscribeFromSelectionSource()
        {
            if (selectionSource != null)
            {
                selectionSource.SelectionChanged -= HandleSelectionChanged;
            }
        }

        private void HandleSelectionChanged(CharacterData character)
        {
            SelectedCharacter = character;
        }
    }
}
