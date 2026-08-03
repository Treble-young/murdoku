using UnityEngine;

namespace Murdoku.Characters
{
    public sealed class CharacterPanelView : MonoBehaviour
    {
        [SerializeField] private RectTransform characterGrid;

        public RectTransform CharacterGrid => characterGrid;
    }
}
