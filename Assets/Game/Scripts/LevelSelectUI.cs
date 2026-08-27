using System.Collections.Generic;
using Murdoku.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Murdoku
{
    /// <summary>
    /// 选关场景 UI：已保存关卡按“每行两个”的网格展示，支持翻页；
    /// 每个关卡卡片带删除按钮（点两次确认删除）。
    /// </summary>
    public sealed class LevelSelectUI : MonoBehaviour
    {
        private const int PageSize = 12;
        private const int TabCount = 6;
        private static readonly string[] TabNames = { "全部", "教程", "简单", "中等", "困难", "噩梦" };

        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private GameObject itemTemplate;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text emptyHint;

        private readonly List<GameObject> spawnedItems = new List<GameObject>();
        private readonly List<Image> tabImages = new List<Image>();
        private readonly List<TMP_Text> tabLabels = new List<TMP_Text>();
        private List<PuzzleData> puzzles = new List<PuzzleData>();
        private int currentPage;
        private int selectedTab = -1; // -1=全部，0~4=教程~噩梦

        private void Awake()
        {
            if (itemTemplate != null)
            {
                itemTemplate.SetActive(false);
            }

            if (backButton != null)
            {
                UiClickFeedback.Ensure(backButton);
                backButton.onClick.AddListener(BackToMenu);
            }

            if (prevButton != null)
            {
                UiClickFeedback.Ensure(prevButton);
                prevButton.onClick.AddListener(PreviousPage);
            }

            if (nextButton != null)
            {
                UiClickFeedback.Ensure(nextButton);
                nextButton.onClick.AddListener(NextPage);
            }

            EnsureDifficultyTabs();
            ApplyScenePolish();
        }

        /// <summary>
        /// 选关界面整体美化（深色精致解谜风）：
        /// 背景暗纹装饰、卡片圆角+阴影层次、按钮圆角、标题金色装饰线。
        /// 纯外观改造，不改变布局与功能。
        /// </summary>
        private void ApplyScenePolish()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            CreateDecorativeBackground(canvas);
            PolishCardTemplate();
            PolishButtons();
        }

        /// <summary>背景暗纹装饰层（点阵+细斜纹，低调不抢内容）。</summary>
        private void CreateDecorativeBackground(Canvas canvas)
        {
            if (canvas.transform.Find("LevelSelectDecor") != null)
            {
                return;
            }

            Texture2D pattern = CreatePatternTexture(64, 64);
            Sprite patternSprite = Sprite.Create(
                pattern,
                new Rect(0f, 0f, 64f, 64f),
                new Vector2(0.5f, 0.5f),
                100f);
            patternSprite.texture.wrapMode = TextureWrapMode.Repeat;

            GameObject decor = new GameObject("LevelSelectDecor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            decor.layer = LayerMask.NameToLayer("UI");
            RectTransform decorRect = (RectTransform)decor.transform;
            decorRect.SetParent(canvas.transform, false);
            decorRect.anchorMin = Vector2.zero;
            decorRect.anchorMax = Vector2.one;
            decorRect.offsetMin = Vector2.zero;
            decorRect.offsetMax = Vector2.zero;
            decorRect.SetSiblingIndex(1);

            Image decorImage = decor.GetComponent<Image>();
            decorImage.sprite = patternSprite;
            decorImage.type = Image.Type.Tiled;
            decorImage.color = Color.white;
            decorImage.raycastTarget = false;
        }

        private static Texture2D CreatePatternTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = Color.clear;
                    // 细斜纹。
                    if ((x + y) % 24 == 0)
                    {
                        color = new Color(1f, 1f, 1f, 0.035f);
                    }

                    // 十字星点阵。
                    if (x % 16 == 0 && y % 16 == 0)
                    {
                        color = new Color(1f, 1f, 1f, 0.09f);
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>卡片模板美化：圆角 + 米白卡面；删除/编辑按钮圆角化。</summary>
        private void PolishCardTemplate()
        {
            if (itemTemplate == null)
            {
                return;
            }

            Sprite cardSprite = CreateRoundedRectSprite(64, 18);
            Image cardImage = itemTemplate.GetComponent<Image>();
            if (cardImage != null)
            {
                cardImage.sprite = cardSprite;
                cardImage.type = Image.Type.Sliced;
                cardImage.pixelsPerUnitMultiplier = 1f;
                cardImage.color = new Color(0.97f, 0.96f, 0.93f, 1f);
            }

            Sprite buttonSprite = CreateRoundedRectSprite(64, 10);
            Transform labelTransform = itemTemplate.transform.Find("Label");
            if (labelTransform != null)
            {
                TMP_Text label = labelTransform.GetComponent<TMP_Text>();
                if (label != null)
                {
                    label.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                }
            }

            Transform deleteTransform = itemTemplate.transform.Find("DeleteButton");
            if (deleteTransform != null)
            {
                Image deleteImage = deleteTransform.GetComponent<Image>();
                if (deleteImage != null)
                {
                    deleteImage.sprite = buttonSprite;
                    deleteImage.type = Image.Type.Sliced;
                    deleteImage.pixelsPerUnitMultiplier = 1f;
                }
            }

            Transform editTransform = itemTemplate.transform.Find("EditButton");
            if (editTransform != null)
            {
                Image editImage = editTransform.GetComponent<Image>();
                if (editImage != null)
                {
                    editImage.sprite = buttonSprite;
                    editImage.type = Image.Type.Sliced;
                    editImage.pixelsPerUnitMultiplier = 1f;
                }
            }
        }

        /// <summary>翻页/返回按钮圆角化（场景按钮运行时替换）。</summary>
        private void PolishButtons()
        {
            Sprite buttonSprite = CreateRoundedRectSprite(64, 12);
            ApplyRoundedSprite(prevButton, buttonSprite);
            ApplyRoundedSprite(nextButton, buttonSprite);
            ApplyRoundedSprite(backButton, buttonSprite);
        }

        private static void ApplyRoundedSprite(Button button, Sprite sprite)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>给实例化后的关卡卡叠加一层底部阴影（深色圆角，制造悬浮层次感）。</summary>
        private static void AddCardShadow(GameObject item)
        {
            if (item == null || item.transform.Find("CardShadow") != null)
            {
                return;
            }

            RectTransform cardRect = (RectTransform)item.transform;
            GameObject shadow = new GameObject("CardShadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadow.layer = LayerMask.NameToLayer("UI");
            RectTransform shadowRect = (RectTransform)shadow.transform;
            shadowRect.SetParent(item.transform, false);
            // 锚点拉伸跟随卡片尺寸（不依赖布局完成时的 rect.width）。
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(-5f, -5f);
            shadowRect.offsetMax = new Vector2(5f, -9f);
            shadowRect.SetAsFirstSibling();

            Image shadowImage = shadow.GetComponent<Image>();
            shadowImage.sprite = CreateRoundedRectSprite(64, 18);
            shadowImage.type = Image.Type.Sliced;
            shadowImage.pixelsPerUnitMultiplier = 1f;
            shadowImage.color = new Color(0f, 0f, 0f, 0.28f);
            shadowImage.raycastTarget = false;
        }

        /// <summary>生成 9-slice 圆角矩形 sprite（透明四角）。</summary>
        private static Sprite CreateRoundedRectSprite(int size, int radius)
        {
            string key = "Rounded_" + radius;
            if (spriteCache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = radius;
            float rSq = r * r;
            float cornerCenter = size - r - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = true;
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    if (px >= cornerCenter && py >= cornerCenter)
                    {
                        float dx = px - cornerCenter;
                        float dy = py - cornerCenter;
                        inside = dx * dx + dy * dy <= rSq;
                    }
                    else if (px <= r + 0.5f && py >= cornerCenter)
                    {
                        float dx = px - (r + 0.5f);
                        float dy = py - cornerCenter;
                        inside = dx * dx + dy * dy <= rSq;
                    }
                    else if (px >= cornerCenter && py <= r + 0.5f)
                    {
                        float dx = px - cornerCenter;
                        float dy = py - (r + 0.5f);
                        inside = dx * dx + dy * dy <= rSq;
                    }
                    else if (px <= r + 0.5f && py <= r + 0.5f)
                    {
                        float dx = px - (r + 0.5f);
                        float dy = py - (r + 0.5f);
                        inside = dx * dx + dy * dy <= rSq;
                    }

                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(r + 1f, r + 1f, r + 1f, r + 1f));
            spriteCache[key] = sprite;
            return sprite;
        }

        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        /// <summary>
        /// 在界面左侧创建 6 个难度 Tab（全部/教程/简单/中等/困难/噩梦），点击切换筛选。
        /// </summary>
        private void EnsureDifficultyTabs()
        {
            if (tabImages.Count > 0)
            {
                return;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            TMP_Text anyText = FindFirstObjectByType<TextMeshProUGUI>();
            if (canvas == null || anyText == null)
            {
                return;
            }

            TMP_FontAsset font = anyText.font;
            Color selectedColor = new Color(0.22f, 0.48f, 0.86f, 1f);
            Color normalColor = new Color(0.93f, 0.94f, 0.96f, 1f);

            for (int index = 0; index < TabCount; index++)
            {
                int captured = index;
                GameObject tabObject = new GameObject("DifficultyTab_" + TabNames[index], typeof(RectTransform), typeof(Image));
                tabObject.layer = LayerMask.NameToLayer("UI");
                RectTransform rect = (RectTransform)tabObject.transform;
                rect.SetParent(canvas.transform, false);
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(130f, 52f);
                rect.anchoredPosition = new Vector2(40f, 160f - index * 64f);

                Image background = tabObject.GetComponent<Image>();
                background.color = normalColor;
                background.raycastTarget = true;
                background.sprite = CreateRoundedRectSprite(64, 12);
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = 1f;

                TabClickZone zone = tabObject.AddComponent<TabClickZone>();
                zone.Clicked = () => HandleTabClicked(captured);

                GameObject labelObject = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                labelObject.layer = LayerMask.NameToLayer("UI");
                RectTransform labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                TMP_Text label = labelObject.GetComponent<TextMeshProUGUI>();
                label.font = font;
                label.fontSize = 22f;
                label.fontStyle = FontStyles.Bold;
                label.color = new Color(0.16f, 0.20f, 0.26f, 1f);
                label.alignment = TextAlignmentOptions.Center;
                label.text = TabNames[index];
                label.raycastTarget = false;

                tabImages.Add(background);
                tabLabels.Add(label);
            }

            RefreshDifficultyTabs();
        }

        private void HandleTabClicked(int index)
        {
            selectedTab = index - 1; // 0=全部 → -1
            RefreshDifficultyTabs();
            RefreshList(0);
        }

        private void RefreshDifficultyTabs()
        {
            Color selectedColor = new Color(0.22f, 0.48f, 0.86f, 1f);
            Color normalColor = new Color(0.93f, 0.94f, 0.96f, 1f);
            for (int index = 0; index < tabImages.Count; index++)
            {
                bool selected = index - 1 == selectedTab;
                if (tabImages[index] != null)
                {
                    tabImages[index].color = selected ? selectedColor : normalColor;
                }

                if (tabLabels[index] != null)
                {
                    tabLabels[index].color = selected ? Color.white : new Color(0.16f, 0.20f, 0.26f, 1f);
                }
            }
        }

        private void OnEnable()
        {
            RefreshList();
        }

        private void OnDestroy()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(BackToMenu);
            }

            if (prevButton != null)
            {
                prevButton.onClick.RemoveListener(PreviousPage);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(NextPage);
            }
        }

        private void RefreshList(int preferredPage = 0)
        {
            List<PuzzleData> all = PuzzleSaveManager.ListPuzzles();
            // 按难度 Tab 筛选：全部(-1)显示所有关卡，否则只显示对应难度。
            puzzles = selectedTab < 0
                ? all
                : all.FindAll(puzzle => puzzle != null && puzzle.difficulty == selectedTab);
            currentPage = Mathf.Clamp(preferredPage, 0, Mathf.Max(0, GetPageCount() - 1));

            if (emptyHint != null)
            {
                emptyHint.gameObject.SetActive(puzzles.Count == 0);
            }

            RenderPage();
            UpdatePageControls();
        }

        private void RenderPage()
        {
            foreach (GameObject item in spawnedItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }

            spawnedItems.Clear();

            if (gridRoot == null || itemTemplate == null || puzzles.Count == 0)
            {
                return;
            }

            int start = currentPage * PageSize;
            int count = Mathf.Min(PageSize, puzzles.Count - start);
            for (int index = 0; index < count; index++)
            {
                PuzzleData puzzle = puzzles[start + index];
                GameObject item = Instantiate(itemTemplate, gridRoot);
                item.name = "PuzzleItem_" + puzzle.id;
                item.SetActive(true);
                AddCardShadow(item);

                Transform labelTransform = item.transform.Find("Label");
                TMP_Text label = labelTransform == null ? null : labelTransform.GetComponent<TMP_Text>();
                if (label != null)
                {
                    label.text = puzzle.name + "（" + puzzle.size + "x" + puzzle.size + "）";
                }

                Button enterButton = item.GetComponent<Button>();
                if (enterButton != null)
                {
                    UiClickFeedback.Ensure(enterButton);
                    PuzzleData captured = puzzle;
                    enterButton.onClick.AddListener(() => EnterPuzzle(captured));
                }

                Transform deleteTransform = item.transform.Find("DeleteButton");
                Button deleteButton = deleteTransform == null ? null : deleteTransform.GetComponent<Button>();
                if (deleteButton != null)
                {
                    SetupDeleteButton(deleteButton, puzzle);
                }

                Transform editTransform = item.transform.Find("EditButton");
                Button editButton = editTransform == null ? null : editTransform.GetComponent<Button>();
                if (editButton != null)
                {
                    UiClickFeedback.Ensure(editButton);
                    PuzzleData captured = puzzle;
                    editButton.onClick.AddListener(() => EditPuzzle(captured));
                }

                spawnedItems.Add(item);
            }
        }

        private void SetupDeleteButton(Button deleteButton, PuzzleData puzzle)
        {
            TMP_Text deleteLabel = deleteButton.GetComponentInChildren<TMP_Text>();
            Image deleteImage = deleteButton.GetComponent<Image>();
            bool confirmed = false;

            deleteButton.onClick.AddListener(() =>
            {
                if (!confirmed)
                {
                    confirmed = true;
                    if (deleteLabel != null)
                    {
                        deleteLabel.text = "确认?";
                    }

                    if (deleteImage != null)
                    {
                        deleteImage.color = new Color(0.72f, 0.16f, 0.16f, 1f);
                    }

                    return;
                }

                int pageBefore = currentPage;
                PuzzleSaveManager.DeletePuzzle(puzzle.id);
                RefreshList(pageBefore);
            });
        }

        private void PreviousPage()
        {
            if (currentPage <= 0)
            {
                return;
            }

            currentPage--;
            RenderPage();
            UpdatePageControls();
        }

        private void NextPage()
        {
            if (currentPage >= GetPageCount() - 1)
            {
                return;
            }

            currentPage++;
            RenderPage();
            UpdatePageControls();
        }

        private int GetPageCount()
        {
            if (puzzles == null || puzzles.Count == 0)
            {
                return 1;
            }

            return Mathf.CeilToInt(puzzles.Count / (float)PageSize);
        }

        private void UpdatePageControls()
        {
            int pageCount = GetPageCount();
            if (prevButton != null)
            {
                prevButton.interactable = currentPage > 0;
            }

            if (nextButton != null)
            {
                nextButton.interactable = currentPage < pageCount - 1;
            }

            if (pageText != null)
            {
                pageText.text = "第 " + (currentPage + 1) + " / " + pageCount + " 页";
            }
        }

        private void EnterPuzzle(PuzzleData puzzle)
        {
            PuzzleSession.SelectedPuzzleId = puzzle.id;
            PuzzleSession.EditMode = false;
            SceneManager.LoadScene("PuzzleScene");
        }

        /// <summary>
        /// 编辑关卡：跳转到出题界面并以编辑模式载入该关卡（在原有基础上修改）。
        /// </summary>
        private void EditPuzzle(PuzzleData puzzle)
        {
            PuzzleSession.SelectedPuzzleId = puzzle.id;
            PuzzleSession.EditMode = true;
            SceneManager.LoadScene("PuzzleScene");
        }

        public void BackToMenu()
        {
            SceneManager.LoadScene("SampleScene");
        }
    }
}
