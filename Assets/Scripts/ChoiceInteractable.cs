using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Shows the cue while the player is inside this object's trigger. A tap anywhere replaces the cue with the ChoiceWheel.
// When several interactables are in range, the InteractionCue picks the nearest and only that one takes the tap.
// With a single choice there is nothing to pick, so the tap runs it directly and no wheel is needed.
// Picking a choice plays its Yarn node or its dialog lines above this object; the cue stays hidden until the whole action ends.
public class ChoiceInteractable : MonoBehaviour
{
    [Tooltip("Optional. Leave empty to use the InteractionCue on the Player, which shows above this object.")]
    [SerializeField] private GameObject cue;
    [SerializeField] private ChoiceWheel wheel;
    [SerializeField] private DialogBubble dialog;
    [Tooltip("Only needed for choices with a Yarn Node.")]
    [SerializeField] private YarnDialogPresenter yarnDialog;
    [Tooltip("World offset from the top of this object's sprite to the bottom of the dialog box.")]
    [SerializeField] private Vector2 dialogOffset = new Vector2(0f, 0.25f);
    [Tooltip("This object's actions. The first one starts in the center of the wheel. A single action skips the wheel.")]
    [SerializeField] private List<ChoiceWheel.Choice> choices = new List<ChoiceWheel.Choice>
    {
        new ChoiceWheel.Choice
        {
            label = "Examine",
            dialogLines = new List<string>
            {
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
                "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
            },
        },
        new ChoiceWheel.Choice { label = "Item" },
        new ChoiceWheel.Choice { label = "" },
        new ChoiceWheel.Choice { label = "" },
        new ChoiceWheel.Choice { label = "" },
    };
    [SerializeField, Range(0.001f, 0.1f)] private float tapMaxTravel = 0.02f;
    [SerializeField, Min(0.05f)] private float tapMaxSeconds = 0.35f;

    private PlayerMove nearbyPlayer;
    private Vector2 pressPosition;
    private float pressTime;
    private float maxTravel;
    private bool tracking;
    private bool cueShown;
    // The cue field when it is this object's own cue, not the shared InteractionCue.
    private GameObject ownCue;

    private void Awake()
    {
        ownCue = InteractionCue.IsSharedCue(cue) ? null : cue;
        SharedCue.Init(ownCue);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMove player = other.GetComponentInParent<PlayerMove>();
        if (player != null) nearbyPlayer = player;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (nearbyPlayer != null && other.GetComponentInParent<PlayerMove>() == nearbyPlayer)
        {
            nearbyPlayer = null;
            tracking = false;
        }
    }

    private void Update()
    {
        // The action runs from opening the wheel until the chosen dialog ends.
        bool busy = (wheel != null && wheel.IsOpen) || (dialog != null && dialog.IsOpen) || (yarnDialog != null && yarnDialog.IsPlaying);
        bool want = nearbyPlayer != null && !busy;
        InteractionCue.Request(this, want, ownCue != null);
        bool isTarget = InteractionCue.IsTarget(this);
        SharedCue.Set(ownCue, ref cueShown, want && isTarget);
        // Presses that start during the action belong to the wheel or the dialog.
        if (!want || !isTarget || (wheel == null && choices.Count != 1))
        {
            tracking = false;
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
        if (maxTravel > Screen.width * tapMaxTravel || Time.unscaledTime - pressTime > tapMaxSeconds) return;
        if (choices.Count == 1) OnChosen(0);
        else wheel.Open(nearbyPlayer, choices, OnChosen);
    }

    private void OnChosen(int index)
    {
        ChoiceWheel.Choice choice = choices[index];
        Debug.Log($"{name}: chose {index + 1} ({choice.label})", this);
        if (yarnDialog != null && !string.IsNullOrWhiteSpace(choice.yarnNode))
            yarnDialog.Play(choice.yarnNode.Trim(), transform, dialogOffset, nearbyPlayer, choice.onChoose.Invoke);
        else if (dialog != null && choice.dialogLines.Count > 0)
            dialog.Show(transform, dialogOffset, choice.dialogLines, nearbyPlayer, choice.onChoose.Invoke);
        else
            choice.onChoose.Invoke();
    }

    private void OnDisable()
    {
        tracking = false;
        nearbyPlayer = null;
        InteractionCue.Request(this, false, ownCue != null);
        SharedCue.Set(ownCue, ref cueShown, false);
    }
}
