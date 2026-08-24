using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    public sealed class TestBoardCellUI : MonoBehaviour, ICharacterPlacementCell,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private const float LongPressSeconds = 0.45f;

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

        private static readonly Color RowColumnHighlightColor = new Color(0.30f, 0.55f, 0.95f, 0.30f);
        private static readonly Color ErrorHighlightColor = new Color(0.92f, 0.22f, 0.22f, 0.45f);

        private Color? backgroundOverride;
        private Color? floorColor;
        private Sprite floorSprite;
        private int floorTileIndex = -1;
        private int propIndex = -1;
        private bool interactionEnabled = true;
        private CanvasGroup interactionGroup;
        private Image rowColumnHighlight;
        private Image errorHighlight;
        private Image regionOverlay;
        private Image propImage;
        private bool editorForbidden;
        private bool playerMarked;
        private TMP_Text forbiddenMark;
        private readonly List<CharacterData> candidateMarks = new List<CharacterData>();
        private readonly List<GameObject> candidateMarkChips = new List<GameObject>();
        private float pressStartTime;
        private bool suppressClick;

        public event Action<ICharacterPlacementCell> Clicked;
        public event Action<ICharacterPlacementCell> LongPressed;
        public event Action<CharacterData, ICharacterPlacementCell> CharacterDropped;

        public Vector2Int GridPosition => gridPosition;
        public bool IsPlaceable => isPlaceable;
        public bool IsOccupied => currentCharacter != null;
        public CharacterData CurrentCharacter => currentCharacter;

        public void RefreshCharacterVisual()
        {
            RefreshToken();
        }

        /// <summary>是否禁止放置人物（出题人禁放规则 + 游玩玩家标记任一为真）。</summary>
        public bool IsForbidden => editorForbidden || playerMarked;

        /// <summary>出题人禁放状态（保存进关卡；游玩模式隐形生效）。</summary>
        public bool EditorForbidden => editorForbidden;

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
            floorColor = null;
            floorSprite = null;
            floorTileIndex = -1;
            propIndex = -1;
            editorForbidden = false;
            playerMarked = false;
            candidateMarks.Clear();
            RebuildCandidateMarkVisual();
            Refresh();
        }

        /// <summary>
        /// 游玩模式候选标记：为选中人物在该格打/取消一个暗色人物图标候选（推理辅助，不保存）。
        /// 返回 true 表示新增标记，false 表示取消标记。
        /// </summary>
        public bool ToggleCandidateMark(CharacterData character)
        {
            if (character == null)
            {
                return false;
            }

            bool added;
            if (candidateMarks.Contains(character))
            {
                candidateMarks.Remove(character);
                added = false;
            }
            else
            {
                candidateMarks.Add(character);
                added = true;
            }

            RebuildCandidateMarkVisual();
            return added;
        }

        public void RemoveCandidateMark(CharacterData character)
        {
            if (character == null)
            {
                return;
            }

            if (candidateMarks.Remove(character))
            {
                RebuildCandidateMarkVisual();
            }
        }

        public void ClearCandidateMarks()
        {
            if (candidateMarks.Count == 0)
            {
                return;
            }

            candidateMarks.Clear();
            RebuildCandidateMarkVisual();
        }

        public bool HasCandidateMark(CharacterData character)
        {
            return character != null && candidateMarks.Contains(character);
        }

        /// <summary>该格是否存在任意候选标记（用于行列占用提示）。</summary>
        public bool HasAnyCandidateMark => candidateMarks.Count > 0;

        private void RebuildCandidateMarkVisual()
        {
            foreach (GameObject chip in candidateMarkChips)
            {
                if (chip != null)
                {
                    Destroy(chip);
                }
            }

            candidateMarkChips.Clear();

            RectTransform cellRect = (RectTransform)transform;
            float cellSize = Mathf.Min(cellRect.rect.width, cellRect.rect.height);
            float chipSize = Mathf.Clamp(cellSize * 0.8f, 40f, 75f);
            float x = 2f;
            for (int index = 0; index < candidateMarks.Count; index++)
            {
                CharacterData character = candidateMarks[index];
                bool hasPortrait = character != null && character.Portrait != null;

                GameObject chip = new GameObject(
                    "MarkChip",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                chip.layer = LayerMask.NameToLayer("UI");
                RectTransform chipRect = chip.GetComponent<RectTransform>();
                chipRect.SetParent(transform, false);
                chipRect.anchorMin = new Vector2(0f, 1f);
                chipRect.anchorMax = new Vector2(0f, 1f);
                chipRect.pivot = new Vector2(0f, 1f);
                chipRect.anchoredPosition = new Vector2(x, -2f);
                chipRect.sizeDelta = new Vector2(chipSize, chipSize);

                Image chipImage = chip.GetComponent<Image>();
                chipImage.raycastTarget = false;
                if (hasPortrait)
                {
                    chipImage.sprite = character.Portrait;
                    chipImage.color = new Color(1f, 1f, 1f, 0.45f);
                }
                else
                {
                    Color baseColor = character == null ? Color.gray : character.PlaceholderColor;
                    chipImage.color = new Color(
                        baseColor.r * 0.7f,
                        baseColor.g * 0.7f,
                        baseColor.b * 0.7f,
                        0.38f);
                }

                if (!hasPortrait && character != null && character.Initial.Length > 0)
                {
                    GameObject labelObject = new GameObject(
                        "Initial",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(TextMeshProUGUI));
                    RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                    labelRect.SetParent(chipRect, false);
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = Vector2.zero;
                    labelRect.offsetMax = Vector2.zero;

                    TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
                    if (coordinateText != null)
                    {
                        label.font = coordinateText.font;
                    }

                    label.text = character.Initial;
                    label.fontSize = Mathf.Max(9f, chipSize * 0.5f);
                    label.fontStyle = FontStyles.Bold;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = new Color(1f, 1f, 1f, 0.55f);
                    label.raycastTarget = false;
                }

                candidateMarkChips.Add(chip);
                x += chipSize + 2f;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressStartTime = Time.unscaledTime;
            suppressClick = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (Time.unscaledTime - pressStartTime >= LongPressSeconds &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)transform,
                    eventData.position,
                    eventData.pressEventCamera))
            {
                suppressClick = true;
                LongPressed?.Invoke(this);
            }
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
        /// 设置格子的地块颜色（出题时给格子涂色，持久保留，不随模式切换消失）；
        /// 传 null 恢复默认棋盘格颜色。渲染优先级：区域着色 &gt; 地块图案 &gt; 地块颜色 &gt; 默认棋盘格。
        /// </summary>
        public void SetFloorColor(Color? color)
        {
            floorColor = color;
            floorSprite = null;
            Refresh();
        }

        /// <summary>
        /// 设置格子的地块图案（样式地块：方格地砖/木地板/沙滩/草坪/水域等）；
        /// 传 null 恢复默认棋盘格颜色。渲染优先级：区域着色 &gt; 地块图案 &gt; 地块颜色 &gt; 默认棋盘格。
        /// </summary>
        public void SetFloorSprite(Sprite sprite)
        {
            floorSprite = sprite;
            floorColor = null;
            floorTileIndex = -1;
            Refresh();
        }

        /// <summary>
        /// 设置格子的地块（样式索引 + 图案，用于保存/载入）；
        /// tileIndex 为 -1 表示清除地块。
        /// </summary>
        public void SetFloorTile(int tileIndex, Sprite sprite)
        {
            floorTileIndex = tileIndex;
            floorSprite = tileIndex < 0 ? null : sprite;
            floorColor = null;
            Refresh();
        }

        /// <summary>当前地块样式索引（-1 = 无地块），用于保存关卡。</summary>
        public int FloorTileIndex => floorTileIndex;

        /// <summary>
        /// 设置格子的道具（propIndex &lt; 0 表示清除）。
        /// 道具渲染在地块之上、棋子之下（图层叠放），不占格、不阻挡人物放置。
        /// </summary>
        public void SetProp(int propIndex, Sprite icon)
        {
            this.propIndex = propIndex;
            if (propIndex >= 0 && propImage == null)
            {
                // 创建道具层：背景（index 0）之上、棋子之下。
                GameObject overlay = new GameObject(
                    "PropOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                overlay.layer = LayerMask.NameToLayer("UI");
                RectTransform rect = overlay.GetComponent<RectTransform>();
                rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetSiblingIndex(1);

                propImage = overlay.GetComponent<Image>();
                propImage.raycastTarget = false;
            }

            if (propImage != null)
            {
                bool hasProp = propIndex >= 0;
                propImage.gameObject.SetActive(hasProp);
                if (hasProp)
                {
                    propImage.sprite = icon;
                    propImage.color = Color.white;
                }
            }

            Refresh();
        }

        /// <summary>当前道具索引（-1 = 无道具），用于保存关卡。</summary>
        public int PropIndex => propIndex;

        /// <summary>
        /// 设置出题人禁放状态（保存进关卡）；showMark 控制是否显示黑叉
        /// （出题模式显示、游玩模式隐形生效——避免剧透禁放格）。
        /// </summary>
        public void SetEditorForbidden(bool forbidden, bool showMark)
        {
            editorForbidden = forbidden;
            editorMarkVisible = showMark;
            RefreshForbiddenMark();
        }

        /// <summary>
        /// 游玩模式玩家打叉/取消（推理辅助：标记已排除的区域；显示黑叉、不保存）。
        /// </summary>
        public void TogglePlayerMark()
        {
            playerMarked = !playerMarked;
            RefreshForbiddenMark();
        }

        private bool editorMarkVisible;

        private void RefreshForbiddenMark()
        {
            bool show = playerMarked || (editorForbidden && editorMarkVisible);
            if (show)
            {
                EnsureForbiddenMark();
            }

            if (forbiddenMark != null)
            {
                forbiddenMark.gameObject.SetActive(show);
            }
        }

        private void EnsureForbiddenMark()
        {
            if (forbiddenMark != null)
            {
                return;
            }

            GameObject markObject = new GameObject(
                "ForbiddenMark",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            markObject.layer = LayerMask.NameToLayer("UI");
            RectTransform markRect = (RectTransform)markObject.transform;
            markRect.SetParent(transform, false);
            markRect.anchorMin = Vector2.zero;
            markRect.anchorMax = Vector2.one;
            markRect.offsetMin = Vector2.zero;
            markRect.offsetMax = Vector2.zero;

            TMP_Text mark = markObject.GetComponent<TextMeshProUGUI>();
            if (coordinateText != null)
            {
                mark.font = coordinateText.font;
            }

            mark.fontSize = 72f;
            mark.fontStyle = FontStyles.Bold;
            mark.color = Color.black;
            mark.alignment = TextAlignmentOptions.Center;
            mark.text = "×";
            mark.raycastTarget = false;
            forbiddenMark = mark;
        }

        /// <summary>
        /// 设置区域区分叠加层（半透明，叠加在地块图案上方辅助区分区域，不覆盖地块）；
        /// 传 null 清除。渲染顺序：背景（地块）→ 区域叠加层 → 棋子 → 高亮。
        /// </summary>
        public void SetRegionOverlay(Color? color)
        {
            if (color.HasValue)
            {
                Image overlay = EnsureHighlight(ref regionOverlay, color.Value);
                if (overlay != null)
                {
                    overlay.gameObject.SetActive(true);
                    overlay.color = color.Value;
                }
            }
            else if (regionOverlay != null)
            {
                regionOverlay.gameObject.SetActive(false);
            }
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

        /// <summary>
        /// 行/列占用提示：所在行或列已被放置角色时显示淡蓝色覆盖层。
        /// </summary>
        public void SetRowColumnHighlight(bool on)
        {
            Image image = EnsureHighlight(ref rowColumnHighlight, RowColumnHighlightColor);
            if (image == null)
            {
                return;
            }

            image.gameObject.SetActive(on);
            if (on)
            {
                image.color = RowColumnHighlightColor;
            }
        }

        /// <summary>
        /// 错误提示（提交失败时标红相关格子）。
        /// </summary>
        public void SetErrorHighlight(bool on)
        {
            Image image = EnsureHighlight(ref errorHighlight, ErrorHighlightColor);
            if (image == null)
            {
                return;
            }

            image.gameObject.SetActive(on);
            if (on)
            {
                image.color = ErrorHighlightColor;
            }
        }

        private Image EnsureHighlight(ref Image field, Color color)
        {
            if (field != null)
            {
                return field;
            }

            GameObject overlay = new GameObject(
                "HighlightOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlay.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // 插到背景（index 0）之上、棋子之下，避免盖住角色。
            rect.SetSiblingIndex(1);

            Image image = overlay.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            overlay.SetActive(false);
            field = image;
            return image;
        }

        public bool TryPlaceCharacter(CharacterData character)
        {
            if (!isPlaceable || currentCharacter != null || character == null || IsForbidden)
            {
                return false;
            }

            currentCharacter = character;
            ClearCandidateMarks();
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
                    backgroundImage.sprite = null;
                    backgroundImage.color = backgroundOverride.Value;
                }
                else if (floorSprite != null)
                {
                    backgroundImage.sprite = floorSprite;
                    backgroundImage.color = Color.white;
                }
                else if (floorColor.HasValue)
                {
                    backgroundImage.sprite = null;
                    backgroundImage.color = floorColor.Value;
                }
                else
                {
                    backgroundImage.sprite = null;
                    bool isOffset = (gridPosition.x + gridPosition.y) % 2 != 0;
                    backgroundImage.color = isPlaceable
                        ? (isOffset ? darkCellColor : lightCellColor)
                        : blockedCellColor;
                }
            }

            if (coordinateText != null)
            {
                // 有地块（图案/颜色）或道具时隐藏坐标文字，避免文字叠加在图案上造成"色差"；
                // 无地块无道具时仍显示坐标（测试/编辑辅助）。
                bool hasFloor = floorSprite != null || floorColor.HasValue || backgroundOverride.HasValue
                    || propIndex >= 0;
                coordinateText.gameObject.SetActive(!hasFloor);
                if (!hasFloor)
                {
                    coordinateText.text = $"{gridPosition.x},{gridPosition.y}";
                }
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
            if (suppressClick)
            {
                suppressClick = false;
                return;
            }

            Clicked?.Invoke(this);
        }
    }
}
