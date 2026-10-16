using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Shows the cue while the player is inside this object's trigger. A tap anywhere replaces the cue with the ChoiceWheel.
// Picking a choice plays its Yarn node or its dialog lines above this object; the cue stays hidden until the whole action ends.
public class ChoiceInteractable : MonoBehaviour
{
    [SerializeField] private GameObject cue;
    [SerializeField] private ChoiceWheel wheel;
    [SerializeField] private DialogBubble dialog;
    [Tooltip("Only needed for choices with a Yarn Node.")]
    [SerializeField] private YarnDialogPresenter yarnDialog;
    [Tooltip("World offset from the top of this object's sprite to the bottom of the dialog box.")]
    [SerializeField] private Vector2 dialogOffset = new Vector2(0f, 0.25f);
    [Tooltip("This object's actions. The first one starts in the center of the wheel.")]
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

    private void Awake()
    {
        if (cue != null) cue.SetActive(false);
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
        if (cue != null) cue.SetActive(nearbyPlayer != null && !busy);
        // Presses that start during the action belong to the wheel or the dialog.
        if (nearbyPlayer == null || wheel == null || busy)
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
        if (maxTravel <= Screen.width * tapMaxTravel && Time.unscaledTime - pressTime <= tapMaxSeconds)
            wheel.Open(nearbyPlayer, choices, OnChosen);
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
        if (cue != null) cue.SetActive(false);
    }
}
