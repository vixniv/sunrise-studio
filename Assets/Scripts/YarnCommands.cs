using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;

// Registers the custom <<commands>> used in the Yarn scripts on this scene's Dialogue Runner.
// Put it on the same GameObject as the Dialogue Runner, in every scene that runs Yarn dialogue.
// Yarn stops on any command nobody registered, so every command in loop01.yarn is registered here;
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

    [SerializeField] private DialogueRunner runner;
    [Tooltip("Other location names in <<scene_load>> that mean this scene. This scene's own name always counts.")]
    [SerializeField] private List<string> locationsInThisScene = new List<string>();
    [Tooltip("Plays <<sfx>> sounds. Leave empty to use an Audio Source on this object.")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private List<Sound> sounds = new List<Sound>();

    private bool leaving;
    private bool pausedAudio;

    // Last time of day passed to <<scene_load>>; it carries over to the next scene.
    public static string TimeOfDay { get; private set; } = "";

    private void Awake()
    {
        if (runner == null) runner = GetComponent<DialogueRunner>();
        if (runner == null)
        {
            Debug.LogError($"{name} has no Dialogue Runner assigned.", this);
            return;
        }
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();

        runner.AddCommandHandler("scene_load", (Func<string, string, YarnTask>)SceneLoad);
        runner.AddCommandHandler("sfx", (Action<string>)PlaySound);
        // The player gets control back when the node ends, so there is nothing to do here.
        runner.AddCommandHandler("give_control", (Action)(() => { }));
        runner.AddCommandHandler("sound_cut", (Action)(() => SetAudioPaused(true)));
        runner.AddCommandHandler("sound_resume", (Action)(() => SetAudioPaused(false)));

        // Not built yet.
        runner.AddCommandHandler("title_card", (Action)(() => Pending("title_card")));
        runner.AddCommandHandler("quest_start", (Action<string, string>)((id, text) => Pending($"quest_start {id}: {text}")));
        runner.AddCommandHandler("quest_update", (Action<string, string>)((id, text) => Pending($"quest_update {id}: {text}")));
        runner.AddCommandHandler("quest_check", (Action<string, string>)((id, item) => Pending($"quest_check {id}: {item}")));
        runner.AddCommandHandler("quest_strike", (Action<string>)(id => Pending($"quest_strike {id}")));
        runner.AddCommandHandler("quest_complete", (Action<string>)(id => Pending($"quest_complete {id}")));
        runner.AddCommandHandler("minimap_show", (Action)(() => Pending("minimap_show")));
        runner.AddCommandHandler("minimap_set_marker", (Action<string>)(place => Pending($"minimap_set_marker {place}")));
        runner.AddCommandHandler("minimap_set_event_marker", (Action<string>)(place => Pending($"minimap_set_event_marker {place}")));
        runner.AddCommandHandler("companion_join", (Action<string>)(who => Pending($"companion_join {who}")));
        runner.AddCommandHandler("search_mode_start", (Action<string>)(id => Pending($"search_mode_start {id}")));
        runner.AddCommandHandler("search_mode_end", (Action<string>)(id => Pending($"search_mode_end {id}")));
        runner.AddCommandHandler("freeze_frame", (Action)(() => Pending("freeze_frame")));
        runner.AddCommandHandler("loop_end", (Action<int>)(loop => Pending($"loop_end {loop}")));
    }

    // <<scene_load "Dorm" "Morning">>: does nothing when the location is this scene, so a node can open with the
    // scene it belongs to. Otherwise it loads that scene and the dialogue never continues here: the rest of the node
    // (such as a <<jump>>) is left to the next scene, which plays its own start node.
    private YarnTask SceneLoad(string location, string timeOfDay)
    {
        TimeOfDay = timeOfDay;
        string scene = location.Trim();
        if (scene == SceneManager.GetActiveScene().name || locationsInThisScene.Contains(scene))
            return YarnTask.CompletedTask;

        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"<<scene_load>>: scene \"{scene}\" is not in the Build Profile scene list. Ending the dialogue.", this);
            runner.Stop().Forget();
            return YarnTask.CompletedTask;
        }

        if (!leaving)
        {
            leaving = true;
            if (CircleTransition.Instance != null)
                CircleTransition.Instance.LoadScene(scene);
            else
                SceneManager.LoadScene(scene);
        }
        // Keeps the dialogue (and the frozen player) waiting until this scene unloads.
        return YarnTask.WaitUntil(() => this == null);
    }

    private void PlaySound(string soundName)
    {
        Sound sound = sounds.Find(s => s.name == soundName);
        if (sound == null || sound.clip == null)
        {
            Debug.LogWarning($"<<sfx>>: no clip named \"{soundName}\" in {name}'s Sounds list.", this);
            return;
        }
        if (sfxSource == null)
        {
            Debug.LogWarning($"<<sfx>>: {name} has no Audio Source.", this);
            return;
        }
        sfxSource.PlayOneShot(sound.clip);
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
