using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 道具面板：35 种测试道具（5 列 × 7 行网格，圆形图标 + 编号，滚轮滚动查看），单选管理。
    /// 选中某道具后，点击棋盘格子即为该格放置道具（放置/移除由协调器处理）。
    /// 卡片在 Configure 时动态创建，无需 prefab。
    /// </summary>
    public sealed class PropPanelUI : MonoBehaviour
    {
        private const int Columns = 5;
        private const int Rows = 7;
        private const float CardWidth = 150f;
        private const float CardHeight = 138f;
        private const float ColumnSpacing = 162f; // 150 + 12
        private const float RowSpacing = 142f;    // 138 + 4
        private const float ContentPadding = 20f; // 网格与内容顶部/底部的留白
        private RectTransform contentRect;

        private readonly List<PropCardUI> cards = new List<PropCardUI>();
        private PropCardUI selectedCard;
        private TMP_FontAsset font;

        /// <summary>选中变化时触发；prop 为 null 表示取消选择。</summary>
        public event Action<PropDefinition> SelectionChanged;

        public PropDefinition SelectedProp => selectedCard == null ? null : selectedCard.Prop;

        /// <summary>
        /// 由协调器调用：配置字体并构建道具卡片。
        /// </summary>
        public void Configure(TMP_FontAsset uiFont)
        {
            font = uiFont;
            PropStyleFactory.EnsureSprites();
            ApplyPanelStyle();
            EnsureScrollContainer();
            Rebuild();
        }

        /// <summary>
        /// 构建滚动容器：面板自身作为视口（ScrollRect + RectMask2D 裁剪），
        /// 卡片挂到可滚动的 content 上（7 行网格超出面板高度，用滚轮查看）。
        /// </summary>
        private void EnsureScrollContainer()
        {
            if (contentRect != null)
            {
                return;
            }

            RectTransform panelRect = (RectTransform)transform;

            // 面板 Image 需要参与射线命中，滚轮事件才能落在面板空隙上（卡片上已有命中）。
            Image background = GetComponent<Image>();
            if (background != null)
            {
                background.raycastTarget = true;
            }

            ScrollRect scroll = GetComponent<ScrollRect>();
            if (scroll == null)
            {
                scroll = gameObject.AddComponent<ScrollRect>();
            }

            RectMask2D mask = GetComponent<RectMask2D>();
            if (mask == null)
            {
                mask = gameObject.AddComponent<RectMask2D>();
            }

            contentRect = new GameObject("ScrollContent", typeof(RectTransform)).GetComponent<RectTransform>();
            contentRect.SetParent(transform, false);
            contentRect.anchorMin = new Vector2(0.5f, 1f);
            contentRect.anchorMax = new Vector2(0.5f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            float panelWidth = panelRect.rect.width > 0f ? panelRect.rect.width : 940f;
            float contentHeight = (Rows - 1) * RowSpacing + CardHeight + ContentPadding * 2f;
            contentRect.sizeDelta = new Vector2(panelWidth, contentHeight);

            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.inertia = true;
        }

        /// <summary>
        /// 清除当前选择。
        /// </summary>
        public void ClearSelection()
        {
            SelectCard(null);
        }

        /// <summary>
        /// 面板样式：白色底板 + 标题改深色（与地块面板一致）。
        /// </summary>
        private void ApplyPanelStyle()
        {
            Image background = GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.98f, 0.98f, 0.98f, 1f);
            background.raycastTarget = false;

            foreach (Transform child in transform)
            {
                if (child.name == "TitleText")
                {
                    TMP_Text title = child.GetComponent<TMP_Text>();
                    if (title != null)
                    {
                        title.color = new Color(0.16f, 0.20f, 0.26f, 1f);
                    }
                }
            }
        }

        private void Rebuild()
        {
            // 隐藏「功能开发中…」占位提示。
            foreach (Transform child in transform)
            {
                if (child.name == "HintText")
                {
                    child.gameObject.SetActive(false);
                }
            }

            foreach (PropCardUI card in cards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            cards.Clear();
            selectedCard = null;

            PropDefinition[] definitions = PropStyleFactory.All;
            for (int index = 0; index < definitions.Length; index++)
            {
                PropCardUI card = CreateCard(definitions[index], index);
                cards.Add(card);
            }
        }

        private PropCardUI CreateCard(PropDefinition definition, int index)
        {
            int column = index % Columns;
            int row = index / Columns;

            GameObject rootObject = new GameObject(
                definition.DisplayName + "Card",
                typeof(RectTransform),
                typeof(Image),
                typeof(PropCardUI));
            rootObject.layer = LayerMask.NameToLayer("UI");
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(contentRect != null ? contentRect : (RectTransform)transform, false);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(CardWidth, CardHeight);
            // x 相对内容中心居中；y 从内容顶部往下排（滚轮滚动查看）。
            root.anchoredPosition = new Vector2(
                (column - (Columns - 1) / 2f) * ColumnSpacing,
                -(ContentPadding + row * RowSpacing + CardHeight * 0.5f));

            // 根 Image：白色卡片底面，同时参与射线检测（IPointerClickHandler 依赖它命中）。
            Image rootImage = rootObject.GetComponent<Image>();
            rootImage.color = Color.white;
            rootImage.raycastTarget = true;

            // 选中边框：蓝色框线（4 条细边，中间留空——真正的"边框"效果）。
            RectTransform border = CreateRect("SelectionBorder", root);
            Stretch(border);
            border.gameObject.SetActive(false);

            Color borderColor = new Color(0.22f, 0.48f, 0.86f, 1f);
            CreateBorderBar(border, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 6f), new Vector2(0f, -3f), borderColor);
            CreateBorderBar(border, "BottomBar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(0f, 3f), borderColor);
            CreateBorderBar(border, "LeftBar", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(6f, 0f), new Vector2(3f, 0f), borderColor);
            CreateBorderBar(border, "RightBar", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(6f, 0f), new Vector2(-3f, 0f), borderColor);

            // 圆形图标（中心区域，四周留白边露出卡面；preserveAspect 保持圆形不被拉伸）。
            RectTransform icon = CreateRect("CircleIcon", root);
            icon.anchorMin = Vector2.zero;
            icon.anchorMax = Vector2.one;
            icon.offsetMin = new Vector2(12f, 38f);
            icon.offsetMax = new Vector2(-12f, -12f);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = definition.Sprite;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            // 编号：圆形中心（深色粗体，在亮色圆上清晰可读）。
            TMP_Text numberText = CreateText(
                "NumberText",
                icon,
                definition.Number.ToString(),
                font,
                40f,
                new Color(0.10f, 0.14f, 0.22f, 1f));
            Stretch(numberText.rectTransform);
            numberText.fontStyle = FontStyles.Bold;

            // 名字：深色（白色卡面上）。
            TMP_Text nameText = CreateText(
                "NameText",
                root,
                definition.DisplayName,
                font,
                20f,
                new Color(0.16f, 0.20f, 0.26f, 1f));
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.sizeDelta = new Vector2(0f, 32f);
            nameRect.anchoredPosition = new Vector2(0f, 16f);
            nameText.fontStyle = FontStyles.Bold;

            PropCardUI card = rootObject.GetComponent<PropCardUI>();
            card.Configure(definition, font);
            card.Clicked += HandleCardClicked;
            card.transform.SetAsLastSibling();
            return card;
        }

        private void HandleCardClicked(PropCardUI card)
        {
            SelectCard(card == selectedCard ? null : card);
        }

        private void SelectCard(PropCardUI card)
        {
            if (selectedCard == card)
            {
                return;
            }

            if (selectedCard != null)
            {
                selectedCard.SetSelected(false);
            }

            selectedCard = card;
            if (selectedCard != null)
            {
                selectedCard.SetSelected(true);
            }

            SelectionChanged?.Invoke(selectedCard == null ? null : selectedCard.Prop);
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>
        /// 创建选中边框的一条边（细条 Image）。
        /// </summary>
        private static void CreateBorderBar(
            RectTransform parent,
            string barName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta,
            Vector2 anchoredPosition,
            Color color)
        {
            RectTransform bar = CreateRect(barName, parent);
            bar.anchorMin = anchorMin;
            bar.anchorMax = anchorMax;
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.sizeDelta = sizeDelta;
            bar.anchoredPosition = anchoredPosition;

            Image image = bar.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static TMP_Text CreateText(
            string name,
            RectTransform parent,
            string content,
            TMP_FontAsset font,
            float fontSize,
            Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            TMP_Text text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
