using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// World-space speech box shown above whoever is talking. Tap anywhere for the next line; after the last line it closes.
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

    private PlayerMove player;
    private Transform speaker;
    private Renderer speakerRenderer;
    private Vector2 offset;
    private IReadOnlyList<string> lines;
    private int lineIndex;
    private Action onFinished;
    private int openedFrame;
    private int boxHeight;

    public bool IsOpen => lines != null;

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
        // Ignore the tap that picked the choice which started this dialog.
        if (!IsOpen || Time.frameCount == openedFrame) return;
        Pointer pointer = Pointer.current;
        // Advance on release, so the touch that ends the dialog cannot also start a new interaction.
        if (pointer == null || !pointer.press.wasReleasedThisFrame) return;
        if (lineIndex + 1 < lines.Count) ShowLine(lineIndex + 1);
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
        LayoutLine(string.IsNullOrWhiteSpace(text.text) ? "Lorem ipsum dolor sit amet, consectetur adipiscing elit." : text.text);
    }

    private void ShowLine(int index)
    {
        lineIndex = index;
        LayoutLine(lines[index]);
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
