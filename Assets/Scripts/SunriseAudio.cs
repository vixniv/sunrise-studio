using System.Collections;
using System.Collections.Generic;
using SunriseStudio.UI;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SunriseAudio : MonoBehaviour
{
    const string ResourceName = "SunriseAudioLibrary";
    const string MusicVolumeKey = "Sunrise.Settings.MusicVolume";
    const string SfxVolumeKey = "Sunrise.Settings.SfxVolume";

    static SunriseAudio instance;
    static SunriseAudioLibrary library;
    static bool pendingDoorClose;
    static float pendingDoorCloseVolume = 1f;

    AudioSource oneShotSource;
    AudioSource loopSource;
    AudioSource musicSource;
    AudioSource ambienceSource;
    AudioLowPassFilter ambienceFilter;
    Coroutine ambienceFade;
    bool ambienceTransitioning;
    float musicCueVolume = 1f;
    float musicVolumeScale = 1f;
    float sfxVolumeScale = 1f;
    float loopCueVolume = 1f;
    FMOD.Studio.EventInstance fmodMusic;
    string fmodMusicPath;
    readonly List<StudioEventEmitter> sceneSfxEmitters = new List<StudioEventEmitter>();

    static SunriseAudio Instance
    {
        get
        {
            if (instance != null)
                return instance;

            var host = new GameObject("Sunrise Audio");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<SunriseAudio>();
            return instance;
        }
    }

    public static float SfxVolume => Instance.sfxVolumeScale;

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
        foreach (SunriseAudio stale in Resources.FindObjectsOfTypeAll<SunriseAudio>())
        {
            if (stale == null || stale.gameObject.name != "Sunrise Audio")
                continue;

            foreach (AudioSource source in stale.GetComponentsInChildren<AudioSource>(true))
            {
                source.Stop();
                source.clip = null;
            }

            if (stale.gameObject.scene.IsValid())
                Destroy(stale.gameObject);
            else
                DestroyImmediate(stale.gameObject);
        }

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
        sfxVolumeScale = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));

        // Keep the cutscene filter off the host: filtering the host would also muffle music and UI sounds.
        var ambienceObject = new GameObject("Cutscene Ambience");
        ambienceObject.transform.SetParent(transform, false);
        ambienceSource = ambienceObject.AddComponent<AudioSource>();
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceSource.spatialBlend = 0f;
        ambienceSource.mute = sfxVolumeScale <= 0.0001f;
        ambienceFilter = ambienceObject.AddComponent<AudioLowPassFilter>();
        ambienceFilter.cutoffFrequency = 22000f;

        SceneManager.sceneLoaded += OnSceneLoaded;
        YarnCommands.TimeOfDayChanged += RefreshMusicForCurrentScene;
        WireButtons(SceneManager.GetActiveScene());
        RefreshMusicForCurrentScene();
    }

    void OnDestroy()
    {
        StopFmodMusic();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        YarnCommands.TimeOfDayChanged -= RefreshMusicForCurrentScene;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        KeepFmodMusicAcrossScenes(scene);
        CacheFmodSfx(scene);
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
            Instance.oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume * volumeScale) * Instance.sfxVolumeScale);
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
        Instance.loopCueVolume = volume;
        source.volume = volume * Instance.sfxVolumeScale;
        source.Play();
    }

    public static void StopLoop()
    {
        if (instance == null || instance.loopSource == null)
            return;
        instance.loopSource.Stop();
        instance.loopSource.clip = null;
    }

    public static bool IsAmbienceTransitioning => instance != null && instance.ambienceTransitioning;

    public static bool StartAmbience(string key)
    {
        if (Library == null)
            return false;

        float volume;
        AudioClip clip = Library.GetClip(key, out volume);
        if (clip == null)
            return false;

        SunriseAudio audio = Instance;
        audio.CancelAmbienceFade();
        audio.ambienceSource.Stop();
        audio.ambienceSource.clip = clip;
        audio.ambienceSource.volume = Mathf.Clamp01(volume);
        audio.ambienceFilter.cutoffFrequency = 22000f;
        audio.ambienceSource.Play();
        return true;
    }

    public static void MuffleAmbience(float seconds)
    {
        if (instance == null || instance.ambienceSource == null || !instance.ambienceSource.isPlaying)
            return;
        instance.BeginAmbienceFade(1000f, instance.ambienceSource.volume * 0.55f, seconds, false);
    }

    public static void StopAmbience(float seconds)
    {
        if (instance == null || instance.ambienceSource == null)
            return;
        instance.BeginAmbienceFade(instance.ambienceFilter.cutoffFrequency, 0f, seconds, true);
    }

    void CancelAmbienceFade()
    {
        if (ambienceFade != null)
            StopCoroutine(ambienceFade);
        ambienceFade = null;
        ambienceTransitioning = false;
    }

    void BeginAmbienceFade(float cutoff, float volume, float seconds, bool stop)
    {
        CancelAmbienceFade();
        if (seconds <= 0f)
        {
            ambienceFilter.cutoffFrequency = cutoff;
            ambienceSource.volume = volume;
            if (stop)
            {
                ambienceSource.Stop();
                ambienceSource.clip = null;
            }
            return;
        }

        ambienceTransitioning = true;
        ambienceFade = StartCoroutine(FadeAmbienceRoutine(cutoff, volume, seconds, stop));
    }

    IEnumerator FadeAmbienceRoutine(float cutoff, float volume, float seconds, bool stop)
    {
        float startCutoff = ambienceFilter.cutoffFrequency;
        float startVolume = ambienceSource.volume;
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / seconds);
            ambienceFilter.cutoffFrequency = Mathf.Lerp(startCutoff, cutoff, t);
            ambienceSource.volume = Mathf.Lerp(startVolume, volume, t);
            yield return null;
        }

        ambienceFilter.cutoffFrequency = cutoff;
        ambienceSource.volume = volume;
        if (stop)
        {
            ambienceSource.Stop();
            ambienceSource.clip = null;
        }
        ambienceTransitioning = false;
        ambienceFade = null;
    }


    public static void SetMusicVolume(float volume)
    {
        float clamped = Mathf.Clamp01(volume);
        foreach (SunriseAudio audio in Resources.FindObjectsOfTypeAll<SunriseAudio>())
        {
            if (audio == null)
                continue;

            audio.musicVolumeScale = clamped;
            audio.ApplyFmodMusicVolume();
            AudioSource source = audio.musicSource;
            if (source == null)
            {
                foreach (AudioSource candidate in audio.GetComponents<AudioSource>())
                {
                    if (candidate.clip == null)
                        continue;
                    string clipName = candidate.clip.name;
                    if (clipName == "Sunrise_Main_Menu" ||
                        clipName == "Sunrise_Happy" ||
                        clipName == "Sunrise_midnight")
                    {
                        source = candidate;
                        break;
                    }
                }
            }

            if (source == null)
                continue;
            source.mute = clamped <= 0.0001f;
            source.volume = audio.musicCueVolume * clamped;
        }

        if (instance == null)
            _ = Instance;
    }

    public static void SetSfxVolume(float volume)
    {
        SunriseAudio audio = Instance;
        audio.sfxVolumeScale = Mathf.Clamp01(volume);
        if (audio.loopSource != null && audio.loopSource.clip != null)
            audio.loopSource.volume = audio.loopCueVolume * audio.sfxVolumeScale;
        if (audio.ambienceSource != null)
            audio.ambienceSource.mute = audio.sfxVolumeScale <= 0.0001f;
        audio.ApplyFmodSfxVolume();
    }

    void LateUpdate()
    {
        // Some UI emitters create a new FMOD instance only when clicked.
        if (sfxVolumeScale < 0.999f)
            ApplyFmodSfxVolume();
    }

    void CacheFmodSfx(Scene scene)
    {
        sceneSfxEmitters.Clear();
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (StudioEventEmitter emitter in root.GetComponentsInChildren<StudioEventEmitter>(true))
            if (emitter != null && emitter.EventReference.Path?.StartsWith("event:/Music/", System.StringComparison.OrdinalIgnoreCase) != true)
                sceneSfxEmitters.Add(emitter);
        ApplyFmodSfxVolume();
    }

    void ApplyFmodSfxVolume()
    {
        foreach (StudioEventEmitter emitter in sceneSfxEmitters)
            if (emitter != null && emitter.EventInstance.isValid())
                emitter.EventInstance.setVolume(sfxVolumeScale);
    }

    void KeepFmodMusicAcrossScenes(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (StudioEventEmitter emitter in root.GetComponentsInChildren<StudioEventEmitter>(true))
        {
            if (emitter == null || emitter.EventReference.Path?.StartsWith("event:/Music/", System.StringComparison.OrdinalIgnoreCase) != true)
                continue;

            // Room emitters would start at Start, then stop when the room unloads.
            emitter.enabled = false;
            string path = emitter.EventReference.Path;
            if (path == fmodMusicPath && fmodMusic.isValid())
                continue;

            StopFmodMusic();
            try
            {
                fmodMusic = RuntimeManager.CreateInstance(emitter.EventReference);
                fmodMusicPath = path;
                ApplyFmodMusicVolume();
                fmodMusic.start();
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"Unable to play FMOD music {path}: {exception.Message}", this);
                StopFmodMusic();
            }
        }
    }

    void ApplyFmodMusicVolume()
    {
        if (fmodMusic.isValid()) fmodMusic.setVolume(musicVolumeScale);
    }

    void StopFmodMusic()
    {
        if (fmodMusic.isValid())
        {
            fmodMusic.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            fmodMusic.release();
            fmodMusic.clearHandle();
        }
        fmodMusicPath = null;
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
        musicSource.mute = musicVolumeScale <= 0.0001f;
        musicSource.volume = musicCueVolume * musicVolumeScale;
        musicSource.loop = true;
        if (musicSource.isPlaying && musicSource.clip == clip)
            return;

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.Play();
    }
}
