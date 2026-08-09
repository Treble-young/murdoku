using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Murdoku.Characters
{
    /// <summary>
    /// 性别切换按钮的点击接收组件。
    /// 使用最底层的 IPointerClickHandler 事件接口（不依赖 Button/Selectable 组件），
    /// 避免 Selectable 状态或 targetGraphic 问题导致点击不生效。
    /// </summary>
    public sealed class GenderToggleZone : MonoBehaviour, IPointerClickHandler
    {
        public Action OnClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClicked?.Invoke();
        }
    }
}
