using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Murdoku.Characters
{
    /// <summary>
    /// 嫌疑人面板「禁止放置」黑叉卡的点击接收组件（IPointerClickHandler，
    /// 动态创建的 UI 上比 Button 组件更可靠）。
    /// </summary>
    public sealed class BlackXCardHandler : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke();
        }
    }
}
