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

        private RegionPanelUI regionPanel;
        private PropPanelUI propPanel;

        private GameObject popupRoot;
        private TMP_Text popupTitleText;
        private TMP_Text popupMessageText;
        private TMP_Text statusText;
        private Button clueButton;
        private Button submitButton;
        private Button undoButton;
        private Button redoButton;
        private Button regionNameButton;
        private GameObject cluePanelRoot;
        private RectTransform clueContentRect;
        private readonly List<GameObject> clueRows = new List<GameObject>();
        private readonly List<TMP_InputField> clueInputs = new List<TMP_InputField>();
        private readonly List<CharacterData> clueInputCharacters = new List<CharacterData>();
        private GameObject regionNamePanelRoot;
        private RectTransform regionNameContentRect;
        private readonly List<GameObject> regionNameRows = new List<GameObject>();
        private readonly List<TMP_InputField> regionNameInputs = new List<TMP_InputField>();
        private readonly List<TMP_InputField> regionNameInputXs = new List<TMP_InputField>();
        private readonly List<TMP_InputField> regionNameInputYs = new List<TMP_InputField>();
        private readonly List<int> regionNameIds = new List<int>();
        private bool playMode;
        private List<PuzzlePlacementData> solutionPlacements;

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
            playMode = !string.IsNullOrEmpty(PuzzleSession.SelectedPuzzleId);
            GameAudio.SetMusic(playMode ? MusicCue.Investigation : MusicCue.Main);
            SetStatus("点击人物后选择格子，或直接拖动人物卡到右侧格子。");
            EnsureGameplayButtons();
            EnsureRegionPanel();
            EnsurePropsPanel();
            ApplyModeVisibility();
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
            // 地块涂色优先：选中地块卡时点击格子 = 给格子铺上对应图案，不进入人物放置。
            RegionDefinition selectedRegion = regionPanel == null ? null : regionPanel.SelectedRegion;
            if (selectedRegion != null)
            {
                if (cell is TestBoardCellUI cellUI)
                {
                    cellUI.SetFloorTile(selectedRegion.Index, selectedRegion.Sprite);
                }

                return;
            }

            // 道具放置/移除：选中道具卡时点击格子 = 放置道具；同格已是该道具则再点一次移除。
            PropDefinition selectedProp = propPanel == null ? null : propPanel.SelectedProp;
            if (selectedProp != null)
            {
                if (cell is TestBoardCellUI cellUI)
                {
                    if (cellUI.PropIndex == selectedProp.Index)
                    {
                        cellUI.SetProp(-1, null);
                    }
                    else
                    {
                        cellUI.SetProp(selectedProp.Index, selectedProp.Sprite);
                    }
                }

                return;
            }

            // 禁止放置（黑叉）：出题模式标记禁放格（保存规则、显示黑叉）；
            // 游玩模式玩家标记已排除区域（显示黑叉、不保存）。再点一次取消。
            CharacterPanelUI characterPanel = placementController == null ? null : placementController.SelectionSource;
            if (characterPanel != null && characterPanel.BlackXActive)
            {
                if (cell is TestBoardCellUI cellUI)
                {
                    if (playMode)
                    {
                        cellUI.TogglePlayerMark();
                    }
                    else
                    {
                        cellUI.SetEditorForbidden(!cellUI.EditorForbidden, true);
                    }
                }

                return;
            }

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
                ShowErrorPopup("保存失败", "已存在同名关卡  " + puzzleName + "  ，请更换关卡名后再保存。");
                return;
            }

            if (placementController != null && placementController.SelectionSource != null)
            {
                int missing = placementController.CountMissingCharacters(placementController.SelectionSource.Characters);
                if (missing > 0)
                {
                    ShowErrorPopup("保存失败", "还有 " + missing + " 名角色没有摆到棋盘上，请先摆好所有角色的位置作为标准答案再保存。");
                    return;
                }
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

            // 保存区域名字（按区域 id 索引）与名字文字偏移。
            if (wallEditController != null)
            {
                data.regionNames = new List<string>(wallEditController.RegionNames);
                data.regionNameOffsets = new List<Vector2>(wallEditController.RegionNameOffsets);
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
                        clue = character.Clue ?? string.Empty,
                        name = character.DisplayName,
                        gender = character.Gender
                    });
                }
            }

            // 保存格子地块（-1 = 无地块，否则为地块样式索引）。
            data.floorTiles = new int[size * size];
            for (int index = 0; index < testBoard.Cells.Count && index < data.floorTiles.Length; index++)
            {
                data.floorTiles[index] = testBoard.Cells[index].FloorTileIndex;
            }

            // 保存格子道具（-1 = 无道具，否则为道具索引）。
            data.props = new int[size * size];
            for (int index = 0; index < testBoard.Cells.Count && index < data.props.Length; index++)
            {
                data.props[index] = testBoard.Cells[index].PropIndex;
            }

            // 保存出题人禁放格（true = 禁止放置人物）。
            data.forbiddenCells = new bool[size * size];
            for (int index = 0; index < testBoard.Cells.Count && index < data.forbiddenCells.Length; index++)
            {
                data.forbiddenCells[index] = testBoard.Cells[index].EditorForbidden;
            }

            PuzzleSaveManager.SavePuzzle(data);
            SetSaveHint(string.Empty, false);
            ShowPopup("保存成功", "已保存关卡  " + puzzleName + "  。");
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
                // 恢复区域名字与文字偏移（旧存档无字段自动跳过）。
                wallEditController.ApplyRegionNames(data.regionNames);
                wallEditController.ApplyRegionNameOffsets(data.regionNameOffsets);
            }

            // 恢复格子地块（出题时铺的地块图案）。
            RestoreFloorTiles(data);
            RestoreProps(data);
            RestoreForbiddenCells(data);

            if (placementController != null && placementController.SelectionSource != null)
            {
                placementController.SelectionSource.ApplyClues(data.clues);
            }

            solutionPlacements = data.placements;
            if (placementController != null)
            {
                placementController.ClearUndoHistory();
            }

            RefreshHighlights();
            SetStatus("已载入关卡：" + data.name + "，请根据线索放置人物后提交。");
        }

        /// <summary>
        /// 把存档中的格子地块恢复显示到棋盘（-1 = 无地块）。
        /// </summary>
        private void RestoreFloorTiles(PuzzleData data)
        {
            if (testBoard == null || data.floorTiles == null || data.floorTiles.Length == 0)
            {
                return;
            }

            RegionStyleFactory.EnsureSprites();
            RegionDefinition[] definitions = RegionStyleFactory.All;

            for (int index = 0; index < testBoard.Cells.Count && index < data.floorTiles.Length; index++)
            {
                int tileIndex = data.floorTiles[index];
                if (tileIndex < 0 || tileIndex >= definitions.Length)
                {
                    continue;
                }

                testBoard.Cells[index].SetFloorTile(tileIndex, definitions[tileIndex].Sprite);
            }
        }

        /// <summary>
        /// 把存档中的格子道具恢复显示到棋盘（-1 = 无道具；旧存档无字段自动跳过）。
        /// </summary>
        private void RestoreProps(PuzzleData data)
        {
            if (testBoard == null || data.props == null || data.props.Length == 0)
            {
                return;
            }

            PropStyleFactory.EnsureSprites();
            PropDefinition[] definitions = PropStyleFactory.All;

            for (int index = 0; index < testBoard.Cells.Count && index < data.props.Length; index++)
            {
                int propIndex = data.props[index];
                if (propIndex < 0 || propIndex >= definitions.Length)
                {
                    continue;
                }

                testBoard.Cells[index].SetProp(propIndex, definitions[propIndex].Sprite);
            }
        }

        /// <summary>
        /// 恢复出题人禁放格（游玩模式隐形生效：格子拒绝放置，但不显示黑叉，避免剧透）。
        /// 旧存档无字段自动跳过。
        /// </summary>
        private void RestoreForbiddenCells(PuzzleData data)
        {
            if (testBoard == null || data.forbiddenCells == null || data.forbiddenCells.Length == 0)
            {
                return;
            }

            for (int index = 0; index < testBoard.Cells.Count && index < data.forbiddenCells.Length; index++)
            {
                if (data.forbiddenCells[index])
                {
                    testBoard.Cells[index].SetEditorForbidden(true, false);
                }
            }
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
            if (clueButton != null && submitButton != null && undoButton != null && redoButton != null)
            {
                return;
            }

            // 优先把按钮放进嫌疑人面板的标题区：隐藏「嫌疑人」标题文本，按钮占用该区域，
            // 并随面板的 Tab 显隐（面板 SetActive 时按钮一并隐藏/显示，与嫌疑人卡片一致）。
            GameObject characterPanel = GameObject.Find("CharacterPanel");
            RectTransform header = characterPanel == null
                ? null
                : FindChildByName<RectTransform>(characterPanel.transform, "Header");

            if (header != null)
            {
                TMP_Text title = FindChildByName<TMP_Text>(characterPanel.transform, "TitleText");
                if (title != null)
                {
                    title.gameObject.SetActive(false);
                }

                if (clueButton == null)
                {
                    clueButton = CreateHeaderButton(header, "ClueButton", "编辑线索",
                        new Vector2(0f, 0.46f), new Vector2(0.5f, 1f));
                    clueButton.onClick.AddListener(OpenClueEditor);
                }

                // 区域命名按钮：编辑线索旁边（右侧），出题模式显示（与游玩模式的提交按钮同位置，靠模式显隐切换）。
                if (regionNameButton == null)
                {
                    regionNameButton = CreateHeaderButton(header, "RegionNameButton", "区域命名",
                        new Vector2(0.5f, 0.46f), new Vector2(1f, 1f));
                    regionNameButton.onClick.AddListener(OpenRegionNameEditor);
                }

                // 游玩模式按钮区（恢复/撤销/提交）：恢复在撤销左边。
                if (redoButton == null)
                {
                    redoButton = CreateHeaderButton(header, "RedoButton", "恢复",
                        new Vector2(0f, 0.46f), new Vector2(0.25f, 1f));
                    redoButton.onClick.AddListener(HandleRedoClicked);
                }

                if (undoButton == null)
                {
                    undoButton = CreateHeaderButton(header, "UndoButton", "撤销",
                        new Vector2(0.25f, 0.46f), new Vector2(0.5f, 1f));
                    undoButton.onClick.AddListener(HandleUndoClicked);
                }

                if (submitButton == null)
                {
                    submitButton = CreateHeaderButton(header, "SubmitButton", "提交",
                        new Vector2(0.5f, 0.46f), new Vector2(1f, 1f));
                    submitButton.onClick.AddListener(SubmitPuzzle);
                }

                return;
            }

            // 兜底：找不到嫌疑人面板标题区时，按钮挂 Canvas 顶部（旧行为）。
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

        /// <summary>
        /// 在嫌疑人面板 Header 的标题区创建按钮（原「嫌疑人」文本区域），
        /// 通过 anchorMin/anchorMax 指定占用区段（如左半/右半/三分区）。
        /// </summary>
        private Button CreateHeaderButton(
            RectTransform header,
            string objectName,
            string labelText,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            RectTransform rect = CreateUiObject(objectName, header).GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(4f, 6f);
            rect.offsetMax = new Vector2(-4f, -4f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.48f, 0.86f, 1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UiClickFeedback.Ensure(button);

            TMP_Text label = CreateText("Label", rect, GetUiFont(), 22f, FontStyles.Bold);
            label.text = labelText;
            Stretch(label.rectTransform);
            label.raycastTarget = false;
            return button;
        }

        private static T FindChildByName<T>(Transform root, string name) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform child in root)
            {
                if (child.name == name)
                {
                    T component = child.GetComponent<T>();
                    if (component != null)
                    {
                        return component;
                    }
                }

                T nested = FindChildByName<T>(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        /// <summary>
        /// 初始化地块面板：在「地块」Tab 的 RegionPanel 上挂载 RegionPanelUI 并构建卡片，
        /// 同时接入「地块选择 ⇄ 人物选择」互斥逻辑。
        /// 注意：不能用 GameObject.Find（只能找到激活状态的对象），
        /// 地块面板可能已被 Tab 切换设为隐藏——从 Canvas 递归查找（Transform 树不受 active 影响）。
        /// </summary>
        private void EnsureRegionPanel()
        {
            if (regionPanel != null)
            {
                return;
            }

            GameObject regionObject = null;
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                RectTransform regionRect = FindChildByName<RectTransform>(canvas.transform, "RegionPanel");
                if (regionRect != null)
                {
                    regionObject = regionRect.gameObject;
                }
            }

            // 兜底：万一不在 Canvas 层级下。
            if (regionObject == null)
            {
                regionObject = GameObject.Find("RegionPanel");
            }

            if (regionObject == null)
            {
                return;
            }

            regionPanel = regionObject.GetComponent<RegionPanelUI>();
            if (regionPanel == null)
            {
                regionPanel = regionObject.AddComponent<RegionPanelUI>();
            }

            regionPanel.Configure(GetUiFont());
            regionPanel.SelectionChanged += HandleRegionSelectionChanged;

            CharacterPanelUI panel = placementController == null ? null : placementController.SelectionSource;
            if (panel != null)
            {
                panel.SelectionChanged += HandleCharacterSelectionChanged;
                panel.BlackXModeChanged += HandleBlackXModeChanged;
            }
        }

        /// <summary>
        /// 黑叉模式激活时取消地块/道具选择（互斥）。
        /// </summary>
        private void HandleBlackXModeChanged(bool active)
        {
            if (!active)
            {
                return;
            }

            if (regionPanel != null)
            {
                regionPanel.ClearSelection();
            }

            if (propPanel != null)
            {
                propPanel.ClearSelection();
            }
        }

        /// <summary>
        /// 选中地块时取消人物/道具/黑叉选择（互斥）。
        /// </summary>
        private void HandleRegionSelectionChanged(RegionDefinition region)
        {
            if (region == null)
            {
                return;
            }

            CharacterPanelUI panel = placementController == null ? null : placementController.SelectionSource;
            if (panel != null)
            {
                panel.ClearSelection();
                panel.SetBlackXActive(false);
            }

            if (propPanel != null)
            {
                propPanel.ClearSelection();
            }
        }

        /// <summary>
        /// 选中人物时取消地块/道具选择（互斥）。
        /// </summary>
        private void HandleCharacterSelectionChanged(CharacterData character)
        {
            if (character != null)
            {
                if (regionPanel != null)
                {
                    regionPanel.ClearSelection();
                }

                if (propPanel != null)
                {
                    propPanel.ClearSelection();
                }
            }
        }

        /// <summary>
        /// 初始化道具面板：在「道具」Tab 的 PropsPanel 上挂载 PropPanelUI 并构建卡片，
        /// 同时接入「道具选择 ⇄ 人物/地块选择」互斥逻辑。
        /// 与地块面板一样从 Canvas 递归查找（不受 SetActive(false) 影响）。
        /// </summary>
        private void EnsurePropsPanel()
        {
            if (propPanel != null)
            {
                return;
            }

            GameObject propObject = null;
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                RectTransform propRect = FindChildByName<RectTransform>(canvas.transform, "PropsPanel");
                if (propRect != null)
                {
                    propObject = propRect.gameObject;
                }
            }

            // 兜底：万一不在 Canvas 层级下。
            if (propObject == null)
            {
                propObject = GameObject.Find("PropsPanel");
            }

            if (propObject == null)
            {
                return;
            }

            propPanel = propObject.GetComponent<PropPanelUI>();
            if (propPanel == null)
            {
                propPanel = propObject.AddComponent<PropPanelUI>();
            }

            propPanel.Configure(GetUiFont());
            propPanel.SelectionChanged += HandlePropSelectionChanged;
        }

        /// <summary>
        /// 选中道具时取消人物/地块/黑叉选择（互斥）。
        /// </summary>
        private void HandlePropSelectionChanged(PropDefinition prop)
        {
            if (prop == null)
            {
                return;
            }

            CharacterPanelUI panel = placementController == null ? null : placementController.SelectionSource;
            if (panel != null)
            {
                panel.ClearSelection();
                panel.SetBlackXActive(false);
            }

            if (regionPanel != null)
            {
                regionPanel.ClearSelection();
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

            // 提示文字：优先挂到左侧面板容器下（面板正下方、随面板居中）；
            // 找不到容器时回退为全局底部居中（旧行为）。
            RectTransform container = FindChildByName<RectTransform>(canvas.transform, "LeftPanelContainer");
            if (container != null)
            {
                statusText = CreateText("PlacementStatusText", container, font, 22f, FontStyles.Normal);
                RectTransform rect = statusText.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -40f);
                rect.sizeDelta = new Vector2(880f, 40f);
            }
            else
            {
                statusText = CreateText("PlacementStatusText", canvas.transform as RectTransform, font, 22f, FontStyles.Normal);
                RectTransform rect = statusText.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 95f);
                rect.sizeDelta = new Vector2(620f, 40f);
            }

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

        /// <summary>
        /// 打开区域命名面板：列出棋盘当前所有区域，逐个输入名字（交互方式与线索编辑一致）。
        /// </summary>
        private void OpenRegionNameEditor()
        {
            if (wallEditController == null || wallEditController.Walls == null)
            {
                SetStatus("墙壁控制器不可用，无法编辑区域命名。", true);
                return;
            }

            if (regionNamePanelRoot == null)
            {
                BuildRegionNamePanel();
            }

            if (regionNamePanelRoot == null)
            {
                SetStatus("无法创建区域命名窗口，请检查 Canvas 配置。", true);
                return;
            }

            RebuildRegionNameRows();
            regionNamePanelRoot.SetActive(true);
        }

        private void BuildRegionNamePanel()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            TMP_FontAsset font = GetUiFont();
            if (canvas == null || font == null)
            {
                return;
            }

            RectTransform root = CreateUiObject("RegionNamePanelRoot", canvas.transform).GetComponent<RectTransform>();
            regionNamePanelRoot = root.gameObject;
            Image mask = root.gameObject.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.6f);
            Stretch(root);

            RectTransform panel = CreateUiObject("Panel", root).GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(760f, 760f);
            panel.anchoredPosition = Vector2.zero;
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.13f, 0.15f, 0.20f, 0.99f);

            TMP_Text title = CreateText("TitleText", panel, font, 28f, FontStyles.Bold);
            title.text = "区域命名";
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 56f);
            titleRect.anchoredPosition = new Vector2(0f, -12f);

            regionNameContentRect = CreateUiObject("Content", panel).GetComponent<RectTransform>();
            regionNameContentRect.anchorMin = new Vector2(0f, 0f);
            regionNameContentRect.anchorMax = new Vector2(1f, 1f);
            regionNameContentRect.offsetMin = new Vector2(20f, 84f);
            regionNameContentRect.offsetMax = new Vector2(-20f, -70f);

            RectTransform applyRect = CreateUiObject("ApplyButton", panel).GetComponent<RectTransform>();
            applyRect.anchorMin = new Vector2(0.5f, 0f);
            applyRect.anchorMax = new Vector2(0.5f, 0f);
            applyRect.pivot = new Vector2(0.5f, 0.5f);
            applyRect.sizeDelta = new Vector2(150f, 48f);
            applyRect.anchoredPosition = new Vector2(-90f, 24f);
            MakeButton(applyRect, "应用", font, ApplyRegionNameEdits);

            RectTransform cancelRect = CreateUiObject("CancelButton", panel).GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.5f, 0f);
            cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0.5f);
            cancelRect.sizeDelta = new Vector2(150f, 48f);
            cancelRect.anchoredPosition = new Vector2(90f, 24f);
            MakeButton(cancelRect, "取消", font, CloseRegionNamePanel);
        }

        private void RebuildRegionNameRows()
        {
            foreach (GameObject row in regionNameRows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }

            regionNameRows.Clear();
            regionNameInputs.Clear();
            regionNameInputXs.Clear();
            regionNameInputYs.Clear();
            regionNameIds.Clear();

            if (regionNameContentRect == null || wallEditController == null || wallEditController.Walls == null)
            {
                return;
            }

            TMP_FontAsset font = GetUiFont();
            int[,] regions = wallEditController.Walls.ComputeRegions();
            int size = wallEditController.Walls.Size;
            int regionCount = 0;
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    if (regions[row, col] >= regionCount)
                    {
                        regionCount = regions[row, col] + 1;
                    }
                }
            }

            for (int regionId = 0; regionId < regionCount; regionId++)
            {
                RectTransform rowRect = CreateUiObject("RegionNameRow", regionNameContentRect).GetComponent<RectTransform>();
                regionNameRows.Add(rowRect.gameObject);
                rowRect.anchorMin = new Vector2(0.5f, 1f);
                rowRect.anchorMax = new Vector2(0.5f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -12f - regionId * 64f);
                rowRect.sizeDelta = new Vector2(720f, 56f);

                TMP_Text label = CreateText("Label", rowRect, font, 18f, FontStyles.Bold);
                label.text = "区域 " + (regionId + 1);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                LayoutInRow(label.rectTransform, 4f, 80f, 40f);

                // 名字输入框（左 88 ~ 右 520，与右侧 X/Y 区不重叠）。
                TMP_InputField input = CreateClueInput(rowRect, font, 12);
                LayoutInRow(input.GetComponent<RectTransform>(), 88f, 520f, 44f);
                input.text = regionId < wallEditController.RegionNames.Count
                    ? wallEditController.RegionNames[regionId] ?? string.Empty
                    : string.Empty;
                regionNameInputs.Add(input);
                regionNameIds.Add(regionId);

                // X 偏移输入框（标签 + 数字框）。
                TMP_Text xLabel = CreateText("XLabel", rowRect, font, 16f, FontStyles.Normal);
                xLabel.text = "X";
                xLabel.alignment = TextAlignmentOptions.MidlineLeft;
                LayoutInRow(xLabel.rectTransform, 528f, 552f, 40f);

                TMP_InputField inputX = CreateNumberInput(rowRect, font);
                LayoutInRow(inputX.GetComponent<RectTransform>(), 556f, 630f, 44f);

                // Y 偏移输入框（标签 + 数字框）。
                TMP_Text yLabel = CreateText("YLabel", rowRect, font, 16f, FontStyles.Normal);
                yLabel.text = "Y";
                yLabel.alignment = TextAlignmentOptions.MidlineLeft;
                LayoutInRow(yLabel.rectTransform, 636f, 660f, 40f);

                TMP_InputField inputY = CreateNumberInput(rowRect, font);
                LayoutInRow(inputY.GetComponent<RectTransform>(), 664f, 716f, 44f);

                Vector2 offset = regionId < wallEditController.RegionNameOffsets.Count
                    ? wallEditController.RegionNameOffsets[regionId]
                    : Vector2.zero;
                inputX.text = offset.x.ToString("0");
                inputY.text = offset.y.ToString("0");
                regionNameInputXs.Add(inputX);
                regionNameInputYs.Add(inputY);
            }
        }

        /// <summary>创建允许输入负数与小数的数字输入框。</summary>
        private TMP_InputField CreateNumberInput(RectTransform parent, TMP_FontAsset font)
        {
            TMP_InputField input = CreateClueInput(parent, font, 8);
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.text = "0";
            return input;
        }

        /// <summary>
        /// 在行内布局一个元素：锚点统一为行左边缘垂直居中，
        /// 用 offsetMin/offsetMax 精确指定左右边界与高度（杜绝元素重叠）。
        /// </summary>
        private static void LayoutInRow(RectTransform rect, float left, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = new Vector2(left, -height * 0.5f);
            rect.offsetMax = new Vector2(right, height * 0.5f);
        }

        private void ApplyRegionNameEdits()
        {
            if (wallEditController == null)
            {
                return;
            }

            for (int index = 0; index < regionNameInputs.Count && index < regionNameIds.Count; index++)
            {
                TMP_InputField input = regionNameInputs[index];
                if (input == null)
                {
                    continue;
                }

                int regionId = regionNameIds[index];
                wallEditController.SetRegionName(regionId, input.text.Trim());

                float x = 0f;
                float y = 0f;
                if (index < regionNameInputXs.Count && regionNameInputXs[index] != null)
                {
                    float.TryParse(regionNameInputXs[index].text, out x);
                }

                if (index < regionNameInputYs.Count && regionNameInputYs[index] != null)
                {
                    float.TryParse(regionNameInputYs[index].text, out y);
                }

                wallEditController.SetRegionNameOffset(regionId, new Vector2(x, y));
            }

            CloseRegionNamePanel();
            SetStatus("区域名字已更新，保存关卡时会一起存入存档。", false);
        }

        private void CloseRegionNamePanel()
        {
            if (regionNamePanelRoot != null)
            {
                regionNamePanelRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 游玩模式撤销：回退最近一步人物放置/移动操作（多步可连续撤销）。
        /// </summary>
        private void HandleUndoClicked()
        {
            if (placementController == null)
            {
                SetStatus("放置控制器不可用。", true);
                return;
            }

            if (placementController.UndoLastPlacement())
            {
                RefreshHighlights();
                SetStatus("已撤销上一步操作。");
            }
            else
            {
                SetStatus("没有可撤销的操作。");
            }
        }

        /// <summary>
        /// 游玩模式恢复：重做最近一次被撤销的放置/移动操作（多步可连续恢复）。
        /// </summary>
        private void HandleRedoClicked()
        {
            if (placementController == null)
            {
                SetStatus("放置控制器不可用。", true);
                return;
            }

            if (placementController.RedoLastPlacement())
            {
                RefreshHighlights();
                SetStatus("已恢复上一步操作。");
            }
            else
            {
                SetStatus("没有可恢复的操作。");
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

            if (testBoard == null)
            {
                ShowErrorPopup("提交失败", "棋盘未配置，无法判定。");
                return;
            }

            if (solutionPlacements == null || solutionPlacements.Count == 0)
            {
                ShowErrorPopup("提交失败", "这关没有标准答案，无法判定是否正确（请回到创建界面摆好人物再保存）。");
                return;
            }

            HashSet<string> solvedIds = new HashSet<string>();
            foreach (PuzzlePlacementData solution in solutionPlacements)
            {
                if (solution == null || string.IsNullOrEmpty(solution.characterId))
                {
                    continue;
                }

                CharacterData solutionCharacter = placementController.FindCharacterById(solution.characterId);
                if (solutionCharacter == null)
                {
                    ShowErrorPopup("提交失败", "标准答案与当前关卡不匹配，无法判定。");
                    return;
                }

                if (!placementController.TryGetPlacement(solutionCharacter, out ICharacterPlacementCell solutionCell) ||
                    solutionCell == null)
                {
                    ShowErrorPopup("提交失败", solutionCharacter.DisplayName + " 还没有放置，请根据线索继续推理。");
                    return;
                }

                int expectedIndex = solution.cellIndex;
                int actualIndex = solutionCell.GridPosition.y * testBoard.Columns + solutionCell.GridPosition.x;
                if (actualIndex != expectedIndex)
                {
                    HighlightCells(new List<ICharacterPlacementCell> { solutionCell });
                    ShowErrorPopup("位置错误", solutionCharacter.DisplayName + " 的位置不对，请再核对线索（该格子已标红）。");
                    return;
                }

                solvedIds.Add(solution.characterId);
            }

            if (solvedIds.Count < characters.Count)
            {
                ShowErrorPopup("提交失败", "标准答案不完整：有角色没有答案位置，无法判定（请回到创建界面补全）。");
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
                GameAudio.Play(SfxCue.CaseSolved);
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

        /// <summary>
        /// 按进入模式隐藏/显示编辑器与游玩 UI：
        /// - 创建模式：显示保存条、棋盘大小、地块面板与「编辑线索」按钮，隐藏「提交」；
        /// - 游玩模式：隐藏保存条、棋盘大小、地块面板与「编辑线索」按钮，显示「提交」，并强制回到放置模式。
        /// </summary>
        private void ApplyModeVisibility()
        {
            GameObject savePanelObject = nameInput != null && nameInput.transform.parent != null
                ? nameInput.transform.parent.gameObject
                : null;

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (savePanelObject == null)
            {
                RectTransform savePanelRect = canvas == null
                    ? null
                    : FindChildByName<RectTransform>(canvas.transform, "SavePanel");
                savePanelObject = savePanelRect == null ? null : savePanelRect.gameObject;
            }

            BoardSizePanelUI boardSizePanel = FindFirstObjectByType<BoardSizePanelUI>();

            GameObject regionsPanelObject = regionPanel != null ? regionPanel.gameObject : null;
            if (regionsPanelObject == null)
            {
                RectTransform regionRect = canvas == null
                    ? null
                    : FindChildByName<RectTransform>(canvas.transform, "RegionPanel");
                regionsPanelObject = regionRect == null ? null : regionRect.gameObject;
            }

            RectTransform regionsTabRect = canvas == null
                ? null
                : FindChildByName<RectTransform>(canvas.transform, "RegionsTab");
            GameObject regionsTabObject = regionsTabRect == null ? null : regionsTabRect.gameObject;

            GameObject propsPanelObject = propPanel != null ? propPanel.gameObject : null;
            if (propsPanelObject == null)
            {
                RectTransform propRect = canvas == null
                    ? null
                    : FindChildByName<RectTransform>(canvas.transform, "PropsPanel");
                propsPanelObject = propRect == null ? null : propRect.gameObject;
            }

            RectTransform propsTabRect = canvas == null
                ? null
                : FindChildByName<RectTransform>(canvas.transform, "PropsTab");
            GameObject propsTabObject = propsTabRect == null ? null : propsTabRect.gameObject;

            if (savePanelObject != null)
            {
                savePanelObject.SetActive(!playMode);
            }

            if (boardSizePanel != null)
            {
                boardSizePanel.gameObject.SetActive(!playMode);
            }

            // 面板显隐交给 LeftPanelTabsUI 的 Tab 切换管理，这里不再直接 SetActive 地块/道具面板，
            // 避免覆盖 Tab 切换的初始状态（否则出题模式初始会错误显示编辑面板）。
            // 仅在游玩模式强制切到嫌疑人面板（地块/道具 Tab 不可用）；出题模式保持 Tab 的初始状态（嫌疑人面板）。
            if (playMode)
            {
                if (regionsPanelObject != null)
                {
                    regionsPanelObject.SetActive(false);
                }

                if (propsPanelObject != null)
                {
                    propsPanelObject.SetActive(false);
                }
            }

            if (regionsTabObject != null)
            {
                regionsTabObject.SetActive(!playMode);
            }

            if (propsTabObject != null)
            {
                propsTabObject.SetActive(!playMode);
            }

            if (clueButton != null)
            {
                clueButton.gameObject.SetActive(!playMode);
            }

            if (regionNameButton != null)
            {
                regionNameButton.gameObject.SetActive(!playMode);
            }

            if (submitButton != null)
            {
                submitButton.gameObject.SetActive(playMode);
            }

            if (undoButton != null)
            {
                undoButton.gameObject.SetActive(playMode);
            }

            if (redoButton != null)
            {
                redoButton.gameObject.SetActive(playMode);
            }

            // 游玩模式禁用嫌疑人卡的性别切换（出题模式可编辑性别）。
            CharacterPanelUI characterPanel = placementController == null
                ? null
                : placementController.SelectionSource;
            if (characterPanel != null)
            {
                characterPanel.SetGenderToggleEnabled(!playMode);
            }

            if (playMode && wallEditController != null)
            {
                wallEditController.SetMode(WallEditController.EditorMode.Place);
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
