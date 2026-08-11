using UnityEngine;
using UnityEngine.UI;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 左侧面板 Tab 切换：嫌疑人面板 ⇄ 地块面板。
    /// 点击 Tab 按钮切换显示对应面板，并高亮当前 Tab（选中蓝 / 未选灰）。
    /// </summary>
    public sealed class LeftPanelTabsUI : MonoBehaviour
    {
        [Header("Tab 按钮")]
        [SerializeField] private Button suspectsTabButton;
        [SerializeField] private Button regionsTabButton;

        [Header("面板")]
        [SerializeField] private GameObject suspectsPanel;
        [SerializeField] private GameObject regionsPanel;

        private static readonly Color ActiveColor = new Color(0.22f, 0.48f, 0.86f, 1f);
        private static readonly Color InactiveColor = new Color(0.30f, 0.33f, 0.40f, 1f);

        private void Awake()
        {
            if (suspectsTabButton != null)
            {
                suspectsTabButton.onClick.AddListener(() => SelectTab(0));
            }

            if (regionsTabButton != null)
            {
                regionsTabButton.onClick.AddListener(() => SelectTab(1));
            }
        }

        private void OnDestroy()
        {
            if (suspectsTabButton != null)
            {
                suspectsTabButton.onClick.RemoveAllListeners();
            }

            if (regionsTabButton != null)
            {
                regionsTabButton.onClick.RemoveAllListeners();
            }
        }

        private void Start()
        {
            EnsurePanelReferences();
            SelectTab(0);
        }

        /// <summary>
        /// 切换到指定页签：0 = 嫌疑人，1 = 地块。
        /// </summary>
        public void SelectTab(int index)
        {
            EnsurePanelReferences();

            bool showSuspects = index == 0;

            if (suspectsPanel != null)
            {
                suspectsPanel.SetActive(showSuspects);
            }

            if (regionsPanel != null)
            {
                regionsPanel.SetActive(!showSuspects);
            }

            SetButtonColor(suspectsTabButton, showSuspects);
            SetButtonColor(regionsTabButton, !showSuspects);
        }

        /// <summary>
        /// 运行时补齐面板引用（场景序列化的引用可能丢失/为空），
        /// 在自身（LeftPanelContainer）下按名字递归查找，不依赖激活状态。
        /// </summary>
        private void EnsurePanelReferences()
        {
            if (suspectsPanel == null)
            {
                suspectsPanel = FindChildObject("CharacterPanel");
            }

            if (regionsPanel == null)
            {
                regionsPanel = FindChildObject("RegionPanel");
            }
        }

        private GameObject FindChildObject(string name)
        {
            foreach (Transform child in transform)
            {
                if (child.name == name)
                {
                    return child.gameObject;
                }

                GameObject nested = FindChildRecursive(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static GameObject FindChildRecursive(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name)
                {
                    return child.gameObject;
                }

                GameObject nested = FindChildRecursive(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void SetButtonColor(Button button, bool active)
        {
            if (button == null || button.targetGraphic == null)
            {
                return;
            }

            button.targetGraphic.color = active ? ActiveColor : InactiveColor;
        }
    }
}
