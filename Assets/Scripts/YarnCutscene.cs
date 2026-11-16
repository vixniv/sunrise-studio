using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays a Yarn node as soon as the scene starts, then loads the next scene when the node ends.
// With no Next Scene the player stays in this scene and gets control back when the node ends.
// Lines type out in the DialogBubble; a tap shows the whole line, the next tap goes to the next line.
// Uses CircleTransition when one exists (it persists across scenes); otherwise loads the scene directly.
// To play it only at one time of day, add ActiveDuringTime to this object.
public class YarnCutscene : MonoBehaviour
{
    // Start nodes already played this loop. <<hard_reset>> clears it.
    private static readonly HashSet<string> played = new HashSet<string>();

    [SerializeField] private YarnDialogPresenter yarnDialog;
    [SerializeField] private string startNode = "Ch01_Prologue";
    [Tooltip("Lines without a character name show above this. Leave empty to use this object.")]
    [SerializeField] private Transform narrator;
    [Tooltip("World offset from the top of the narrator's sprite to the bottom of the dialog box.")]
    [SerializeField] private Vector2 dialogOffset = new Vector2(0f, 0.25f);
    [Tooltip("Seconds before the first line, so the scene can fade in first.")]
    [SerializeField, Min(0f)] private float startDelay = 0.5f;
    [Tooltip("Frozen from the moment the scene opens until the node ends; the ChoiceWheel shows above this player. " +
             "Leave empty to use the PlayerMove in the scene.")]
    [SerializeField] private PlayerMove player;
    [Tooltip("Scene to load when the cutscene ends. Must be in the Build Profile scene list. Leave empty to stay in this scene.")]
    [SerializeField] private string nextScene = "Dorm";
    [Tooltip("Plays the node only the first time this scene opens, until <<hard_reset>> starts the day over.")]
    [SerializeField] private bool playOnce = true;
    [Tooltip("On: when this cutscene plays, the player stays where they are placed in the scene. " +
             "Off: the player still starts at the door (see DoorSpawn).")]
    [SerializeField] private bool keepPlayerPosition = true;
    [Tooltip("Optional: Anin's WakeUpPose (on her Visual child). Anin lies in bed while the scene fades in, " +
             "plays the wake-up animation after Start Delay, a circle transition puts her back in her standing idle, " +
             "then the node starts. Leave empty for no wake-up.")]
    [SerializeField] private WakeUpPose wakeUp;

    [Header("Wake-up timing (optional)")]
    [Tooltip("Seconds Anin stays asleep after the opening transition. Set to -1 to use WakeUpPose's Asleep Seconds.")]
    [SerializeField] private float wakeUpAsleepSeconds = 2f;
    [Tooltip("Audio cue to play after the asleep hold. The wake animation waits until it finishes. Leave empty for no sound.")]
    [SerializeField] private string wakeUpSound = "alarm_clock";
    [Tooltip("Optional character already awake when the room appears. Used by Scene 7 for Kirana.")]
    [SerializeField] private GameObject awakeOnArrival;

    [Header("Optional proximity start")]
    [Tooltip("When assigned, wait until the player is this close to the target on the X axis instead of starting on scene load.")]
    [SerializeField] private Transform proximityTarget;
    [SerializeField, Min(0f)] private float proximityDistance = 3f;

    private bool leaving;
    private bool started;

    public bool HasStarted => started || (playOnce && played.Contains(startNode.Trim()));

    // True when this cutscene is about to play and keeps the player where the scene places them. Checked before Start.
    public bool KeepsPlayerInPlace =>
        proximityTarget == null && keepPlayerPosition && isActiveAndEnabled && yarnDialog != null &&
        !(playOnce && played.Contains(startNode.Trim()));

    // Clears the played nodes when Play Mode starts, even with Domain Reload turned off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => played.Clear();

    public static void ForgetPlayed() => played.Clear();

    private IEnumerator Start()
    {
        if (yarnDialog == null)
        {
            Debug.LogError($"{name} has no Yarn Dialog assigned.", this);
            yield break;
        }
        string node = startNode.Trim();
        if (playOnce && played.Contains(node)) yield break;

        if (player == null) player = FindAnyObjectByType<PlayerMove>();
        if (proximityTarget != null)
        {
            if (player == null)
            {
                Debug.LogError($"{name} needs a Player for its proximity start.", this);
                yield break;
            }

            while (isActiveAndEnabled && proximityTarget != null && player != null &&
                   (Mathf.Abs(player.transform.position.x - proximityTarget.position.x) > proximityDistance ||
                    player.InteractionMenuOpen || yarnDialog.IsPlaying))
                yield return null;

            if (!isActiveAndEnabled || proximityTarget == null || player == null) yield break;
        }

        if (playOnce && !played.Add(node)) yield break;
        started = true;
        // Frozen during Start Delay too, so the player cannot walk before the first line shows.
        if (player != null) player.InteractionMenuOpen = true;
        // Runs while the transition still covers the screen, so the scene fades in on Anin asleep in bed.
        if (wakeUp != null)
        {
            wakeUp.LieDown();
            if (awakeOnArrival != null) awakeOnArrival.SetActive(true);
        }
        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);
        if (wakeUp != null)
        {
            // Waits for the transition to open fully, in case Start Delay is shorter than the fade.
            while (CircleTransition.Instance != null && CircleTransition.Instance.IsTransitioning)
                yield return null;
            yield return wakeUp.PlayWakeUp(wakeUpAsleepSeconds, wakeUpSound);
        }
        yarnDialog.Play(node, narrator != null ? narrator : transform, dialogOffset, player, LoadNextScene);
        // Play refuses while other dialogue runs. When that is not the presenter's own dialogue (which unfreezes
        // the player when it ends), nothing else would unfreeze the player, so do it here.
        if (player != null && !yarnDialog.IsPlaying) player.InteractionMenuOpen = false;
    }

    private void LoadNextScene()
    {
        // Play also reports back when the scene is unloading; only the first call counts.
        if (leaving || !isActiveAndEnabled || string.IsNullOrWhiteSpace(nextScene)) return;

        leaving = true;
        if (CircleTransition.Instance != null)
            CircleTransition.Instance.LoadScene(nextScene.Trim());
        else
            SceneManager.LoadScene(nextScene.Trim());
    }
}
