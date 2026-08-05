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

        private Color? backgroundOverride;
        private bool interactionEnabled = true;
        private CanvasGroup interactionGroup;

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
            backgroundOverride = null;
            Refresh();
        }

        /// <summary>
        /// 覆盖格子背景色（用于编辑器区域着色）；传 null 恢复默认棋盘格颜色。
        /// </summary>
        public void SetBackgroundOverride(Color? color)
        {
            backgroundOverride = color;
            Refresh();
        }

        /// <summary>
        /// 控制格子是否可交互（编辑器墙壁模式下应禁用点击与拖放）。
        /// 注意：不能用 button.interactable 或 CanvasGroup.interactable（Unity 会叠加禁用色覆盖格子背景），
        /// 只用 blocksRaycasts 让射线穿透格子，既能禁用交互又不改变格子颜色，且允许点击到达下方边界线。
        /// </summary>
        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
            if (interactionGroup == null)
            {
                interactionGroup = GetComponent<CanvasGroup>();
                if (interactionGroup == null)
                {
                    interactionGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // interactable 保持 true：Selectable.IsInteractable() 会检查 CanvasGroup，
            // 一旦为 false，按钮 ColorTint 会用禁用色覆盖格子背景（表现为整格变深灰）。
            interactionGroup.interactable = true;
            interactionGroup.blocksRaycasts = enabled;
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
            if (interactionEnabled && currentCharacter != null)
            {
                CharacterDragPreview.Show(currentCharacter, this, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (interactionEnabled && currentCharacter != null)
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
            if (!interactionEnabled || eventData.pointerDrag == null)
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
                if (backgroundOverride.HasValue)
                {
                    backgroundImage.color = backgroundOverride.Value;
                }
                else
                {
                    bool isOffset = (gridPosition.x + gridPosition.y) % 2 != 0;
                    backgroundImage.color = isPlaceable
                        ? (isOffset ? darkCellColor : lightCellColor)
                        : blockedCellColor;
                }
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
