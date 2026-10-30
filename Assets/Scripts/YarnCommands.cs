using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Yarn.Unity;

// Registers the custom <<commands>> used in the Yarn scripts on this scene's Dialogue Runner.
// Put it on the same GameObject as the Dialogue Runner, in every scene that runs Yarn dialogue.
// Yarn stops on any command nobody registered, so every command in scene1.yarn is registered here;
// the ones without a game system yet only log what they would do.
public class YarnCommands : MonoBehaviour
{
    [Serializable]
    public class Sound
    {
        [Tooltip("Name used in <<sfx \"name\">>.")]
        public string name;
        public AudioClip clip;
    }

    [Serializable]
    public class Actor
    {
        [Tooltip("Name used in <<show \"name\">> and <<hide \"name\">>, such as \"Fal\".")]
        public string name;
        public GameObject target;
    }

    [Serializable]
    public class Cutscene
    {
        [Tooltip("Id used in <<cutscene \"id\">>, such as \"anin_joins_table\".")]
        public string id;
        [Tooltip("Timeline to play. Turn off its Play On Awake.")]
        public PlayableDirector director;
    }

    [SerializeField] private DialogueRunner runner;
    [Tooltip("Other location names in <<scene_transition>> that mean this scene. This scene's own name always counts. Case, spaces, and underscores are ignored.")]
    [SerializeField] private List<string> locationsInThisScene = new List<string>();
    [Tooltip("Plays <<sfx>> sounds. Leave empty to use an Audio Source on this object.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private List<Sound> sounds = new List<Sound>();
    [Tooltip("Characters that <<show>> and <<hide>> during dialogue. Enabled DialogSpeakers can be hidden without being listed.")]
    [SerializeField] private List<Actor> actors = new List<Actor>();
    [Tooltip("Timelines that <<cutscene>> plays. An id missing here is skipped with a log message.")]
    [SerializeField] private List<Cutscene> cutscenes = new List<Cutscene>();
    [Tooltip("Plays the white flash of <<hard_reset>> and <<freeze_frame>>. Only needed in scenes whose dialogue uses them.")]
    [SerializeField] private FreezeFrame freezeFrame;
    [Tooltip("Scene <<hard_reset>> loads. Must be in the Build Profile scene list. Leave empty to stay in this scene.")]
    [FormerlySerializedAs("sceneAfterLoopEnd")]
    [SerializeField] private string sceneAfterHardReset = "";
    [Tooltip("Seconds the screen stays covered during <<advance_time>> and <<fade>>.")]
    [SerializeField, Min(0f)] private float fadeHoldSeconds = 0.5f;

    public const string DefaultTimeOfDay = "morning";

    // While true, SceneDoors with a Story Node run that node instead of leaving freely.
    // Turned off every time a scene loads, so a start node turns it on only for the scene whose story needs the door.
    public const string StoryExitVariable = "$story_exit";

    private bool leaving;
    private bool pausedAudio;

    // Time slot set by <<advance_time>> (morning, lunch, after_school). It carries over to the next scene.
    public static string TimeOfDay { get; private set; } = DefaultTimeOfDay;

    // Number of <<hard_reset>>s so far; 0 until the first loop ends.
    public static int LoopsCompleted { get; private set; }

    // Runs after TimeOfDay changes. During <<advance_time>> it runs while the screen is covered.
    public static event Action TimeOfDayChanged;

    // Clears the static values when Play Mode starts, even with Domain Reload turned off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        TimeOfDay = DefaultTimeOfDay;
        LoopsCompleted = 0;
        TimeOfDayChanged = null;
    }

    // Time slot names ignore case, spaces, and underscores, so "After School" matches "after_school".
    public static bool IsTimeOfDay(string slot) => Key(slot) == Key(TimeOfDay);

    private void Awake()
    {
        if (runner == null) runner = GetComponent<DialogueRunner>();
        if (runner == null)
        {
            Debug.LogError($"{name} has no Dialogue Runner assigned.", this);
            return;
        }
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
        if (!(runner.VariableStorage is PersistentVariableStorage))
            Debug.LogWarning($"{runner.name}'s Variable Storage is not a PersistentVariableStorage, so Yarn variables start over in this scene.", runner);
        if (runner.VariableStorage != null)
            runner.VariableStorage.SetValue(StoryExitVariable, false);

        // The player gets control back when the node ends, so there is nothing to do here.
        runner.AddCommandHandler("enable_control", (Action)(() => { }));
        runner.AddCommandHandler("scene_transition", (Func<string, string, YarnTask>)SceneTransition);
        runner.AddCommandHandler("advance_time", (Func<string, YarnTask>)AdvanceTime);
        runner.AddCommandHandler("fade", (Func<YarnTask>)(() => Fade(null)));
        runner.AddCommandHandler("show", (Action<string>)(actor => SetActorVisible(actor, true)));
        runner.AddCommandHandler("hide", (Action<string>)(actor => SetActorVisible(actor, false)));
        runner.AddCommandHandler("cutscene", (Func<string, YarnTask>)PlayCutscene);
        runner.AddCommandHandler("hard_reset", (Func<YarnTask>)HardReset);
        runner.AddCommandHandler("sfx", (Action<string>)PlaySound);
        runner.AddCommandHandler("sound_cut", (Action)(() => SetAudioPaused(true)));
        runner.AddCommandHandler("sound_resume", (Action)(() => SetAudioPaused(false)));
        runner.AddCommandHandler("freeze_frame", (Func<YarnTask>)PlayFreezeFrame);

        // Not built yet.
        runner.AddCommandHandler("quest_start", (Action<string, string>)((id, text) => Pending($"quest_start {id}: {text}")));
        runner.AddCommandHandler("quest_update", (Action<string, string>)((id, text) => Pending($"quest_update {id}: {text}")));
        runner.AddCommandHandler("quest_complete", (Action<string>)(id => Pending($"quest_complete {id}")));
    }

    // <<scene_transition "Classroom">>: does nothing when the location is this scene, so a node can open with the
    // scene it belongs to. Otherwise it loads that scene and the dialogue never continues here: the rest of the node
    // (such as a <<jump>>) is left to the next scene, which plays its own start node with YarnCutscene.
    // Location names ignore case, spaces, and underscores, so "music_room" loads the MusicRoom scene.
    // <<scene_transition "Classroom" "door">> also plays the Sounds entry "door" as the transition starts closing.
    // It plays on the CircleTransition, so the sound is not cut off when this scene unloads.
    private YarnTask SceneTransition(string location, string sound = "")
    {
        string key = Key(location);
        if (key == Key(SceneManager.GetActiveScene().name) || locationsInThisScene.Exists(l => Key(l) == key))
            return YarnTask.CompletedTask;

        string scene = FindBuildScene(key);
        if (scene == null)
        {
            Debug.LogError($"<<scene_transition>>: no scene in the Build Profile scene list matches \"{location.Trim()}\". Ending the dialogue.", this);
            runner.Stop().Forget();
            return YarnTask.CompletedTask;
        }

        if (!leaving)
        {
            leaving = true;
            AudioClip clip = string.IsNullOrWhiteSpace(sound) ? null : FindClip(sound, "scene_transition");
            if (CircleTransition.Instance != null)
                CircleTransition.Instance.LoadScene(scene, clip);
            else
                SceneManager.LoadScene(scene);
        }
        // Keeps the dialogue (and the frozen player) waiting until this scene unloads.
        return YarnTask.WaitUntil(() => this == null);
    }

    private static string Key(string name) => (name ?? "").Trim().Replace("_", "").Replace(" ", "").ToLowerInvariant();

    // Name of the Build Profile scene whose Key is key, or null when none is.
    private static string FindBuildScene(string key)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scene = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            if (Key(scene) == key) return scene;
        }
        return null;
    }

    // <<advance_time "lunch">>: covers the screen, switches the time slot (ActiveDuringTime objects follow), uncovers it.
    private YarnTask AdvanceTime(string slot)
    {
        return Fade(() =>
        {
            TimeOfDay = slot.Trim();
            Debug.Log($"<<advance_time>>: it is now {TimeOfDay}.", this);
            TimeOfDayChanged?.Invoke();
        });
    }

    // Closes and reopens the CircleTransition without loading a scene; whileCovered runs while the screen is covered.
    private YarnTask Fade(Action whileCovered)
    {
        CircleTransition transition = CircleTransition.Instance;
        if (transition == null || !transition.Cover(whileCovered, fadeHoldSeconds))
        {
            whileCovered?.Invoke();
            return YarnTask.CompletedTask;
        }
        return YarnTask.WaitUntil(() => transition == null || !transition.IsTransitioning);
    }

    // <<show "Fal">> / <<hide "Fal">>: turns the character's GameObject on or off.
    private void SetActorVisible(string actorName, bool visible)
    {
        string key = actorName.Trim();
        Actor actor = actors.Find(a => a.name.Trim() == key);
        GameObject target = actor?.target;
        if (target == null && !visible && DialogSpeaker.TryFind(key, out DialogSpeaker speaker))
            target = speaker.gameObject;
        if (target == null)
        {
            Debug.LogWarning($"<<{(visible ? "show" : "hide")}>>: no actor named \"{key}\" in {name}'s Actors list.", this);
            return;
        }
        target.SetActive(visible);
    }

    // <<cutscene "anin_joins_table">>: plays the matching Timeline and waits for it to finish.
    private YarnTask PlayCutscene(string id)
    {
        Cutscene cutscene = cutscenes.Find(c => c.id.Trim() == id.Trim());
        if (cutscene == null || cutscene.director == null)
        {
            Pending($"cutscene {id}");
            return YarnTask.CompletedTask;
        }
        PlayableDirector director = cutscene.director;
        director.time = 0;
        director.Play();
        return YarnTask.WaitUntil(() => director == null || director.state != PlayState.Playing);
    }

    private YarnTask PlayFreezeFrame()
    {
        if (freezeFrame == null)
        {
            Debug.LogWarning($"<<freeze_frame>>: {name} has no Freeze Frame assigned.", this);
            return YarnTask.CompletedTask;
        }
        return freezeFrame.Play();
    }

    // <<hard_reset>>: white flash, then the day starts over: morning again, start nodes play again,
    // and Scene After Hard Reset loads. Yarn variables are kept.
    private async YarnTask HardReset()
    {
        if (freezeFrame != null) await freezeFrame.Play();
        else Debug.LogWarning($"<<hard_reset>>: {name} has no Freeze Frame assigned, so there is no white flash.", this);

        LoopsCompleted++;
        TimeOfDay = DefaultTimeOfDay;
        TimeOfDayChanged?.Invoke();
        YarnCutscene.ForgetPlayed();
        Debug.Log($"<<hard_reset>>: loop {LoopsCompleted} ended.", this);
        // A frozen world stays frozen until the scene unloads (FreezeFrame releases it then).
        if (string.IsNullOrWhiteSpace(sceneAfterHardReset))
        {
            if (freezeFrame != null) freezeFrame.Release();
            return;
        }
        await SceneTransition(sceneAfterHardReset, "");
    }

    // Clip of the Sounds entry named soundName, or null (with a warning naming command) when there is none.
    private AudioClip FindClip(string soundName, string command)
    {
        Sound sound = sounds.Find(s => s.name == soundName);
        if (sound == null || sound.clip == null)
        {
            Debug.LogWarning($"<<{command}>>: no clip named \"{soundName}\" in {name}'s Sounds list.", this);
            return null;
        }
        return sound.clip;
    }

    private void PlaySound(string soundName)
    {
        AudioClip clip = FindClip(soundName, "sfx");
        if (clip == null) return;
        if (sfxSource == null)
        {
            Debug.LogWarning($"<<sfx>>: {name} has no Audio Source.", this);
            return;
        }
        sfxSource.PlayOneShot(clip);
    }

    // Pauses every Audio Source in the game. AudioListener.pause outlives the scene, so it is undone on unload.
    private void SetAudioPaused(bool paused)
    {
        AudioListener.pause = paused;
        pausedAudio = paused;
    }

    private void Pending(string command) => Debug.Log($"<<{command}>> (not implemented yet)", this);

    private void OnDestroy()
    {
        if (pausedAudio) AudioListener.pause = false;
    }
}
