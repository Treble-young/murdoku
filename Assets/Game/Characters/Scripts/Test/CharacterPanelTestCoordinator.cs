using System;
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
        private TMP_Text statusText;
        private Button clueButton;
        private Button submitButton;
        private GameObject cluePanelRoot;
        private RectTransform clueContentRect;
        private readonly List<GameObject> clueRows = new List<GameObject>();
        private readonly List<TMP_InputField> clueInputs = new List<TMP_InputField>();
        private readonly List<CharacterData> clueInputCharacters = new List<CharacterData>();

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
            EnsureGameplayButtons();
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
            RefreshHighlights();
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
            RefreshHighlights();
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
                case CharacterPlacementResult.RowColumnConflict:
                    SetStatus("该位置所在的行或列已经有人了，请换一行或一列放置。", true);
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

            CharacterPanelUI panel = placementController == null ? null : placementController.SelectionSource;
            if (panel != null)
            {
                data.clues = new List<PuzzleClueData>();
                foreach (CharacterData character in panel.Characters)
                {
                    if (character == null)
                    {
                        continue;
                    }

                    data.clues.Add(new PuzzleClueData
                    {
                        characterId = character.CharacterId,
                        clue = character.Clue ?? string.Empty
                    });
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

            if (placementController != null && placementController.SelectionSource != null)
            {
                placementController.SelectionSource.ApplyClues(data.clues);
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

            RefreshHighlights();
            SetStatus("已载入关卡：" + data.name);
        }

        private void ShowErrorPopup(string title, string message)
        {
            ShowPopup(title, message);
        }

        private void ShowPopup(string title, string message)
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

        private void RefreshHighlights()
        {
            if (testBoard != null)
            {
                testBoard.RefreshRowColumnHighlights();
            }
        }

        private void EnsureGameplayButtons()
        {
            if (clueButton != null && submitButton != null)
            {
                return;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            if (clueButton == null)
            {
                clueButton = CreateTopButton(canvas.transform, "ClueButton", "编辑线索", new Vector2(-270f, -12f));
                clueButton.onClick.AddListener(OpenClueEditor);
            }

            if (submitButton == null)
            {
                submitButton = CreateTopButton(canvas.transform, "SubmitButton", "提交", new Vector2(-70f, -12f));
                submitButton.onClick.AddListener(SubmitPuzzle);
            }
        }

        private Button CreateTopButton(Transform parent, string objectName, string labelText, Vector2 anchoredPosition)
        {
            RectTransform rect = CreateUiObject(objectName, parent).GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(120f, 44f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.48f, 0.86f, 1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UiClickFeedback.Ensure(button);

            TMP_Text label = CreateText("Label", rect, GetUiFont(), 18f, FontStyles.Bold);
            label.text = labelText;
            Stretch(label.rectTransform);
            return button;
        }

        private TMP_Text EnsureStatusText()
        {
            if (statusText != null)
            {
                return statusText;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            TMP_FontAsset font = GetUiFont();
            if (canvas == null || font == null)
            {
                return null;
            }

            statusText = CreateText("PlacementStatusText", canvas.transform as RectTransform, font, 22f, FontStyles.Normal);
            RectTransform rect = statusText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 95f);
            rect.sizeDelta = new Vector2(620f, 40f);
            statusText.color = new Color(0.85f, 0.90f, 0.95f, 1f);
            return statusText;
        }

        private TMP_FontAsset GetUiFont()
        {
            if (saveHint != null && saveHint.font != null)
            {
                return saveHint.font;
            }

            if (nameInput != null && nameInput.textComponent != null)
            {
                return nameInput.textComponent.font;
            }

            return null;
        }

        private void OpenClueEditor()
        {
            CharacterPanelUI panel = placementController == null ? null : placementController.SelectionSource;
            if (panel == null)
            {
                SetStatus("角色面板不可用，无法编辑线索。", true);
                return;
            }

            if (cluePanelRoot == null)
            {
                BuildCluePanel();
            }

            if (cluePanelRoot == null)
            {
                SetStatus("无法创建线索编辑窗口，请检查 Canvas 配置。", true);
                return;
            }

            RebuildClueRows(panel.Characters);
            cluePanelRoot.SetActive(true);
        }

        private void BuildCluePanel()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            TMP_FontAsset font = GetUiFont();
            if (canvas == null || font == null)
            {
                return;
            }

            RectTransform root = CreateUiObject("ClueEditPanel", canvas.transform).GetComponent<RectTransform>();
            cluePanelRoot = root.gameObject;
            Image mask = root.gameObject.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.6f);
            Stretch(root);

            RectTransform panel = CreateUiObject("Panel", root).GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(760f, 900f);
            panel.anchoredPosition = Vector2.zero;
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.13f, 0.15f, 0.20f, 0.99f);

            TMP_Text title = CreateText("TitleText", panel, font, 28f, FontStyles.Bold);
            title.text = "编辑线索";
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 56f);
            titleRect.anchoredPosition = new Vector2(0f, -12f);

            clueContentRect = CreateUiObject("Content", panel).GetComponent<RectTransform>();
            clueContentRect.anchorMin = new Vector2(0f, 0f);
            clueContentRect.anchorMax = new Vector2(1f, 1f);
            clueContentRect.offsetMin = new Vector2(20f, 84f);
            clueContentRect.offsetMax = new Vector2(-20f, -70f);

            RectTransform applyRect = CreateUiObject("ApplyButton", panel).GetComponent<RectTransform>();
            applyRect.anchorMin = new Vector2(0.5f, 0f);
            applyRect.anchorMax = new Vector2(0.5f, 0f);
            applyRect.pivot = new Vector2(0.5f, 0.5f);
            applyRect.sizeDelta = new Vector2(150f, 48f);
            applyRect.anchoredPosition = new Vector2(-90f, 24f);
            MakeButton(applyRect, "应用", font, ApplyClueEdits);

            RectTransform cancelRect = CreateUiObject("CancelButton", panel).GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.5f, 0f);
            cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0.5f);
            cancelRect.sizeDelta = new Vector2(150f, 48f);
            cancelRect.anchoredPosition = new Vector2(90f, 24f);
            MakeButton(cancelRect, "取消", font, CloseCluePanel);
        }

        private void RebuildClueRows(IReadOnlyList<CharacterData> characters)
        {
            foreach (GameObject row in clueRows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }

            clueRows.Clear();
            clueInputs.Clear();
            clueInputCharacters.Clear();

            if (clueContentRect == null || characters == null)
            {
                return;
            }

            TMP_FontAsset font = GetUiFont();
            int count = characters.Count;
            for (int index = 0; index < count; index++)
            {
                CharacterData character = characters[index];
                if (character == null)
                {
                    continue;
                }

                RectTransform rowRect = CreateUiObject("ClueRow", clueContentRect).GetComponent<RectTransform>();
                clueRows.Add(rowRect.gameObject);
                rowRect.anchorMin = new Vector2(0.5f, 1f);
                rowRect.anchorMax = new Vector2(0.5f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -12f - index * 64f);
                rowRect.sizeDelta = new Vector2(720f, 56f);

                TMP_Text label = CreateText("Label", rowRect, font, 18f, FontStyles.Bold);
                label.text = character.Initial + " · " + character.DisplayName;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0.5f);
                labelRect.anchorMax = new Vector2(0f, 0.5f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = new Vector2(4f, 0f);
                labelRect.sizeDelta = new Vector2(150f, 40f);

                TMP_InputField input = CreateClueInput(rowRect, font, 24);
                RectTransform inputRect = input.GetComponent<RectTransform>();
                inputRect.anchorMin = new Vector2(0f, 0.5f);
                inputRect.anchorMax = new Vector2(1f, 0.5f);
                inputRect.pivot = new Vector2(0.5f, 0.5f);
                inputRect.anchoredPosition = new Vector2(165f, 0f);
                inputRect.sizeDelta = new Vector2(-330f, 44f);
                input.text = character.Clue ?? string.Empty;
                clueInputs.Add(input);
                clueInputCharacters.Add(character);
            }
        }

        private TMP_InputField CreateClueInput(RectTransform parent, TMP_FontAsset font, int characterLimit)
        {
            GameObject inputObject = new GameObject(
                "ClueInput",
                typeof(RectTransform),
                typeof(Image),
                typeof(TMP_InputField));
            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Image background = inputObject.GetComponent<Image>();
            background.color = new Color(0.09f, 0.11f, 0.15f, 1f);

            GameObject viewportObject = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(rect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(6f, 2f);
            viewport.offsetMax = new Vector2(-6f, -2f);

            TMP_Text text = CreateText("Text", viewport, font, 18f, FontStyles.Normal);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            Stretch(text.rectTransform);

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.textComponent = text;
            input.textViewport = viewport;
            input.characterLimit = characterLimit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private Button MakeButton(RectTransform rect, string labelText, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.48f, 0.86f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            UiClickFeedback.Ensure(button);

            TMP_Text label = CreateText("Label", rect, font, 20f, FontStyles.Bold);
            label.text = labelText;
            Stretch(label.rectTransform);
            return button;
        }

        private void ApplyClueEdits()
        {
            for (int index = 0; index < clueInputs.Count && index < clueInputCharacters.Count; index++)
            {
                TMP_InputField input = clueInputs[index];
                CharacterData character = clueInputCharacters[index];
                if (input == null || character == null)
                {
                    continue;
                }

                character.SetClue(input.text.Trim());
            }

            if (placementController != null && placementController.SelectionSource != null)
            {
                placementController.SelectionSource.RefreshAllClues();
            }

            CloseCluePanel();
            SetStatus("线索已更新，保存关卡时会一起存入存档。", false);
        }

        private void CloseCluePanel()
        {
            if (cluePanelRoot != null)
            {
                cluePanelRoot.SetActive(false);
            }
        }

        private void SubmitPuzzle()
        {
            if (testBoard != null)
            {
                testBoard.ClearErrorHighlights();
            }

            if (placementController == null)
            {
                ShowErrorPopup("提交失败", "角色放置控制器未配置。");
                return;
            }

            CharacterPanelUI panel = placementController.SelectionSource;
            List<CharacterData> characters = panel == null ? null : new List<CharacterData>(panel.Characters);
            if (characters == null || characters.Count == 0)
            {
                ShowErrorPopup("提交失败", "未找到角色列表，请先配置角色面板。");
                return;
            }

            int missing = placementController.CountMissingCharacters(characters);
            if (missing > 0)
            {
                ShowErrorPopup("提交失败", "还有 " + missing + " 名角色没有放置到棋盘上，请先摆满再提交。");
                return;
            }

            List<ICharacterPlacementCell> conflictCells = placementController.GetRowColumnConflictCells();
            if (conflictCells.Count > 0)
            {
                HighlightCells(conflictCells);
                ShowErrorPopup("提交失败", "存在同一行或同一列放了多人的情况，请先调整（已标红冲突格子）。");
                return;
            }

            if (wallEditController == null || wallEditController.Walls == null)
            {
                ShowErrorPopup("提交失败", "棋盘墙体数据不可用，无法判定房间。");
                return;
            }

            CharacterData victim = FindVictim(characters);
            if (victim == null ||
                !placementController.TryGetPlacement(victim, out ICharacterPlacementCell victimCell) ||
                victimCell == null)
            {
                ShowErrorPopup("提交失败", "未找到受害者或受害者未放置，无法判定凶手。");
                return;
            }

            int[,] regions = wallEditController.Walls.ComputeRegions();
            int victimRegion = regions[victimCell.GridPosition.y, victimCell.GridPosition.x];
            List<CharacterData> roomMates = new List<CharacterData>();
            List<ICharacterPlacementCell> roomMateCells = new List<ICharacterPlacementCell>();
            foreach (CharacterData character in characters)
            {
                if (character == null || ReferenceEquals(character, victim))
                {
                    continue;
                }

                if (!placementController.TryGetPlacement(character, out ICharacterPlacementCell cell) || cell == null)
                {
                    continue;
                }

                if (regions[cell.GridPosition.y, cell.GridPosition.x] == victimRegion)
                {
                    roomMates.Add(character);
                    roomMateCells.Add(cell);
                }
            }

            if (roomMates.Count == 1)
            {
                ShowPopup("破案成功！", "凶手是 " + roomMates[0].DisplayName + "：TA 与受害者同处一室且身边没有其他人。");
                SetStatus("破案成功！凶手是 " + roomMates[0].DisplayName + "。", false);
                return;
            }

            if (roomMates.Count == 0)
            {
                HighlightCells(new List<ICharacterPlacementCell> { victimCell });
                ShowErrorPopup("无法确定凶手", "受害者所在房间没有任何其他人，题目可能无解（受害者格子已标红）。");
                return;
            }

            HighlightCells(roomMateCells);
            ShowErrorPopup("无法确定凶手", "与受害者同处一室的有 " + roomMates.Count + " 人，无法唯一确定凶手（相关格子已标红）。");
        }

        private CharacterData FindVictim(IReadOnlyList<CharacterData> characters)
        {
            if (characters == null)
            {
                return null;
            }

            foreach (CharacterData character in characters)
            {
                if (character == null)
                {
                    continue;
                }

                if (string.Equals(character.CharacterId, "V", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(character.DisplayName, "Victim", StringComparison.OrdinalIgnoreCase))
                {
                    return character;
                }
            }

            return null;
        }

        private void HighlightCells(IEnumerable<ICharacterPlacementCell> cells)
        {
            if (testBoard == null)
            {
                return;
            }

            foreach (ICharacterPlacementCell cell in cells)
            {
                if (cell is TestBoardCellUI cellUI)
                {
                    cellUI.SetErrorHighlight(true);
                }
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

        private void SetStatus(string message, bool isError = false)
        {
            if (placementStatusText != null)
            {
                placementStatusText.text = message;
                return;
            }

            TMP_Text text = EnsureStatusText();
            if (text != null)
            {
                text.text = message;
                text.color = isError ? ErrorColor : new Color(0.85f, 0.90f, 0.95f, 1f);
            }
        }
    }
}
