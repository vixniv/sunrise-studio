using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps FMOD scene BGM aligned with YarnCommands.TimeOfDay:
/// - Morning / Lunch / After School -> event:/Music/Daylight
/// - Evening / Night                -> event:/Music/Evening
/// - Midnight (Gap)                 -> event:/Music/Midnight
/// Automatically works alongside existing scene FMOD_BGM StudioEventEmitters
/// without requiring manual per-scene edits.
/// </summary>
public sealed class FmodTimeOfDayMusic : MonoBehaviour
{
    const string DaylightPath = "event:/Music/Daylight";
    const string EveningPath = "event:/Music/Evening";
    const string MidnightPath = "event:/Music/Midnight";
    const string MusicVolumeKey = "Sunrise.Settings.MusicVolume";

    static FmodTimeOfDayMusic instance;

    EventInstance managedMusicInstance;
    string managedMusicPath;
    float musicVolumeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        EnsureInstance();
    }

    static FmodTimeOfDayMusic EnsureInstance()
    {
        if (instance != null)
            return instance;

        var host = new GameObject("FMOD Time Of Day Music");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<FmodTimeOfDayMusic>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        musicVolumeScale = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));

        SceneManager.sceneLoaded += OnSceneLoaded;
        YarnCommands.TimeOfDayChanged += RefreshForCurrentScene;
        RefreshForCurrentScene();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        YarnCommands.TimeOfDayChanged -= RefreshForCurrentScene;
        StopManagedMusic();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Refresh(scene);
    }

    /// <summary>
    /// Smoothly fades out both managed FMOD BGM and any active scene FMOD_BGM emitter
    /// (for example when AfterSchool_To_Evening_Transition takes over bus:/Music).
    /// </summary>
    public static void FadeOutActiveMusic()
    {
        FmodTimeOfDayMusic active = EnsureInstance();
        active.StopManagedMusic();

        StudioEventEmitter[] emitters = FindObjectsByType<StudioEventEmitter>(FindObjectsInactive.Exclude);
        for (int i = 0; i < emitters.Length; i++)
        {
            StudioEventEmitter emitter = emitters[i];
            if (emitter != null && IsMusicEmitter(emitter))
            {
                emitter.AllowFadeout = true;
                emitter.Stop();
            }
        }
    }

    public static void SetMusicVolume(float volume)
    {
        FmodTimeOfDayMusic active = EnsureInstance();
        active.musicVolumeScale = Mathf.Clamp01(volume);
        active.ApplyVolumeToActiveMusic();
    }

    public static void RefreshForCurrentScene()
    {
        EnsureInstance().Refresh(SceneManager.GetActiveScene());
    }

    public static void ResumeAfterInterruption()
    {
        if (RuntimeManager.IsInitialized && RuntimeManager.StudioSystem.isValid())
        {
            RuntimeManager.CoreSystem.mixerResume();
            RuntimeManager.PauseAllEvents(false);
        }

        if (instance != null)
            instance.EnsureActiveMusicAfterInterruption();
    }

#if (UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS) && !UNITY_EDITOR
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ResumeAfterInterruption();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            ResumeAfterInterruption();
    }
#endif

    void EnsureActiveMusicAfterInterruption()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (scene.name == "TitleScreen")
        {
            StudioEventEmitter[] emitters = FindObjectsByType<StudioEventEmitter>(FindObjectsInactive.Exclude);
            for (int i = 0; i < emitters.Length; i++)
            {
                StudioEventEmitter emitter = emitters[i];
                if (emitter == null || !IsMusicEmitter(emitter))
                    continue;

                if (!emitter.IsPlaying())
                    emitter.Play();

                if (emitter.EventInstance.isValid())
                    emitter.EventInstance.setVolume(musicVolumeScale);
            }
            return;
        }

        if (TimeTransition.Instance != null && TimeTransition.Instance.IsTransitioning)
            return;

        PlayManagedMusic(GetTargetMusicPath());
    }

    void Refresh(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        // TitleScreen manages its own event:/Music/Main_Menu emitter
        if (scene.name == "TitleScreen")
        {
            StopManagedMusic();
            ApplyVolumeToSceneMusicEmitters();
            return;
        }

        // Disable room music emitters so BGM stays seamless across rooms and never doubles up
        StudioEventEmitter[] emitters = FindObjectsByType<StudioEventEmitter>(FindObjectsInactive.Include);
        for (int i = 0; i < emitters.Length; i++)
        {
            StudioEventEmitter emitter = emitters[i];
            if (emitter != null && IsMusicEmitter(emitter))
            {
                emitter.AllowFadeout = true;
                if (emitter.IsPlaying())
                    emitter.Stop();
                emitter.enabled = false;
            }
        }

        // Wait until TimeTransition finishes so Evening BGM crossfades in cleanly after the transition track
        if (TimeTransition.Instance != null && TimeTransition.Instance.IsTransitioning)
            return;

        string targetPath = GetTargetMusicPath();
        PlayManagedMusic(targetPath);
    }

    void PlayManagedMusic(string eventPath)
    {
        if (string.IsNullOrEmpty(eventPath))
            return;

        if (managedMusicInstance.isValid() && managedMusicPath == eventPath)
        {
            managedMusicInstance.getPlaybackState(out PLAYBACK_STATE state);
            if (state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING)
            {
                managedMusicInstance.setVolume(musicVolumeScale);
                return;
            }
        }

        StopManagedMusic();

        try
        {
            managedMusicInstance = RuntimeManager.CreateInstance(eventPath);
            managedMusicInstance.setVolume(musicVolumeScale);
            managedMusicInstance.start();
            managedMusicPath = eventPath;
        }
        catch (EventNotFoundException)
        {
            Debug.LogWarning($"FmodTimeOfDayMusic: FMOD event '{eventPath}' not found. Run FMOD -> Refresh Banks in Unity Editor.", this);
            managedMusicPath = null;
        }
    }

    void ApplyVolumeToActiveMusic()
    {
        if (managedMusicInstance.isValid())
            managedMusicInstance.setVolume(musicVolumeScale);
        ApplyVolumeToSceneMusicEmitters();
    }

    void ApplyVolumeToSceneMusicEmitters()
    {
        StudioEventEmitter[] emitters = FindObjectsByType<StudioEventEmitter>(FindObjectsInactive.Exclude);
        for (int i = 0; i < emitters.Length; i++)
        {
            StudioEventEmitter emitter = emitters[i];
            if (emitter != null && IsMusicEmitter(emitter) && emitter.EventInstance.isValid())
                emitter.EventInstance.setVolume(musicVolumeScale);
        }
    }

    void StopManagedMusic()
    {
        if (managedMusicInstance.isValid())
        {
            managedMusicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            managedMusicInstance.release();
            managedMusicInstance.clearHandle();
        }
        managedMusicPath = null;
    }

    static bool IsMusicEmitter(StudioEventEmitter emitter)
    {
        string path = GetEventPath(emitter);
        return !string.IsNullOrEmpty(path) && path.StartsWith("event:/Music/", System.StringComparison.OrdinalIgnoreCase);
    }

    static string GetEventPath(StudioEventEmitter emitter)
    {
        if (emitter == null || emitter.EventReference.IsNull)
            return null;

#if UNITY_EDITOR
        return emitter.EventReference.Path;
#else
        try
        {
            EventDescription description = RuntimeManager.GetEventDescription(emitter.EventReference);
            return description.getPath(out string path) == FMOD.RESULT.OK ? path : null;
        }
        catch (EventNotFoundException)
        {
            return null;
        }
#endif
    }

    static string GetTargetMusicPath()
    {
        if (YarnCommands.IsTimeOfDay(YarnCommands.GapTimeOfDay))
            return MidnightPath;

        if (YarnCommands.IsTimeOfDay("evening") || YarnCommands.IsTimeOfDay("night"))
            return EveningPath;

        return DaylightPath;
    }
}
