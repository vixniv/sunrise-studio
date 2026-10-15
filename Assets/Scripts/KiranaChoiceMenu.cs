using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class KiranaChoiceMenu : MonoBehaviour
{
    [Serializable]
    public class Choice
    {
        public string name;
        [TextArea] public string description;
        public Sprite icon;
        public UnityEvent onChoose = new UnityEvent();
    }

    [Serializable]
    public class Slot
    {
        public UnityEngine.UI.Button button;
        public UnityEngine.UI.Image icon;
        public TMP_Text number;
    }

    [Tooltip("Five fixed positions: bottom-left, top-left, top-center, top-right, bottom-right.")]
    [SerializeField] private Slot[] slots = new Slot[5];
    [SerializeField] private Choice[] choices = new Choice[5];
    [SerializeField] private TMP_Text explanation;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector3 playerOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color normalColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    [SerializeField, Range(0.01f, 0.5f)] private float swipeScreenFraction = 0.08f;
    [SerializeField, Range(0.001f, 0.05f)] private float tapScreenFraction = 0.015f;
    [SerializeField, Min(0.1f)] private float maximumTapSeconds = 0.35f;

    private PlayerMove player;
    private RectTransform root;
    private RectTransform parent;
    private int selectedIndex;
    private int openedFrame;
    private Vector2 dragStart;
    private Vector2 rollStart;
    private float pressTime;
    private float maximumTravel;
    private bool tracking;
    private bool rolled;
    private bool ready;

    public bool IsOpen => player != null && gameObject.activeInHierarchy;

    private void Awake() => Initialize();

    private void Start()
    {
        if (player == null) gameObject.SetActive(false);
    }

    private void Initialize()
    {
        root = transform as RectTransform;
        parent = transform.parent as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (worldCamera == null) worldCamera = Camera.main;
        ready = root != null && parent != null && worldCamera != null
            && canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            && slots != null && slots.Length == 5 && choices != null && choices.Length >= 5;
        if (ready)
        {
            foreach (Slot slot in slots)
                ready &= slot != null && slot.button != null && slot.button.image != null;
            foreach (Choice choice in choices) ready &= choice != null;
        }
        if (!ready)
            Debug.LogError("KiranaChoiceMenu needs an overlay Canvas, a camera, five Button slots, and at least five choices.", this);
    }

    public void Open(PlayerMove interactingPlayer)
    {
        Initialize();
        if (!ready || interactingPlayer == null || IsOpen) return;
        player = interactingPlayer;
        player.InteractionMenuOpen = true;
        tracking = false;
        openedFrame = Time.frameCount;
        gameObject.SetActive(true);
        Refresh();
        FollowPlayer();
    }

    public void Close() => gameObject.SetActive(false);

    private void OnDisable()
    {
        if (player != null) player.InteractionMenuOpen = false;
        player = null;
        tracking = false;
    }

    private int ChoiceAtSlot(int slotIndex)
        => (selectedIndex + slotIndex - 2 + choices.Length) % choices.Length;

    private void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame) return;
        Pointer pointer = Pointer.current;
        if (pointer == null) { tracking = false; return; }
        Vector2 position = pointer.position.ReadValue();
        if (pointer.press.wasPressedThisFrame)
        {
            tracking = true;
            rolled = false;
            maximumTravel = 0f;
            pressTime = Time.unscaledTime;
            dragStart = rollStart = position;
        }
        if (!tracking) return;
        maximumTravel = Mathf.Max(maximumTravel, Vector2.Distance(position, dragStart));
        Vector2 delta = position - rollStart;
        float step = Mathf.Max(1f, Screen.width * swipeScreenFraction);
        if (Mathf.Abs(delta.x) >= step && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            int steps = Mathf.FloorToInt(Mathf.Abs(delta.x) / step);
            int direction = delta.x < 0f ? 1 : -1;
            selectedIndex = (selectedIndex + direction * (steps % choices.Length) + choices.Length) % choices.Length;
            rollStart = new Vector2(rollStart.x + Mathf.Sign(delta.x) * steps * step, position.y);
            rolled = true;
            Refresh();
        }
        if (!pointer.press.wasReleasedThisFrame) return;
        tracking = false;
        if (!rolled && maximumTravel <= Screen.width * tapScreenFraction
            && Time.unscaledTime - pressTime <= maximumTapSeconds)
        {
            Choice chosen = choices[selectedIndex];
            Close();
            Debug.Log("Kirana choice: " + chosen.name, this);
            chosen.onChoose.Invoke();
        }
    }

    private void Refresh()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int index = ChoiceAtSlot(i);
            if (slots[i].icon != null)
            {
                slots[i].icon.sprite = choices[index].icon;
                slots[i].icon.enabled = slots[i].icon.sprite != null;
            }
            if (slots[i].number != null) slots[i].number.text = (index + 1).ToString();
            slots[i].button.image.color = i == 2 ? selectedColor : normalColor;
        }
        if (explanation != null)
        {
            Choice choice = choices[selectedIndex];
            explanation.text = string.IsNullOrEmpty(choice.description)
                ? choice.name : choice.name + "\n" + choice.description;
        }
    }

    private void LateUpdate()
    {
        if (IsOpen) FollowPlayer();
    }

    private void FollowPlayer()
    {
        Vector3 screen = worldCamera.WorldToScreenPoint(player.transform.position + playerOffset);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local)) return;
        Vector2 min = parent.rect.min + Vector2.Scale(root.rect.size, root.pivot);
        Vector2 max = parent.rect.max - Vector2.Scale(root.rect.size, Vector2.one - root.pivot);
        local.x = Mathf.Clamp(local.x, min.x, Mathf.Max(min.x, max.x));
        local.y = Mathf.Clamp(local.y, min.y, Mathf.Max(min.y, max.y));
        root.localPosition = new Vector3(Mathf.Round(local.x), Mathf.Round(local.y), 0f);
    }
}
