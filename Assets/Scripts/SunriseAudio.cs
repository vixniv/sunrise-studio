using System;
using System.Collections;
using System.Collections.Generic;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using SunriseStudio.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public sealed class SunriseAudio : MonoBehaviour
{
    const string ResourceName = "SunriseAudioLibrary";
    const string MusicVolumeKey = "Sunrise.Settings.MusicVolume";
    const string SfxVolumeKey = "Sunrise.Settings.SfxVolume";
    const string MusicBusPath = "bus:/Music";
    const string SfxBusPath = "bus:/SFX";
    const string UiBusPath = "bus:/UI";

    static readonly Dictionary<string, string> CueToFmodEvent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Music
        { "music_main_menu", "event:/Music/Main_Menu" },
        { "music_daylight", "event:/Music/Daylight" },
        { "music_midnight", "event:/Music/Midnight" },

        // Dialogue
        { "dialogue_blip", "event:/Dialogue/Blip" },
        { "dialogue_advance", "event:/Dialogue/Advance" },
        { "dialogue_choice", "event:/Dialogue/Choice" },

        // UI
        { "ui_click", "event:/UI/Click" },
        { "ui_click_short", "event:/UI/Click_Short" },
        { "ui_confirm", "event:/UI/Confirm" },
        { "ui_error", "event:/UI/Error" },
        { "ui_interact", "event:/UI/Interact" },
        { "ui_item_pickup", "event:/UI/Item_Pickup" },
        { "ui_quest_popup", "event:/UI/Quest_Popup" },
        { "ui_quest_complete", "event:/UI/Quest_Complete" },
        { "ui_achievement", "event:/UI/Achievement" },

        // World / Interact / Doors / Lockers
        { "interact", "event:/SFX/Interact" },
        { "door", "event:/SFX/Door/Generic" },
        { "door_open", "event:/SFX/Door/Open" },
        { "door_close", "event:/SFX/Door/Close" },
        { "locker_open", "event:/SFX/Locker/Open" },
        { "locker_close", "event:/SFX/Locker/Close" },
        { "locker_unlock", "event:/SFX/Locker/Unlock" },
        { "locker_grab_rattle", "event:/SFX/Locker/Grab_Rattle" },

        // Time System
        { "day_transition", "event:/SFX/Time/Day_Transition" },
        { "night_transition", "event:/SFX/Time/Night_Transition" },
        { "soft_reset", "event:/SFX/Time/Soft_Reset" },
        { "hard_reset", "event:/SFX/Time/Hard_Reset" },
        { "clock_ticking", "event:/SFX/Time/Clock_Ticking" },
        { "alarm_clock", "event:/SFX/Time/Alarm_Clock" },

        // Footsteps
        { "footstep_carpet", "event:/SFX/Footsteps/Carpet" },
        { "footstep_cement", "event:/SFX/Footsteps/Cement" },
        { "footstep_concrete", "event:/SFX/Footsteps/Concrete" },
        { "footstep_grass", "event:/SFX/Footsteps/Grass" },
        { "footstep_linoleum", "event:/SFX/Footsteps/Linoleum" },
        { "footstep_wood", "event:/SFX/Footsteps/Wood" },

        // Story / Scene 1-5
        { "fall_impact", "event:/SFX/Story/Scene1_5/Fall_Impact" },
        { "distant_scream", "event:/SFX/Story/Scene1_5/Distant_Scream" },
        { "running_to_fal", "event:/SFX/Story/Scene1_5/Running_To_Fal" },
        { "wardrobe_open", "event:/SFX/Story/Scene1_5/Wardrobe_Open" },
        { "wardrobe_close", "event:/SFX/Story/Scene1_5/Wardrobe_Close" },
        { "clothes_rustle", "event:/SFX/Story/Scene1_5/Clothes_Rustle" },
        { "chair_move", "event:/SFX/Story/Scene1_5/Chair_Move" },
        { "food_serving", "event:/SFX/Story/Scene1_5/Food_Serving" },
        { "plate_put_down", "event:/SFX/Story/Scene1_5/Plate_Put_Down" },
        { "ambience_drop_after_fal", "event:/SFX/Story/Scene1_5/Ambience_Drop_After_Fal" },
        { "sting_before_again", "event:/SFX/Story/Scene1_5/Sting_Before_Again" },
        { "janitor_cabinet_open", "event:/SFX/Story/Scene1_5/Janitor_Cabinet_Open" },
        { "keys_pickup", "event:/SFX/Story/Scene1_5/Keys_Pickup" },
        { "keys_return", "event:/SFX/Story/Scene1_5/Keys_Return" },
        { "paper_pickup", "event:/SFX/Story/Scene1_5/Paper_Pickup" },
        { "paper_handle", "event:/SFX/Story/Scene1_5/Paper_Handle" },
        { "paper_placement", "event:/SFX/Story/Scene1_5/Paper_Placement" },
        { "writing_pencil", "event:/SFX/Story/Scene1_5/Writing_Pencil" },
        { "book_pickup", "event:/SFX/Story/Scene1_5/Book_Pickup" },
        { "book_open", "event:/SFX/Story/Scene1_5/Book_Open" },
        { "book_page_turn", "event:/SFX/Story/Scene1_5/Book_Page_Turn" },
        { "paper_drop", "event:/SFX/Story/Scene1_5/Paper_Drop" },
        { "books_drop", "event:/SFX/Story/Scene1_5/Books_Drop" },
        { "crumpled_note_unfold", "event:/SFX/Story/Scene1_5/Crumpled_Note_Unfold" },
        { "blazer_pocket", "event:/SFX/Story/Scene1_5/Blazer_Pocket" },
        { "bolt_unscrew", "event:/SFX/Story/Scene1_5/Bolt_Unscrew" },
        { "flag_cloth", "event:/SFX/Story/Scene1_5/Flag_Cloth" },
        { "bolt_tighten", "event:/SFX/Story/Scene1_5/Bolt_Tighten" },
        { "memory_transition_in", "event:/SFX/Story/Scene1_5/Memory_Transition_In" },
        { "memory_transition_back", "event:/SFX/Story/Scene1_5/Memory_Transition_Back" },
        { "basketball_practice", "event:/SFX/Story/Scene1_5/Basketball_Practice" },
        { "bed_drop", "event:/SFX/Story/Scene1_5/Bed_Drop" },

        // Story / Scene 6-8
        { "calendar_paper_handle", "event:/SFX/Story/Scene6_8/Calendar_Paper_Handle" },
        { "calendar_writing", "event:/SFX/Story/Scene6_8/Calendar_Writing" },
        { "sketchbook_page_turn", "event:/SFX/Story/Scene6_8/Sketchbook_Page_Turn" },
        { "pencil_sketching", "event:/SFX/Story/Scene6_8/Pencil_Sketching" },
        { "light_switch", "event:/SFX/Story/Scene6_8/Light_Switch" },
        { "drowsiness", "event:/SFX/Story/Scene6_8/Drowsiness" },
        { "snap_awake", "event:/SFX/Story/Scene6_8/Snap_Awake" },
        { "reveal_sting", "event:/SFX/Story/Scene6_8/Reveal_Sting" },
        { "gap_transition", "event:/SFX/Story/Scene6_8/Gap_Transition" },
        { "night_anin_reveal", "event:/SFX/Story/Scene6_8/Night_Anin_Reveal" },
        { "festival_transition", "event:/SFX/Story/Scene6_8/Festival_Transition" },
        { "festival_crowd", "event:/SFX/Story/Scene6_8/Festival_Crowd" },
        { "separation_transition", "event:/SFX/Story/Scene6_8/Separation_Transition" },
        { "end_chapter_sting", "event:/SFX/Story/Scene6_8/End_Chapter_Sting" }
    };

    static readonly Dictionary<string, string> SceneToFootstepEvent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Library", "event:/SFX/Footsteps/Carpet" },
        { "Auditorium", "event:/SFX/Footsteps/Linoleum" },
        { "SchoolPark", "event:/SFX/Footsteps/Grass" }
    };

    static SunriseAudio instance;
    static SunriseAudioLibrary library;
    static bool pendingDoorClose;
    static float pendingDoorCloseVolume = 1f;

    // Legacy Unity AudioSources kept only as graceful fallback before FMOD banks are refreshed
    AudioSource oneShotSource;
    AudioSource loopSource;
    AudioSource musicSource;
    float musicCueVolume = 1f;
    float musicVolumeScale = 1f;
    float sfxVolumeScale = 1f;

    // Active FMOD instances
    EventInstance activeMusicInstance;
    string activeMusicPath;
    EventInstance activeLoopInstance;
    string activeLoopPath;

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

            stale.StopAllFmodInstancesImmediate();
            foreach (AudioSource source in stale.GetComponents<AudioSource>())
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

        ApplyFmodBusVolumes();

        SceneManager.sceneLoaded += OnSceneLoaded;
        YarnCommands.TimeOfDayChanged += RefreshMusicForCurrentScene;
        Scene activeScene = SceneManager.GetActiveScene();
        EnsureStudioListener(activeScene);
        WireButtons(activeScene);
        RefreshMusicForCurrentScene();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        YarnCommands.TimeOfDayChanged -= RefreshMusicForCurrentScene;
        StopAllFmodInstancesImmediate();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureStudioListener(scene);
        WireButtons(scene);
        ApplyFmodBusVolumes();
        RefreshMusic(scene);
        if (!pendingDoorClose)
            return;

        float volume = pendingDoorCloseVolume;
        pendingDoorClose = false;
        pendingDoorCloseVolume = 1f;
        TryPlay("door_close", volume);
    }

    static void EnsureStudioListener(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (FindAnyObjectByType<StudioListener>() != null)
            return;

        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.GetComponent<StudioListener>() == null)
            mainCam.gameObject.AddComponent<StudioListener>();
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
                    button.GetComponent<StudioEventEmitter>() != null ||
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

    public static string ResolveEventPath(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        string trimmed = key.Trim();
        if (trimmed.StartsWith("event:/", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (CueToFmodEvent.TryGetValue(trimmed, out string path))
            return path;

        return null;
    }

    public static string GetFootstepEventPathForScene(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName) &&
            SceneToFootstepEvent.TryGetValue(sceneName.Trim(), out string path))
            return path;

        FootstepSurface surface = GetSurfaceForScene(sceneName);
        return GetFootstepEventPathForSurface(surface);
    }

    public static string GetFootstepEventPathForSurface(FootstepSurface surface)
    {
        if (surface == null || string.IsNullOrWhiteSpace(surface.name))
            return null;

        string key = "footstep_" + surface.name.Trim().ToLowerInvariant();
        return ResolveEventPath(key);
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

    public static bool TryPlayFmodEvent(EventReference eventRef, float volumeScale = 1f)
    {
        return PlayFmodEventAndGetDuration(eventRef, volumeScale) >= 0f;
    }

    public static float PlayFmodEventAndGetDuration(EventReference eventRef, float volumeScale = 1f)
    {
        if (eventRef.IsNull)
            return -1f;

        if (!TryGetEventDescription(eventRef, out EventDescription desc))
            return -1f;

        return StartOneShotFromDescription(desc, volumeScale);
    }

    public static bool TryPlayFmodPath(string eventPath, float volumeScale = 1f)
    {
        return PlayFmodPathAndGetDuration(eventPath, volumeScale) >= 0f;
    }

    public static float PlayFmodPathAndGetDuration(string eventPath, float volumeScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(eventPath))
            return -1f;

        if (!TryGetEventDescription(eventPath, out EventDescription desc))
            return -1f;

        return StartOneShotFromDescription(desc, volumeScale);
    }

    public static bool TryPlay(string key, float volumeScale = 1f)
    {
        return PlayAndGetDuration(key, volumeScale) >= 0f;
    }

    public static float PlayAndGetDuration(string key, float volumeScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(key))
            return -1f;

        string fmodPath = ResolveEventPath(key);
        if (!string.IsNullOrEmpty(fmodPath))
        {
            float fmodDuration = PlayFmodPathAndGetDuration(fmodPath, volumeScale);
            if (fmodDuration >= 0f)
                return fmodDuration;
        }

        if (Library != null)
        {
            float volume;
            AudioClip clip = Library.GetClip(key, out volume);
            if (clip != null)
            {
                Instance.oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume * volumeScale * Instance.sfxVolumeScale));
                return clip.length;
            }
        }

        return -1f;
    }

    public static AudioClip Play(string key, float volumeScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        string fmodPath = ResolveEventPath(key);
        if (!string.IsNullOrEmpty(fmodPath) && PlayFmodPathAndGetDuration(fmodPath, volumeScale) >= 0f)
            return Library != null ? Library.GetClip(key, out _) : null;

        if (Library == null)
            return null;

        float volume;
        AudioClip clip = Library.GetClip(key, out volume);
        if (clip != null)
            Instance.oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume * volumeScale * Instance.sfxVolumeScale));
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
            float duration = PlayAndGetDuration(keys[i]);
            if (duration >= 0f && i + 1 < keys.Length)
                yield return new WaitForSecondsRealtime(Mathf.Max(0.08f, duration * 0.55f));
        }
    }

    public static void PlayLoop(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        SunriseAudio audio = Instance;
        string fmodPath = ResolveEventPath(key);
        if (!string.IsNullOrEmpty(fmodPath) && TryGetEventDescription(fmodPath, out EventDescription desc))
        {
            if (audio.activeLoopInstance.isValid() &&
                string.Equals(audio.activeLoopPath, fmodPath, StringComparison.OrdinalIgnoreCase))
            {
                audio.activeLoopInstance.getPlaybackState(out PLAYBACK_STATE state);
                if (state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING)
                    return;
            }

            audio.StopFmodLoop(STOP_MODE.ALLOWFADEOUT);
            if (desc.createInstance(out EventInstance inst) == RESULT.OK)
            {
                inst.start();
                audio.activeLoopInstance = inst;
                audio.activeLoopPath = fmodPath;
                return;
            }
        }

        if (Library == null)
            return;

        float volume;
        AudioClip clip = Library.GetClip(key, out volume);
        if (clip == null)
            return;

        AudioSource source = audio.loopSource;
        if (source.isPlaying && source.clip == clip)
            return;
        source.Stop();
        source.clip = clip;
        source.volume = volume * audio.sfxVolumeScale;
        source.Play();
    }

    public static void StopLoop()
    {
        if (instance == null)
            return;

        instance.StopFmodLoop(STOP_MODE.ALLOWFADEOUT);
        if (instance.loopSource != null)
        {
            instance.loopSource.Stop();
            instance.loopSource.clip = null;
        }
    }

    public static void SetPaused(bool paused)
    {
        AudioListener.pause = paused;
        if (TryGetStudioSystem(out _))
            RuntimeManager.PauseAllEvents(paused);
    }

    public static void SetMusicVolume(float volume)
    {
        float clamped = Mathf.Clamp01(volume);
        bool busApplied = SetFmodBusVolume(MusicBusPath, clamped);

        foreach (SunriseAudio audio in Resources.FindObjectsOfTypeAll<SunriseAudio>())
        {
            if (audio == null)
                continue;

            audio.musicVolumeScale = clamped;
            if (audio.activeMusicInstance.isValid())
                audio.activeMusicInstance.setVolume(busApplied ? 1f : clamped);

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
        float clamped = Mathf.Clamp01(volume);
        SetFmodBusVolume(SfxBusPath, clamped);
        SetFmodBusVolume(UiBusPath, clamped);

        foreach (SunriseAudio audio in Resources.FindObjectsOfTypeAll<SunriseAudio>())
        {
            if (audio == null)
                continue;
            audio.sfxVolumeScale = clamped;
        }

        if (instance == null)
            _ = Instance;
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
        string fmodPath = ResolveEventPath(key);
        if (!string.IsNullOrEmpty(fmodPath) && TryGetEventDescription(fmodPath, out EventDescription desc))
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Stop();
                musicSource.clip = null;
            }

            if (activeMusicInstance.isValid() &&
                string.Equals(activeMusicPath, fmodPath, StringComparison.OrdinalIgnoreCase))
            {
                activeMusicInstance.getPlaybackState(out PLAYBACK_STATE state);
                if (state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING)
                {
                    ApplyFmodBusVolumes();
                    return;
                }
            }

            StopFmodMusic(STOP_MODE.ALLOWFADEOUT);
            if (desc.createInstance(out EventInstance inst) == RESULT.OK)
            {
                bool busApplied = SetFmodBusVolume(MusicBusPath, musicVolumeScale);
                inst.setVolume(busApplied ? 1f : musicVolumeScale);
                inst.start();
                activeMusicInstance = inst;
                activeMusicPath = fmodPath;
                ApplyFmodBusVolumes();
                return;
            }
        }

        // Legacy fallback when FMOD banks are not built/refreshed yet
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

    void ApplyFmodBusVolumes()
    {
        SetFmodBusVolume(MusicBusPath, musicVolumeScale);
        SetFmodBusVolume(SfxBusPath, sfxVolumeScale);
        SetFmodBusVolume(UiBusPath, sfxVolumeScale);
    }

    void StopFmodMusic(STOP_MODE mode)
    {
        if (!activeMusicInstance.isValid())
            return;
        activeMusicInstance.stop(mode);
        activeMusicInstance.release();
        activeMusicInstance.clearHandle();
        activeMusicPath = null;
    }

    void StopFmodLoop(STOP_MODE mode)
    {
        if (!activeLoopInstance.isValid())
            return;
        activeLoopInstance.stop(mode);
        activeLoopInstance.release();
        activeLoopInstance.clearHandle();
        activeLoopPath = null;
    }

    void StopAllFmodInstancesImmediate()
    {
        StopFmodMusic(STOP_MODE.IMMEDIATE);
        StopFmodLoop(STOP_MODE.IMMEDIATE);
    }

    static bool TryGetStudioSystem(out FMOD.Studio.System system)
    {
        system = default;
        try
        {
            system = RuntimeManager.StudioSystem;
            return system.isValid();
        }
        catch (Exception)
        {
            return false;
        }
    }

    static bool TryGetEventDescription(EventReference eventRef, out EventDescription desc)
    {
        desc = default;
        if (eventRef.IsNull || !TryGetStudioSystem(out FMOD.Studio.System system))
            return false;

        if (!eventRef.Guid.IsNull && system.getEventByID(eventRef.Guid, out desc) == RESULT.OK)
            return true;

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(eventRef.Path) && system.getEvent(eventRef.Path, out desc) == RESULT.OK)
            return true;
#endif
        return false;
    }

    static bool TryGetEventDescription(string eventPath, out EventDescription desc)
    {
        desc = default;
        if (string.IsNullOrWhiteSpace(eventPath) || !TryGetStudioSystem(out FMOD.Studio.System system))
            return false;

        return system.getEvent(eventPath, out desc) == RESULT.OK;
    }

    static float StartOneShotFromDescription(EventDescription desc, float volumeScale)
    {
        if (desc.createInstance(out EventInstance inst) != RESULT.OK)
            return -1f;

        if (volumeScale < 0.999f || volumeScale > 1.001f)
            inst.setVolume(Mathf.Clamp(volumeScale, 0f, 2f));

        inst.start();
        inst.release();

        int lengthMs = 0;
        desc.getLength(out lengthMs);
        return lengthMs > 0 ? lengthMs * 0.001f : 0f;
    }

    static bool SetFmodBusVolume(string busPath, float volume)
    {
        if (!TryGetStudioSystem(out FMOD.Studio.System system))
            return false;

        if (system.getBus(busPath, out Bus bus) == RESULT.OK)
        {
            bus.setVolume(Mathf.Clamp01(volume));
            return true;
        }
        return false;
    }
}
