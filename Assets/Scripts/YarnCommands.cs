using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Yarn.Unity;

// Registers the custom <<commands>> used in the Yarn scripts on this scene's Dialogue Runner.
// Put it on the same GameObject as the Dialogue Runner, in every scene that runs Yarn dialogue.
// Yarn stops on any command nobody registered, so every command in scene1.yarn to scene8.yarn is registered here;
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
        [Tooltip("Name used by Yarn actor commands such as <<show>>, <<hide>>, and <<move_character>>.")]
        public string name;
        [Tooltip("The character's root GameObject in this scene.")]
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
    [Tooltip("Scene <<end_gap>> and <<soft_reset>> load: the room where every day and every gap starts. Must be in the Build Profile scene list.")]
    [SerializeField] private string dormScene = "Dorm";
    [Tooltip("Seconds the screen stays covered during <<advance_time>> and <<fade>>.")]
    [SerializeField, Min(0f)] private float fadeHoldSeconds = 0.5f;

    public const string DefaultTimeOfDay = "morning";
    // Time slot of the gaps, set by <<hard_reset>> and <<soft_reset>>; <<end_gap>> goes back to morning.
    public const string GapTimeOfDay = "midnight";

    // While true, SceneDoors with a Story Node run that node instead of leaving freely.
    // Turned off every time a scene loads, so a start node turns it on only for the scene whose story needs the door.
    public const string StoryExitVariable = "$story_exit";

    private bool leaving;
    private bool pausedAudio;
    private YarnDialogPresenter presenter;
    private readonly HashSet<GameObject> movingActors = new HashSet<GameObject>();

    private static readonly int IsWalkingParameter = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningParameter = Animator.StringToHash("IsRunning");

    private enum MoveAnimation
    {
        None,
        Walk,
        Run
    }

    // Time slot set by <<advance_time>> (morning, lunch, after_school) or a reset (midnight). It carries over to the next scene.
    public static string TimeOfDay { get; private set; } = DefaultTimeOfDay;

    // Number of <<hard_reset>>s and <<soft_reset>>s so far; 0 until the first loop ends.
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

    // Playtesting only (Tools > Playtest Start): jumps to a time slot without <<advance_time>> or its fade.
    public static void SetTimeOfDayForTesting(string slot)
    {
        TimeOfDay = slot.Trim();
        TimeOfDayChanged?.Invoke();
    }

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
        runner.AddCommandHandler("scene_transition_at_x", (Func<string, float, string, YarnTask>)SceneTransitionAtX);
        runner.AddCommandHandler("advance_time", (Func<string, YarnTask>)AdvanceTime);
        runner.AddCommandHandler("advance_time_silent", (Func<string, YarnTask>)AdvanceTimeSilent);
        runner.AddCommandHandler("advance_time_to_scene", (Func<string, string, YarnTask>)AdvanceTimeToScene);
        runner.AddCommandHandler("advance_time_to_scene_at_x", (Func<string, string, float, string, YarnTask>)AdvanceTimeToSceneAtX);
        runner.AddCommandHandler("advance_time_teleport_character", (Func<string, string, float, string, YarnTask>)AdvanceTimeTeleportCharacter);
        runner.AddCommandHandler("delay", (Func<float, YarnTask>)Delay);
        runner.AddCommandHandler("fade", (Func<string, YarnTask>)FadeCommand);
        runner.AddCommandHandler("teleport_character", (Action<string, float, string>)TeleportCharacter);
        runner.AddCommandHandler("fade_teleport_character", (Func<string, float, string, YarnTask>)FadeTeleportCharacter);
        runner.AddCommandHandler("outfit", (Action<string, string>)SetOutfit);
        runner.AddCommandHandler("fade_outfit", (Func<string, string, YarnTask>)((actor, outfit) => Fade(() => SetOutfit(actor, outfit))));
        runner.AddCommandHandler("face_character", (Action<string, string>)FaceCharacter);
        runner.AddCommandHandler("sit", (Action<string>)(actor => SetSitting(actor, true)));
        runner.AddCommandHandler("stand", (Action<string>)(actor => SetSitting(actor, false)));
        runner.AddCommandHandler("show", (Action<string>)(actor => SetActorVisible(actor, true)));
        runner.AddCommandHandler("hide", (Action<string>)(actor => SetActorVisible(actor, false)));
        runner.AddCommandHandler("close_dialogue_bubble", (Action)CloseDialogueBubble);
        // Same as show and hide; the later scenes use these names.
        runner.AddCommandHandler("npc_enter", (Action<string>)(actor => SetActorVisible(actor, true)));
        runner.AddCommandHandler("npc_exit", (Action<string>)(actor => SetActorVisible(actor, false)));
        runner.AddCommandHandler("move_character", (Func<string, float, float, string, YarnTask>)MoveCharacter);
        runner.AddCommandHandler("move_character_to_actor", (Func<string, string, float, float, string, YarnTask>)MoveCharacterToActor);
        runner.AddCommandHandler("start_character_move", (Action<string, float, float, string, float>)StartCharacterMove);
        runner.AddCommandHandler("start_character_move_to_actor", (Action<string, string, float, float, string, float>)StartCharacterMoveToActor);
        runner.AddCommandHandler("wait_for_character_moves", (Func<YarnTask>)WaitForCharacterMoves);
        runner.AddCommandHandler("open_door", (Func<string, YarnTask>)(door => AnimateDoor(door, true)));
        runner.AddCommandHandler("close_door", (Func<string, YarnTask>)(door => AnimateDoor(door, false)));
        runner.AddCommandHandler("door_enter", (Func<string, string, YarnTask>)((actor, door) => DoorEnter(actor, door, null)));
        runner.AddCommandHandler("door_enter_to", (Func<string, string, float, YarnTask>)((actor, door, x) => DoorEnter(actor, door, x)));
        runner.AddCommandHandler("door_exit", (Func<string, string, YarnTask>)DoorExit);
        runner.AddCommandHandler("cutscene", (Func<string, YarnTask>)PlayCutscene);
        runner.AddCommandHandler("cutscene_image", (Func<string, YarnTask>)(sprite => ShowCutsceneImage(sprite, false)));
        runner.AddCommandHandler("cutscene_image_keep_text", (Func<string, YarnTask>)(sprite => ShowCutsceneImage(sprite, true)));
        runner.AddCommandHandler("cutscene_end", (Func<YarnTask>)EndCutsceneImages);
        runner.AddCommandHandler("hard_reset", (Func<YarnTask>)HardReset);
        runner.AddCommandHandler("hard_reset_from_cutscene", (Func<YarnTask>)HardResetFromCutscene);
        runner.AddCommandHandler("soft_reset", (Func<YarnTask>)SoftReset);
        runner.AddCommandHandler("end_gap", (Func<YarnTask>)EndGap);
        runner.AddCommandHandler("sfx", (Action<string>)PlaySound);
        runner.AddCommandHandler("sound_cut", (Action)(() => SetAudioPaused(true)));
        runner.AddCommandHandler("sound_resume", (Action)(() => SetAudioPaused(false)));
        runner.AddCommandHandler("freeze_frame", (Func<YarnTask>)PlayFreezeFrame);

        runner.AddCommandHandler("quest_start", (Action<string, string>)((id, text) =>
        {
            SunriseStudio.UI.QuestTracker.StartQuest(id, text);
            SunriseAudio.Play("ui_quest_popup");
            Debug.Log($"Quest started: {id} — {text}", this);
        }));
        runner.AddCommandHandler("quest_update", (Action<string, string>)((id, text) =>
        {
            SunriseStudio.UI.QuestTracker.UpdateQuest(id, text);
            SunriseAudio.Play("ui_quest_popup");
            Debug.Log($"Quest updated: {id} — {text}", this);
        }));
        runner.AddCommandHandler("quest_complete", (Action<string>)(id =>
        {
            SunriseStudio.UI.QuestTracker.CompleteQuest(id);
            SunriseAudio.Play("ui_quest_complete");
            Debug.Log($"Quest completed: {id}", this);
        }));
        runner.AddCommandHandler("quest_clear", (Action)(() => SunriseStudio.UI.QuestTracker.ClearQuest()));
        runner.AddCommandHandler("event_marker", (Action<string, bool>)((id, visible) =>
        {
            if (visible) SunriseAudio.Play("ui_achievement");
            Pending($"event_marker {id} {visible}");
        }));
        runner.AddCommandHandler("give_item", (Action<string>)GiveItem);
        runner.AddCommandHandler("remove_item", (Action<string>)RemoveItem);
        runner.AddCommandHandler("open_locker", (Action<string>)(id =>
        {
            SunriseAudio.PlaySequence("locker_unlock", "locker_open");
            Pending($"open_locker {id}");
        }));
        runner.AddCommandHandler("close_locker", (Action<string>)(id =>
        {
            SunriseAudio.Play("locker_close");
            Pending($"close_locker {id}");
        }));
        runner.AddCommandHandler("blur_writing", (Action<string>)(id =>
        {
            SunriseAudio.PlaySequence("paper_handle", "writing_pencil");
            Pending($"blur_writing {id}");
        }));
        runner.AddCommandHandler("blur_action", (Action<string>)(id =>
        {
            SunriseAudio.Play(Key(id).Contains("bolt") || Key(id).Contains("flagpole")
                ? "bolt_unscrew"
                : "ui_interact");
            Pending($"blur_action {id}");
        }));
        runner.AddCommandHandler("gap_mode", (Action<bool>)(on =>
        {
            if (on) SunriseAudio.Play("gap_transition");
            Pending($"gap_mode {on}");
        }));
        runner.AddCommandHandler("gap_begin_awake", (Action)(() => Pending("gap_begin_awake")));
        runner.AddCommandHandler("memory_montage", (Action<bool>)(on =>
        {
            SunriseAudio.Play(on ? "memory_transition_in" : "memory_transition_back");
            Pending($"memory_montage {on}");
        }));
        runner.AddCommandHandler("stay_awake", (Action<string>)(result =>
        {
            SunriseAudio.PlaySequence(Key(result) == "success"
                ? new[] { "drowsiness", "snap_awake" }
                : new[] { "drowsiness" });
            Pending($"stay_awake {result}");
        }));
        runner.AddCommandHandler("edit_calendar", (Action)(() =>
        {
            SunriseAudio.PlaySequence("calendar_paper_handle", "calendar_writing");
            Pending("edit_calendar");
        }));
        runner.AddCommandHandler("draw_sketchbook", (Action)(() =>
        {
            SunriseAudio.PlaySequence("sketchbook_page_turn", "pencil_sketching");
            Pending("draw_sketchbook");
        }));
        runner.AddCommandHandler("ticking_clock", (Action)(() =>
        {
            SunriseAudio.PlayLoop("clock_ticking");
            Pending("ticking_clock");
        }));
        runner.AddCommandHandler("end_card", (Action<string>)(text =>
        {
            SunriseAudio.StopLoop();
            SunriseAudio.Play("end_chapter_sting");
            Pending($"end_card {text}");
        }));
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
        return LoadLocation(location, sound);
    }

    // <<scene_transition_at_x "Library" -8 "right">>: loads a room with the player at X before its first frame.
    // Use "sit" to seat the player at that X before the transition opens.
    private YarnTask SceneTransitionAtX(string location, float destinationX, string facing)
    {
        if (leaving)
            return YarnTask.CompletedTask;

        if (float.IsNaN(destinationX) || float.IsInfinity(destinationX))
        {
            Debug.LogError("<<scene_transition_at_x>>: destination X must be finite.", this);
            return YarnTask.CompletedTask;
        }

        string facingKey = (facing ?? string.Empty).Trim().ToLowerInvariant();
        if (facingKey != "left" && facingKey != "right" && facingKey != "sit")
        {
            Debug.LogError("<<scene_transition_at_x>>: facing must be \"left\", \"right\", or \"sit\".", this);
            return YarnTask.CompletedTask;
        }

        string scene = FindBuildScene(Key(location));
        if (scene == null)
        {
            Debug.LogError($"<<scene_transition_at_x>>: no Build Profile scene matches \"{location}\".", this);
            return YarnTask.CompletedTask;
        }

        if (scene == SceneManager.GetActiveScene().name)
        {
            Debug.LogError("<<scene_transition_at_x>>: destination must be a different scene.", this);
            return YarnTask.CompletedTask;
        }

        DoorSpawn.SetNextSpawn(scene, destinationX, facingKey != "left", facingKey == "sit");
        return LoadLocation(scene, "");
    }

    // Loads location even when it is this scene, so a reset reloads the room with the objects of the new $chapter_scene.
    private YarnTask LoadLocation(string location, string sound, Action whileCovered = null)
    {
        string scene = FindBuildScene(Key(location));
        if (scene == null)
        {
            Debug.LogError($"<<scene_transition>>: no scene in the Build Profile scene list matches \"{location.Trim()}\". Ending the dialogue.", this);
            runner.Stop().Forget();
            return YarnTask.CompletedTask;
        }

        if (!leaving)
        {
            leaving = true;
            HideDialogueLine();
            bool doorTransition = Key(sound) == "door";
            AudioClip clip = doorTransition
                ? SunriseAudio.GetClip("door_open")
                : string.IsNullOrWhiteSpace(sound) ? null : FindClip(sound, "scene_transition");
            if (doorTransition)
                SunriseAudio.QueueDoorClose();

            if (CircleTransition.Instance != null)
                CircleTransition.Instance.LoadScene(scene, clip, 1f, whileCovered);
            else
            {
                if (doorTransition)
                    SunriseAudio.Play("door_open");
                whileCovered?.Invoke();
                SceneManager.LoadScene(scene);
            }
        }
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
        SunriseAudio.Play(Key(slot) == Key(GapTimeOfDay)
            ? "night_transition"
            : "day_transition");
        return AdvanceTimeSilent(slot);
    }

    // Covers the screen and changes time without starting a sound cue.
    // Yarn can play a sound after the screen opens with <<sfx>>.
    private YarnTask AdvanceTimeSilent(string slot)
    {
        return Fade(() => ApplyTimeOfDay(slot));
    }

    // <<advance_time_to_scene "lunch" "Cafeteria">>: changes time while the screen is covered,
    // then loads the destination before uncovering it. This uses one transition for both actions.
    private YarnTask AdvanceTimeToScene(string slot, string location)
    {
        SunriseAudio.Play(Key(slot) == Key(GapTimeOfDay)
            ? "night_transition"
            : "day_transition");
        return LoadLocation(location, "", () => ApplyTimeOfDay(slot));
    }

    // <<advance_time_to_scene_at_x "lunch" "Cafeteria" -5.5 "sit">>: places and seats
    // Anin before the new scene's first visible frame, using the same time-change transition.
    private YarnTask AdvanceTimeToSceneAtX(string slot, string location, float destinationX, string facing)
    {
        if (leaving)
            return YarnTask.CompletedTask;

        if (float.IsNaN(destinationX) || float.IsInfinity(destinationX))
        {
            Debug.LogError("<<advance_time_to_scene_at_x>>: destination X must be finite.", this);
            return YarnTask.CompletedTask;
        }

        string facingKey = (facing ?? string.Empty).Trim().ToLowerInvariant();
        if (facingKey != "left" && facingKey != "right" && facingKey != "sit")
        {
            Debug.LogError("<<advance_time_to_scene_at_x>>: facing must be \"left\", \"right\", or \"sit\".", this);
            return YarnTask.CompletedTask;
        }

        string scene = FindBuildScene(Key(location));
        if (scene == null)
        {
            Debug.LogError($"<<advance_time_to_scene_at_x>>: no Build Profile scene matches \"{location}\".", this);
            return YarnTask.CompletedTask;
        }

        DoorSpawn.SetNextSpawn(scene, destinationX, facingKey != "left", facingKey == "sit");
        return AdvanceTimeToScene(slot, scene);
    }

    // Changes the time and places an actor while the same transition covers the screen.
    private YarnTask AdvanceTimeTeleportCharacter(string slot, string actorName, float destinationX, string facing)
    {
        SunriseAudio.Play(Key(slot) == Key(GapTimeOfDay)
            ? "night_transition"
            : "day_transition");
        return Fade(() =>
        {
            ApplyTimeOfDay(slot);
            TeleportCharacter(actorName, destinationX, facing);
        });
    }

    private void ApplyTimeOfDay(string slot)
    {
        TimeOfDay = slot.Trim();
        Debug.Log($"<<advance_time>>: it is now {TimeOfDay}.", this);
        TimeOfDayChanged?.Invoke();
    }

    // <<delay 1.5>>: pauses this Yarn node for 1.5 real seconds, even when the world is frozen.
    private YarnTask Delay(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
        {
            Debug.LogError("<<delay>>: seconds must be a finite number of 0 or greater.", this);
            return YarnTask.CompletedTask;
        }

        if (seconds == 0f)
            return YarnTask.CompletedTask;

        double resumeAt = Time.unscaledTimeAsDouble + seconds;
        return YarnTask.WaitUntil(() => this == null || Time.unscaledTimeAsDouble >= resumeAt);
    }

    // <<fade>>: covers and uncovers the screen. <<fade "out">> and <<fade "in">> only log for now:
    // the scene transitions next to them already cover and uncover the screen.
    private YarnTask FadeCommand(string direction = "")
    {
        if (string.IsNullOrWhiteSpace(direction)) return Fade(null);
        Pending($"fade {direction.Trim()}");
        return YarnTask.CompletedTask;
    }

    // <<fade_teleport_character "Anin" -8 "left">>: changes X and facing while the screen is covered.
    private YarnTask FadeTeleportCharacter(string actorName, float destinationX, string facing)
    {
        return Fade(() => TeleportCharacter(actorName, destinationX, facing));
    }

    // <<outfit "Anin" "uniform">> / <<outfit "Anin" "casual">>: swaps the character's art right away (CharacterOutfit).
    // <<fade_outfit "Anin" "uniform">> does the same while the screen is covered.
    private void SetOutfit(string characterName, string outfit)
    {
        CharacterOutfit target = CharacterOutfit.Find(characterName);
        if (target == null)
        {
            Debug.LogError($"<<outfit>>: no enabled CharacterOutfit with Character Name \"{characterName}\" in this scene.", this);
            return;
        }
        target.Wear(outfit);
    }

    // <<teleport_character "Anin" -8 "left">>: changes world X and facing immediately, keeping Y and Z.
    // "sit" seats the character; "stand" gets them up without changing their facing.
    // <<fade_teleport_character>> applies either pose while the screen is covered.
    private void TeleportCharacter(string actorName, float destinationX, string facing)
    {
        GameObject target = FindActorTarget(actorName, "teleport_character");
        if (target == null)
            return;

        if (float.IsNaN(destinationX) || float.IsInfinity(destinationX))
        {
            Debug.LogError($"<<teleport_character>>: destination X must be finite for \"{target.name}\".", this);
            return;
        }

        if (movingActors.Contains(target))
        {
            Debug.LogError($"<<teleport_character>>: \"{target.name}\" is still moving. Wait for its move to finish first.", this);
            return;
        }

        string facingKey = (facing ?? string.Empty).Trim().ToLowerInvariant();
        if (facingKey != "left" && facingKey != "right" && facingKey != "keep" && facingKey != "sit" && facingKey != "stand")
        {
            Debug.LogError($"<<teleport_character>>: facing must be \"left\", \"right\", \"keep\", \"sit\", or \"stand\" for \"{target.name}\".", this);
            return;
        }

        Vector3 position = target.transform.position;
        target.transform.position = new Vector3(destinationX, position.y, position.z);
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body != null)
            body.position = new Vector2(destinationX, body.position.y);
        if (facingKey == "sit")
            SetSitting(target, true, "teleport_character");
        else if (facingKey == "stand")
            SetSitting(target, false, "teleport_character");
        else if (facingKey != "keep")
            FaceActor(target, facingKey == "right" ? 1f : -1f);
    }

    // <<face_character "Anin" "left">>: changes only the actor's sprite facing.
    private void FaceCharacter(string actorName, string facing)
    {
        GameObject target = FindActorTarget(actorName, "face_character");
        if (target == null)
            return;

        string facingKey = (facing ?? string.Empty).Trim().ToLowerInvariant();
        if (facingKey != "left" && facingKey != "right")
        {
            Debug.LogError($"<<face_character>>: facing must be \"left\" or \"right\" for \"{target.name}\".", this);
            return;
        }

        FaceActor(target, facingKey == "right" ? 1f : -1f);
    }

    // <<sit "Dira">> / <<stand "Dira">>: plays or stops the SitPose on the character's Visual child.
    // Sits in place: put the character on the chair first with <<teleport_character>> or <<move_character>>.
    // Looks for the enabled DialogSpeaker of that name first (like <<hide>>), then the Actors list.
    private void SetSitting(string actorName, bool sit)
    {
        string command = sit ? "sit" : "stand";
        string key = (actorName ?? string.Empty).Trim();
        GameObject target = DialogSpeaker.TryFind(key, out DialogSpeaker speaker)
            ? speaker.gameObject
            : FindActorTarget(key, command);
        if (target != null)
            SetSitting(target, sit, command);
    }

    private void SetSitting(GameObject target, bool sit, string command)
    {
        if (sit && movingActors.Contains(target))
        {
            Debug.LogError($"<<{command}>>: \"{target.name}\" is still moving. Wait for its move to finish before sitting.", this);
            return;
        }

        SitPose[] poses = target.GetComponentsInChildren<SitPose>(true);
        if (poses.Length == 0)
        {
            Debug.LogError($"<<{command}>>: \"{target.name}\" has no SitPose on its Visual child.", this);
            return;
        }

        foreach (SitPose pose in poses)
        {
            if (sit) pose.Sit();
            else pose.Stand();
        }
    }

    private static void StandUp(GameObject target)
    {
        foreach (SitPose pose in target.GetComponentsInChildren<SitPose>(true))
            pose.Stand();
        // Anin still in bed after her wake-up gets out of bed too.
        foreach (WakeUpPose pose in target.GetComponentsInChildren<WakeUpPose>(true))
            pose.GetUp();
    }

    // Closes and reopens the CircleTransition without loading a scene; whileCovered runs while the screen is covered.
    private YarnTask Fade(Action whileCovered)
    {
        HideDialogueLine();
        CircleTransition transition = CircleTransition.Instance;
        if (transition == null || !transition.Cover(whileCovered, fadeHoldSeconds))
        {
            whileCovered?.Invoke();
            return YarnTask.CompletedTask;
        }
        return YarnTask.WaitUntil(() => transition == null || !transition.IsTransitioning);
    }

    // Takes the last line off screen before a transition, so it is not still showing when the screen opens again.
    private void HideDialogueLine()
    {
        if (presenter == null && runner != null) presenter = runner.GetComponentInChildren<YarnDialogPresenter>();
        if (presenter == null) presenter = FindAnyObjectByType<YarnDialogPresenter>();
        if (presenter != null) presenter.HideCurrentLine();
    }

    // <<close_dialogue_bubble>>: hides the current dialogue UI before a cutscene action.
    private void CloseDialogueBubble()
    {
        YarnDialogPresenter presenter = GetComponent<YarnDialogPresenter>();
        if (presenter == null)
        {
            Debug.LogError("<<close_dialogue_bubble>>: no YarnDialogPresenter on the Dialogue object.", this);
            return;
        }

        presenter.HideDialogueForCutscene();
    }

    // <<show "Fal">> / <<hide "Fal">>: turns the character's GameObject on or off.
    // Hiding prefers the enabled DialogSpeaker of that name, so a character placed in a story group
    // can leave even when the Actors entry points to a hidden copy that walks in on other days.
    private void SetActorVisible(string actorName, bool visible)
    {
        string key = actorName.Trim();
        GameObject target = null;
        if (!visible && DialogSpeaker.TryFind(key, out DialogSpeaker speaker))
            target = speaker.gameObject;
        if (target == null)
            target = actors.Find(a => a.name.Trim() == key)?.target;
        if (target == null)
        {
            Debug.LogWarning($"<<{(visible ? "show" : "hide")}>>: no actor named \"{key}\" in {name}'s Actors list.", this);
            return;
        }
        target.SetActive(visible);
    }

    // <<move_character "Anin" 12 3 "walk">>: moves one Actor to a world X and waits for arrival.
    private YarnTask MoveCharacter(string actorName, float destinationX, float speed, string animation)
    {
        GameObject target = FindActorTarget(actorName, "move_character");
        if (target == null || !TryStartCharacterMove(target, destinationX, speed, animation, 0f))
            return YarnTask.CompletedTask;

        return YarnTask.WaitUntil(() => this == null || target == null || !movingActors.Contains(target));
    }

    // <<move_character_to_actor "Fal" "Anin" -1 3 "walk">>: walks to Anin's current X minus one unit.
    private YarnTask MoveCharacterToActor(string actorName, string referenceActorName, float offsetX, float speed, string animation)
    {
        GameObject reference = FindActorTarget(referenceActorName, "move_character_to_actor");
        if (reference == null)
            return YarnTask.CompletedTask;

        if (float.IsNaN(offsetX) || float.IsInfinity(offsetX))
        {
            Debug.LogError("<<move_character_to_actor>>: X offset must be finite.", this);
            return YarnTask.CompletedTask;
        }

        return MoveCharacter(actorName, reference.transform.position.x + offsetX, speed, animation);
    }

    // <<start_character_move "Dira" 20 3 "walk" 0.5>>: starts a move after a delay and returns immediately.
    private void StartCharacterMove(string actorName, float destinationX, float speed, string animation, float delay)
    {
        GameObject target = FindActorTarget(actorName, "start_character_move");
        if (target != null)
            TryStartCharacterMove(target, destinationX, speed, animation, delay);
    }

    // <<start_character_move_to_actor "Fal" "Anin" -1 3 "walk" 0.5>>: starts a move to Anin's current X minus one.
    private void StartCharacterMoveToActor(string actorName, string referenceActorName, float offsetX, float speed, string animation, float delay)
    {
        GameObject reference = FindActorTarget(referenceActorName, "start_character_move_to_actor");
        if (reference == null)
            return;

        if (float.IsNaN(offsetX) || float.IsInfinity(offsetX))
        {
            Debug.LogError("<<start_character_move_to_actor>>: X offset must be finite.", this);
            return;
        }

        StartCharacterMove(actorName, reference.transform.position.x + offsetX, speed, animation, delay);
    }

    // <<wait_for_character_moves>>: waits for all non-blocking character moves to finish.
    private YarnTask WaitForCharacterMoves()
    {
        return YarnTask.WaitUntil(() => this == null || movingActors.Count == 0);
    }

    // <<open_door "Dorm">> / <<close_door "Dorm">>: plays the DoorAnimator with that Door Id and waits for it.
    private YarnTask AnimateDoor(string doorId, bool open)
    {
        DoorAnimator door = FindDoor(doorId, open ? "open_door" : "close_door");
        if (door == null)
            return YarnTask.CompletedTask;

        if (open) door.Open();
        else door.Close();
        return YarnTask.WaitUntil(() => this == null || door == null || !door.IsAnimating);
    }

    // <<door_enter "Kirana" "Dorm">>: the door opens, Kirana appears in the doorway and walks to where she was
    // placed in the scene (or where she stood before her last <<door_exit>>), and the door closes behind her.
    // <<door_enter_to "Kirana" "Dorm" -6.5>> walks her to world X -6.5 instead.
    private YarnTask DoorEnter(string actorName, string doorId, float? destinationX)
    {
        string command = destinationX.HasValue ? "door_enter_to" : "door_enter";
        GameObject target = FindActorTarget(actorName, command);
        DoorAnimator door = FindDoor(doorId, command);
        if (target == null || !CanStartDoorWalk(target, command))
            return YarnTask.CompletedTask;
        if (door == null)
        {
            // No door set up yet: still bring the character in, like <<npc_enter>>.
            target.SetActive(true);
            return YarnTask.CompletedTask;
        }
        if (destinationX.HasValue && (float.IsNaN(destinationX.Value) || float.IsInfinity(destinationX.Value)))
        {
            Debug.LogError($"<<{command}>>: destination X must be finite for \"{target.name}\".", this);
            return YarnTask.CompletedTask;
        }

        float homeX = destinationX ?? RememberDoorHome(target);
        bool done = false;
        StartCoroutine(DoorEnterRoutine(target, door, homeX, () => done = true));
        return YarnTask.WaitUntil(() => this == null || done);
    }

    // <<door_exit "Kirana" "Dorm">>: Kirana walks to the doorway, the door opens, she disappears, and the door closes.
    // Like <<hide>>, it looks for the enabled DialogSpeaker of that name first, then the Actors list.
    private YarnTask DoorExit(string actorName, string doorId)
    {
        string key = (actorName ?? string.Empty).Trim();
        GameObject target = DialogSpeaker.TryFind(key, out DialogSpeaker speaker)
            ? speaker.gameObject
            : FindActorTarget(key, "door_exit");
        DoorAnimator door = FindDoor(doorId, "door_exit");
        if (target == null || !CanStartDoorWalk(target, "door_exit"))
            return YarnTask.CompletedTask;
        if (door == null)
        {
            // No door set up yet: still take the character out, like <<npc_exit>>.
            target.SetActive(false);
            return YarnTask.CompletedTask;
        }

        RememberDoorHome(target);
        bool done = false;
        StartCoroutine(DoorExitRoutine(target, door, () => done = true));
        return YarnTask.WaitUntil(() => this == null || done);
    }

    private IEnumerator DoorEnterRoutine(GameObject target, DoorAnimator door, float homeX, Action finished)
    {
        try
        {
            float doorX = door.WalkPointX;
            // Out of sight while the door opens, then in the doorway.
            target.SetActive(false);
            PlaceActorAtX(target, doorX);
            door.Open();
            while (door != null && door.IsAnimating)
                yield return null;
            if (target == null || door == null)
                yield break;

            target.SetActive(true);
            bool moving = Mathf.Abs(homeX - doorX) > 0.0001f &&
                          TryStartCharacterMove(target, homeX, door.WalkSpeed, "walk", 0f);
            // Close behind them once they are clear of the doorway.
            while (moving && target != null && door != null && movingActors.Contains(target) &&
                   Mathf.Abs(target.transform.position.x - doorX) < door.DoorwayDistance)
                yield return null;
            if (door != null)
                door.Close();
            while ((door != null && door.IsAnimating) || (target != null && movingActors.Contains(target)))
                yield return null;
        }
        finally
        {
            finished();
        }
    }

    private IEnumerator DoorExitRoutine(GameObject target, DoorAnimator door, Action finished)
    {
        try
        {
            float doorX = door.WalkPointX;
            bool moving = Mathf.Abs(target.transform.position.x - doorX) > 0.0001f &&
                          TryStartCharacterMove(target, doorX, door.WalkSpeed, "walk", 0f);
            // The door starts opening as they reach it.
            while (moving && target != null && door != null && movingActors.Contains(target) &&
                   Mathf.Abs(target.transform.position.x - doorX) > door.DoorwayDistance)
                yield return null;
            if (door != null)
                door.Open();
            while ((door != null && door.IsAnimating) || (target != null && movingActors.Contains(target)))
                yield return null;

            if (target != null)
                target.SetActive(false);
            if (door == null)
                yield break;
            door.Close();
            while (door != null && door.IsAnimating)
                yield return null;
        }
        finally
        {
            finished();
        }
    }

    // X each character walks back to on <<door_enter>>: where they first stood when a door command used them.
    private readonly Dictionary<GameObject, float> doorHomeX = new Dictionary<GameObject, float>();

    private float RememberDoorHome(GameObject target)
    {
        if (!doorHomeX.TryGetValue(target, out float x))
        {
            x = target.transform.position.x;
            doorHomeX[target] = x;
        }
        return x;
    }

    private bool CanStartDoorWalk(GameObject target, string command)
    {
        if (!movingActors.Contains(target))
            return true;
        Debug.LogError($"<<{command}>>: \"{target.name}\" is still moving. Wait for its move to finish first.", this);
        return false;
    }

    private static void PlaceActorAtX(GameObject target, float x)
    {
        Vector3 position = target.transform.position;
        target.transform.position = new Vector3(x, position.y, position.z);
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body != null)
            body.position = new Vector2(x, body.position.y);
    }

    private DoorAnimator FindDoor(string doorId, string commandName)
    {
        if (DoorAnimator.TryFind(doorId, out DoorAnimator door))
            return door;
        Debug.LogError($"<<{commandName}>>: no enabled DoorAnimator with Door Id \"{doorId}\" in this scene. " +
                       $"Door Ids found: {DoorAnimator.KnownIds()}.", this);
        return null;
    }

    private GameObject FindActorTarget(string actorName, string commandName)
    {
        string key = (actorName ?? string.Empty).Trim();
        Actor actor = actors != null ? actors.Find(candidate => candidate != null &&
            !string.IsNullOrWhiteSpace(candidate.name) &&
            string.Equals(candidate.name.Trim(), key, StringComparison.OrdinalIgnoreCase)) : null;

        if (actor == null || actor.target == null)
        {
            Debug.LogError($"<<{commandName}>>: no Actor named \"{key}\" with a target GameObject is assigned in {name}'s Actors list.", this);
            return null;
        }

        return actor.target;
    }

    private bool TryStartCharacterMove(GameObject target, float destinationX, float speed, string animation, float delay)
    {
        if (float.IsNaN(destinationX) || float.IsInfinity(destinationX))
        {
            Debug.LogError($"<<move_character>>: destination X must be a finite number for \"{target.name}\".", this);
            return false;
        }

        if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
        {
            Debug.LogError($"<<move_character>>: speed must be greater than 0 for \"{target.name}\".", this);
            return false;
        }

        if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0f)
        {
            Debug.LogError($"<<start_character_move>>: delay must be 0 or greater for \"{target.name}\".", this);
            return false;
        }

        if (movingActors.Contains(target))
        {
            Debug.LogError($"A character move is already active for \"{target.name}\". Wait for it to finish before starting another.", this);
            return false;
        }

        MoveAnimation moveAnimation = ParseMoveAnimation(animation);
        if (moveAnimation == MoveAnimation.None)
            Debug.LogError($"Move animation \"{animation}\" is not supported for \"{target.name}\". Use \"walk\" or \"run\"; the character will still move without an animation.", this);

        movingActors.Add(target);
        StartCoroutine(MoveCharacterRoutine(target, destinationX, speed, moveAnimation, delay));
        return true;
    }

    private static MoveAnimation ParseMoveAnimation(string animation)
    {
        string key = (animation ?? string.Empty).Trim().ToLowerInvariant();
        switch (key)
        {
            case "walk": return MoveAnimation.Walk;
            case "run": return MoveAnimation.Run;
            default: return MoveAnimation.None;
        }
    }

    private IEnumerator MoveCharacterRoutine(GameObject target, float destinationX, float speed, MoveAnimation moveAnimation, float delay)
    {
        Animator animator = null;
        SpriteFrameLoop[] frameLoops = null;
        bool animationStarted = false;
        PlayerMove[] playerMoves = null;
        bool[] playerMoveWasEnabled = null;
        Rigidbody2D[] bodies = null;
        bool[] bodyWasSimulated = null;

        try
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (target == null)
                yield break;

            // A seated character gets up before walking off.
            StandUp(target);

            float deltaX = destinationX - target.transform.position.x;
            if (Mathf.Abs(deltaX) <= 0.0001f)
                yield break;

            float facingDirection = Mathf.Sign(deltaX);
            FaceActor(target, facingDirection);

            animator = target.GetComponentInChildren<Animator>(true);
            // NPCs without an Animator (Dira, Kirana, Lila, Fal) animate with SpriteFrameLoop instead.
            if (animator == null)
                frameLoops = target.GetComponentsInChildren<SpriteFrameLoop>(true);
            animationStarted = TryStartMoveAnimation(target, animator, frameLoops, moveAnimation);

            // Pause player input and physics while moving by Transform so the cutscene reaches its exact X.
            playerMoves = target.GetComponentsInChildren<PlayerMove>(true);
            playerMoveWasEnabled = new bool[playerMoves.Length];
            for (int i = 0; i < playerMoves.Length; i++)
            {
                playerMoveWasEnabled[i] = playerMoves[i] != null && playerMoves[i].enabled;
                if (playerMoveWasEnabled[i])
                    playerMoves[i].enabled = false;
            }

            bodies = target.GetComponentsInChildren<Rigidbody2D>(true);
            bodyWasSimulated = new bool[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null)
                    continue;

                bodyWasSimulated[i] = bodies[i].simulated;
                if (bodyWasSimulated[i])
                {
                    bodies[i].linearVelocity = Vector2.zero;
                    bodies[i].simulated = false;
                }
            }

            while (target != null)
            {
                Vector3 position = target.transform.position;
                float nextX = Mathf.MoveTowards(position.x, destinationX, speed * Time.deltaTime);
                target.transform.position = new Vector3(nextX, position.y, position.z);

                if (Mathf.Abs(destinationX - nextX) <= 0.0001f)
                {
                    target.transform.position = new Vector3(destinationX, position.y, position.z);
                    break;
                }

                yield return null;
            }
        }
        finally
        {
            if (target != null)
            {
                if (animationStarted)
                    StopMoveAnimation(animator, frameLoops);

                if (bodies != null)
                {
                    for (int i = 0; i < bodies.Length; i++)
                    {
                        if (bodies[i] == null || !bodyWasSimulated[i])
                            continue;

                        bodies[i].position = bodies[i].transform.position;
                        bodies[i].linearVelocity = Vector2.zero;
                        bodies[i].simulated = true;
                    }
                }

                if (playerMoves != null)
                {
                    for (int i = 0; i < playerMoves.Length; i++)
                    {
                        if (playerMoves[i] != null && playerMoveWasEnabled[i])
                            playerMoves[i].enabled = true;
                    }
                }
            }

            movingActors.Remove(target);
        }
    }

    private static void FaceActor(GameObject target, float direction)
    {
        PlayerMove playerMove = target.GetComponent<PlayerMove>();

        // Seated characters keep their front-facing sit art (or swap to look-left frames); SitPose remembers the facing for when they stand.
        SitPose[] poses = target.GetComponentsInChildren<SitPose>(true);
        bool seated = false;
        foreach (SitPose pose in poses)
        {
            if (pose == null || !pose.IsSitting)
                continue;

            bool flip = playerMove != null
                ? (playerMove.artFacesLeft ? direction > 0f : direction < 0f)
                : direction > 0f;
            pose.SetFacing(direction, flip);
            seated = true;
        }
        if (seated)
            return;

        if (playerMove != null)
        {
            playerMove.Face(direction);
            return;
        }

        // The current character sprite art faces left when unflipped.
        bool faceRight = direction > 0f;
        foreach (SpriteRenderer spriteRenderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (spriteRenderer != null)
                spriteRenderer.flipX = faceRight;
        }
    }

    private bool TryStartMoveAnimation(GameObject target, Animator animator, SpriteFrameLoop[] frameLoops, MoveAnimation animation)
    {
        if (animation == MoveAnimation.None)
            return false;

        if (animator == null)
            return TryStartFrameLoopWalk(target, frameLoops, animation);

        bool hasWalking = HasBoolParameter(animator, IsWalkingParameter);
        bool hasRunning = HasBoolParameter(animator, IsRunningParameter);
        if (!hasWalking || (animation == MoveAnimation.Run && !hasRunning))
        {
            string missing = !hasWalking ? "IsWalking" : "IsRunning";
            Debug.LogError($"<<move_character>>: \"{target.name}\" Animator is missing the bool parameter {missing}. It will move without animation.", this);
            return false;
        }

        animator.SetBool(IsWalkingParameter, true);
        if (hasRunning)
            animator.SetBool(IsRunningParameter, animation == MoveAnimation.Run);
        return true;
    }

    // Walk for characters animated by SpriteFrameLoop. "run" plays the walk frames faster.
    private bool TryStartFrameLoopWalk(GameObject target, SpriteFrameLoop[] frameLoops, MoveAnimation animation)
    {
        bool started = false;
        if (frameLoops != null)
        {
            foreach (SpriteFrameLoop frameLoop in frameLoops)
            {
                if (frameLoop == null || !frameLoop.HasWalkFrames)
                    continue;

                frameLoop.SetMoving(true, animation == MoveAnimation.Run);
                started = true;
            }
        }

        if (!started)
            Debug.LogError($"<<move_character>>: \"{target.name}\" has no Animator and no SpriteFrameLoop with Walk Frames. It will move without animation.", this);
        return started;
    }

    private static void StopMoveAnimation(Animator animator, SpriteFrameLoop[] frameLoops)
    {
        if (frameLoops != null)
        {
            foreach (SpriteFrameLoop frameLoop in frameLoops)
            {
                if (frameLoop != null)
                    frameLoop.SetMoving(false, false);
            }
        }

        if (animator == null)
            return;

        if (HasBoolParameter(animator, IsWalkingParameter))
            animator.SetBool(IsWalkingParameter, false);
        if (HasBoolParameter(animator, IsRunningParameter))
            animator.SetBool(IsRunningParameter, false);
    }

    private static bool HasBoolParameter(Animator animator, int parameterHash)
    {
        if (animator == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Bool)
                return true;
        }

        return false;
    }

    private void OnDestroy()
    {
        SunriseAudio.StopLoop();
        if (pausedAudio) AudioListener.pause = false;
    }

    // <<cutscene "anin_joins_table">>: plays the matching Timeline and waits for it to finish.
    private YarnTask PlayCutscene(string id)
    {
        PlayCutsceneAudio(id);
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

    // <<cutscene_image "cutscene_prologue_1">>: opens the picture cutscene (CutsceneView) on that picture,
    // or changes the picture when it is already open. Lines after it type out under the picture.
    // <<cutscene_image_keep_text "name">>: changes the picture but leaves the last line on screen.
    private YarnTask ShowCutsceneImage(string sprite, bool keepText)
    {
        CutsceneView view = CutsceneView.Active;
        if (view == null)
        {
            Debug.LogWarning($"<<cutscene_image \"{sprite}\">>: this scene has no CutsceneView, so the picture is skipped.", this);
            return YarnTask.CompletedTask;
        }
        // The last line would otherwise still show when the cutscene closes.
        if (!view.IsOpen) HideDialogueLine();
        view.ShowPicture(sprite, keepText);
        return YarnTask.WaitUntil(() => view == null || !view.IsBusy);
    }

    // <<cutscene_end>>: fades the picture cutscene out and shows the scene again.
    private YarnTask EndCutsceneImages()
    {
        CutsceneView view = CutsceneView.Active;
        if (view == null || !view.IsOpen) return YarnTask.CompletedTask;
        view.Close();
        return YarnTask.WaitUntil(() => view == null || !view.IsBusy);
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

    // <<hard_reset>>: white flash, then the loop starts over at midnight (the gap): start nodes play again,
    // and Scene After Hard Reset loads, even when it is this scene. Yarn variables are kept.
    private async YarnTask HardReset()
    {
        SunriseAudio.PlaySequence("hard_reset", "night_transition");
        if (freezeFrame != null) await freezeFrame.Play();
        else Debug.LogWarning($"<<hard_reset>>: {name} has no Freeze Frame assigned, so there is no white flash.", this);

        StartOver(GapTimeOfDay, true);
        if (string.IsNullOrWhiteSpace(sceneAfterHardReset))
        {
            if (freezeFrame != null) freezeFrame.Release();
            return;
        }
        await LoadLocation(sceneAfterHardReset, "");
    }

    // Resets directly from an open picture cutscene. The scene transition covers the picture
    // before the current room can be shown again; other hard resets keep their white flash.
    private YarnTask HardResetFromCutscene()
    {
        SunriseAudio.PlaySequence("hard_reset", "night_transition");
        StartOver(GapTimeOfDay, true);
        if (string.IsNullOrWhiteSpace(sceneAfterHardReset))
        {
            Debug.LogError("<<hard_reset_from_cutscene>>: Scene After Hard Reset is not assigned.", this);
            return YarnTask.CompletedTask;
        }
        return LoadLocation(sceneAfterHardReset, "");
    }

    // <<soft_reset>>: midnight comes without a death, so there is no white flash. The loop starts over in the Dorm at midnight.
    private YarnTask SoftReset()
    {
        SunriseAudio.PlaySequence("soft_reset", "night_transition");
        StartOver(GapTimeOfDay, true);
        return LoadLocation(dormScene, "");
    }

    // <<end_gap>>: the gap is over and Dec 10 starts again in the Dorm, in the morning.
    private YarnTask EndGap()
    {
        SunriseAudio.Play("day_transition");
        SunriseAudio.StopLoop();
        StartOver(DefaultTimeOfDay, false);
        return LoadLocation(dormScene, "");
    }

    // Sets the time slot and lets every start node play again. Set $chapter_scene before the reset so the
    // reloaded room shows the next scene's objects.
    private void StartOver(string slot, bool loopEnded)
    {
        if (loopEnded) LoopsCompleted++;
        TimeOfDay = slot;
        TimeOfDayChanged?.Invoke();
        YarnCutscene.ForgetPlayed();
        Debug.Log(loopEnded ? $"Loop {LoopsCompleted} ended; it is now {slot}." : $"The gap ended; it is now {slot}.", this);
    }

    // Clip of the Sounds entry named soundName, or null (with a warning naming command) when there is none.
    private AudioClip FindClip(string soundName, string command)
    {
        AudioClip libraryClip = SunriseAudio.GetClip(soundName);
        if (libraryClip != null)
            return libraryClip;

        Sound sound = sounds.Find(s => s.name == soundName);
        if (sound == null || sound.clip == null)
        {
            Debug.LogWarning($"<<{command}>>: no audio cue or serialized clip named \"{soundName}\".", this);
            return null;
        }
        return sound.clip;
    }

    private void PlaySound(string soundName)
    {
        switch (Key(soundName))
        {
            case "gettingdressed":
                SunriseAudio.PlaySequence("wardrobe_open", "clothes_rustle", "wardrobe_close");
                return;
            case "writeandplacenote":
                SunriseAudio.PlaySequence("writing_pencil", "paper_placement");
                return;
            case "booknote":
                SunriseAudio.PlaySequence("book_open", "book_page_turn", "writing_pencil");
                return;
            case "blazerkey":
                SunriseAudio.PlaySequence("blazer_pocket", "keys_pickup");
                return;
            case "festivalmemory":
                SunriseAudio.PlaySequence("festival_transition", "festival_crowd");
                return;
        }

        if (SunriseAudio.Play(soundName) != null)
            return;

        Sound sound = sounds.Find(s => s.name == soundName);
        if (sound == null || sound.clip == null)
        {
            Debug.LogWarning($"<<sfx>>: no audio cue or serialized clip named \"{soundName}\".", this);
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

    private void PlayCutsceneAudio(string id)
    {
        switch (Key(id))
        {
            case "aninjoinstable":
                SunriseAudio.PlaySequence("chair_move", "food_serving", "plate_put_down");
                break;
            case "falreadsnotes":
                SunriseAudio.PlaySequence("book_pickup", "book_open", "book_page_turn", "paper_drop", "paper_pickup", "paper_handle", "blazer_pocket");
                break;
            case "falfalloffscreen":
                SunriseAudio.PlaySequence("fall_impact", "distant_scream", "running_to_fal", "ambience_drop_after_fal", "sting_before_again");
                break;
            case "falfalloffscreen2":
                SunriseAudio.PlaySequence("fall_impact", "distant_scream", "books_drop", "locker_grab_rattle", "ambience_drop_after_fal");
                break;
            case "day4lunchmatches":
                SunriseAudio.Play("reveal_sting");
                break;
        }
    }

    private void GiveItem(string id)
    {
        if (Key(id).Contains("key"))
            SunriseAudio.PlaySequence("keys_pickup", "ui_item_pickup");
        else
            SunriseAudio.Play("ui_item_pickup");
        Pending($"give_item {id}");
    }

    private void RemoveItem(string id)
    {
        SunriseAudio.Play(Key(id).Contains("key") ? "keys_return" : "ui_click_short");
        Pending($"remove_item {id}");
    }

    private void PendingWithAudio(string command, string cue)
    {
        SunriseAudio.Play(cue);
        Pending(command);
    }

    private void Pending(string command) =>
        Debug.Log($"<<{command}>> (not implemented yet)", this);

}
