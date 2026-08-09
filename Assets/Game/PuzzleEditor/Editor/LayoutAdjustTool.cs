using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 出题场景（CharacterPanelTest）布局一键调整工具：
    /// 右侧棋盘面板收为 900×900 正方形、棋盘居中；控制条移到棋盘面板正上方；
    /// 保存面板移到棋盘面板正下方；返回按钮移到左下角；嫌疑人面板拓宽并底部留空。
    /// 直接操作内存中的场景并保存，在 Unity 内执行立即生效。
    /// </summary>
    public static class LayoutAdjustTool
    {
        private const string ScenePath = "Assets/Scenes/CharacterPanelTest.unity";

        [MenuItem("Tools/Murdoku/Apply Board Layout (CharacterPanelTest)")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            AdjustBoardPanel();
            AdjustTestGrid();
            AdjustControlBar();
            AdjustSavePanel();
            AdjustBackButton();
            AdjustCharacterPanel();

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("CharacterPanelTest 布局已应用并保存。");
            }
        }

        private static void AdjustBoardPanel()
        {
            RectTransform panel = FindRect("TestBoardPanel");
            if (panel == null)
            {
                return;
            }

            Undo.RecordObject(panel, "Adjust TestBoardPanel");
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(900f, 900f);
            panel.anchoredPosition = new Vector2(490f, 0f);
            EditorUtility.SetDirty(panel);
        }

        private static void AdjustTestGrid()
        {
            RectTransform grid = FindRect("TestGrid");
            if (grid == null)
            {
                return;
            }

            Undo.RecordObject(grid, "Adjust TestGrid");
            grid.anchorMin = new Vector2(0.5f, 0.5f);
            grid.anchorMax = new Vector2(0.5f, 0.5f);
            grid.pivot = new Vector2(0.5f, 0.5f);
            grid.sizeDelta = new Vector2(850f, 850f);
            grid.anchoredPosition = Vector2.zero;
            EditorUtility.SetDirty(grid);
        }

        /// <summary>
        /// 控制条移到 Canvas 顶部右侧（棋盘面板正上方），避免与棋盘重叠。
        /// </summary>
        private static void AdjustControlBar()
        {
            RectTransform bar = FindRect("BoardSizePanel");
            if (bar == null)
            {
                return;
            }

            RectTransform canvas = FindRect("Canvas");
            if (canvas != null && bar.parent != canvas)
            {
                Undo.SetTransformParent(bar, canvas, "Move BoardSizePanel to Canvas");
            }

            Undo.RecordObject(bar, "Adjust BoardSizePanel");
            bar.anchorMin = new Vector2(0.5f, 1f);
            bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.sizeDelta = new Vector2(900f, 64f);
            bar.anchoredPosition = new Vector2(490f, -32f);
            EditorUtility.SetDirty(bar);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bar);
        }

        /// <summary>
        /// 保存面板移到 Canvas 底部右侧（棋盘面板正下方），避开左侧嫌疑人面板。
        /// </summary>
        private static void AdjustSavePanel()
        {
            RectTransform save = FindRect("SavePanel");
            if (save == null)
            {
                return;
            }

            RectTransform canvas = FindRect("Canvas");
            if (canvas != null && save.parent != canvas)
            {
                Undo.SetTransformParent(save, canvas, "Move SavePanel to Canvas");
            }

            Undo.RecordObject(save, "Adjust SavePanel");
            save.anchorMin = Vector2.zero;
            save.anchorMax = Vector2.zero;
            save.pivot = new Vector2(0.5f, 0.5f);
            save.sizeDelta = new Vector2(900f, 45f);
            save.anchoredPosition = new Vector2(1450f, 32f);
            EditorUtility.SetDirty(save);
        }

        /// <summary>
        /// 返回按钮移到 Canvas 左下角（嫌疑人面板底部留出的空隙中）。
        /// </summary>
        private static void AdjustBackButton()
        {
            RectTransform back = FindRect("BackButton");
            if (back == null)
            {
                return;
            }

            Undo.RecordObject(back, "Adjust BackButton");
            back.anchorMin = Vector2.zero;
            back.anchorMax = Vector2.zero;
            back.pivot = new Vector2(0.5f, 0.5f);
            back.anchoredPosition = new Vector2(110f, 32f);
            EditorUtility.SetDirty(back);
        }

        /// <summary>
        /// 嫌疑人面板拓宽到 940，高度收窄（底部留 100px 给返回按钮）。
        /// </summary>
        private static void AdjustCharacterPanel()
        {
            RectTransform characterPanel = FindRect("CharacterPanel");
            if (characterPanel == null)
            {
                return;
            }

            Undo.RecordObject(characterPanel, "Adjust CharacterPanel");
            characterPanel.sizeDelta = new Vector2(940f, -100f);
            EditorUtility.SetDirty(characterPanel);
            // 嫌疑人面板是 prefab 实例，需要把尺寸修改记录为实例覆盖，才会保存进场景。
            PrefabUtility.RecordPrefabInstancePropertyModifications(characterPanel);
        }

        private static RectTransform FindRect(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null)
            {
                Debug.LogWarning($"LayoutAdjustTool: 未找到 {objectName}");
                return null;
            }

            return target.GetComponent<RectTransform>();
        }
    }
}
