using System.Collections.Generic;
using UnityEngine;

namespace Murdoku.Audio
{
    public enum SfxCue
    {
        UiClick,
        CharacterPlace
    }

    /// <summary>
    /// Persistent two-dimensional audio service for the shared BGM and short sound effects.
    /// It bootstraps before any scene, so direct scene play and normal scene transitions behave identically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        private const string ClickResourcePath = "Audio/SFX/ui_click";
        private const string PlaceResourcePath = "Audio/SFX/character_place";
        private const string MusicResourcePath = "Audio/Music/murdoku_light_mystery";

        private static GameAudio instance;

        private readonly Dictionary<SfxCue, AudioClip> sfxClips = new Dictionary<SfxCue, AudioClip>();
        private AudioSource sfxSource;
        private AudioSource musicSource;
        private bool initialized;

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
            GameAudio audio = EnsureInstance();
            if (audio != null)
            {
                audio.PlayInternal(cue);
            }
        }

        private static GameAudio EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<GameAudio>();
            if (instance != null)
            {
                instance.Initialize();
                return instance;
            }

            GameObject audioObject = new GameObject("[GameAudio]");
            instance = audioObject.AddComponent<GameAudio>();
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

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            sfxSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(sfxSource, 0.8f);

            musicSource = gameObject.AddComponent<AudioSource>();
            ConfigureSource(musicSource, 0.3f);
            musicSource.loop = true;

            sfxClips[SfxCue.UiClick] = LoadClip(ClickResourcePath, "UI click");
            sfxClips[SfxCue.CharacterPlace] = LoadClip(PlaceResourcePath, "character place");

            AudioClip musicClip = LoadClip(MusicResourcePath, "background music");
            if (musicClip != null)
            {
                musicSource.clip = musicClip;
                musicSource.Play();
            }

            initialized = true;
        }

        private static void ConfigureSource(AudioSource source, float volume)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.ignoreListenerPause = true;
        }

        private AudioClip LoadClip(string resourcePath, string displayName)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"GameAudio could not load {displayName} at Resources/{resourcePath}.", this);
            }

            return clip;
        }

        private void PlayInternal(SfxCue cue)
        {
            Initialize();

            if (sfxSource != null && sfxClips.TryGetValue(cue, out AudioClip clip) && clip != null)
            {
                sfxSource.PlayOneShot(clip);
            }
        }
    }
}
