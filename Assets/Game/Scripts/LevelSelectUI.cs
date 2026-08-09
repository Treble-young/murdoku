using System.Collections.Generic;
using Murdoku.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Murdoku
{
    /// <summary>
    /// 选关场景 UI：列出所有已保存的关卡，点击进入出题/游戏场景。
    /// </summary>
    public sealed class LevelSelectUI : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private GameObject itemTemplate;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text emptyHint;

        private readonly List<GameObject> spawnedItems = new List<GameObject>();

        private void Awake()
        {
            if (itemTemplate != null)
            {
                itemTemplate.SetActive(false);
            }

            if (backButton != null)
            {
                UiSfxFeedback.Ensure(backButton);
                backButton.onClick.AddListener(BackToMenu);
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
        }

        private void RefreshList()
        {
            foreach (GameObject item in spawnedItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }

            spawnedItems.Clear();

            List<PuzzleData> puzzles = PuzzleSaveManager.ListPuzzles();
            if (emptyHint != null)
            {
                emptyHint.gameObject.SetActive(puzzles.Count == 0);
            }

            if (contentRoot == null || itemTemplate == null)
            {
                return;
            }

            foreach (PuzzleData puzzle in puzzles)
            {
                GameObject item = Instantiate(itemTemplate, contentRoot);
                item.name = "PuzzleItem_" + puzzle.id;
                item.SetActive(true);

                TMP_Text label = item.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = puzzle.name + "（" + puzzle.size + "x" + puzzle.size + "）";
                }

                Button button = item.GetComponentInChildren<Button>();
                if (button != null)
                {
                    UiSfxFeedback.Ensure(button);
                    PuzzleData captured = puzzle;
                    button.onClick.AddListener(() => EnterPuzzle(captured));
                }

                spawnedItems.Add(item);
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
