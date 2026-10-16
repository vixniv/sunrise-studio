using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// World-space speech box shown above whoever is talking. Each line types out letter by letter; a tap while typing
// shows the whole line, the next tap goes to the next line. After the last line it closes.
// Yarn dialogue uses Say instead: one line at a time, and the box stays open until Close.
// The box grows to fit each line: text wraps at maxTextWidth and the box height follows the line count.
public class DialogBubble : MonoBehaviour
{
    [Tooltip("Needs a 9-sliced sprite with a center pivot and Draw Mode set to Sliced or Tiled.")]
    [SerializeField] private SpriteRenderer box;
    [Tooltip("Put it inside the box.")]
    [SerializeField] private TMP_Text text;
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;
    [Tooltip("Widest text line in pixels before it wraps.")]
    [SerializeField, Min(16)] private int maxTextWidth = 160;
    [Tooltip("Pixels between the text and the box edges (x: left/right, y: top/bottom).")]
    [SerializeField] private Vector2Int padding = new Vector2Int(6, 4);

    [Header("Typewriter")]
    [Tooltip("Letters revealed per second. 0 shows each line at once.")]
    [SerializeField, Min(0f)] private float charactersPerSecond = 40f;
    [Tooltip("Extra seconds after . ! ? (half of it after , ; :).")]
    [SerializeField, Min(0f)] private float punctuationPause = 0.2f;

    private PlayerMove player;
    private Transform speaker;
    private Renderer speakerRenderer;
    private Vector2 offset;
    private IReadOnlyList<string> lines;
    private int lineIndex;
    private Action onFinished;
    private int openedFrame;
    private int boxHeight;
    private bool waitForNext; // Say mode: the last tap reports back instead of closing
    private int visibleCount;
    private int totalCount;
    private float typeTimer;

    public bool IsOpen => lines != null;
    public bool IsTyping => IsOpen && visibleCount < totalCount;

    private void Awake()
    {
        if (box != null) box.gameObject.SetActive(false);
    }

    // Shows the lines one at a time above talker's sprite (plus talkerOffset). finished runs after the last line closes.
    public void Show(Transform talker, Vector2 talkerOffset, IReadOnlyList<string> dialogLines, PlayerMove listener, Action finished)
    {
        if (IsOpen || talker == null || dialogLines == null || dialogLines.Count == 0 || box == null || text == null)
        {
            finished?.Invoke();
            return;
        }
        speaker = talker;
        speakerRenderer = talker.GetComponent<Renderer>();
        offset = talkerOffset;
        lines = dialogLines;
        onFinished = finished;
        player = listener;
        if (player != null) player.InteractionMenuOpen = true;
        waitForNext = false;
        openedFrame = Time.frameCount;
        box.gameObject.SetActive(true);
        ShowLine(0);
        Follow();
    }

    // Shows one line above talker's sprite (plus talkerOffset); advanced runs on the tap that ends it.
    // The line stays up until the next Say or Close, so the question is still readable while choices are open.
    public void Say(Transform talker, Vector2 talkerOffset, string line, PlayerMove listener, Action advanced)
    {
        if ((IsOpen && !waitForNext) || talker == null || string.IsNullOrEmpty(line) || box == null || text == null)
        {
            advanced?.Invoke();
            return;
        }
        speaker = talker;
        speakerRenderer = talker.GetComponent<Renderer>();
        offset = talkerOffset;
        lines = new[] { line };
        onFinished = advanced;
        if (player != null && player != listener) player.InteractionMenuOpen = false;
        player = listener;
        if (player != null) player.InteractionMenuOpen = true;
        waitForNext = true;
        openedFrame = Time.frameCount;
        box.gameObject.SetActive(true);
        ShowLine(0);
        Follow();
    }

    public void Close()
    {
        if (!IsOpen) return;
        if (player != null) player.InteractionMenuOpen = false;
        Action finished = onFinished;
        player = null;
        speaker = null;
        lines = null;
        onFinished = null;
        if (box != null) box.gameObject.SetActive(false);
        finished?.Invoke();
    }

    private void OnDisable() => Close();

    private void Update()
    {
        if (!IsOpen) return;
        // Ignore the tap that picked the choice which started this dialog.
        if (Time.frameCount != openedFrame) ReadTap();
        if (IsOpen) Type();
    }

    private void ReadTap()
    {
        Pointer pointer = Pointer.current;
        // Advance on release, so the touch that ends the dialog cannot also start a new interaction.
        if (pointer == null || !pointer.press.wasReleasedThisFrame) return;
        if (IsTyping) RevealAll();
        else if (lineIndex + 1 < lines.Count) ShowLine(lineIndex + 1);
        else if (waitForNext)
        {
            // Later taps do nothing until the next Say.
            Action advanced = onFinished;
            onFinished = null;
            advanced?.Invoke();
        }
        else Close();
    }

    private void LateUpdate()
    {
        if (IsOpen) Follow();
    }

    // Editor helper: lays out the box around the text currently typed in the Text object, exactly as the game does.
    [ContextMenu("Preview Layout")]
    private void PreviewLayout()
    {
        if (box == null || text == null) return;
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { box, text, text.rectTransform }, "Preview Dialog Layout");
#endif
        text.maxVisibleCharacters = 99999;
        LayoutLine(string.IsNullOrWhiteSpace(text.text) ? "Lorem ipsum dolor sit amet, consectetur adipiscing elit." : text.text);
    }

    // The box is laid out for the whole line first, so it keeps its size while the letters appear.
    private void ShowLine(int index)
    {
        lineIndex = index;
        LayoutLine(lines[index]);
        text.ForceMeshUpdate();
        totalCount = text.textInfo.characterCount;
        visibleCount = charactersPerSecond > 0f ? 0 : totalCount;
        typeTimer = 0f;
        text.maxVisibleCharacters = visibleCount;
    }

    private void RevealAll()
    {
        visibleCount = totalCount;
        text.maxVisibleCharacters = visibleCount;
    }

    private void Type()
    {
        if (visibleCount >= totalCount) return;
        if (charactersPerSecond <= 0f) { RevealAll(); return; }
        typeTimer -= Time.unscaledDeltaTime;
        while (typeTimer <= 0f && visibleCount < totalCount)
        {
            typeTimer += 1f / charactersPerSecond + PauseAfter(visibleCount);
            visibleCount++;
        }
        text.maxVisibleCharacters = visibleCount;
    }

    private float PauseAfter(int index)
    {
        switch (text.textInfo.characterInfo[index].character)
        {
            case '.': case '!': case '?': return punctuationPause;
            case ',': case ';': case ':': return punctuationPause * 0.5f;
            default: return 0f;
        }
    }

    private void LayoutLine(string line)
    {
        float maxWidth = maxTextWidth / (float)pixelsPerUnit;
        text.text = line;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
        // Top-left pivot: the text hangs down from a corner placed on a whole pixel, so it stays crisp.
        text.rectTransform.pivot = new Vector2(0f, 1f);
        text.rectTransform.sizeDelta = new Vector2(maxWidth, 0f);

        Vector2 size = text.GetPreferredValues(line, maxWidth, 0f);
        int width = Mathf.CeilToInt(size.x * pixelsPerUnit - 0.01f) + padding.x * 2;
        int height = Mathf.CeilToInt(size.y * pixelsPerUnit - 0.01f) + padding.y * 2;
        // Even sizes keep the centered box edges on whole pixels.
        width += width % 2;
        height += height % 2;
        boxHeight = height;
        box.size = new Vector2(width, height) / pixelsPerUnit;
        text.transform.localPosition = new Vector3(-width / 2 + padding.x, height / 2 - padding.y, 0f) / pixelsPerUnit;
    }

    // Keeps the box's bottom edge just above the top of the speaker's sprite.
    private void Follow()
    {
        Vector3 top = speakerRenderer != null
            ? new Vector3(speakerRenderer.bounds.center.x, speakerRenderer.bounds.max.y, 0f)
            : speaker.position;
        Vector3 anchor = top + (Vector3)offset;
        float x = Mathf.Round(anchor.x * pixelsPerUnit) / pixelsPerUnit;
        float y = (Mathf.Round(anchor.y * pixelsPerUnit) + boxHeight / 2) / pixelsPerUnit;
        box.transform.position = new Vector3(x, y, box.transform.position.z);
    }
}
