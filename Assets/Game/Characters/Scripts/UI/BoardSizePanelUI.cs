using Murdoku.Audio;
using Murdoku.PuzzleEditor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    /// <summary>
    /// 谜题创建界面的「棋盘大小」控制条：
    /// 输入边长（方形 N×N，范围 TestBoardController.MinSize~MaxSize），点击按钮重新生成棋盘。
    /// 参考 Murdoku Playground 的输入行列方式实现。
    /// </summary>
    public sealed class BoardSizePanelUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private TMP_InputField sizeInput;
        [SerializeField] private Button generateButton;
        [SerializeField] private TMP_Text hintText;

        [Header("编辑模式")]
        [SerializeField] private Button placeModeButton;
        [SerializeField] private Button wallModeButton;
        [SerializeField] private Color activeModeColor = new Color(0.22f, 0.48f, 0.86f, 1f);
        [SerializeField] private Color inactiveModeColor = new Color(0.35f, 0.38f, 0.45f, 1f);

        [Header("外部依赖")]
        [SerializeField] private TestBoardController boardController;
        [SerializeField] private WallEditController wallEditController;

        [Header("提示颜色")]
        [SerializeField] private Color errorColor = new Color(0.92f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color successColor = new Color(0.45f, 0.80f, 0.50f, 1f);

        private void Awake()
        {
            if (generateButton != null)
            {
                UiSfxFeedback.Ensure(generateButton);
                generateButton.onClick.AddListener(HandleGenerateClicked);
            }

            if (placeModeButton != null)
            {
                UiSfxFeedback.Ensure(placeModeButton);
                placeModeButton.onClick.AddListener(HandlePlaceModeClicked);
            }

            if (wallModeButton != null)
            {
                UiSfxFeedback.Ensure(wallModeButton);
                wallModeButton.onClick.AddListener(HandleWallModeClicked);
            }

            RefreshModeButtons();
        }

        private void OnDestroy()
        {
            if (generateButton != null)
            {
                generateButton.onClick.RemoveListener(HandleGenerateClicked);
            }

            if (placeModeButton != null)
            {
                placeModeButton.onClick.RemoveListener(HandlePlaceModeClicked);
            }

            if (wallModeButton != null)
            {
                wallModeButton.onClick.RemoveListener(HandleWallModeClicked);
            }
        }

        public void Configure(TestBoardController controller, WallEditController walls)
        {
            boardController = controller;
            wallEditController = walls;
        }

        private void HandleGenerateClicked()
        {
            if (boardController == null)
            {
                SetHint("棋盘控制器未配置。", true);
                return;
            }

            if (!TryParseSize(out int size))
            {
                SetHint($"请输入 {TestBoardController.MinSize}~{TestBoardController.MaxSize} 之间的整数。", true);
                return;
            }

            boardController.SetGridSize(size, size);
            SetHint($"已生成 {size}×{size} 棋盘。", false);
        }

        private void HandlePlaceModeClicked()
        {
            if (wallEditController != null)
            {
                wallEditController.SetMode(WallEditController.EditorMode.Place);
            }

            RefreshModeButtons();
            SetHint("放置模式：点击或拖动人物卡放置到棋盘。", false);
        }

        private void HandleWallModeClicked()
        {
            if (wallEditController != null)
            {
                wallEditController.SetMode(WallEditController.EditorMode.EditWalls);
            }

            RefreshModeButtons();
            SetHint("墙壁模式：点击格子之间的边界线切换墙（粗线=墙）。", false);
        }

        private void RefreshModeButtons()
        {
            bool editWalls = wallEditController != null &&
                             wallEditController.Mode == WallEditController.EditorMode.EditWalls;
            SetModeButtonColor(placeModeButton, !editWalls);
            SetModeButtonColor(wallModeButton, editWalls);
        }

        private void SetModeButtonColor(Button button, bool active)
        {
            if (button == null || button.targetGraphic == null)
            {
                return;
            }

            button.targetGraphic.color = active ? activeModeColor : inactiveModeColor;
        }

        private bool TryParseSize(out int size)
        {
            size = 0;
            if (sizeInput == null || string.IsNullOrWhiteSpace(sizeInput.text))
            {
                return false;
            }

            if (!int.TryParse(sizeInput.text.Trim(), out size))
            {
                return false;
            }

            return size >= TestBoardController.MinSize && size <= TestBoardController.MaxSize;
        }

        private void SetHint(string message, bool isError)
        {
            if (hintText == null)
            {
                return;
            }

            hintText.text = message;
            hintText.color = isError ? errorColor : successColor;
        }
    }
}
