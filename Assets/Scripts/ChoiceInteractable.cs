using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SunriseStudio.UI;

// Shows the cue while the player is inside this object's trigger. A tap anywhere replaces the cue with the ChoiceWheel.
// When several interactables are in range, the InteractionCue picks the nearest and only that one takes the tap.
// With a single choice there is nothing to pick, so the tap runs it directly and no wheel is needed.
// Picking a choice plays its Yarn node or its dialog lines above this object; the cue stays hidden until the whole action ends.
public class ChoiceInteractable : MonoBehaviour, IInteractionTarget
{
    [Tooltip("Optional. Leave empty to use the InteractionCue on the Player, which shows above this object.")]
    [SerializeField] private GameObject cue;
    [SerializeField] private ChoiceWheel wheel;
    [Tooltip("Optional screen-space replacement for ChoiceWheel. Found automatically when present.")]
    [SerializeField] private ActionInventoryCarouselPresenter actionCarousel;
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
    [Header("Approach")]
    [Tooltip("Before a choice's dialogue starts, the player walks to a spot beside this object and turns to face it, so the two do not overlap.")]
    [SerializeField] private bool approachBeforeTalking;
    [Tooltip("Horizontal distance, in world units, between this object and the spot the player walks to. 1 unit = 16 pixels.")]
    [SerializeField, Min(0f)] private float approachDistance = 1.5f;
    [Tooltip("Side of this object the player stands on. Nearest keeps the player on the side they came from.")]
    [SerializeField] private ApproachSide approachSide = ApproachSide.Nearest;

    public enum ApproachSide { Nearest, Left, Right }

    [Tooltip("Play the interact sound (set on the Player's InteractionCue) when this object is tapped.")]
    [SerializeField] private bool playInteractSound = true;
    [SerializeField, Range(0.001f, 0.1f)] private float tapMaxTravel = 0.02f;
    [SerializeField, Min(0.05f)] private float tapMaxSeconds = 0.35f;

    private PlayerMove nearbyPlayer;
    private Vector2 pressPosition;
    private float pressTime;
    private float maxTravel;
    private bool tracking;
    private bool cueShown;
    private bool approaching;
    private bool ownsCarousel;
    // The cue field when it is this object's own cue, not the shared InteractionCue.
    private GameObject ownCue;
    private readonly List<int> visibleChoiceIndices = new List<int>();
    private readonly List<InteractionAction> carouselActions = new List<InteractionAction>();

    private void Awake()
    {
        ownCue = InteractionCue.IsSharedCue(cue) ? null : cue;
        SharedCue.Init(ownCue);
        // Empty references are found in the scene, so this works from a prefab without per-scene setup.
        if (wheel == null) wheel = FindAnyObjectByType<ChoiceWheel>();
        if (actionCarousel == null) actionCarousel = FindAnyObjectByType<ActionInventoryCarouselPresenter>(FindObjectsInactive.Include);
        if (dialog == null) dialog = FindAnyObjectByType<DialogBubble>();
        if (yarnDialog == null) yarnDialog = FindAnyObjectByType<YarnDialogPresenter>();
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
        bool busy = (wheel != null && wheel.IsOpen) || (actionCarousel != null && actionCarousel.IsOpen) ||
                    (dialog != null && dialog.IsOpen) || (yarnDialog != null && yarnDialog.IsPlaying) ||
                    approaching;
        bool want = nearbyPlayer != null && !busy;
        InteractionCue.Request(this, want, ownCue != null);
        bool isTarget = InteractionCue.IsTarget(this);
        SharedCue.Set(ownCue, ref cueShown, want && isTarget);
        // Presses that start during the action belong to the wheel or the dialog.
        if (!want || !isTarget || (wheel == null && actionCarousel == null && choices.Count != 1))
        {
            tracking = false;
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) { tracking = false; return; }
        Vector2 position = pointer.position.ReadValue();
        if (pointer.press.wasPressedThisFrame)
        {
            if (InteractionCue.PointerOverUi(position)) { tracking = false; return; }
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
        TryInteract();
    }

    public bool TryInteract()
    {
        bool busy = (wheel != null && wheel.IsOpen) || (actionCarousel != null && actionCarousel.IsOpen) ||
                    (dialog != null && dialog.IsOpen) || (yarnDialog != null && yarnDialog.IsPlaying) ||
                    approaching;
        if (nearbyPlayer == null || busy || !InteractionCue.IsTarget(this))
            return false;

        BuildVisibleChoices();
        if (visibleChoiceIndices.Count == 0)
            return false;
        // Several choices need a carousel or a wheel to pick from; bail out before the sound plays if there is neither.
        if (visibleChoiceIndices.Count > 1 && actionCarousel == null && wheel == null)
            return false;
        if (playInteractSound) InteractionCue.PlayInteractSound();
        if (visibleChoiceIndices.Count == 1)
            OnChosen(visibleChoiceIndices[0]);
        else if (actionCarousel != null)
        {
            PlayerMove interactingPlayer = nearbyPlayer;
            interactingPlayer.InteractionMenuOpen = true;
            ownsCarousel = true;
            actionCarousel.OpenActions(
                carouselActions,
                action =>
                {
                    if (int.TryParse(action.Id, out int index) && index >= 0 && index < choices.Count)
                        OnChosen(index);
                },
                () =>
                {
                    ownsCarousel = false;
                    if (interactingPlayer != null)
                        interactingPlayer.InteractionMenuOpen = false;
                },
                interactingPlayer.transform);
        }
        else
            wheel.Open(nearbyPlayer, choices, OnChosen);
        return true;
    }

    private void BuildVisibleChoices()
    {
        visibleChoiceIndices.Clear();
        carouselActions.Clear();
        for (int index = 0; index < choices.Count; index++)
        {
            ChoiceWheel.Choice choice = choices[index];
            if (choice == null || string.IsNullOrWhiteSpace(choice.label))
                continue;
            visibleChoiceIndices.Add(index);
            carouselActions.Add(new InteractionAction(
                index.ToString(), choice.label, choice.description, choice.timeCostMinutes, choice.icon,
                choice.locked, choice.lockedReason));
        }
    }

    private void OnChosen(int index)
    {
        ChoiceWheel.Choice choice = choices[index];
        Debug.Log($"{name}: chose {index + 1} ({choice.label})", this);
        bool hasYarn = yarnDialog != null && !string.IsNullOrWhiteSpace(choice.yarnNode);
        bool hasLines = dialog != null && choice.dialogLines.Count > 0;
        PlayerMove player = nearbyPlayer;
        if (approachBeforeTalking && player != null && (hasYarn || hasLines))
        {
            Approach(player, () => StartChoice(choice, player));
            return;
        }
        StartChoice(choice, player);
    }

    private void StartChoice(ChoiceWheel.Choice choice, PlayerMove player)
    {
        if (yarnDialog != null && !string.IsNullOrWhiteSpace(choice.yarnNode))
            yarnDialog.Play(choice.yarnNode.Trim(), transform, dialogOffset, player, choice.onChoose.Invoke);
        else if (dialog != null && choice.dialogLines.Count > 0)
            dialog.Show(transform, dialogOffset, choice.dialogLines, player, choice.onChoose.Invoke);
        else
            choice.onChoose.Invoke();
    }

    // Walks the player to the standing spot beside this object, turns them to face it, then runs arrived.
    // The player stays locked (InteractionMenuOpen) for the walk, so no other interaction takes a tap meanwhile.
    private void Approach(PlayerMove player, System.Action arrived)
    {
        float npcX = transform.position.x;
        float playerX = player.transform.position.x;
        float side = approachSide == ApproachSide.Left ? -1f
                   : approachSide == ApproachSide.Right ? 1f
                   : playerX < npcX ? -1f : 1f;
        float targetX = npcX + side * approachDistance;

        approaching = true;
        player.InteractionMenuOpen = true;
        player.WalkTo(targetX, -side, () =>
        {
            approaching = false;
            if (this == null || !isActiveAndEnabled || player == null) return;
            player.InteractionMenuOpen = false;
            arrived();
        });
    }

    private void OnDisable()
    {
        approaching = false;
        if (ownsCarousel && actionCarousel != null)
            actionCarousel.Close();
        ownsCarousel = false;
        tracking = false;
        nearbyPlayer = null;
        InteractionCue.Request(this, false, ownCue != null);
        SharedCue.Set(ownCue, ref cueShown, false);
    }

    // Scene view preview of the approach spots, shown while this object is selected.
    // Green: where the player stands. Grey: the side Approach Side never uses. Faded: Approach Before Talking is off.
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position;
        Collider2D area = GetComponent<Collider2D>();
        float height = area != null ? area.bounds.size.y : 2f;
        float alpha = approachBeforeTalking ? 1f : 0.35f;
        DrawApproachSpot(origin, -1f, height, approachSide != ApproachSide.Right, alpha);
        DrawApproachSpot(origin, 1f, height, approachSide != ApproachSide.Left, alpha);
    }

    private void DrawApproachSpot(Vector3 origin, float side, float height, bool used, float alpha)
    {
        Color color = used ? new Color(0.2f, 1f, 0.3f, alpha) : new Color(0.6f, 0.6f, 0.6f, alpha * 0.6f);
        Gizmos.color = color;
        Vector3 feet = origin + Vector3.right * (side * approachDistance);
        // Where the player's feet land, a line as tall as this object, and an arrow showing which way the player faces.
        Gizmos.DrawLine(origin, feet);
        Gizmos.DrawLine(feet, feet + Vector3.up * height);
        Gizmos.DrawWireSphere(feet, 0.15f);
        Vector3 eye = feet + Vector3.up * (height * 0.75f);
        Vector3 tip = eye + Vector3.right * (-side * 0.5f);
        Gizmos.DrawLine(eye, tip);
        Gizmos.DrawLine(tip, tip + new Vector3(side * 0.2f, 0.15f, 0f));
        Gizmos.DrawLine(tip, tip + new Vector3(side * 0.2f, -0.15f, 0f));
    }
}
