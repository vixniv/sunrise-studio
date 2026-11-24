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

    static FmodTimeOfDayMusic instance;

    EventInstance managedMusicInstance;
    string managedMusicPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
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

    public static void RefreshForCurrentScene()
    {
        EnsureInstance().Refresh(SceneManager.GetActiveScene());
    }

    void Refresh(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        // TitleScreen manages its own event:/Music/Main_Menu emitter
        if (scene.name == "TitleScreen")
        {
            StopManagedMusic();
            return;
        }

        // Wait until TimeTransition finishes so Evening BGM crossfades in cleanly after the transition track
        if (TimeTransition.Instance != null && TimeTransition.Instance.IsTransitioning)
            return;

        string targetPath = GetTargetMusicPath();
        StudioEventEmitter[] emitters = FindObjectsByType<StudioEventEmitter>(FindObjectsInactive.Exclude);

        // If the scene already has an emitter playing the exact target track, let it play
        if (targetPath == DaylightPath)
        {
            bool sceneHasDaylight = false;
            for (int i = 0; i < emitters.Length; i++)
            {
                StudioEventEmitter emitter = emitters[i];
                if (emitter != null && SunriseAudio.EventPath(emitter.EventReference) == DaylightPath)
                {
                    sceneHasDaylight = true;
                    if (!emitter.IsPlaying())
                        emitter.Play();
                }
            }

            if (sceneHasDaylight)
            {
                StopManagedMusic();
                return;
            }
        }

        // Stop any scene music emitters playing a different track (e.g. Daylight when it's now Evening/Midnight)
        for (int i = 0; i < emitters.Length; i++)
        {
            StudioEventEmitter emitter = emitters[i];
            if (emitter != null && IsMusicEmitter(emitter) && SunriseAudio.EventPath(emitter.EventReference) != targetPath)
            {
                emitter.AllowFadeout = true;
                emitter.Stop();
            }
        }

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
                return;
        }

        StopManagedMusic();

        try
        {
            managedMusicInstance = RuntimeManager.CreateInstance(eventPath);
            managedMusicInstance.start();
            managedMusicPath = eventPath;
        }
        catch (EventNotFoundException)
        {
            Debug.LogWarning($"FmodTimeOfDayMusic: FMOD event '{eventPath}' not found. Run FMOD -> Refresh Banks in Unity Editor.", this);
            managedMusicPath = null;
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
        string path = SunriseAudio.EventPath(emitter.EventReference);
        return !string.IsNullOrEmpty(path) && path.StartsWith("event:/Music/");
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
