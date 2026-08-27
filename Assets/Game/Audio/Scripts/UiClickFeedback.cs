using Murdoku.Characters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Murdoku.Audio
{
    /// <summary>
    /// Adds the shared click sound to ordinary UI buttons. Board cells are excluded because
    /// successful placement has its own sound and failed placement is intentionally silent.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UiClickFeedback : MonoBehaviour, IPointerClickHandler
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
            if (targetButton == null || targetButton.GetComponent<UiClickFeedback>() != null)
            {
                return;
            }

            if (targetButton.GetComponentInParent<PuzzleBoardCellUI>(true) != null)
            {
                return;
            }

            targetButton.gameObject.AddComponent<UiClickFeedback>();
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

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable())
            {
                GameAudio.Play(SfxCue.UiClick);
            }
        }
    }
}
