using System;
using System.Collections;
using Murdoku.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    public sealed class CharacterCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Interaction")]
        [SerializeField] private Button button;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private GameObject selectionBorder;

        [Header("Character Content")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image portraitPlaceholder;
        [SerializeField] private TMP_Text initialText;
        [SerializeField] private TMP_Text genderText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text clueText;

        [Header("Selection Animation")]
        [Min(1f)]
        [SerializeField] private float selectedScale = 1.1f;
        [Min(0f)]
        [SerializeField] private float animationDuration = 0.15f;

        private static readonly Color MaleColor = new Color(0.22f, 0.48f, 0.92f, 1f);
        private static readonly Color FemaleColor = new Color(0.92f, 0.28f, 0.30f, 1f);

        private CharacterData character;
        private Action<CharacterCardUI> clicked;
        private Action<CharacterCardUI> dragStarted;
        private Coroutine scaleRoutine;
        private RectTransform genderCircleRect;
        private TMP_Text genderSymbolText;
        private bool genderToggleEnabled = true;
        private readonly Vector3[] genderCorners = new Vector3[4];

        public CharacterData Character => character;

        /// <summary>
        /// 控制性别切换按钮是否可点击（游玩模式禁用：按钮仍显示 ♂/♀ 供玩家查看，但点击不切换；
        /// 出题模式启用）。注意：卡片可能是 Rebuild 时动态创建的，调用时机与创建顺序无关（状态持久化）。
        /// </summary>
        public void SetGenderToggleEnabled(bool enabled)
        {
            genderToggleEnabled = enabled;
        }

        /// <summary>
        /// 重新把角色当前线索同步到卡片线索文本（编辑线索后调用）。
        /// </summary>
        public void RefreshClue()
        {
            if (clueText != null)
            {
                clueText.text = character == null ? string.Empty : character.Clue;
            }
        }

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleButtonClicked);
                UiClickFeedback.Ensure(button);
            }

            SetupGenderToggle();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleButtonClicked);
            }
        }

        /// <summary>
        /// 性别图标变为可点击的圆形按钮：点击在男/女之间切换。
        /// 圆形按钮直接挂在卡片根的最上层（SetAsLastSibling），不会被卡片内部任何元素遮挡；
        /// 性别符号作为圆形的子文本（渲染在圆形之上，不会被白底挡住）；
        /// 位置在 LateUpdate 里每帧跟随原性别图标的矩形中心（原图标隐藏、仅作位置参照）。
        /// 使用 IPointerClickHandler（GenderToggleZone）接收点击。
        /// </summary>
        private void SetupGenderToggle()
        {
            if (genderText == null)
            {
                return;
            }

            // 原性别图标：隐藏（保留 RectTransform 作为圆形按钮的位置参照），不拦截射线。
            genderText.raycastTarget = false;
            Color hidden = genderText.color;
            hidden.a = 0f;
            genderText.color = hidden;

            GameObject circleObject = new GameObject(
                "GenderCircle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            circleObject.layer = LayerMask.NameToLayer("UI");
            RectTransform circleRect = circleObject.GetComponent<RectTransform>();
            circleRect.SetParent(transform, false);
            circleRect.SetAsLastSibling();

            circleRect.anchorMin = new Vector2(0.5f, 0.5f);
            circleRect.anchorMax = new Vector2(0.5f, 0.5f);
            circleRect.pivot = new Vector2(0.5f, 0.5f);
            circleRect.sizeDelta = new Vector2(50f, 50f);

            Image circle = circleObject.GetComponent<Image>();
            circle.sprite = CreateCircleSprite(64);
            circle.color = Color.white;
            circle.raycastTarget = true;

            GenderToggleZone zone = circleObject.AddComponent<GenderToggleZone>();
            zone.OnClicked = HandleGenderClicked;

            // 性别符号：圆形的子文本（铺满圆形、居中），渲染在白色圆形之上。
            GameObject symbolObject = new GameObject(
                "Symbol",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            symbolObject.layer = LayerMask.NameToLayer("UI");
            RectTransform symbolRect = symbolObject.GetComponent<RectTransform>();
            symbolRect.SetParent(circleRect, false);
            symbolRect.anchorMin = Vector2.zero;
            symbolRect.anchorMax = Vector2.one;
            symbolRect.offsetMin = Vector2.zero;
            symbolRect.offsetMax = Vector2.zero;

            genderSymbolText = symbolObject.GetComponent<TextMeshProUGUI>();
            genderSymbolText.font = genderText.font;
            genderSymbolText.fontSize = 32f;
            genderSymbolText.fontStyle = FontStyles.Bold;
            genderSymbolText.alignment = TextAlignmentOptions.Center;
            genderSymbolText.raycastTarget = false;

            genderCircleRect = circleRect;

            UpdateGenderVisual();
        }

        /// <summary>
        /// 每帧把圆形按钮对齐到性别图标的实际矩形中心（用 GetWorldCorners 计算，不受 pivot 影响；
        /// 卡片缩放/布局变化时依然精确贴合）。
        /// </summary>
        private void LateUpdate()
        {
            if (genderCircleRect == null || genderText == null)
            {
                return;
            }

            genderText.rectTransform.GetWorldCorners(genderCorners);
            genderCircleRect.position = (genderCorners[0] + genderCorners[2]) * 0.5f;
        }

        private void HandleGenderClicked()
        {
            // 游玩模式禁用：按钮仍显示性别符号，但点击不切换。
            if (!genderToggleEnabled || character == null)
            {
                return;
            }

            GameAudio.Play(SfxCue.UiClick);
            character.ToggleGender();
            UpdateGenderVisual();
        }

        /// <summary>
        /// 刷新性别符号的内容与颜色（♂蓝 / ♀红），显示在圆形按钮的子文本上。
        /// </summary>
        private void UpdateGenderVisual()
        {
            if (genderSymbolText == null)
            {
                return;
            }

            if (character == null)
            {
                genderSymbolText.text = string.Empty;
                return;
            }

            genderSymbolText.text = character.GenderSymbol;
            genderSymbolText.color = character.Gender == CharacterGender.Female ? FemaleColor : MaleColor;
        }

        private static Sprite CreateCircleSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - size / 2f + 0.5f;
                    float dy = y - size / 2f + 0.5f;
                    texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            texture.name = "GenderCircle";
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public void Bind(
            CharacterData data,
            Action<CharacterCardUI> onClicked,
            Action<CharacterCardUI> onDragStarted = null)
        {
            character = data;
            clicked = onClicked;
            dragStarted = onDragStarted;

            if (data == null)
            {
                return;
            }

            bool hasPortrait = data.Portrait != null;
            if (portraitImage != null)
            {
                portraitImage.sprite = data.Portrait;
                portraitImage.enabled = hasPortrait;
            }

            if (portraitPlaceholder != null)
            {
                portraitPlaceholder.color = data.PlaceholderColor;
                portraitPlaceholder.gameObject.SetActive(!hasPortrait);
            }

            if (initialText != null)
            {
                initialText.text = data.Initial;
                initialText.gameObject.SetActive(!hasPortrait);
            }

            if (genderText != null)
            {
                UpdateGenderVisual();
            }

            if (nameText != null)
            {
                nameText.text = data.DisplayName;
            }

            if (clueText != null)
            {
                clueText.text = data.Clue;
            }

            SetSelected(false, false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (character == null)
            {
                return;
            }

            dragStarted?.Invoke(this);
            CharacterDragPreview.Show(character, this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            CharacterDragPreview.Move(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            CharacterDragPreview.Hide();
        }

        public void SetSelected(bool selected, bool animate = true)
        {
            if (selectionBorder != null)
            {
                selectionBorder.SetActive(selected);
            }

            if (visualRoot == null)
            {
                return;
            }

            Vector3 targetScale = Vector3.one * (selected ? selectedScale : 1f);
            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
                scaleRoutine = null;
            }

            if (!animate || !Application.isPlaying || animationDuration <= 0f || !isActiveAndEnabled)
            {
                visualRoot.localScale = targetScale;
                return;
            }

            scaleRoutine = StartCoroutine(AnimateScale(targetScale));
        }

        private IEnumerator AnimateScale(Vector3 targetScale)
        {
            Vector3 startScale = visualRoot.localScale;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                t = t * t * (3f - 2f * t);
                visualRoot.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            visualRoot.localScale = targetScale;
            scaleRoutine = null;
        }

        private void HandleButtonClicked()
        {
            if (character != null)
            {
                clicked?.Invoke(this);
            }
        }
    }
}
