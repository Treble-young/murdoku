using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Murdoku.Characters
{
    public sealed class CharacterCardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Interaction")]
        [SerializeField] private Button button;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private GameObject selectionBorder;

        [Header("Character Content")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image portraitPlaceholder;
        [SerializeField] private TMP_Text initialText;
        [SerializeField] private TMP_Text genderText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text clueText;

        [Header("Selection Animation")]
        [Min(1f)]
        [SerializeField] private float selectedScale = 1.1f;
        [Min(0f)]
        [SerializeField] private float animationDuration = 0.15f;

        private CharacterData character;
        private Action<CharacterCardUI> clicked;
        private Action<CharacterCardUI> dragStarted;
        private Coroutine scaleRoutine;

        public CharacterData Character => character;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleButtonClicked);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleButtonClicked);
            }
        }

        public void Bind(
            CharacterData data,
            Action<CharacterCardUI> onClicked,
            Action<CharacterCardUI> onDragStarted = null)
        {
            character = data;
            clicked = onClicked;
            dragStarted = onDragStarted;

            if (data == null)
            {
                return;
            }

            bool hasPortrait = data.Portrait != null;
            if (portraitImage != null)
            {
                portraitImage.sprite = data.Portrait;
                portraitImage.enabled = hasPortrait;
            }

            if (portraitPlaceholder != null)
            {
                portraitPlaceholder.color = data.PlaceholderColor;
                portraitPlaceholder.gameObject.SetActive(!hasPortrait);
            }

            if (initialText != null)
            {
                initialText.text = data.Initial;
                initialText.gameObject.SetActive(!hasPortrait);
            }

            if (genderText != null)
            {
                genderText.text = data.GenderSymbol;
            }

            if (nameText != null)
            {
                nameText.text = data.DisplayName;
            }

            if (clueText != null)
            {
                clueText.text = data.Clue;
            }

            SetSelected(false, false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (character == null)
            {
                return;
            }

            dragStarted?.Invoke(this);
            CharacterDragPreview.Show(character, this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            CharacterDragPreview.Move(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            CharacterDragPreview.Hide();
        }

        public void SetSelected(bool selected, bool animate = true)
        {
            if (selectionBorder != null)
            {
                selectionBorder.SetActive(selected);
            }

            if (visualRoot == null)
            {
                return;
            }

            Vector3 targetScale = Vector3.one * (selected ? selectedScale : 1f);
            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
                scaleRoutine = null;
            }

            if (!animate || !Application.isPlaying || animationDuration <= 0f || !isActiveAndEnabled)
            {
                visualRoot.localScale = targetScale;
                return;
            }

            scaleRoutine = StartCoroutine(AnimateScale(targetScale));
        }

        private IEnumerator AnimateScale(Vector3 targetScale)
        {
            Vector3 startScale = visualRoot.localScale;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                t = t * t * (3f - 2f * t);
                visualRoot.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            visualRoot.localScale = targetScale;
            scaleRoutine = null;
        }

        private void HandleButtonClicked()
        {
            if (character != null)
            {
                clicked?.Invoke(this);
            }
        }
    }
}
