using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 道具卡片：圆形图标 + 编号 + 名字，点击选中（通过事件回调通知面板）。
    /// 交互方式与地块卡片一致：点击切换选中，选中时显示蓝色边框。
    /// 点击使用 IPointerClickHandler（最底层事件接口，不依赖 Button 组件）。
    /// 注意：卡片是纯代码创建的，序列化字段不会自动赋值——
    /// 所有子物体引用通过 EnsureReferences 按名字查找（transform.Find）。
    /// </summary>
    public sealed class PropCardUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameObject selectionBorder;
        [SerializeField] private Image circleIcon;
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text nameText;

        public event Action<PropCardUI> Clicked;

        public PropDefinition Prop { get; private set; }

        public void Configure(PropDefinition definition, TMP_FontAsset font)
        {
            EnsureReferences();

            Prop = definition;
            if (definition == null)
            {
                return;
            }

            if (circleIcon != null)
            {
                circleIcon.sprite = definition.Sprite;
                circleIcon.color = Color.white;
            }

            if (numberText != null)
            {
                numberText.text = definition.Number.ToString();
                if (font != null)
                {
                    numberText.font = font;
                }
            }

            if (nameText != null)
            {
                nameText.text = definition.DisplayName;
                if (font != null)
                {
                    nameText.font = font;
                }
            }
        }

        public void SetSelected(bool selected)
        {
            EnsureReferences();

            if (selectionBorder != null)
            {
                selectionBorder.SetActive(selected);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        /// <summary>
        /// 纯代码创建的卡片序列化字段为 null，必须按名字从子物体查找。
        /// </summary>
        private void EnsureReferences()
        {
            if (selectionBorder == null)
            {
                Transform border = transform.Find("SelectionBorder");
                if (border != null)
                {
                    selectionBorder = border.gameObject;
                }
            }

            if (circleIcon == null)
            {
                Transform icon = transform.Find("CircleIcon");
                if (icon != null)
                {
                    circleIcon = icon.GetComponent<Image>();
                }
            }

            if (numberText == null)
            {
                Transform number = transform.Find("NumberText");
                if (number != null)
                {
                    numberText = number.GetComponent<TMP_Text>();
                }
            }

            if (nameText == null)
            {
                Transform label = transform.Find("NameText");
                if (label != null)
                {
                    nameText = label.GetComponent<TMP_Text>();
                }
            }
        }
    }
}
