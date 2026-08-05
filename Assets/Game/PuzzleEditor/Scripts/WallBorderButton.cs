using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Murdoku.PuzzleEditor
{
    /// <summary>
    /// 墙壁边界线的悬停检测组件。
    /// 悬停高亮仅在墙壁编辑模式下生效（由 WallEditController 判断模式后处理）。
    /// </summary>
    public sealed class WallBorderButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public int BorderIndex { get; set; }

        public Action<int, bool> HoverChanged;

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverChanged?.Invoke(BorderIndex, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverChanged?.Invoke(BorderIndex, false);
        }
    }
}
