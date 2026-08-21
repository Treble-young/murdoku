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
        private const int PageSize = 6;
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
        }

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
            SceneManager.LoadScene("CharacterPanelTest");
        }

        /// <summary>
        /// 编辑关卡：跳转到出题界面并以编辑模式载入该关卡（在原有基础上修改）。
        /// </summary>
        private void EditPuzzle(PuzzleData puzzle)
        {
            PuzzleSession.SelectedPuzzleId = puzzle.id;
            PuzzleSession.EditMode = true;
            SceneManager.LoadScene("CharacterPanelTest");
        }

        public void BackToMenu()
        {
            SceneManager.LoadScene("SampleScene");
        }
    }
}
