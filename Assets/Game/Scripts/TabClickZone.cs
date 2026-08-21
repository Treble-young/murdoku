using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Murdoku
{
    /// <summary>
    /// 谜题列表难度 Tab 的点击接收组件（IPointerClickHandler，动态创建的 UI 上比 Button 可靠）。
    /// </summary>
    public sealed class TabClickZone : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke();
        }
    }
}
