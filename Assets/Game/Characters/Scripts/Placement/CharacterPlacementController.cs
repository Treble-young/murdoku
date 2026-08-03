using System.Collections.Generic;
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
