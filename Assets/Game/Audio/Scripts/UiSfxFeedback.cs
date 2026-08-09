using Murdoku.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Murdoku.Audio
{
    /// <summary>
    /// Generic hover/click feedback for ordinary UI buttons. Semantic controls such as
    /// suspect cards and board cells play their own cues and are deliberately excluded.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UiSfxFeedback : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private static bool sceneHookInstalled;

        private Button button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            sceneHookInstalled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForFirstScene()
        {
            if (!sceneHookInstalled)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                sceneHookInstalled = true;
            }

            InstallOnLoadedButtons();
        }

        public static void Ensure(Button targetButton)
        {
            if (targetButton == null || targetButton.GetComponent<UiSfxFeedback>() != null)
            {
                return;
            }

            if (targetButton.GetComponent<global::TextButtonHover>() != null ||
                targetButton.GetComponentInParent<CharacterCardUI>(true) != null ||
                targetButton.GetComponentInParent<TestBoardCellUI>(true) != null)
            {
                return;
            }

            targetButton.gameObject.AddComponent<UiSfxFeedback>();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallOnLoadedButtons();
        }

        private static void InstallOnLoadedButtons()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Button targetButton in buttons)
            {
                if (targetButton != null && targetButton.gameObject.scene.IsValid())
                {
                    Ensure(targetButton);
                }
            }
        }

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable())
            {
                SfxPlayer.Play(SfxCue.UiHover);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable())
            {
                SfxPlayer.Play(SfxCue.UiClick);
            }
        }
    }
}
