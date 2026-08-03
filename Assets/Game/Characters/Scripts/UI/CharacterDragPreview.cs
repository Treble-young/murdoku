using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    internal static class CharacterDragPreview
    {
        private static RectTransform previewRoot;
        private static RectTransform canvasRoot;
        private static Camera eventCamera;

        public static void Show(
            CharacterData character,
            Component source,
            PointerEventData eventData)
        {
            Hide();
            if (character == null || source == null)
            {
                return;
            }

            Canvas canvas = source.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvas = canvas.rootCanvas;
            canvasRoot = canvas.transform as RectTransform;
            eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            GameObject previewObject = new GameObject(
                "CharacterDragPreview",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));
            previewRoot = previewObject.GetComponent<RectTransform>();
            previewRoot.SetParent(canvas.transform, false);
            previewRoot.SetAsLastSibling();
            previewRoot.sizeDelta = new Vector2(96f, 96f);

            CanvasGroup canvasGroup = previewObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0.9f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Image background = previewObject.GetComponent<Image>();
            background.color = character.PlaceholderColor;
            background.sprite = character.Portrait;
            background.preserveAspect = true;
            background.raycastTarget = false;

            if (character.Portrait == null)
            {
                GameObject labelObject = new GameObject("InitialText", typeof(RectTransform), typeof(TextMeshProUGUI));
                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.SetParent(previewRoot, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
                label.text = character.Initial;
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 40f;
                label.fontStyle = FontStyles.Bold;
                label.color = Color.white;
                label.raycastTarget = false;
            }

            Move(eventData);
        }

        public static void Move(PointerEventData eventData)
        {
            if (previewRoot == null || canvasRoot == null || eventData == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRoot,
                    eventData.position,
                    eventCamera,
                    out Vector2 localPoint))
            {
                previewRoot.anchoredPosition = localPoint;
            }
        }

        public static void Hide()
        {
            if (previewRoot != null)
            {
                Object.Destroy(previewRoot.gameObject);
            }

            previewRoot = null;
            canvasRoot = null;
            eventCamera = null;
        }
    }
}
