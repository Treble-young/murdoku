using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Murdoku.Audio
{
    public enum SfxCue
    {
        UiClick,
        CharacterPlace,
        CaseSolved
    }

    public enum MusicCue
    {
        Main,
        Investigation
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
        private const string CaseSolvedResourcePath = "Audio/SFX/case_solved";
        private const string MainMusicResourcePath = "Audio/Music/murdoku_light_mystery";
        private const string InvestigationMusicResourcePath = "Audio/Music/investigation_strings_choir";
        private const string MainMenuSceneName = "MainMenuScene";
        private const string LevelSelectSceneName = "LevelSelectScene";
        private const string MasterVolumePreferenceKey = "audio.masterVolume";
        private const float MusicVolume = 0.3f;
        private const float MusicFadeDuration = 0.75f;

        private static GameAudio instance;

        private readonly Dictionary<SfxCue, AudioClip> sfxClips = new Dictionary<SfxCue, AudioClip>();
        private readonly Dictionary<MusicCue, AudioClip> musicClips = new Dictionary<MusicCue, AudioClip>();
        private AudioSource sfxSource;
        private AudioSource musicSource;
        private Coroutine musicTransitionCoroutine;
        private MusicCue? currentMusicCue;
        private bool initialized;
        private float masterVolume = 1f;

        public static float MasterVolume
        {
            get
            {
                GameAudio audio = EnsureInstance();
                return audio == null ? 1f : audio.masterVolume;
            }
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
            GameAudio audio = EnsureInstance();
            if (audio != null)
            {
                audio.PlayInternal(cue);
            }
        }

        public static void SetMusic(MusicCue cue)
        {
            GameAudio audio = EnsureInstance();
            if (audio != null)
            {
                audio.SetMusicInternal(cue);
            }
        }

        public static void SetMasterVolume(float volume)
        {
            GameAudio audio = EnsureInstance();
            if (audio == null)
            {
                return;
            }

            audio.masterVolume = Mathf.Clamp01(volume);
            AudioListener.volume = audio.masterVolume;
            PlayerPrefs.SetFloat(MasterVolumePreferenceKey, audio.masterVolume);
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
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Initialize();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
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
            ConfigureSource(musicSource, MusicVolume);
            musicSource.loop = true;

            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePreferenceKey, 1f));
            AudioListener.volume = masterVolume;

            sfxClips[SfxCue.UiClick] = LoadClip(ClickResourcePath, "UI click");
            sfxClips[SfxCue.CharacterPlace] = LoadClip(PlaceResourcePath, "character place");
            sfxClips[SfxCue.CaseSolved] = LoadClip(CaseSolvedResourcePath, "case solved");

            musicClips[MusicCue.Main] = LoadClip(MainMusicResourcePath, "main background music");
            musicClips[MusicCue.Investigation] = LoadClip(
                InvestigationMusicResourcePath,
                "investigation background music");

            initialized = true;
            SetMusicImmediate(MusicCue.Main);
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

        private void SetMusicInternal(MusicCue cue)
        {
            Initialize();

            if (musicSource == null || !musicClips.TryGetValue(cue, out AudioClip clip) || clip == null)
            {
                return;
            }

            if (currentMusicCue == cue &&
                musicSource.clip == clip &&
                musicSource.isPlaying &&
                musicTransitionCoroutine == null &&
                Mathf.Approximately(musicSource.volume, MusicVolume))
            {
                return;
            }

            if (musicTransitionCoroutine != null)
            {
                StopCoroutine(musicTransitionCoroutine);
            }

            musicTransitionCoroutine = StartCoroutine(TransitionMusicRoutine(cue, clip));
        }

        private void SetMusicImmediate(MusicCue cue)
        {
            if (musicSource == null || !musicClips.TryGetValue(cue, out AudioClip clip) || clip == null)
            {
                return;
            }

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.volume = MusicVolume;
            currentMusicCue = cue;
            musicSource.Play();
        }

        private IEnumerator TransitionMusicRoutine(MusicCue cue, AudioClip clip)
        {
            bool needsClipChange = currentMusicCue != cue || musicSource.clip != clip;
            if (needsClipChange && musicSource.isPlaying)
            {
                yield return FadeMusicVolume(musicSource.volume, 0f);
            }

            if (needsClipChange)
            {
                musicSource.Stop();
                musicSource.clip = clip;
                musicSource.volume = 0f;
                currentMusicCue = cue;
            }

            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }

            yield return FadeMusicVolume(musicSource.volume, MusicVolume);
            musicTransitionCoroutine = null;
        }

        private IEnumerator FadeMusicVolume(float startVolume, float targetVolume)
        {
            float elapsed = 0f;
            while (elapsed < MusicFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / MusicFadeDuration);
                musicSource.volume = Mathf.Lerp(startVolume, targetVolume, progress);
                yield return null;
            }

            musicSource.volume = targetVolume;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (scene.name == MainMenuSceneName || scene.name == LevelSelectSceneName)
            {
                SetMusicInternal(MusicCue.Main);
            }
        }
    }
}
