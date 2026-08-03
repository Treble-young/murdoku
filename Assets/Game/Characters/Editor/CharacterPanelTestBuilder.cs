using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Murdoku.Characters;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Murdoku.Characters.Editor
{
    public static class CharacterPanelTestBuilder
    {
        private const string RequiredUnityVersion = "6000.3.20f1";
        private const string Root = "Assets/Game/Characters";
        private const string DataRoot = Root + "/Data";
        private const string PrefabRoot = Root + "/Prefabs";
        private const string ScenePath = "Assets/Scenes/CharacterPanelTest.unity";
        private const string AutoPlayCommandLineFlag = "-murdokuCharacterPanelPlay";
        private const string AutoPlaySessionKey = "Murdoku.CharacterPanelTest.AutoPlayStarted";

        private static TMP_FontAsset cachedFont;

        [InitializeOnLoadMethod]
        private static void ScheduleCommandLineAutoPlay()
        {
            if (!Array.Exists(
                    Environment.GetCommandLineArgs(),
                    argument => string.Equals(argument, AutoPlayCommandLineFlag, StringComparison.Ordinal)))
            {
                return;
            }

            EditorApplication.update -= FocusGameViewWhenPlaying;
            EditorApplication.update += FocusGameViewWhenPlaying;

            if (SessionState.GetBool(AutoPlaySessionKey, false))
            {
                return;
            }

            EditorApplication.update -= OpenCommandLineSceneWhenReady;
            EditorApplication.update += OpenCommandLineSceneWhenReady;
        }

        private static void FocusGameViewWhenPlaying()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= FocusGameViewWhenPlaying;
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameView.Show();
            gameView.Focus();
            gameView.Repaint();
        }

        private static void OpenCommandLineSceneWhenReady()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.timeSinceStartup < 5d)
            {
                return;
            }

            EditorApplication.update -= OpenCommandLineSceneWhenReady;
            SessionState.SetBool(AutoPlaySessionKey, true);
            OpenCharacterPanelTestAndPlay();
        }

        [MenuItem("Tools/Murdoku/Build Character Panel Test")]
        public static void BuildCharacterPanelTest()
        {
            RequireExactEditorVersion();
            EnsureFolders();

            CreateOrUpdateCharacter(
                "Leo",
                CharacterGender.Male,
                "测试线索：Leo 的行动路线仍待确认。",
                new Color(0.32f, 0.55f, 0.84f, 1f));
            CreateOrUpdateCharacter(
                "Mina",
                CharacterGender.Female,
                "测试线索：Mina 在案发时见过一名可疑人物。",
                new Color(0.82f, 0.45f, 0.58f, 1f));
            CreateOrUpdateCharacter(
                "Owen",
                CharacterGender.Male,
                "测试线索：Owen 的证词与现场时间不一致。",
                new Color(0.50f, 0.68f, 0.42f, 1f));

            CreateCharacterCardPrefab();
            CreateCharacterPanelPrefab();
            CreateTestBoardCellPrefab();

            // Flush temporary scene-object identities before loading the saved prefabs.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            CharacterCardUI cardPrefab = LoadPrefabComponent<CharacterCardUI>(
                $"{PrefabRoot}/CharacterCard.prefab");
            CreateCharacterSystemPrefab(cardPrefab);

            CreateTestScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Character panel test assets created at {Root} and {ScenePath}.");
        }

        [MenuItem("Tools/Murdoku/Validate Character Panel Test")]
        public static void ValidateCharacterPanelTest()
        {
            RequireExactEditorVersion();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Camera[] cameras = FindSceneComponents<Camera>(scene);
            EventSystem[] eventSystems = FindSceneComponents<EventSystem>(scene);
            Canvas[] canvases = FindSceneComponents<Canvas>(scene);
            Require(cameras.Length == 1, "The test scene must contain exactly one Camera.");
            Require(eventSystems.Length == 1, "The test scene must contain exactly one EventSystem.");
            Require(canvases.Length == 1, "The test scene must contain exactly one Canvas.");
            Require(
                eventSystems[0].GetComponent<InputSystemUIInputModule>() != null,
                "The EventSystem must use InputSystemUIInputModule.");

            CharacterPanelView panelView = FindSingleSceneComponent<CharacterPanelView>(scene);
            CharacterPanelUI panelUI = FindSingleSceneComponent<CharacterPanelUI>(scene);
            CharacterPlacementController placement = FindSingleSceneComponent<CharacterPlacementController>(scene);
            TestBoardController board = FindSingleSceneComponent<TestBoardController>(scene);
            FindSingleSceneComponent<CharacterPanelTestCoordinator>(scene);

            Require(panelView.CharacterGrid != null, "CharacterPanelView.CharacterGrid is not assigned.");
            SerializedObject panelSerialized = new SerializedObject(panelUI);
            Require(panelSerialized.FindProperty("view").objectReferenceValue == panelView, "CharacterPanelUI.View is not assigned.");
            Require(panelSerialized.FindProperty("cardPrefab").objectReferenceValue != null, "CharacterPanelUI.CardPrefab is not assigned.");
            Require(panelSerialized.FindProperty("characters").arraySize == 3, "CharacterPanelUI must contain three test characters.");

            SerializedObject boardSerialized = new SerializedObject(board);
            Require(boardSerialized.FindProperty("rows").intValue == 6, "Test board row count must be 6.");
            Require(boardSerialized.FindProperty("columns").intValue == 6, "Test board column count must be 6.");
            Require(boardSerialized.FindProperty("gridRoot").objectReferenceValue != null, "TestBoardController.GridRoot is not assigned.");
            Require(boardSerialized.FindProperty("cellPrefab").objectReferenceValue != null, "TestBoardController.CellPrefab is not assigned.");

            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                Require(buildScene.path != ScenePath, "CharacterPanelTest must not be included in Build Settings.");
            }

            string[] dependencies = AssetDatabase.GetDependencies(ScenePath, true);
            foreach (string dependency in dependencies)
            {
                Require(dependency != "Assets/Scenes/Level01.unity", "The test scene must not depend on Level01.unity.");
                Require(dependency != "Assets/Gridmap/Scripts/GridManager.cs", "The test scene must not depend on GridManager.cs.");
                Require(dependency != "Assets/Gridmap/Scripts/Tile.cs", "The test scene must not depend on Tile.cs.");
                Require(dependency != "Assets/Tile.prefab", "The test scene must not depend on Tile.prefab.");
            }

            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/CharacterPanel.prefab");
            Require(panelPrefab != null, "CharacterPanel.prefab is missing.");
            Require(panelPrefab.GetComponentInChildren<Camera>(true) == null, "CharacterPanel.prefab must not contain a Camera.");
            Require(panelPrefab.GetComponentInChildren<EventSystem>(true) == null, "CharacterPanel.prefab must not contain an EventSystem.");
            Require(panelPrefab.GetComponentInChildren<TestBoardController>(true) == null, "CharacterPanel.prefab must not contain a test board.");

            GameObject systemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/CharacterSystem.prefab");
            Require(systemPrefab != null, "CharacterSystem.prefab is missing.");
            Require(systemPrefab.GetComponentInChildren<CharacterPanelUI>(true) != null, "CharacterSystem.prefab is missing CharacterPanelUI.");
            Require(systemPrefab.GetComponentInChildren<CharacterPlacementController>(true) != null, "CharacterSystem.prefab is missing CharacterPlacementController.");
            Require(systemPrefab.GetComponentInChildren<TestBoardController>(true) == null, "CharacterSystem.prefab must not reference the test board.");

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                ValidateNoMissingScripts(rootObject);
            }

            panelUI.Rebuild();
            CharacterCardUI[] cards = panelView.CharacterGrid.GetComponentsInChildren<CharacterCardUI>(true);
            Require(cards.Length == 3, "Character panel must generate exactly three cards.");
            CharacterCardUI leoCard = FindCard(cards, "Leo");
            CharacterCardUI minaCard = FindCard(cards, "Mina");
            FindCard(cards, "Owen");

            InvokeCardClick(leoCard);
            Require(panelUI.SelectedCharacter != null && panelUI.SelectedCharacter.DisplayName == "Leo", "Clicking Leo must select Leo.");
            Require(leoCard.transform.localScale == Vector3.one, "The GridLayoutGroup card root must remain at scale 1.");
            Transform leoVisualRoot = leoCard.transform.Find("LayoutRoot/VisualRoot");
            Require(leoVisualRoot != null && Approximately(leoVisualRoot.localScale.x, 1.1f), "Leo VisualRoot must scale to 1.1 when selected.");
            Require(leoCard is IBeginDragHandler, "Character cards must support drag operations.");

            InvokeCardClick(leoCard);
            Require(panelUI.SelectedCharacter == null, "Clicking the selected Leo card again must clear selection.");
            Require(Approximately(leoVisualRoot.localScale.x, 1f), "Deselected Leo VisualRoot must return to scale 1.");
            InvokeCardClick(leoCard);
            Require(panelUI.SelectedCharacter == leoCard.Character, "Leo must be selectable again after toggling off.");

            board.GenerateGrid();
            Require(board.Cells.Count == 36, "TestBoardController must generate 36 cells.");
            Require(board.Cells[0] is IDropHandler, "Test board cells must accept character drops.");
            placement.SetSelectionSource(null);
            placement.SetSelectionSource(panelUI);
            Require(
                placement.HandleCharacterDropped(leoCard.Character, board.Cells[0]) == CharacterPlacementResult.Placed,
                "Dragging Leo must place it in the first empty cell.");
            Require(
                placement.HandleCharacterDropped(leoCard.Character, board.Cells[1]) == CharacterPlacementResult.Moved,
                "Dragging Leo must move it to a second empty cell.");
            Require(!board.Cells[0].IsOccupied && board.Cells[1].IsOccupied, "Moving Leo must clear the old cell.");

            InvokeCardClick(minaCard);
            placement.SetSelectionSource(null);
            placement.SetSelectionSource(panelUI);
            Require(panelUI.SelectedCharacter != null && panelUI.SelectedCharacter.DisplayName == "Mina", "Clicking Mina must switch selection to Mina.");
            Require(
                placement.HandleCharacterDropped(minaCard.Character, board.Cells[1]) == CharacterPlacementResult.CellOccupied,
                "Dragging Mina must not allow it to occupy Leo's cell.");
            Require(
                placement.HandleCharacterDropped(minaCard.Character, board.Cells[2]) == CharacterPlacementResult.Placed,
                "Dragging Mina must place it in a different empty cell.");
            Require(board.Cells[1].CurrentCharacter.DisplayName == "Leo", "Leo must remain in place after a rejected placement.");
            Require(board.Cells[2].CurrentCharacter.DisplayName == "Mina", "Mina must occupy the new cell.");

            Debug.Log("CharacterPanelTest validation passed: hierarchy, selection toggle, drag placement, movement, and occupancy checks succeeded.");
        }

        [MenuItem("Tools/Murdoku/Open Character Panel Test and Play")]
        public static void OpenCharacterPanelTestAndPlay()
        {
            RequireExactEditorVersion();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.isPlaying = true;
                Debug.Log("Opened CharacterPanelTest.unity and entered Play Mode.");
            };
        }

        [MenuItem("Tools/Murdoku/Focus Character Panel Game View %#g")]
        public static void FocusCharacterPanelGameView()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameView.Show();
            gameView.Focus();
            gameView.Repaint();
            Debug.Log("Focused the CharacterPanelTest Game view.");
        }

        private static void RequireExactEditorVersion()
        {
            if (!string.Equals(Application.unityVersion, RequiredUnityVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"CharacterPanelTestBuilder requires Unity {RequiredUnityVersion}. " +
                    $"The current editor is {Application.unityVersion}; generation was cancelled to protect ProjectSettings.");
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
            {
                throw new InvalidOperationException(
                    "The active scene has unsaved changes. Save it manually before running the builder; " +
                    "the builder will never save the current scene on your behalf.");
            }
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game", "Characters", "Data"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game", "Characters", "Prefabs"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game", "Characters", "Editor"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game", "Characters", "Scripts", "Data"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game", "Characters", "Scripts", "Placement"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game", "Characters", "Scripts", "UI"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game", "Characters", "Scripts", "Test"));
            AssetDatabase.Refresh();
        }

        private static CharacterData CreateOrUpdateCharacter(
            string displayName,
            CharacterGender gender,
            string clue,
            Color placeholderColor)
        {
            string path = $"{DataRoot}/{displayName}.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CharacterData>();
                AssetDatabase.CreateAsset(data, path);
            }

            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("characterId").stringValue = displayName.ToLowerInvariant();
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("gender").enumValueIndex = (int)gender;
            serialized.FindProperty("clue").stringValue = clue;
            serialized.FindProperty("portrait").objectReferenceValue = null;
            serialized.FindProperty("placeholderColor").colorValue = placeholderColor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }

        private static CharacterCardUI CreateCharacterCardPrefab()
        {
            RectTransform root = CreateRect("CharacterCard", null);
            root.sizeDelta = new Vector2(170f, 320f);
            LayoutElement layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 170f;
            layoutElement.preferredHeight = 320f;

            RectTransform layoutRoot = CreateRect("LayoutRoot", root);
            Stretch(layoutRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform visualRoot = CreateRect("VisualRoot", layoutRoot);
            Place(visualRoot, new Vector2(0.5f, 0.5f), new Vector2(154f, 288f), Vector2.zero);

            Image selectionBorder = AddImage("SelectionBorder", visualRoot, new Color(0.20f, 0.52f, 0.95f, 1f));
            Stretch(selectionBorder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image cardBackground = AddImage("CardBackground", visualRoot, new Color(0.96f, 0.97f, 0.99f, 1f));
            Stretch(cardBackground.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));

            Image portraitFrame = AddImage("PortraitFrame", visualRoot, new Color(0.84f, 0.88f, 0.94f, 1f));
            Place(portraitFrame.rectTransform, new Vector2(0.5f, 1f), new Vector2(138f, 128f), new Vector2(0f, -15f), new Vector2(0.5f, 1f));

            Image portraitPlaceholder = AddImage("PortraitPlaceholder", portraitFrame.rectTransform, Color.white);
            Stretch(portraitPlaceholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            Image portraitImage = AddImage("PortraitImage", portraitFrame.rectTransform, Color.white);
            Stretch(portraitImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            portraitImage.preserveAspect = true;

            TextMeshProUGUI initialText = AddText(
                "InitialText",
                portraitFrame.rectTransform,
                "L",
                62f,
                Color.white,
                TextAlignmentOptions.Center);
            Stretch(initialText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            TextMeshProUGUI genderText = AddText(
                "GenderIcon",
                visualRoot,
                "♂",
                30f,
                new Color(0.15f, 0.33f, 0.55f, 1f),
                TextAlignmentOptions.Center);
            Place(genderText.rectTransform, new Vector2(1f, 1f), new Vector2(34f, 34f), new Vector2(-10f, -10f), new Vector2(1f, 1f));

            TextMeshProUGUI nameText = AddText(
                "NameText",
                visualRoot,
                "Leo",
                25f,
                new Color(0.10f, 0.13f, 0.18f, 1f),
                TextAlignmentOptions.Center);
            Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(145f, 38f), new Vector2(0f, -148f), new Vector2(0.5f, 1f));
            nameText.fontStyle = FontStyles.Bold;

            Image clueBox = AddImage("ClueBox", visualRoot, new Color(0.89f, 0.92f, 0.96f, 1f));
            Stretch(clueBox.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -190f));

            TextMeshProUGUI clueText = AddText(
                "ClueText",
                clueBox.rectTransform,
                "测试线索",
                17f,
                new Color(0.18f, 0.20f, 0.24f, 1f),
                TextAlignmentOptions.TopLeft);
            Stretch(clueText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -8f));
            clueText.overflowMode = TextOverflowModes.Ellipsis;

            Image buttonGraphic = AddImage("Button", root, Color.clear);
            Stretch(buttonGraphic.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Button button = buttonGraphic.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonGraphic;
            button.transition = Selectable.Transition.None;

            CharacterCardUI card = root.gameObject.AddComponent<CharacterCardUI>();
            SetReference(card, "button", button);
            SetReference(card, "visualRoot", visualRoot);
            SetReference(card, "selectionBorder", selectionBorder.gameObject);
            SetReference(card, "portraitImage", portraitImage);
            SetReference(card, "portraitPlaceholder", portraitPlaceholder);
            SetReference(card, "initialText", initialText);
            SetReference(card, "genderText", genderText);
            SetReference(card, "nameText", nameText);
            SetReference(card, "clueText", clueText);
            selectionBorder.gameObject.SetActive(false);

            CharacterCardUI saved = SavePrefab(root.gameObject, $"{PrefabRoot}/CharacterCard.prefab")
                .GetComponent<CharacterCardUI>();
            return saved;
        }

        private static CharacterPanelView CreateCharacterPanelPrefab()
        {
            RectTransform root = CreateRect("CharacterPanel", null);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(610f, -40f);
            root.anchoredPosition = new Vector2(20f, 0f);

            Image background = AddImage("Background", root, new Color(0.94f, 0.95f, 0.98f, 0.98f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform header = CreateRect("Header", root);
            Stretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -120f), new Vector2(-20f, -10f));

            TextMeshProUGUI title = AddText(
                "TitleText",
                header,
                "嫌疑人",
                38f,
                new Color(0.09f, 0.12f, 0.18f, 1f),
                TextAlignmentOptions.Center);
            Stretch(title.rectTransform, new Vector2(0f, 0.46f), Vector2.one, Vector2.zero, Vector2.zero);
            title.fontStyle = FontStyles.Bold;

            TextMeshProUGUI instruction = AddText(
                "InstructionText",
                header,
                "再次点击可取消选择，也可拖动人物卡到右侧格子",
                20f,
                new Color(0.31f, 0.37f, 0.46f, 1f),
                TextAlignmentOptions.Center);
            Stretch(instruction.rectTransform, Vector2.zero, new Vector2(1f, 0.46f), Vector2.zero, Vector2.zero);

            Image scrollViewImage = AddImage("CharacterScrollView", root, new Color(0.88f, 0.90f, 0.94f, 0.72f));
            Stretch(scrollViewImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -130f));
            ScrollRect scrollRect = scrollViewImage.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;

            Image viewportImage = AddImage("Viewport", scrollViewImage.rectTransform, new Color(1f, 1f, 1f, 0.01f));
            Stretch(viewportImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-24f, -8f));
            Mask mask = viewportImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform grid = CreateRect("CharacterGrid", viewportImage.rectTransform);
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.sizeDelta = Vector2.zero;
            grid.anchoredPosition = Vector2.zero;
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.padding = new RectOffset(7, 7, 8, 8);
            gridLayout.cellSize = new Vector2(170f, 320f);
            gridLayout.spacing = new Vector2(4f, 10f);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
            ContentSizeFitter fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image scrollbarImage = AddImage("Scrollbar Vertical", scrollViewImage.rectTransform, new Color(0.62f, 0.67f, 0.75f, 0.25f));
            Stretch(scrollbarImage.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-17f, 8f), new Vector2(-5f, -8f));
            RectTransform slidingArea = CreateRect("Sliding Area", scrollbarImage.rectTransform);
            Stretch(slidingArea, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            Image handleImage = AddImage("Handle", slidingArea, new Color(0.30f, 0.51f, 0.78f, 0.85f));
            Stretch(handleImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Scrollbar scrollbar = scrollbarImage.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleImage.rectTransform;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.size = 0.5f;

            scrollRect.viewport = viewportImage.rectTransform;
            scrollRect.content = grid;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            CharacterPanelView view = root.gameObject.AddComponent<CharacterPanelView>();
            SetReference(view, "characterGrid", grid);

            CharacterPanelView saved = SavePrefab(root.gameObject, $"{PrefabRoot}/CharacterPanel.prefab")
                .GetComponent<CharacterPanelView>();
            return saved;
        }

        private static TestBoardCellUI CreateTestBoardCellPrefab()
        {
            RectTransform root = CreateRect("TestBoardCell", null);
            root.sizeDelta = new Vector2(112f, 112f);

            Image background = AddImage("Background", root, new Color(0.78f, 0.88f, 0.94f, 1f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            TextMeshProUGUI coordinate = AddText(
                "CoordinateText",
                root,
                "0,0",
                13f,
                new Color(0.17f, 0.24f, 0.32f, 0.75f),
                TextAlignmentOptions.TopLeft);
            Stretch(coordinate.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 4f), new Vector2(-5f, -4f));

            RectTransform tokenRoot = CreateRect("tokenRoot", root);
            Place(tokenRoot, new Vector2(0.5f, 0.5f), new Vector2(78f, 84f), new Vector2(0f, -3f));

            Image tokenBackground = AddImage("TokenBackground", tokenRoot, new Color(0.32f, 0.55f, 0.84f, 1f));
            Stretch(tokenBackground.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -18f));

            Image portraitImage = AddImage("PortraitImage", tokenRoot, Color.white);
            Stretch(portraitImage.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 22f), new Vector2(-4f, -4f));
            portraitImage.preserveAspect = true;

            TextMeshProUGUI initialText = AddText(
                "InitialText",
                tokenRoot,
                "L",
                38f,
                Color.white,
                TextAlignmentOptions.Center);
            Stretch(initialText.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 20f), new Vector2(-2f, -2f));
            initialText.fontStyle = FontStyles.Bold;

            TextMeshProUGUI nameText = AddText(
                "NameText",
                tokenRoot,
                "Leo",
                14f,
                Color.white,
                TextAlignmentOptions.Center);
            Stretch(nameText.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 20f));
            nameText.fontStyle = FontStyles.Bold;

            Image buttonGraphic = AddImage("Button", root, Color.clear);
            Stretch(buttonGraphic.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Button button = buttonGraphic.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.86f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.53f, 0.72f, 0.84f, 1f);
            colors.disabledColor = new Color(0.35f, 0.38f, 0.42f, 1f);
            button.colors = colors;

            TestBoardCellUI cell = root.gameObject.AddComponent<TestBoardCellUI>();
            SetReference(cell, "button", button);
            SetReference(cell, "backgroundImage", background);
            SetReference(cell, "coordinateText", coordinate);
            SetReference(cell, "tokenRoot", tokenRoot.gameObject);
            SetReference(cell, "tokenBackground", tokenBackground);
            SetReference(cell, "portraitImage", portraitImage);
            SetReference(cell, "initialText", initialText);
            SetReference(cell, "characterNameText", nameText);
            tokenRoot.gameObject.SetActive(false);

            TestBoardCellUI saved = SavePrefab(root.gameObject, $"{PrefabRoot}/TestBoardCell.prefab")
                .GetComponent<TestBoardCellUI>();
            return saved;
        }

        private static GameObject CreateCharacterSystemPrefab(CharacterCardUI cardPrefab)
        {
            GameObject root = new GameObject("CharacterSystem");
            GameObject panelObject = new GameObject("CharacterPanelUI");
            panelObject.transform.SetParent(root.transform, false);
            CharacterPanelUI panel = panelObject.AddComponent<CharacterPanelUI>();
            SetReference(panel, "cardPrefab", cardPrefab);

            GameObject placementObject = new GameObject("CharacterPlacementController");
            placementObject.transform.SetParent(root.transform, false);
            CharacterPlacementController placement = placementObject.AddComponent<CharacterPlacementController>();
            SetReference(placement, "selectionSource", panel);

            GameObject saved = SavePrefab(root, $"{PrefabRoot}/CharacterSystem.prefab");
            return saved;
        }

        private static void CreateTestScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            CharacterPanelView panelPrefab = LoadPrefabComponent<CharacterPanelView>(
                $"{PrefabRoot}/CharacterPanel.prefab");
            CharacterCardUI cardPrefab = LoadPrefabComponent<CharacterCardUI>(
                $"{PrefabRoot}/CharacterCard.prefab");
            TestBoardCellUI cellPrefab = LoadPrefabComponent<TestBoardCellUI>(
                $"{PrefabRoot}/TestBoardCell.prefab");
            GameObject systemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabRoot}/CharacterSystem.prefab");
            if (systemPrefab == null)
            {
                throw new InvalidOperationException("Failed to load CharacterSystem.prefab.");
            }

            CharacterData[] characters =
            {
                LoadCharacterData("Leo"),
                LoadCharacterData("Mina"),
                LoadCharacterData("Owen")
            };

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.13f, 1f);

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.AddComponent<InputSystemUIInputModule>();

            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab.gameObject, canvasObject.transform);
            panelInstance.name = "CharacterPanel";
            RectTransform panelRect = panelInstance.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.sizeDelta = new Vector2(610f, -40f);
            panelRect.anchoredPosition = new Vector2(20f, 0f);

            Image boardPanel = AddImage("TestBoardPanel", canvasObject.GetComponent<RectTransform>(), new Color(0.11f, 0.14f, 0.20f, 0.96f));
            Stretch(boardPanel.rectTransform, Vector2.zero, Vector2.one, new Vector2(650f, 20f), new Vector2(-20f, -20f));

            TextMeshProUGUI boardTitle = AddText(
                "BoardTitle",
                boardPanel.rectTransform,
                "6×6 人物放置测试棋盘",
                34f,
                Color.white,
                TextAlignmentOptions.Center);
            Stretch(boardTitle.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -64f), new Vector2(-20f, -10f));
            boardTitle.fontStyle = FontStyles.Bold;

            TextMeshProUGUI statusText = AddText(
                "PlacementStatusText",
                boardPanel.rectTransform,
                "点击人物后选择格子，或直接拖动人物卡到右侧格子。",
                21f,
                new Color(0.75f, 0.84f, 0.95f, 1f),
                TextAlignmentOptions.Center);
            Stretch(statusText.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -105f), new Vector2(-20f, -65f));

            RectTransform testGrid = CreateRect("TestGrid", boardPanel.rectTransform);
            Place(testGrid, new Vector2(0.5f, 0.5f), new Vector2(712f, 712f), new Vector2(0f, -35f));
            GridLayoutGroup boardLayout = testGrid.gameObject.AddComponent<GridLayoutGroup>();
            boardLayout.cellSize = new Vector2(112f, 112f);
            boardLayout.spacing = new Vector2(8f, 8f);
            boardLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            boardLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            boardLayout.childAlignment = TextAnchor.MiddleCenter;
            boardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            boardLayout.constraintCount = 6;

            TestBoardController boardController = boardPanel.gameObject.AddComponent<TestBoardController>();
            SetInt(boardController, "rows", 6);
            SetInt(boardController, "columns", 6);
            SetReference(boardController, "gridRoot", testGrid);
            SetReference(boardController, "cellPrefab", cellPrefab);

            GameObject systemInstance = (GameObject)PrefabUtility.InstantiatePrefab(systemPrefab);
            systemInstance.name = "CharacterSystem";
            CharacterPanelUI panelUI = systemInstance.GetComponentInChildren<CharacterPanelUI>(true);
            CharacterPlacementController placement = systemInstance.GetComponentInChildren<CharacterPlacementController>(true);
            SetReference(panelUI, "view", panelInstance.GetComponent<CharacterPanelView>());
            SetReference(panelUI, "cardPrefab", cardPrefab);
            SetObjectArray(panelUI, "characters", characters);

            GameObject coordinatorObject = new GameObject("CharacterPanelTestCoordinator");
            CharacterPanelTestCoordinator coordinator = coordinatorObject.AddComponent<CharacterPanelTestCoordinator>();
            SetReference(coordinator, "testBoard", boardController);
            SetReference(coordinator, "placementController", placement);
            SetReference(coordinator, "placementStatusText", statusText);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"Failed to save {ScenePath}.");
            }
        }

        private static TMP_FontAsset Font
        {
            get
            {
                if (cachedFont == null)
                {
                    cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/fonts/NotoSansSC-Regular SDF.asset");
                    if (cachedFont == null)
                    {
                        cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                    }
                }

                return cachedFont;
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            if (parent != null)
            {
                rect.SetParent(parent, false);
            }

            return rect;
        }

        private static Image AddImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI AddText(
            string name,
            Transform parent,
            string content,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = Font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 size,
            Vector2 position,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject savedInstance = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (savedInstance == null)
            {
                throw new InvalidOperationException($"Failed to save prefab at {path}.");
            }

            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset == null)
            {
                throw new InvalidOperationException($"Failed to reload prefab asset at {path}.");
            }

            return prefabAsset;
        }

        private static T LoadPrefabComponent<T>(string path) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            T component = prefab == null ? null : prefab.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"Failed to load {typeof(T).Name} from {path}.");
            }

            return component;
        }

        private static CharacterData LoadCharacterData(string displayName)
        {
            string path = $"{DataRoot}/{displayName}.asset";
            CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (data == null)
            {
                throw new InvalidOperationException($"Failed to load CharacterData from {path}.");
            }

            return data;
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            List<T> components = new List<T>();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                components.AddRange(rootObject.GetComponentsInChildren<T>(true));
            }

            return components.ToArray();
        }

        private static T FindSingleSceneComponent<T>(Scene scene) where T : Component
        {
            T[] components = FindSceneComponents<T>(scene);
            Require(components.Length == 1, $"The test scene must contain exactly one {typeof(T).Name}.");
            return components[0];
        }

        private static CharacterCardUI FindCard(IReadOnlyList<CharacterCardUI> cards, string displayName)
        {
            foreach (CharacterCardUI card in cards)
            {
                if (card.Character != null && card.Character.DisplayName == displayName)
                {
                    return card;
                }
            }

            throw new InvalidOperationException($"Generated card for {displayName} was not found.");
        }

        private static void InvokeCardClick(CharacterCardUI card)
        {
            MethodInfo method = typeof(CharacterCardUI).GetMethod(
                "HandleButtonClicked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "CharacterCardUI click handler was not found.");
            method.Invoke(card, null);
        }

        private static void ValidateNoMissingScripts(GameObject gameObject)
        {
            Require(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) == 0,
                $"{gameObject.name} contains a missing script.");

            foreach (Transform child in gameObject.transform)
            {
                ValidateNoMissingScripts(child.gameObject);
            }
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) < 0.001f;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized property {target.GetType().Name}.{propertyName}.");
            }

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray<T>(
            UnityEngine.Object target,
            string propertyName,
            IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException($"Missing serialized array {target.GetType().Name}.{propertyName}.");
            }

            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
