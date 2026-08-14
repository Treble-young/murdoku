using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 地块面板：20 种地块（5 列 × 4 行网格，方形图标 + 编号），单选管理。
    /// 选中某地块后，点击棋盘格子即为该格铺上对应图案（涂色由协调器处理）。
    /// 卡片在 Configure 时动态创建，无需 prefab。
    /// </summary>
    public sealed class RegionPanelUI : MonoBehaviour
    {
        private const int Columns = 5;
        private const int Rows = 4;
        private const float CardWidth = 165f;
        private const float CardHeight = 158f;
        private const float ColumnSpacing = 177f; // 165 + 12
        private const float RowSpacing = 170f;    // 158 + 12
        private const float GridOffsetY = -50f;   // 网格整体下移，在标题区与面板底部之间居中

        private readonly List<RegionCardUI> cards = new List<RegionCardUI>();
        private RegionCardUI selectedCard;
        private TMP_FontAsset font;

        /// <summary>选中变化时触发；region 为 null 表示取消选择。</summary>
        public event Action<RegionDefinition> SelectionChanged;

        public RegionDefinition SelectedRegion => selectedCard == null ? null : selectedCard.Region;

        /// <summary>
        /// 由协调器调用：配置字体并构建地块卡片。
        /// </summary>
        public void Configure(TMP_FontAsset uiFont)
        {
            font = uiFont;
            RegionStyleFactory.EnsureSprites();
            ApplyPanelStyle();
            Rebuild();
        }

        /// <summary>
        /// 清除当前选择（切回放置模式）。
        /// </summary>
        public void ClearSelection()
        {
            SelectCard(null);
        }

        /// <summary>
        /// 面板样式：白色底板 + 标题改深色（参考嫌疑人面板的白底风格）。
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

            foreach (RegionCardUI card in cards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            cards.Clear();
            selectedCard = null;

            RegionDefinition[] definitions = RegionStyleFactory.All;
            for (int index = 0; index < definitions.Length; index++)
            {
                RegionCardUI card = CreateCard(definitions[index], index);
                cards.Add(card);
            }
        }

        private RegionCardUI CreateCard(RegionDefinition definition, int index)
        {
            int column = index % Columns;
            int row = index / Columns;

            GameObject rootObject = new GameObject(
                definition.DisplayName + "Card",
                typeof(RectTransform),
                typeof(Image),
                typeof(RegionCardUI));
            rootObject.layer = LayerMask.NameToLayer("UI");
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(CardWidth, CardHeight);
            root.anchoredPosition = new Vector2(
                (column - (Columns - 1) / 2f) * ColumnSpacing,
                ((Rows - 1) / 2f - row) * RowSpacing + GridOffsetY);

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

            // 图案色块（四周留白边，露出白色卡面；preserveAspect 保持方形不被拉伸）。
            RectTransform block = CreateRect("ColorBlock", root);
            block.anchorMin = Vector2.zero;
            block.anchorMax = Vector2.one;
            block.offsetMin = new Vector2(12f, 40f);
            block.offsetMax = new Vector2(-12f, -12f);
            Image blockImage = block.gameObject.AddComponent<Image>();
            blockImage.sprite = definition.Sprite;
            blockImage.color = Color.white;
            blockImage.preserveAspect = true;
            blockImage.raycastTarget = false;

            // 编号：色块中心（深色粗体，与道具卡片一致）。
            TMP_Text numberText = CreateText(
                "NumberText",
                block,
                definition.Number.ToString(),
                font,
                42f,
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

            RegionCardUI card = rootObject.GetComponent<RegionCardUI>();
            card.Configure(definition, font);
            card.Clicked += HandleCardClicked;
            card.transform.SetAsLastSibling();
            return card;
        }

        private void HandleCardClicked(RegionCardUI card)
        {
            SelectCard(card == selectedCard ? null : card);
        }

        private void SelectCard(RegionCardUI card)
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

            SelectionChanged?.Invoke(selectedCard == null ? null : selectedCard.Region);
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
