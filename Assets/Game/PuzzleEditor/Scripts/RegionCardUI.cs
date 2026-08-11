using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 地块卡片：图案色块 + 名字，点击选中（通过事件回调通知面板）。
    /// 结构与嫌疑人卡片类似：选中时显示蓝色边框。
    /// 点击使用 IPointerClickHandler（最底层事件接口，不依赖 Button 组件）。
    /// 注意：卡片是纯代码创建的，序列化字段不会自动赋值——
    /// 所有子物体引用通过 EnsureReferences 按名字查找（transform.Find）。
    /// </summary>
    public sealed class RegionCardUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameObject selectionBorder;
        [SerializeField] private Image colorBlock;
        [SerializeField] private TMP_Text nameText;

        public event Action<RegionCardUI> Clicked;

        public RegionDefinition Region { get; private set; }

        public void Configure(RegionDefinition definition, TMP_FontAsset font)
        {
            EnsureReferences();

            Region = definition;
            if (definition == null)
            {
                return;
            }

            if (colorBlock != null)
            {
                colorBlock.sprite = definition.Sprite;
                colorBlock.color = Color.white;
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

            if (colorBlock == null)
            {
                Transform block = transform.Find("ColorBlock");
                if (block != null)
                {
                    colorBlock = block.GetComponent<Image>();
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
