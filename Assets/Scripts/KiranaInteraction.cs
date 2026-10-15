using UnityEngine;
using UnityEngine.InputSystem;

public class KiranaInteraction : MonoBehaviour
{
    [SerializeField] private PlayerMove player;
    [SerializeField] private GameObject cue;
    [SerializeField] private KiranaChoiceMenu menu;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.5f;
    [SerializeField, Range(0.001f, 0.05f)] private float tapScreenFraction = 0.015f;
    [SerializeField, Min(0.1f)] private float maximumTapSeconds = 0.35f;

    private Vector2 pressPosition;
    private float pressTime;
    private float maximumTravel;
    private bool tracking;

    private void Awake()
    {
        if (cue != null) cue.SetActive(false);
    }

    private void Update()
    {
        bool playerNearby = player != null &&
            ((Vector2)(player.transform.position - transform.position)).sqrMagnitude
            <= interactionDistance * interactionDistance;

        if (!playerNearby && menu != null && menu.IsOpen) menu.Close();
        if (cue != null) cue.SetActive(playerNearby && (menu == null || !menu.IsOpen));
        if (!playerNearby || menu == null || player.InteractionMenuOpen)
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
            maximumTravel = 0f;
            pressPosition = position;
            pressTime = Time.unscaledTime;
        }
        if (!tracking) return;
        maximumTravel = Mathf.Max(maximumTravel, Vector2.Distance(position, pressPosition));
        if (!pointer.press.wasReleasedThisFrame) return;
        tracking = false;
        if (maximumTravel <= Screen.width * tapScreenFraction
            && Time.unscaledTime - pressTime <= maximumTapSeconds)
        {
            menu.Open(player);
            if (cue != null && menu.IsOpen) cue.SetActive(false);
        }
    }

    private void OnDisable()
    {
        tracking = false;
        if (cue != null) cue.SetActive(false);
        if (menu != null && menu.IsOpen) menu.Close();
    }
}
