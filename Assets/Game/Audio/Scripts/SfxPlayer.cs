using System.Collections.Generic;
using UnityEngine;

namespace Murdoku.Audio
{
    public enum SfxCue
    {
        UiHover,
        UiClick,
        TilePlace,
        SuspectSelect,
        CorrectMatch,
        WrongMove
    }

    /// <summary>
    /// Global two-dimensional sound effect player. It bootstraps before the first scene
    /// and persists across scene changes, so callers only need to specify a semantic cue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SfxPlayer : MonoBehaviour
    {
        private const float HoverCooldownSeconds = 0.05f;

        private static readonly ClipDefinition[] ClipDefinitions =
        {
            new ClipDefinition(SfxCue.UiHover, "Audio/SFX/01_ui_hover"),
            new ClipDefinition(SfxCue.UiClick, "Audio/SFX/02_ui_click"),
            new ClipDefinition(SfxCue.TilePlace, "Audio/SFX/03_tile_place"),
            new ClipDefinition(SfxCue.SuspectSelect, "Audio/SFX/08_suspect_select"),
            new ClipDefinition(SfxCue.CorrectMatch, "Audio/SFX/09_correct_match"),
            new ClipDefinition(SfxCue.WrongMove, "Audio/SFX/10_wrong_move")
        };

        private static SfxPlayer instance;

        private readonly Dictionary<SfxCue, AudioClip> clips = new Dictionary<SfxCue, AudioClip>();
        private AudioSource audioSource;
        private float nextHoverTime;
        private bool initialized;

        private readonly struct ClipDefinition
        {
            public ClipDefinition(SfxCue cue, string resourcePath)
            {
                Cue = cue;
                ResourcePath = resourcePath;
            }

            public SfxCue Cue { get; }
            public string ResourcePath { get; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static void Play(SfxCue cue)
        {
            SfxPlayer player = EnsureInstance();
            if (player != null)
            {
                player.PlayInternal(cue);
            }
        }

        private static SfxPlayer EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<SfxPlayer>();
            if (instance != null)
            {
                instance.Initialize();
                return instance;
            }

            GameObject playerObject = new GameObject("[SfxPlayer]");
            instance = playerObject.AddComponent<SfxPlayer>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.8f;
            audioSource.ignoreListenerPause = true;

            clips.Clear();
            foreach (ClipDefinition definition in ClipDefinitions)
            {
                AudioClip clip = Resources.Load<AudioClip>(definition.ResourcePath);
                if (clip == null)
                {
                    Debug.LogWarning($"SfxPlayer could not load cue {definition.Cue} at Resources/{definition.ResourcePath}.", this);
                    continue;
                }

                clips[definition.Cue] = clip;
            }

            nextHoverTime = float.NegativeInfinity;
            initialized = true;
        }

        private void PlayInternal(SfxCue cue)
        {
            Initialize();

            if (cue == SfxCue.UiHover)
            {
                float now = Time.unscaledTime;
                if (now < nextHoverTime)
                {
                    return;
                }

                nextHoverTime = now + HoverCooldownSeconds;
            }

            if (audioSource != null && clips.TryGetValue(cue, out AudioClip clip) && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
