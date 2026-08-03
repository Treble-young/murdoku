using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    public sealed class TestBoardCellUI : MonoBehaviour, ICharacterPlacementCell,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("Cell")]
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text coordinateText;

        [Header("Token")]
        [SerializeField] private GameObject tokenRoot;
        [SerializeField] private Image tokenBackground;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text initialText;
        [SerializeField] private TMP_Text characterNameText;

        [Header("Colors")]
        [SerializeField] private Color lightCellColor = new Color(0.78f, 0.88f, 0.94f, 1f);
        [SerializeField] private Color darkCellColor = new Color(0.66f, 0.79f, 0.87f, 1f);
        [SerializeField] private Color blockedCellColor = new Color(0.35f, 0.38f, 0.42f, 1f);

        [SerializeField] private Vector2Int gridPosition;
        [SerializeField] private bool isPlaceable = true;
        [SerializeField] private CharacterData currentCharacter;

        public event Action<ICharacterPlacementCell> Clicked;
        public event Action<CharacterData, ICharacterPlacementCell> CharacterDropped;

        public Vector2Int GridPosition => gridPosition;
        public bool IsPlaceable => isPlaceable;
        public bool IsOccupied => currentCharacter != null;
        public CharacterData CurrentCharacter => currentCharacter;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleButtonClicked);
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleButtonClicked);
            }
        }

        public void Configure(Vector2Int position, bool placeable)
        {
            gridPosition = position;
            isPlaceable = placeable;
            currentCharacter = null;
            Refresh();
        }

        public bool TryPlaceCharacter(CharacterData character)
        {
            if (!isPlaceable || currentCharacter != null || character == null)
            {
                return false;
            }

            currentCharacter = character;
            RefreshToken();
            return true;
        }

        public void RemoveCharacter()
        {
            currentCharacter = null;
            RefreshToken();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (currentCharacter != null)
            {
                CharacterDragPreview.Show(currentCharacter, this, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (currentCharacter != null)
            {
                CharacterDragPreview.Move(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            CharacterDragPreview.Hide();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null)
            {
                return;
            }

            CharacterCardUI sourceCard = eventData.pointerDrag.GetComponentInParent<CharacterCardUI>();
            TestBoardCellUI sourceCell = eventData.pointerDrag.GetComponentInParent<TestBoardCellUI>();
            CharacterData droppedCharacter = sourceCard != null
                ? sourceCard.Character
                : sourceCell != null ? sourceCell.CurrentCharacter : null;

            if (droppedCharacter != null)
            {
                CharacterDropped?.Invoke(droppedCharacter, this);
            }
        }

        private void Refresh()
        {
            if (button != null)
            {
                button.interactable = isPlaceable;
            }

            if (backgroundImage != null)
            {
                bool isOffset = (gridPosition.x + gridPosition.y) % 2 != 0;
                backgroundImage.color = isPlaceable
                    ? (isOffset ? darkCellColor : lightCellColor)
                    : blockedCellColor;
            }

            if (coordinateText != null)
            {
                coordinateText.text = $"{gridPosition.x},{gridPosition.y}";
            }

            RefreshToken();
        }

        private void RefreshToken()
        {
            bool occupied = currentCharacter != null;
            if (tokenRoot != null)
            {
                tokenRoot.SetActive(occupied);
            }

            if (!occupied)
            {
                return;
            }

            if (tokenBackground != null)
            {
                tokenBackground.color = currentCharacter.PlaceholderColor;
            }

            bool hasPortrait = currentCharacter.Portrait != null;
            if (portraitImage != null)
            {
                portraitImage.sprite = currentCharacter.Portrait;
                portraitImage.enabled = hasPortrait;
            }

            if (initialText != null)
            {
                initialText.text = currentCharacter.Initial;
                initialText.gameObject.SetActive(!hasPortrait);
            }

            if (characterNameText != null)
            {
                characterNameText.text = currentCharacter.DisplayName;
            }
        }

        private void HandleButtonClicked()
        {
            Clicked?.Invoke(this);
        }
    }
}
