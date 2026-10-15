using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.TextCore;

// World-space choice wheel shown above the player's head, laid out from boxes placed by hand in the scene.
// Each swipe left/right rolls exactly one box (wraps endlessly) with a slide animation; a tap picks the center box.
public class ChoiceWheel : MonoBehaviour
{
    [Serializable]
    public class Choice
    {
        [Tooltip("Shown in the description box. Leave empty for an empty slot: the box then stays hidden.")]
        public string label = "Choice";
        [Tooltip("Optional. When empty, the box shows the choice number instead.")]
        public Sprite icon;
        [Tooltip("Optional. Shown one at a time above the object after this choice is picked; tap for the next line.")]
        [TextArea] public List<string> dialogLines = new List<string>();
        [Tooltip("Runs when this choice's action ends (after its dialog, if any). Leave empty for a choice that does nothing.")]
        public UnityEvent onChoose = new UnityEvent();
    }

    private struct Item
    {
        public Transform root;
        public SpriteRenderer box;
        public SpriteRenderer icon;
        public TMP_Text number;
        public int shownIndex;
    }

    [Header("Layout")]
    [Tooltip("The boxes placed in the scene (odd count, any order). They only mark positions: at runtime they are hidden and copies of the middle one slide between them.")]
    [SerializeField] private Transform[] slots = new Transform[5];
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;
    [Tooltip("World offset from the player's pivot to the center (selected) box, so the wheel sits above the head.")]
    [SerializeField] private Vector2 headOffset = new Vector2(0f, 4.5f);

    [Header("Description box (optional)")]
    [Tooltip("Box above the wheel. Needs a 9-sliced sprite with Draw Mode set to Sliced; its width follows the text.")]
    [SerializeField] private SpriteRenderer labelBox;
    [Tooltip("Shows the centered choice's label. Put it inside the box.")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Pixels between the text and the box's left/right edges.")]
    [SerializeField, Min(0)] private int labelPadding = 6;
    [Tooltip("Pixels between the capitals and the box's top/bottom edges. The box height follows the font.")]
    [SerializeField, Min(0)] private int labelVerticalPadding = 4;

    [Header("Look")]
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color normalColor = new Color(0.62f, 0.62f, 0.66f);

    [Header("Input (fractions of screen width)")]
    [Tooltip("Swipe distance that rolls the wheel. One swipe never rolls more than one box.")]
    [SerializeField, Range(0.02f, 0.5f)] private float swipeDistance = 0.08f;
    [SerializeField, Range(0.001f, 0.1f)] private float tapMaxTravel = 0.02f;
    [SerializeField, Min(0.05f)] private float tapMaxSeconds = 0.35f;
    [Tooltip("Duration of the slide between two boxes.")]
    [SerializeField, Min(0.01f)] private float stepSeconds = 0.18f;

    private Item[] items;
    private Vector3[] points; // slot positions in this transform's space, sorted left to right
    private int center;
    private Transform visual;
    private Color numberColor;
    private int labelIndex = -1;
    private Vector3 labelBoxHome; // box position in this transform's space

    private PlayerMove player;
    private IReadOnlyList<Choice> choices;
    private Action<int> onChosen;
    private int openedFrame;
    private int target;
    private float scroll;
    private float animFrom;
    private float animTime;
    private Vector2 pressPosition;
    private float pressTime;
    private float maxTravel;
    private bool pressed;
    private bool stepped;

    public bool IsOpen => player != null;
    public int SelectedIndex => Wrap(target);

    private void Awake()
    {
        Build();
        ShowLabel(false);
    }

    // Opens with the first choice in the center. chosen receives the picked index.
    public void Open(PlayerMove interactingPlayer, IReadOnlyList<Choice> wheelChoices, Action<int> chosen)
    {
        if (IsOpen || interactingPlayer == null || wheelChoices == null || wheelChoices.Count == 0 || !Build()) return;
        player = interactingPlayer;
        player.InteractionMenuOpen = true;
        choices = wheelChoices;
        onChosen = chosen;
        openedFrame = Time.frameCount;
        pressed = false;
        target = 0;
        scroll = animFrom = 0f;
        animTime = stepSeconds;
        labelIndex = -1;
        for (int k = 0; k < items.Length; k++) items[k].shownIndex = -1;
        visual.gameObject.SetActive(true);
        FollowPlayer();
        Layout();
    }

    public void Close()
    {
        if (player != null) player.InteractionMenuOpen = false;
        player = null;
        choices = null;
        onChosen = null;
        pressed = false;
        if (visual != null) visual.gameObject.SetActive(false);
        ShowLabel(false);
    }

    private void OnDisable() => Close();

    private void Update()
    {
        if (!IsOpen) return;
        ReadPointer();
        if (!IsOpen) return;
        animTime += Time.unscaledDeltaTime;
        float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(animTime / stepSeconds), 3f); // ease out
        scroll = Mathf.LerpUnclamped(animFrom, target, p);
    }

    private void LateUpdate()
    {
        if (!IsOpen) return;
        FollowPlayer();
        Layout();
    }

    private void ReadPointer()
    {
        // Ignore the tap that opened the wheel.
        if (Time.frameCount == openedFrame) return;
        Pointer pointer = Pointer.current;
        if (pointer == null) { pressed = false; return; }
        Vector2 position = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            pressed = true;
            stepped = false;
            pressPosition = position;
            pressTime = Time.unscaledTime;
            maxTravel = 0f;
        }
        if (!pressed) return;

        maxTravel = Mathf.Max(maxTravel, Vector2.Distance(position, pressPosition));
        float dx = position.x - pressPosition.x;
        if (!stepped && Mathf.Abs(dx) >= Screen.width * swipeDistance)
        {
            stepped = true;
            // Swipe left moves boxes left, bringing the next choice (right side) to the center.
            Step(dx < 0f ? 1 : -1);
        }

        if (!pointer.press.wasReleasedThisFrame) return;
        pressed = false;
        bool tap = !stepped && maxTravel <= Screen.width * tapMaxTravel && Time.unscaledTime - pressTime <= tapMaxSeconds;
        if (tap) Choose();
    }

    private void Step(int direction)
    {
        animFrom = scroll;
        animTime = 0f;
        target += direction;
    }

    private void Choose()
    {
        int index = SelectedIndex;
        Action<int> callback = onChosen;
        Close();
        callback?.Invoke(index);
    }

    private int Wrap(int i)
    {
        int n = choices != null ? Mathf.Max(1, choices.Count) : 1;
        return ((i % n) + n) % n;
    }

    private Vector3 Snap(Vector3 v)
        => new Vector3(Mathf.Round(v.x * pixelsPerUnit) / pixelsPerUnit, Mathf.Round(v.y * pixelsPerUnit) / pixelsPerUnit, v.z);

    // Position along the slot path, t boxes from the center. Past the end slots the last segment is extended.
    private Vector3 PathPoint(float t)
    {
        float u = t + center;
        int i = Mathf.Clamp(Mathf.FloorToInt(u), 0, points.Length - 2);
        return Vector3.LerpUnclamped(points[i], points[i + 1], u - i);
    }

    private void FollowPlayer()
    {
        Vector3 anchor = Snap(player.transform.position + (Vector3)headOffset);
        transform.position = anchor - transform.TransformVector(points[center]);
    }

    private void Layout()
    {
        int nearest = Mathf.RoundToInt(scroll);
        for (int k = 0; k < items.Length; k++)
        {
            int i = nearest + k - items.Length / 2;
            float t = i - scroll;
            float distance = Mathf.Abs(t);
            float alpha = Mathf.Clamp01(center + 1 - distance); // solid on the slots, fades out one step past the ends
            Item item = items[k];
            item.root.gameObject.SetActive(alpha > 0f);
            if (alpha <= 0f) continue;

            item.root.position = Snap(transform.TransformPoint(PathPoint(t)));

            if (item.box != null)
            {
                Color tint = Color.Lerp(normalColor, selectedColor, Mathf.Clamp01(1f - distance));
                tint.a = alpha;
                item.box.color = tint;
            }

            int index = Wrap(i);
            if (item.shownIndex != index)
            {
                item.shownIndex = index;
                Sprite icon = choices[index].icon;
                if (item.icon != null)
                {
                    item.icon.sprite = icon;
                    item.icon.enabled = icon != null;
                }
                bool showIcon = icon != null && item.icon != null;
                if (item.number != null) item.number.text = showIcon ? string.Empty : (index + 1).ToString();
            }
            if (item.icon != null) item.icon.color = new Color(1f, 1f, 1f, alpha);
            if (item.number != null)
            {
                Color numberTint = numberColor;
                numberTint.a *= alpha;
                item.number.color = numberTint;
            }
            items[k] = item;
        }

        UpdateLabel(Wrap(nearest));
    }

    // Shows the centered choice's label in the box, or hides the box when that slot is empty.
    private void UpdateLabel(int index)
    {
        if (labelBox != null) labelBox.transform.position = Snap(transform.TransformPoint(labelBoxHome));
        if (index == labelIndex) return;
        labelIndex = index;
        string text = choices[index].label;
        bool filled = !string.IsNullOrWhiteSpace(text);
        ShowLabel(filled);
        if (!filled || label == null) return;

        label.text = text;
        // Baseline-left from the transform origin: the text starts on a whole pixel and stays crisp.
        // Centered text of odd width would land between pixels.
        label.horizontalAlignment = HorizontalAlignmentOptions.Left;
        label.verticalAlignment = VerticalAlignmentOptions.Baseline;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.pivot = new Vector2(0f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, 0f);
        float width = Mathf.Round(label.GetPreferredValues(text).x * pixelsPerUnit);
        // Capital height in art pixels, so the capitals sit in the middle of the box for any font and size.
        // With odd heights the spare pixel goes below, where descenders (g, y) need it.
        FaceInfo face = label.font.faceInfo;
        float capHeight = Mathf.Round(face.capLine * face.scale * label.fontSize * 0.1f / face.pointSize * pixelsPerUnit);
        label.transform.localPosition = new Vector3(Mathf.Round(-width * 0.5f), Mathf.Ceil(-capHeight * 0.5f), 0f) / pixelsPerUnit;

        if (labelBox == null) return;
        // Even sizes keep the centered box edges on whole pixels.
        int boxWidth = (int)width + labelPadding * 2;
        int boxHeight = (int)capHeight + labelVerticalPadding * 2;
        boxWidth += boxWidth % 2;
        boxHeight += boxHeight % 2;
        labelBox.size = new Vector2(boxWidth, boxHeight) / pixelsPerUnit;
    }

    private void ShowLabel(bool show)
    {
        if (labelBox != null) labelBox.gameObject.SetActive(show);
        if (label != null) label.gameObject.SetActive(show);
    }

    private bool Build()
    {
        if (items != null) return true;
        var valid = new List<Transform>();
        if (slots != null)
            foreach (Transform slot in slots)
                if (slot != null) valid.Add(slot);
        if (valid.Count < 3)
        {
            Debug.LogError("ChoiceWheel needs at least three slot boxes assigned in Slots.", this);
            return false;
        }
        valid.Sort((a, b) => transform.InverseTransformPoint(a.position).x.CompareTo(transform.InverseTransformPoint(b.position).x));

        points = new Vector3[valid.Count];
        for (int k = 0; k < valid.Count; k++) points[k] = transform.InverseTransformPoint(valid[k].position);
        center = (valid.Count - 1) / 2;
        Transform template = valid[center];

        visual = new GameObject("Items").transform;
        visual.SetParent(transform, false);
        // One extra box on each side slides in or out during a step.
        items = new Item[valid.Count + 2];
        for (int k = 0; k < items.Length; k++)
        {
            Transform copy = Instantiate(template, visual);
            copy.name = "Item " + k;
            copy.gameObject.SetActive(true);
            Transform iconTransform = copy.Find("Icon");
            items[k] = new Item
            {
                root = copy,
                box = copy.GetComponent<SpriteRenderer>(),
                icon = iconTransform != null ? iconTransform.GetComponent<SpriteRenderer>() : null,
                number = copy.GetComponentInChildren<TMP_Text>(true),
                shownIndex = -1,
            };
        }
        numberColor = items[0].number != null ? items[0].number.color : Color.black;
        if (labelBox != null) labelBoxHome = transform.InverseTransformPoint(labelBox.transform.position);

        foreach (Transform slot in valid) slot.gameObject.SetActive(false);
        visual.gameObject.SetActive(false);
        return true;
    }
}
