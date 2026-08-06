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
        Placed,
        Moved
    }

    public sealed class CharacterPlacementController : MonoBehaviour
    {
        [SerializeField] private CharacterPanelUI selectionSource;

        private readonly Dictionary<CharacterData, ICharacterPlacementCell> placements =
            new Dictionary<CharacterData, ICharacterPlacementCell>();

        public CharacterData SelectedCharacter { get; private set; }

        public CharacterPanelUI SelectionSource => selectionSource;

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
            return moved ? CharacterPlacementResult.Moved : CharacterPlacementResult.Placed;
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
