using TMPro;
using UnityEngine;

namespace Murdoku.Characters
{
    public sealed class CharacterPanelTestCoordinator : MonoBehaviour
    {
        [SerializeField] private TestBoardController testBoard;
        [SerializeField] private CharacterPlacementController placementController;
        [SerializeField] private TMP_Text placementStatusText;

        private void OnEnable()
        {
            if (testBoard != null)
            {
                testBoard.CellClicked += HandleCellClicked;
                testBoard.CharacterDropped += HandleCharacterDropped;
            }
        }

        private void Start()
        {
            SetStatus("点击人物后选择格子，或直接拖动人物卡到右侧格子。");
        }

        private void OnDisable()
        {
            if (testBoard != null)
            {
                testBoard.CellClicked -= HandleCellClicked;
                testBoard.CharacterDropped -= HandleCharacterDropped;
            }
        }

        private void HandleCellClicked(ICharacterPlacementCell cell)
        {
            if (placementController == null)
            {
                SetStatus("人物放置控制器未配置。");
                return;
            }

            CharacterData selected = placementController.SelectedCharacter;
            CharacterPlacementResult result = placementController.HandleCellClicked(cell);
            ShowPlacementResult(selected, cell, result);
        }

        private void HandleCharacterDropped(CharacterData character, ICharacterPlacementCell cell)
        {
            if (placementController == null)
            {
                SetStatus("人物放置控制器未配置。");
                return;
            }

            CharacterPlacementResult result = placementController.HandleCharacterDropped(character, cell);
            ShowPlacementResult(character, cell, result);
        }

        private void ShowPlacementResult(
            CharacterData character,
            ICharacterPlacementCell cell,
            CharacterPlacementResult result)
        {
            string characterName = character == null ? "人物" : character.DisplayName;

            switch (result)
            {
                case CharacterPlacementResult.NoCharacterSelected:
                    SetStatus("请先选择或拖动一名人物。");
                    break;
                case CharacterPlacementResult.CellNotPlaceable:
                    SetStatus("该格子不可放置人物。");
                    break;
                case CharacterPlacementResult.CellOccupied:
                    SetStatus("目标格已被其他人物占据，原位置保持不变。");
                    break;
                case CharacterPlacementResult.AlreadyInCell:
                    SetStatus($"{characterName} 已经在这个格子中。");
                    break;
                case CharacterPlacementResult.Placed:
                    SetStatus($"已将 {characterName} 放置到 ({cell.GridPosition.x}, {cell.GridPosition.y})。");
                    break;
                case CharacterPlacementResult.Moved:
                    SetStatus($"已将 {characterName} 移动到 ({cell.GridPosition.x}, {cell.GridPosition.y})。");
                    break;
                default:
                    SetStatus("放置失败，人物位置未改变。");
                    break;
            }
        }

        private void SetStatus(string message)
        {
            if (placementStatusText != null)
            {
                placementStatusText.text = message;
            }
        }
    }
}
