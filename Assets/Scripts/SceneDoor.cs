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

    private void Awake()
    {
        ownCue = InteractionCue.IsSharedCue(cue) ? null : cue;
        SharedCue.Init(ownCue);
        if (!string.IsNullOrWhiteSpace(storyNode))
        {
            runner = FindAnyObjectByType<DialogueRunner>();
            if (yarnDialog == null) yarnDialog = FindAnyObjectByType<YarnDialogPresenter>();
            if (yarnDialog == null)
                Debug.LogError($"{name} has a Story Node but there is no YarnDialogPresenter in the scene, so it always leaves freely.", this);
        }
    }

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
        // While the story node plays, presses belong to the dialog.
        bool want = nearbyPlayer != null && !(yarnDialog != null && yarnDialog.IsPlaying);
        InteractionCue.Request(this, want, ownCue != null);
        bool isTarget = InteractionCue.IsTarget(this);
        SharedCue.Set(ownCue, ref cueShown, want && isTarget);
        if (!want || !isTarget)
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
