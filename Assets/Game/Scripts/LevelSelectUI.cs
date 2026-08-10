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

        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private GameObject itemTemplate;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text emptyHint;

        private readonly List<GameObject> spawnedItems = new List<GameObject>();
        private List<PuzzleData> puzzles = new List<PuzzleData>();
        private int currentPage;

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
            puzzles = PuzzleSaveManager.ListPuzzles();
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
            SceneManager.LoadScene("CharacterPanelTest");
        }

        public void BackToMenu()
        {
            SceneManager.LoadScene("SampleScene");
        }
    }
}
