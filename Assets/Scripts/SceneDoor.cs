using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Shows the cue while the player is inside this object's trigger. A tap anywhere (not a drag) loads the destination scene.
// Uses CircleTransition when one exists (it persists across scenes); otherwise loads the scene directly.
public class SceneDoor : MonoBehaviour
{
    [Tooltip("Can be one cue shared by every interactable, such as a cue above the player's head.")]
    [SerializeField] private GameObject cue;
    [Tooltip("Scene to load. Must be in the Build Profile scene list.")]
    [SerializeField] private string destinationScene = "SchoolMap";
    [SerializeField, Range(0.001f, 0.1f)] private float tapMaxTravel = 0.02f;
    [SerializeField, Min(0.05f)] private float tapMaxSeconds = 0.35f;

    private PlayerMove nearbyPlayer;
    private Vector2 pressPosition;
    private float pressTime;
    private float maxTravel;
    private bool tracking;
    private bool leaving;
    private bool cueShown;

    private void Awake()
    {
        SharedCue.Init(cue);
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
        SharedCue.Set(cue, ref cueShown, nearbyPlayer != null);
        if (nearbyPlayer == null)
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
        if (string.IsNullOrWhiteSpace(destinationScene))
        {
            Debug.LogError($"{name} has no Destination Scene assigned.", this);
            return;
        }

        leaving = true;
        tracking = false;
        SharedCue.Set(cue, ref cueShown, false);
        // Stops the player in place until the new scene replaces this one.
        nearbyPlayer.InteractionMenuOpen = true;

        if (CircleTransition.Instance != null)
            CircleTransition.Instance.LoadScene(destinationScene.Trim());
        else
            SceneManager.LoadScene(destinationScene.Trim());
    }

    private void OnDisable()
    {
        tracking = false;
        if (leaving) return;
        nearbyPlayer = null;
        SharedCue.Set(cue, ref cueShown, false);
    }
}
