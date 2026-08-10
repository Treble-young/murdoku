using System.Collections;
using System.Collections.Generic;
using Murdoku.Audio;
using Murdoku.PuzzleEditor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    /// <summary>
    /// 角色面板测试场景的总调度：
    /// - 正常编辑模式（放置角色 / 画墙划分区域）
    /// - 把当前出题保存为关卡存档
    /// - 从选关场景进入时读取存档并还原棋盘、墙体与角色
    /// </summary>
    public sealed class CharacterPanelTestCoordinator : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private TestBoardController testBoard;
        [SerializeField] private CharacterPlacementController placementController;
        [SerializeField] private WallEditController wallEditController;
        [SerializeField] private TMP_Text placementStatusText;

        [Header("保存出题")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text saveHint;

        private GameObject popupRoot;
        private TMP_Text popupTitleText;
        private TMP_Text popupMessageText;

        private static readonly Color ErrorColor = new Color(0.92f, 0.35f, 0.35f, 1f);
        private static readonly Color SuccessColor = new Color(0.45f, 0.80f, 0.50f, 1f);

        private void OnEnable()
        {
            if (testBoard != null)
            {
                testBoard.CellClicked += HandleCellClicked;
                testBoard.CharacterDropped += HandleCharacterDropped;
            }
        }

        private void Start()
        {
            SetStatus("点击人物后选择格子，或直接拖动人物卡到右侧格子。");
            StartCoroutine(LoadSelectedPuzzleRoutine());
        }

        private void OnDisable()
        {
            if (testBoard != null)
            {
                testBoard.CellClicked -= HandleCellClicked;
                testBoard.CharacterDropped -= HandleCharacterDropped;
            }
        }

        private void HandleCellClicked(ICharacterPlacementCell cell)
        {
            if (placementController == null)
            {
                SetStatus("人物放置控制器未配置。");
                return;
            }

            CharacterData selected = placementController.SelectedCharacter;
            CharacterPlacementResult result = placementController.HandleCellClicked(cell);
            ShowPlacementResult(selected, cell, result);
        }

        private void HandleCharacterDropped(CharacterData character, ICharacterPlacementCell cell)
        {
            if (placementController == null)
            {
                SetStatus("人物放置控制器未配置。");
                return;
            }

            CharacterPlacementResult result = placementController.HandleCharacterDropped(character, cell);
            ShowPlacementResult(character, cell, result);
        }

        private void ShowPlacementResult(
            CharacterData character,
            ICharacterPlacementCell cell,
            CharacterPlacementResult result)
        {
            string characterName = character == null ? "人物" : character.DisplayName;

            if (result == CharacterPlacementResult.Placed || result == CharacterPlacementResult.Moved)
            {
                GameAudio.Play(SfxCue.CharacterPlace);
            }

            switch (result)
            {
                case CharacterPlacementResult.NoCharacterSelected:
                    SetStatus("请先选择或拖动一名人物。");
                    break;
                case CharacterPlacementResult.CellNotPlaceable:
                    SetStatus("该格子不可放置人物。");
                    break;
                case CharacterPlacementResult.CellOccupied:
                    SetStatus("目标格已被其他人物占据，原位置保持不变。");
                    break;
                case CharacterPlacementResult.AlreadyInCell:
                    SetStatus($"{characterName} 已经在这个格子中。");
                    break;
                case CharacterPlacementResult.Placed:
                    SetStatus($"已将 {characterName} 放置到 ({cell.GridPosition.x}, {cell.GridPosition.y})。");
                    break;
                case CharacterPlacementResult.Moved:
                    SetStatus($"已将 {characterName} 移动到 ({cell.GridPosition.x}, {cell.GridPosition.y})。");
                    break;
                default:
                    SetStatus("放置失败，人物位置未改变。");
                    break;
            }
        }

        /// <summary>
        /// 保存当前出题为关卡存档（由保存按钮调用）。
        /// </summary>
        public void SaveCurrentPuzzle()
        {
            if (nameInput == null)
            {
                SetSaveHint("未找到关卡名输入框，请检查场景配置。", true);
                return;
            }

            string puzzleName = nameInput.text.Trim();
            if (string.IsNullOrEmpty(puzzleName))
            {
                SetSaveHint("请先输入关卡名再保存。", true);
                return;
            }

            if (testBoard == null || placementController == null || wallEditController == null)
            {
                SetSaveHint("编辑器组件未配置完整，无法保存。", true);
                return;
            }

            if (PuzzleSaveManager.NameExists(puzzleName))
            {
                ShowErrorPopup("保存失败", "已存在同名关卡「" + puzzleName + "」，请更换关卡名后再保存。");
                return;
            }

            int size = testBoard.Rows;
            PuzzleData data = new PuzzleData
            {
                id = PuzzleSaveManager.GenerateId(),
                name = puzzleName,
                size = size,
                horizontalWalls = new bool[(size - 1) * size],
                verticalWalls = new bool[size * (size - 1)],
                placements = placementController.ExportPlacements(testBoard.Columns)
            };

            WallMap walls = wallEditController.Walls;
            if (walls != null)
            {
                int index = 0;
                for (int row = 0; row < size - 1; row++)
                {
                    for (int col = 0; col < size; col++)
                    {
                        data.horizontalWalls[index++] = walls.GetHorizontalWall(row, col);
                    }
                }

                index = 0;
                for (int row = 0; row < size; row++)
                {
                    for (int col = 0; col < size - 1; col++)
                    {
                        data.verticalWalls[index++] = walls.GetVerticalWall(row, col);
                    }
                }
            }

            PuzzleSaveManager.SavePuzzle(data);
            SetSaveHint("已保存关卡「" + puzzleName + "」。", false);
        }

        private IEnumerator LoadSelectedPuzzleRoutine()
        {
            string puzzleId = PuzzleSession.SelectedPuzzleId;
            PuzzleSession.SelectedPuzzleId = null;

            if (string.IsNullOrEmpty(puzzleId))
            {
                yield break;
            }

            PuzzleData data = PuzzleSaveManager.LoadPuzzle(puzzleId);
            if (data == null || data.size < TestBoardController.MinSize || data.size > TestBoardController.MaxSize)
            {
                SetStatus("未找到关卡存档，已进入空白棋盘。");
                yield break;
            }

            // 等待初始棋盘与墙体边框完成重建。
            yield return null;
            yield return null;

            if (testBoard != null)
            {
                testBoard.SetGridSize(data.size, data.size);
            }

            // 等待新尺寸的棋盘布局与墙体边框重建完成。
            yield return null;
            yield return null;

            if (wallEditController != null)
            {
                wallEditController.ApplyWallState(data.size, data.horizontalWalls, data.verticalWalls);
            }

            // 让队列中触发的重建全部消费掉挂起的墙状态后再清空。
            yield return null;
            yield return null;
            if (wallEditController != null)
            {
                wallEditController.ClearPendingWallState();
            }

            if (placementController != null && data.placements != null && testBoard != null)
            {
                foreach (PuzzlePlacementData placement in data.placements)
                {
                    if (placement == null)
                    {
                        continue;
                    }

                    CharacterData character = placementController.FindCharacterById(placement.characterId);
                    if (character == null)
                    {
                        continue;
                    }

                    if (placement.cellIndex < 0 || placement.cellIndex >= testBoard.Cells.Count)
                    {
                        continue;
                    }

                    placementController.HandleCharacterDropped(character, testBoard.Cells[placement.cellIndex]);
                }
            }

            SetStatus("已载入关卡：" + data.name);
        }

        private void ShowErrorPopup(string title, string message)
        {
            if (popupRoot == null)
            {
                EnsureErrorPopup();
            }

            if (popupRoot == null)
            {
                SetSaveHint("无法显示弹窗，请检查场景 Canvas 配置。", true);
                return;
            }

            popupRoot.SetActive(true);
            if (popupTitleText != null)
            {
                popupTitleText.text = title;
            }

            if (popupMessageText != null)
            {
                popupMessageText.text = message;
            }
        }

        private void EnsureErrorPopup()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            TMP_FontAsset font = saveHint != null ? saveHint.font : null;
            if (font == null && nameInput != null && nameInput.textComponent != null)
            {
                font = nameInput.textComponent.font;
            }

            if (canvas == null || font == null)
            {
                return;
            }

            popupRoot = CreateUiObject("SaveErrorPopup", canvas.transform).gameObject;
            RectTransform root = popupRoot.GetComponent<RectTransform>();
            Image mask = root.gameObject.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.55f);
            Stretch(root);

            RectTransform panel = CreateUiObject("Panel", root).GetComponent<RectTransform>();
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.13f, 0.15f, 0.20f, 0.98f);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1008f, 396f);
            panel.anchoredPosition = Vector2.zero;

            popupTitleText = CreateText("TitleText", panel, font, 40f, FontStyles.Bold);
            RectTransform titleRect = popupTitleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 72f);
            titleRect.anchoredPosition = new Vector2(0f, -18f);

            popupMessageText = CreateText("MessageText", panel, font, 32f, FontStyles.Normal);
            RectTransform messageRect = popupMessageText.rectTransform;
            Stretch(messageRect);
            messageRect.offsetMin = new Vector2(54f, 108f);
            messageRect.offsetMax = new Vector2(-54f, -90f);

            RectTransform okRect = CreateUiObject("OkButton", panel).GetComponent<RectTransform>();
            okRect.anchorMin = new Vector2(0.5f, 0f);
            okRect.anchorMax = new Vector2(0.5f, 0f);
            okRect.pivot = new Vector2(0.5f, 0.5f);
            okRect.sizeDelta = new Vector2(252f, 79f);
            okRect.anchoredPosition = new Vector2(0f, 29f);

            Image okImage = okRect.gameObject.AddComponent<Image>();
            okImage.color = new Color(0.22f, 0.48f, 0.86f, 1f);
            Button okButton = okRect.gameObject.AddComponent<Button>();
            okButton.targetGraphic = okImage;
            okButton.onClick.AddListener(CloseErrorPopup);
            UiClickFeedback.Ensure(okButton);

            TMP_Text okLabel = CreateText("Label", okRect, font, 32f, FontStyles.Normal);
            okLabel.text = "确定";
            Stretch(okLabel.rectTransform);
        }

        private static RectTransform CreateUiObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, TMP_FontAsset font, float fontSize, FontStyles style)
        {
            RectTransform rect = CreateUiObject(name, parent);
            rect.gameObject.AddComponent<CanvasRenderer>();
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void CloseErrorPopup()
        {
            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }
        }

        private void SetSaveHint(string message, bool isError)
        {
            if (saveHint == null)
            {
                return;
            }

            saveHint.text = message;
            saveHint.color = isError ? ErrorColor : SuccessColor;
        }

        private void SetStatus(string message)
        {
            if (placementStatusText != null)
            {
                placementStatusText.text = message;
            }
        }
    }
}
