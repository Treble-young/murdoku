using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    public sealed class PuzzleBoardCellUI : MonoBehaviour, ICharacterPlacementCell,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private const float LongPressSeconds = 0.6f;
        private const float RingShowDelay = 0.15f;
        private const int CandidateMarkColumns = 3;
        private const int CandidateMarkRows = 3;
        private const int MaxVisibleCandidateMarks = CandidateMarkColumns * CandidateMarkRows;

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
        [SerializeField] private TMP_FontAsset candidateMarkFont;

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
        // 玩家主动打的叉与人物行列自动产生的叉分开保存，避免移动人物时误删手动标记。
        private bool playerMarked;
        private readonly HashSet<CharacterData> automaticMarkSources = new HashSet<CharacterData>();
        private TMP_Text forbiddenMark;
        private readonly List<CharacterData> candidateMarks = new List<CharacterData>();
        private readonly List<GameObject> candidateMarkChips = new List<GameObject>();
        private float pressStartTime;
        private bool suppressClick;
        private bool isPressed;
        private bool longPressTriggered;
        private Image longPressRing;
        private static Sprite longPressRingSprite;

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
        public bool IsForbidden => editorForbidden || playerMarked || automaticMarkSources.Count > 0;

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
            automaticMarkSources.Clear();
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
            if (character == null || IsForbidden)
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

        /// <summary>移除该格上某角色的候选标记；返回是否确实移除了标记（用于撤销恢复快照）。</summary>
        public bool RemoveCandidateMark(CharacterData character)
        {
            if (character == null)
            {
                return false;
            }

            if (candidateMarks.Remove(character))
            {
                RebuildCandidateMarkVisual();
                return true;
            }

            return false;
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
            const float gridPadding = 3f;
            float slotWidth = Mathf.Max(
                1f,
                (cellRect.rect.width - gridPadding * 2f) / CandidateMarkColumns);
            float slotHeight = Mathf.Max(
                1f,
                (cellRect.rect.height - gridPadding * 2f) / CandidateMarkRows);
            float fontSize = Mathf.Clamp(Mathf.Min(slotWidth, slotHeight) * 0.88f, 20f, 42f);
            int visibleIndex = 0;
            for (int index = 0; index < candidateMarks.Count; index++)
            {
                if (visibleIndex >= MaxVisibleCandidateMarks)
                {
                    break;
                }

                CharacterData character = candidateMarks[index];
                if (character == null || character.Initial.Length == 0)
                {
                    continue;
                }

                int column = visibleIndex % CandidateMarkColumns;
                int row = visibleIndex / CandidateMarkColumns;

                // 纯字母标记：无底色徽章，使用 NotoSansSC 加粗显示角色首字母，颜色与人物卡一致。
                // 黑色描边确保不同颜色的字母在明暗地块和道具上都清晰可辨。
                GameObject letterObject = new GameObject(
                    "MarkLetter",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                letterObject.layer = LayerMask.NameToLayer("UI");
                RectTransform letterRect = letterObject.GetComponent<RectTransform>();
                letterRect.SetParent(transform, false);
                letterRect.anchorMin = new Vector2(0f, 1f);
                letterRect.anchorMax = new Vector2(0f, 1f);
                letterRect.pivot = new Vector2(0f, 1f);
                letterRect.anchoredPosition = new Vector2(
                    gridPadding + column * slotWidth,
                    -(gridPadding + row * slotHeight));
                letterRect.sizeDelta = new Vector2(slotWidth, slotHeight);

                TextMeshProUGUI label = letterObject.GetComponent<TextMeshProUGUI>();
                if (candidateMarkFont != null)
                {
                    label.font = candidateMarkFont;
                }

                label.text = character.Initial;
                label.fontSize = fontSize;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = character.PlaceholderColor;
                ApplyCandidateMarkOutline(label);
                label.raycastTarget = false;

                candidateMarkChips.Add(letterObject);
                visibleIndex++;
            }
        }

        private static void ApplyCandidateMarkOutline(TextMeshProUGUI label)
        {
            if (label == null || label.fontSharedMaterial == null)
            {
                return;
            }

            // fontMaterial 会为该文字创建独立材质实例，避免修改共享字体材质影响其他 UI。
            Material material = label.fontMaterial;
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            label.UpdateMeshPadding();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressStartTime = Time.unscaledTime;
            isPressed = true;
            longPressTriggered = false;
            suppressClick = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            HideLongPressRing();
            if (!longPressTriggered &&
                Time.unscaledTime - pressStartTime >= LongPressSeconds &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)transform,
                    eventData.position,
                    eventData.pressEventCamera))
            {
                longPressTriggered = true;
                suppressClick = true;
                LongPressed?.Invoke(this);
            }
        }

        private void Update()
        {
            // 长按检测：直接用 Input 系统（不依赖 EventSystem 的 pointer 事件链路，
            // 避免 press/click 事件被其他层拦截导致长按失效）。
            // 鼠标按下且落在本格内时启动计时；按住前 RingShowDelay 秒不显示读条，
            // 之后读条从 0 开始填充，转满 LongPressSeconds 即触发长按放置。
            if (Input.GetMouseButtonDown(0) &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)transform,
                    Input.mousePosition,
                    null))
            {
                pressStartTime = Time.unscaledTime;
                isPressed = true;
                longPressTriggered = false;
                suppressClick = false;
                HideLongPressRing();
            }
            else if (Input.GetMouseButton(0) && isPressed && !longPressTriggered)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        (RectTransform)transform,
                        Input.mousePosition,
                        null))
                {
                    float held = Time.unscaledTime - pressStartTime;
                    if (held >= LongPressSeconds)
                    {
                        longPressTriggered = true;
                        suppressClick = true;
                        HideLongPressRing();
                        LongPressed?.Invoke(this);
                    }
                    else if (held >= RingShowDelay)
                    {
                        // 过了延迟才开始显示读条，进度从 0 填充到 1。
                        ShowLongPressRing((held - RingShowDelay) / (LongPressSeconds - RingShowDelay));
                    }
                    else
                    {
                        HideLongPressRing();
                    }
                }
                else
                {
                    // 长按过程中鼠标移出格子：放弃并隐藏读条。
                    isPressed = false;
                    HideLongPressRing();
                }
            }
            else if (!Input.GetMouseButton(0))
            {
                isPressed = false;
                HideLongPressRing();
            }
        }

        /// <summary>
        /// 长按读条：格子中央的环形进度（Image Filled Radial360 + 圆环纹理）。
        /// </summary>
        private void ShowLongPressRing(float fillAmount)
        {
            if (!interactionEnabled)
            {
                return;
            }

            Image ring = EnsureLongPressRing();
            if (ring != null)
            {
                ring.fillAmount = Mathf.Clamp01(fillAmount);
                ring.gameObject.SetActive(true);
            }
        }

        private void HideLongPressRing()
        {
            if (longPressRing != null)
            {
                longPressRing.gameObject.SetActive(false);
            }
        }

        private Image EnsureLongPressRing()
        {
            if (longPressRing != null)
            {
                return longPressRing;
            }

            RectTransform cellRect = (RectTransform)transform;
            float cellSize = Mathf.Min(cellRect.rect.width, cellRect.rect.height);

            GameObject ringObject = new GameObject(
                "LongPressRing",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            ringObject.layer = LayerMask.NameToLayer("UI");
            RectTransform ringRect = (RectTransform)ringObject.transform;
            ringRect.SetParent(transform, false);
            ringRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.pivot = new Vector2(0.5f, 0.5f);
            ringRect.sizeDelta = new Vector2(cellSize * 0.72f, cellSize * 0.72f);
            ringRect.SetAsLastSibling();

            Image ring = ringObject.GetComponent<Image>();
            ring.sprite = GetLongPressRingSprite();
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.color = new Color(0.22f, 0.48f, 0.86f, 0.95f);
            ring.raycastTarget = false;
            ring.gameObject.SetActive(false);
            longPressRing = ring;
            return ring;
        }

        private static Sprite GetLongPressRingSprite()
        {
            if (longPressRingSprite != null)
            {
                return longPressRingSprite;
            }

            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f - 0.5f;
            float outer = size / 2f - 2f;
            float inner = outer * 0.72f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distanceSq = dx * dx + dy * dy;
                    bool inRing = distanceSq <= outer * outer && distanceSq >= inner * inner;
                    texture.SetPixel(x, y, inRing ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            longPressRingSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            return longPressRingSprite;
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

        /// <summary>显式设置玩家禁用标记（放置人物后给行列空格打黑叉用）。</summary>
        public void SetPlayerMark(bool marked)
        {
            if (playerMarked == marked)
            {
                return;
            }

            playerMarked = marked;
            RefreshForbiddenMark();
        }

        /// <summary>
        /// 添加或移除某个人物造成的行列自动禁放来源。
        /// 返回该来源是否确实发生变化；手动叉号不受影响。
        /// </summary>
        public bool SetAutomaticPlayerMark(CharacterData source, bool marked)
        {
            if (source == null)
            {
                return false;
            }

            bool changed = marked
                ? automaticMarkSources.Add(source)
                : automaticMarkSources.Remove(source);
            if (changed)
            {
                RefreshForbiddenMark();
            }

            return changed;
        }

        /// <summary>
        /// 移除该格上除 keep 外的所有候选标记，返回被移除的角色列表（供撤销时恢复）。
        /// </summary>
        public List<CharacterData> RemoveCandidateMarksExcept(CharacterData keep)
        {
            List<CharacterData> removed = new List<CharacterData>();
            if (candidateMarks.Count == 0)
            {
                return removed;
            }

            for (int index = candidateMarks.Count - 1; index >= 0; index--)
            {
                CharacterData mark = candidateMarks[index];
                if (mark != null && !ReferenceEquals(mark, keep))
                {
                    candidateMarks.RemoveAt(index);
                    removed.Add(mark);
                }
            }

            if (removed.Count > 0)
            {
                RebuildCandidateMarkVisual();
            }

            return removed;
        }

        private bool editorMarkVisible;

        private void RefreshForbiddenMark()
        {
            bool show = playerMarked || automaticMarkSources.Count > 0 || (editorForbidden && editorMarkVisible);
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
            PuzzleBoardCellUI sourceCell = eventData.pointerDrag.GetComponentInParent<PuzzleBoardCellUI>();
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
