using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Yarn.Unity;

// Shows the cue while the player is inside this object's trigger. A tap anywhere (not a drag) loads the destination scene.
// When several interactables are in range, the InteractionCue picks the nearest and only that one takes the tap.
// Uses CircleTransition when one exists (it persists across scenes); otherwise loads the scene directly.
// With a Story Node, the tap runs that Yarn node instead while $story_exit is true, so the story decides
// whether and where the player leaves; the node leaves with <<scene_transition>>.
public class SceneDoor : MonoBehaviour
{
    [Tooltip("Optional. Leave empty to use the InteractionCue on the Player, which shows above this object.")]
    [SerializeField] private GameObject cue;
    [Tooltip("Scene to load when there is no story reason to stop. Must be in the Build Profile scene list.")]
    [SerializeField] private string destinationScene = "SchoolMap";
    [Tooltip("Optional. Plays as the transition starts closing. Needs CircleTransition in the scene.")]
    [SerializeField] private AudioClip enterSound;
    [SerializeField, Range(0f, 1f)] private float enterVolume = 1f;
    [Tooltip("Optional. Its X is where the player appears when the room loads without a cutscene (see DoorSpawn); " +
             "the player keeps their own height. Leave empty to use this door's X.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("In a room with several doors: the player starts here when no door leads back to the previous scene, " +
             "such as when you press Play in this scene.")]
    [SerializeField] private bool defaultEntrance;

    [Header("Story")]
    [Tooltip("Optional. Yarn node to run instead of leaving while $story_exit is true, such as Dorm_Door.")]
    [SerializeField] private string storyNode = "";
    [Tooltip("Plays the Story Node. Leave empty to use the one in the scene.")]
    [SerializeField] private YarnDialogPresenter yarnDialog;
    [Tooltip("World offset from the top of this object's sprite to the bottom of the dialog box.")]
    [SerializeField] private Vector2 dialogOffset = new Vector2(0f, 0.25f);
    [SerializeField, Range(0.001f, 0.1f)] private float tapMaxTravel = 0.02f;
    [SerializeField, Min(0.05f)] private float tapMaxSeconds = 0.35f;

    private PlayerMove nearbyPlayer;
    private Vector2 pressPosition;
    private float pressTime;
    private float maxTravel;
    private bool tracking;
    private bool leaving;
    private bool cueShown;
    // The cue field when it is this object's own cue, not the shared InteractionCue.
    private GameObject ownCue;
    private DialogueRunner runner;
    private DialogBubble bubble;
    private ChoiceWheel wheel;
    // Last frame the player was busy with dialogue or a menu, so the tap that closes it cannot also open this door.
    private int lastBusyFrame = -1;

    private void Awake()
    {
        ownCue = InteractionCue.IsSharedCue(cue) ? null : cue;
        SharedCue.Init(ownCue);
        // Found even without a Story Node, so taps on any dialogue in the room never count as door taps.
        if (yarnDialog == null) yarnDialog = FindAnyObjectByType<YarnDialogPresenter>();
        bubble = FindAnyObjectByType<DialogBubble>();
        wheel = FindAnyObjectByType<ChoiceWheel>();
        if (!string.IsNullOrWhiteSpace(storyNode))
        {
            runner = FindAnyObjectByType<DialogueRunner>();
            if (yarnDialog == null)
                Debug.LogError($"{name} has a Story Node but there is no YarnDialogPresenter in the scene, so it always leaves freely.", this);
        }
    }

    public bool DefaultEntrance => defaultEntrance;

    public bool LeadsTo(string scene) =>
        !string.IsNullOrWhiteSpace(scene) && string.Equals(destinationScene.Trim(), scene.Trim(), StringComparison.OrdinalIgnoreCase);

    // Where DoorSpawn puts the player: only X moves; the player keeps the height they are placed at in the scene,
    // so they land standing on the floor instead of dropping from the door's height.
    public Vector2 SpawnPosition(Vector2 playerPosition) =>
        new Vector2(spawnPoint != null ? spawnPoint.position.x : transform.position.x, playerPosition.y);

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMove player = other.GetComponentInParent<PlayerMove>();
        if (player != null) nearbyPlayer = player;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (leaving) return;
        if (nearbyPlayer != null && other.GetComponentInParent<PlayerMove>() == nearbyPlayer)
        {
            nearbyPlayer = null;
            tracking = false;
        }
    }

    private void Update()
    {
        if (leaving) return;
        // While any dialogue, the ChoiceWheel or a cutscene freeze is up, presses belong to it.
        bool busy = (yarnDialog != null && yarnDialog.IsPlaying) || (bubble != null && bubble.IsOpen) ||
                    (wheel != null && wheel.IsOpen) || (nearbyPlayer != null && nearbyPlayer.InteractionMenuOpen);
        if (busy) lastBusyFrame = Time.frameCount;
        bool want = nearbyPlayer != null && !busy;
        InteractionCue.Request(this, want, ownCue != null);
        bool isTarget = InteractionCue.IsTarget(this);
        SharedCue.Set(ownCue, ref cueShown, want && isTarget);
        // The frame right after the dialogue closes is skipped too: a quick tap can press and release in one frame,
        // and the dialog may have handled that release earlier this frame.
        if (!want || !isTarget || Time.frameCount <= lastBusyFrame + 1)
        {
            tracking = false;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
        {
            Enter();
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) { tracking = false; return; }
        Vector2 position = pointer.position.ReadValue();
        if (pointer.press.wasPressedThisFrame)
        {
            tracking = true;
            pressPosition = position;
            pressTime = Time.unscaledTime;
            maxTravel = 0f;
        }
        if (!tracking) return;
        maxTravel = Mathf.Max(maxTravel, Vector2.Distance(position, pressPosition));
        if (!pointer.press.wasReleasedThisFrame) return;
        tracking = false;
        // Drags move the player, so only a short, still press counts as a tap.
        if (maxTravel <= Screen.width * tapMaxTravel && Time.unscaledTime - pressTime <= tapMaxSeconds)
            Enter();
    }

    private void Enter()
    {
        if (StoryExitActive())
        {
            tracking = false;
            yarnDialog.Play(storyNode.Trim(), transform, dialogOffset, nearbyPlayer, null);
            return;
        }

        if (string.IsNullOrWhiteSpace(destinationScene))
        {
            Debug.LogError($"{name} has no Destination Scene assigned.", this);
            return;
        }

        leaving = true;
        tracking = false;
        InteractionCue.Request(this, false, ownCue != null);
        SharedCue.Set(ownCue, ref cueShown, false);
        // Stops the player in place until the new scene replaces this one.
        nearbyPlayer.InteractionMenuOpen = true;

        if (CircleTransition.Instance != null)
            CircleTransition.Instance.LoadScene(destinationScene.Trim(), enterSound, enterVolume);
        else
            SceneManager.LoadScene(destinationScene.Trim());
    }

    private bool StoryExitActive()
    {
        if (string.IsNullOrWhiteSpace(storyNode) || yarnDialog == null || runner == null || runner.VariableStorage == null)
            return false;
        try
        {
            return runner.VariableStorage.TryGetValue(YarnCommands.StoryExitVariable, out bool active) && active;
        }
        catch (Exception e)
        {
            Debug.LogError($"{name}: could not read {YarnCommands.StoryExitVariable}: {e.Message}", this);
            return false;
        }
    }

    private void OnDisable()
    {
        tracking = false;
        if (leaving) return;
        nearbyPlayer = null;
        InteractionCue.Request(this, false, ownCue != null);
        SharedCue.Set(ownCue, ref cueShown, false);
    }
}
