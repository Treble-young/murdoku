using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 左侧面板 Tab 框架构建工具：
    /// 创建「嫌疑人 / 地块」两个页签容器，把现有嫌疑人面板（CharacterPanel）移入嫌疑人页，
    /// 新建空白地块面板占位，并绑定 LeftPanelTabsUI 实现切换。
    /// 直接操作内存中的场景并保存，在 Unity 内执行立即生效。
    /// </summary>
    public static class LeftPanelTabsBuilder
    {
        private const string ScenePath = "Assets/Scenes/PuzzleScene.unity";
        private const string FontPath = "Assets/fonts/敏锐念念不忘体 SDF.asset";
        private const string CharacterPanelPrefabPath = "Assets/Game/Characters/Prefabs/CharacterPanel.prefab";

        private const float ContainerWidth = 940f;
        private const float ContainerHeight = 920f;
        private const float TabBarHeight = 64f;
        private const float BackButtonWidth = 150f;
        private const float TabButtonWidth = (ContainerWidth - BackButtonWidth) / 3f; // ≈263.33（三 Tab 等宽）

        [MenuItem("Tools/Murdoku/Build Left Panel Tabs")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("Build Left Panel Tabs: 未找到 Canvas。");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError($"Build Left Panel Tabs: 未找到字体 {FontPath}");
                return;
            }

            // 0. 幂等：移除旧容器（如重复运行菜单）。
            GameObject oldContainer = GameObject.Find("LeftPanelContainer");
            if (oldContainer != null)
            {
                UnityEngine.Object.DestroyImmediate(oldContainer);
            }

            // 1. 左侧容器（左上锚点，940×920）。
            RectTransform container = CreateRect("LeftPanelContainer", canvas.transform);
            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(0f, 1f);
            container.pivot = new Vector2(0.5f, 1f);
            container.sizeDelta = new Vector2(ContainerWidth, ContainerHeight);
            container.anchoredPosition = new Vector2(ContainerWidth / 2f, -20f);

            // 2. Tab 栏（顶部 64px，两个按钮各占一半）。
            RectTransform tabBar = CreateRect("TabBar", container);
            tabBar.anchorMin = new Vector2(0f, 1f);
            tabBar.anchorMax = new Vector2(1f, 1f);
            tabBar.pivot = new Vector2(0.5f, 1f);
            tabBar.sizeDelta = new Vector2(0f, TabBarHeight);
            tabBar.anchoredPosition = Vector2.zero;

            Image tabBarBg = tabBar.gameObject.AddComponent<Image>();
            tabBarBg.color = new Color(0.13f, 0.15f, 0.20f, 0.98f);
            tabBarBg.raycastTarget = false;

            Button suspectsTab = CreateTabButton(tabBar, "SuspectsTab", "嫌疑人", font, TabButtonWidth, BackButtonWidth + TabButtonWidth / 2f);
            Button regionsTab = CreateTabButton(tabBar, "RegionsTab", "地块", font, TabButtonWidth, BackButtonWidth + TabButtonWidth + TabButtonWidth / 2f);
            Button propsTab = CreateTabButton(tabBar, "PropsTab", "道具", font, TabButtonWidth, BackButtonWidth + TabButtonWidth * 2f + TabButtonWidth / 2f);

            // 2.5 返回按钮：移入 TabBar 最左（较窄），保留原有返回功能组件。
            GameObject backButton = GameObject.Find("BackButton");
            if (backButton != null)
            {
                Undo.SetTransformParent(backButton.transform, tabBar, "Move BackButton into TabBar");
                RectTransform backRect = backButton.GetComponent<RectTransform>();
                if (backRect == null)
                {
                    backRect = backButton.AddComponent<RectTransform>();
                }

                Undo.RecordObject(backRect, "Layout BackButton in TabBar");
                LayoutTabButton(backRect, BackButtonWidth, BackButtonWidth / 2f);
            }
            else
            {
                Debug.LogWarning("Build Left Panel Tabs: 未找到 BackButton，跳过返回按钮。");
            }

            // 3. 嫌疑人面板：现有 CharacterPanel 移入容器，占 Tab 栏下方全部区域。
            GameObject suspectsPanel = FindOrCreateCharacterPanel();
            if (suspectsPanel == null)
            {
                Debug.LogError("Build Left Panel Tabs: 无法获取嫌疑人面板。");
                return;
            }

            Undo.SetTransformParent(suspectsPanel.transform, container, "Move CharacterPanel into LeftPanelContainer");
            RectTransform suspectsRect = suspectsPanel.GetComponent<RectTransform>();
            if (suspectsRect == null)
            {
                suspectsRect = suspectsPanel.AddComponent<RectTransform>();
            }

            Undo.RecordObject(suspectsRect, "Layout CharacterPanel in tabs");
            FillBelowTabBar(suspectsRect, TabBarHeight);
            PrefabUtility.RecordPrefabInstancePropertyModifications(suspectsRect);

            // 4. 地块面板：空白占位（背景 + 提示文字）。
            RectTransform regionsPanel = CreateRegionPanel(container, font);

            // 4.5 道具面板：空白占位（背景 + 提示文字）。
            RectTransform propsPanel = CreatePropsPanel(container, font);

            // 5. 绑定切换脚本。
            LeftPanelTabsUI tabs = container.GetComponent<LeftPanelTabsUI>();
            if (tabs == null)
            {
                tabs = container.gameObject.AddComponent<LeftPanelTabsUI>();
            }

            SetReference(tabs, "suspectsTabButton", suspectsTab);
            SetReference(tabs, "regionsTabButton", regionsTab);
            SetReference(tabs, "propsTabButton", propsTab);
            SetReference(tabs, "suspectsPanel", suspectsPanel);
            SetReference(tabs, "regionsPanel", regionsPanel.gameObject);
            SetReference(tabs, "propsPanel", propsPanel.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("左侧面板 Tab 框架已构建并保存。");
            }
            else
            {
                Debug.LogError("Build Left Panel Tabs: 场景保存失败。");
            }
        }

        /// <summary>
        /// 增量菜单：把现有返回按钮移入 Tab 栏（最左、较窄），并将嫌疑人/地块按钮重排为等宽。
        /// 适用于已构建过 Tab 栏、但返回按钮还在场景其他位置的情况。
        /// </summary>
        [MenuItem("Tools/Murdoku/Move Back Button To Tab Bar")]
        public static void MoveBackButtonToTabBar()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RectTransform tabBar = FindRect("TabBar");
            if (tabBar == null)
            {
                // 没有 Tab 栏：直接走全量构建（Build 已包含返回按钮处理）。
                Debug.LogWarning("未找到 TabBar，先执行完整构建。");
                Build();
                return;
            }

            GameObject backButton = GameObject.Find("BackButton");
            if (backButton == null)
            {
                Debug.LogError("Move Back Button To Tab Bar: 未找到 BackButton。");
                return;
            }

            Undo.SetTransformParent(backButton.transform, tabBar, "Move BackButton into TabBar");
            RectTransform backRect = backButton.GetComponent<RectTransform>();
            if (backRect == null)
            {
                backRect = backButton.AddComponent<RectTransform>();
            }

            Undo.RecordObject(backRect, "Layout BackButton in TabBar");
            LayoutTabButton(backRect, BackButtonWidth, BackButtonWidth / 2f);

            LayoutExistingTab(tabBar, "SuspectsTab", TabButtonWidth, BackButtonWidth + TabButtonWidth / 2f);
            LayoutExistingTab(tabBar, "RegionsTab", TabButtonWidth, BackButtonWidth + TabButtonWidth + TabButtonWidth / 2f);

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("返回按钮已移入 Tab 栏并保存。");
            }
            else
            {
                Debug.LogError("Move Back Button To Tab Bar: 场景保存失败。");
            }
        }

        /// <summary>
        /// 在 Tab 栏下按名字重排已有按钮的尺寸与位置。
        /// </summary>
        private static void LayoutExistingTab(RectTransform tabBar, string buttonName, float width, float x)
        {
            RectTransform button = FindChildByName<RectTransform>(tabBar, buttonName);
            if (button == null)
            {
                Debug.LogWarning($"Move Back Button To Tab Bar: 未找到 {buttonName}，跳过。");
                return;
            }

            Undo.RecordObject(button, $"Layout {buttonName}");
            LayoutTabButton(button, width, x);
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

        private static RectTransform FindRect(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            return target == null ? null : target.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 查找场景中的嫌疑人面板；找不到时从 CharacterPanel.prefab 实例化。
        /// </summary>
        private static GameObject FindOrCreateCharacterPanel()
        {
            GameObject panel = GameObject.Find("CharacterPanel");
            if (panel != null)
            {
                return panel;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPanelPrefabPath);
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance != null)
            {
                instance.name = "CharacterPanel";
            }

            return instance;
        }

        private static Button CreateTabButton(
            RectTransform parent,
            string objectName,
            string labelText,
            TMP_FontAsset font,
            float width,
            float x)
        {
            RectTransform rect = CreateRect(objectName, parent);
            LayoutTabButton(rect, width, x);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.48f, 0.86f, 1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            TMP_Text text = AddText("Label", rect, labelText, 26f, Color.white, font, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            return button;
        }

        /// <summary>
        /// 在 Tab 栏中布局一个按钮：顶部锚点，指定宽度与水平中心 x（y = -2）。
        /// </summary>
        private static void LayoutTabButton(RectTransform rect, float width, float x)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, TabBarHeight - 4f);
            rect.anchoredPosition = new Vector2(x, -2f);
        }

        /// <summary>
        /// 增量菜单：在已构建的 Tab 栏上添加「道具」页签（第三个 Tab + 空白道具面板）。
        /// 会把返回/嫌疑人/地块按钮重排为三 Tab 等宽；幂等，可重复运行。
        /// 适用于场景已构建过左侧 Tab（有未提交修改时推荐用增量而非全量构建）。
        /// </summary>
        [MenuItem("Tools/Murdoku/Add Props Tab To Left Panel")]
        public static void AddPropsTabToLeftPanel()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RectTransform tabBar = FindRect("TabBar");
            if (tabBar == null)
            {
                Debug.LogWarning("未找到 TabBar，先执行完整构建 Build Left Panel Tabs。");
                Build();
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError($"Add Props Tab: 未找到字体 {FontPath}");
                return;
            }

            // 幂等：移除旧的道具 Tab / 道具面板（重复运行菜单时重建）。
            DestroyChildByName(tabBar, "PropsTab");
            DestroyChildByName(tabBar.transform.parent as RectTransform, "PropsPanel");

            // 重排：返回 + 三 Tab 等宽。
            GameObject backButton = GameObject.Find("BackButton");
            if (backButton != null)
            {
                RectTransform backRect = backButton.GetComponent<RectTransform>();
                Undo.RecordObject(backRect, "Layout BackButton");
                LayoutTabButton(backRect, BackButtonWidth, BackButtonWidth / 2f);
            }

            LayoutExistingTab(tabBar, "SuspectsTab", TabButtonWidth, BackButtonWidth + TabButtonWidth / 2f);
            LayoutExistingTab(tabBar, "RegionsTab", TabButtonWidth, BackButtonWidth + TabButtonWidth + TabButtonWidth / 2f);

            Button propsTab = CreateTabButton(
                tabBar,
                "PropsTab",
                "道具",
                font,
                TabButtonWidth,
                BackButtonWidth + TabButtonWidth * 2f + TabButtonWidth / 2f);

            RectTransform container = tabBar.transform.parent as RectTransform;
            if (container == null)
            {
                Debug.LogError("Add Props Tab: TabBar 的父级不是 RectTransform。");
                return;
            }

            RectTransform propsPanel = CreatePropsPanel(container, font);

            LeftPanelTabsUI tabs = container.GetComponent<LeftPanelTabsUI>();
            if (tabs == null)
            {
                tabs = container.gameObject.AddComponent<LeftPanelTabsUI>();
            }

            SetReference(tabs, "propsTabButton", propsTab);
            SetReference(tabs, "propsPanel", propsPanel.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("道具 Tab 已添加并保存。");
            }
            else
            {
                Debug.LogError("Add Props Tab: 场景保存失败。");
            }
        }

        private static void DestroyChildByName(RectTransform root, string name)
        {
            if (root == null)
            {
                return;
            }

            Transform child = root.Find(name);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>
        /// 创建空白道具面板占位（背景 + 标题 + 提示）。
        /// </summary>
        private static RectTransform CreatePropsPanel(RectTransform parent, TMP_FontAsset font)
        {
            RectTransform rect = CreateRect("PropsPanel", parent);
            FillBelowTabBar(rect, TabBarHeight);

            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.11f, 0.14f, 0.20f, 0.96f);
            background.raycastTarget = false;

            TMP_Text title = AddText(
                "TitleText",
                rect,
                "道具编辑",
                36f,
                Color.white,
                font,
                TextAlignmentOptions.Center);
            Stretch(title.rectTransform);
            title.rectTransform.anchoredPosition = new Vector2(0f, 80f);
            title.fontStyle = FontStyles.Bold;

            TMP_Text hint = AddText(
                "HintText",
                rect,
                "（功能开发中…）",
                24f,
                new Color(0.62f, 0.70f, 0.82f, 1f),
                font,
                TextAlignmentOptions.Center);
            Stretch(hint.rectTransform);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 20f);

            return rect;
        }

        /// <summary>
        /// 创建空白地块面板占位（背景 + 标题 + 提示）。
        /// </summary>
        private static RectTransform CreateRegionPanel(RectTransform parent, TMP_FontAsset font)
        {
            RectTransform rect = CreateRect("RegionPanel", parent);
            FillBelowTabBar(rect, TabBarHeight);

            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.11f, 0.14f, 0.20f, 0.96f);
            background.raycastTarget = false;

            TMP_Text title = AddText(
                "TitleText",
                rect,
                "地块编辑",
                36f,
                Color.white,
                font,
                TextAlignmentOptions.Center);
            Stretch(title.rectTransform);
            title.rectTransform.anchoredPosition = new Vector2(0f, 80f);
            title.fontStyle = FontStyles.Bold;

            TMP_Text hint = AddText(
                "HintText",
                rect,
                "（功能开发中…）",
                24f,
                new Color(0.62f, 0.70f, 0.82f, 1f),
                font,
                TextAlignmentOptions.Center);
            Stretch(hint.rectTransform);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 20f);

            return rect;
        }

        /// <summary>
        /// 让矩形占满父容器、顶部留出 tabHeight 高度的区域（Tab 栏位置）。
        /// </summary>
        private static void FillBelowTabBar(RectTransform rect, float tabHeight)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, -tabHeight);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TMP_Text AddText(
            string name,
            RectTransform parent,
            string content,
            float fontSize,
            Color color,
            TMP_FontAsset font,
            TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            TMP_Text text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
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

        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"LeftPanelTabsBuilder: 缺少属性 {target.GetType().Name}.{propertyName}。");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
