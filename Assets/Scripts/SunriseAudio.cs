using System.Collections;
using SunriseStudio.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SunriseAudio : MonoBehaviour
{
    const string ResourceName = "SunriseAudioLibrary";
    const string MusicVolumeKey = "Sunrise.Settings.MusicVolume";

    static SunriseAudio instance;
    static SunriseAudioLibrary library;
    static bool pendingDoorClose;
    static float pendingDoorCloseVolume = 1f;

    AudioSource oneShotSource;
    AudioSource loopSource;
    AudioSource musicSource;
    float musicCueVolume = 1f;
    float musicVolumeScale = 1f;

    static SunriseAudio Instance
    {
        get
        {
            if (instance != null)
                return instance;

            var host = new GameObject("Sunrise Audio");
            host.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(host);
            instance = host.AddComponent<SunriseAudio>();
            return instance;
        }
    }

    static SunriseAudioLibrary Library
    {
        get
        {
            if (library == null)
                library = Resources.Load<SunriseAudioLibrary>(ResourceName);
            return library;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        library = null;
        pendingDoorClose = false;
        pendingDoorCloseVolume = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        _ = Instance;
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

        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.spatialBlend = 0f;

        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.loop = true;
        loopSource.spatialBlend = 0f;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicVolumeScale = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));

        SceneManager.sceneLoaded += OnSceneLoaded;
        YarnCommands.TimeOfDayChanged += RefreshMusicForCurrentScene;
        WireButtons(SceneManager.GetActiveScene());
        RefreshMusicForCurrentScene();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        YarnCommands.TimeOfDayChanged -= RefreshMusicForCurrentScene;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        WireButtons(scene);
        RefreshMusic(scene);
        if (!pendingDoorClose)
            return;

        float volume = pendingDoorCloseVolume;
        pendingDoorClose = false;
        pendingDoorCloseVolume = 1f;
        Play("door_close", volume);
    }

    static void WireButtons(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button.GetComponent<UIButtonSound>() != null ||
                    button.GetComponentInParent<DialogueHudView>(true) != null)
                    continue;

                UIButtonSound sound = button.gameObject.AddComponent<UIButtonSound>();
                sound.SetCue(ButtonCue(button.name));
            }
        }
    }

    static string ButtonCue(string buttonName)
    {
        string key = (buttonName ?? string.Empty).ToLowerInvariant();
        if (key.Contains("interact"))
            return "ui_interact";
        if (key.Contains("close") || key.Contains("back") || key.Contains("cancel"))
            return "ui_click_short";
        if (key.Contains("play") || key.Contains("save") || key.Contains("confirm") ||
            key.Contains("resume") || key.Contains("return"))
            return "ui_confirm";
        return "ui_click";
    }

    public static AudioClip GetClip(string key)
    {
        float ignored;
        return Library != null ? Library.GetClip(key, out ignored) : null;
    }

    public static FootstepSurface GetSurfaceForScene(string sceneName)
    {
        return Library != null ? Library.GetSurface(sceneName) : null;
    }

    public static void QueueDoorClose(float volumeScale = 1f)
    {
        pendingDoorClose = true;
        pendingDoorCloseVolume = Mathf.Clamp01(volumeScale);
    }


    public static AudioClip Play(string key, float volumeScale = 1f)
    {
        if (Library == null)
            return null;

        float volume;
        AudioClip clip = Library.GetClip(key, out volume);
        if (clip != null)
            Instance.oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume * volumeScale));
        return clip;
    }

    public static void PlaySequence(params string[] keys)
    {
        if (keys == null || keys.Length == 0)
            return;
        Instance.StartCoroutine(Instance.PlaySequenceRoutine(keys));
    }

    IEnumerator PlaySequenceRoutine(string[] keys)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            AudioClip clip = Play(keys[i]);
            if (clip != null && i + 1 < keys.Length)
                yield return new WaitForSecondsRealtime(Mathf.Max(0.08f, clip.length * 0.55f));
        }
    }

    public static void PlayLoop(string key)
    {
        if (Library == null)
            return;

        float volume;
        AudioClip clip = Library.GetClip(key, out volume);
        if (clip == null)
            return;

        AudioSource source = Instance.loopSource;
        if (source.isPlaying && source.clip == clip)
            return;
        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.Play();
    }

    public static void StopLoop()
    {
        if (instance == null || instance.loopSource == null)
            return;
        instance.loopSource.Stop();
        instance.loopSource.clip = null;
    }


    public static void SetMusicVolume(float volume)
    {
        SunriseAudio audio = Instance;
        audio.musicVolumeScale = Mathf.Clamp01(volume);
        if (audio.musicSource != null)
            audio.musicSource.volume = audio.musicCueVolume * audio.musicVolumeScale;
    }

    static void RefreshMusicForCurrentScene()
    {
        Instance.RefreshMusic(SceneManager.GetActiveScene());
    }

    void RefreshMusic(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        string cue = scene.name == "TitleScreen"
            ? "music_main_menu"
            : YarnCommands.IsTimeOfDay(YarnCommands.GapTimeOfDay)
                ? "music_midnight"
                : "music_daylight";
        PlayMusic(cue);
    }

    void PlayMusic(string key)
    {
        if (Library == null || musicSource == null)
            return;

        float volume;
        AudioClip clip = Library.GetClip(key, out volume);
        if (clip == null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        musicCueVolume = volume;
        musicSource.volume = musicCueVolume * musicVolumeScale;
        musicSource.loop = true;
        if (musicSource.isPlaying && musicSource.clip == clip)
            return;

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.Play();
    }
}
