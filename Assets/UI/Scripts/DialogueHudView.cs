using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class DialogueHudView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] GameObject speakerTag;
        [SerializeField] TMP_Text speakerText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] GameObject nextIndicator;
        [SerializeField] Button continueButton;
        [SerializeField] RectTransform choicesRoot;
        [SerializeField] DialogueOptionView optionTemplate;
        [SerializeField, Min(1f)] float charactersPerSecond = 42f;

        [Header("Compact Presentation")]
        [Tooltip("Scales body, speaker tag, tail, indicator, and text together.")]
        [SerializeField, Range(0.25f, 1f)] float visualScale = 0.5f;
        [Tooltip("Rotates the complete dialogue card, including its text, tag, tail, and indicator.")]
        [SerializeField, Range(-10f, 10f)] float tiltDegrees = 2f;

        [Header("Painted Artwork Layout")]
        [SerializeField, Min(1f)] float bodyWidth = 267f;
        [SerializeField] Vector2 speakerMinimumSize = new(70f, 32f);
        [SerializeField] Vector2 speakerTextPadding = new(12f, 5f);
        [SerializeField, Min(0f)] float speakerLeftInset = 18f;
        [SerializeField, Min(0f)] float speakerBodyOverlap = 8f;
        [SerializeField] Vector2 tailPaintedSize = new(29f, 18f);
        [SerializeField, Min(0f)] float tailBodyOverlap = 2f;
        [SerializeField, Range(0f, 1f)] float preferredTailPosition = 0.82f;
        [SerializeField] Vector2 indicatorPaintedSize = new(11f, 9f);
        [SerializeField, HideInInspector] Artwork bodyArtwork = new();
        [SerializeField, HideInInspector] Artwork speakerArtwork = new();
        [SerializeField, HideInInspector] Artwork tailArtwork = new();
        [SerializeField, HideInInspector] Artwork indicatorArtwork = new();

        [Header("Text Pages")]
        [SerializeField, Range(1, 4)] int maximumLinesPerPage = 2;
        [Tooltip("Authored BodyText size. Used when an inactive prefab reports a zero rect during Awake.")]
        [SerializeField] Vector2 textAreaSize = new(210f, 48f);
        [Tooltip("Smallest height of the 9-sliced body image for a short, one-line message.")]
        [SerializeField, Min(1f)] float minimumBodyHeight = 48f;
        [Tooltip("Minimum content padding: left, top, right, bottom, in unscaled UI units.")]
        [SerializeField] Vector4 bodyPadding = new(24f, 20f, 24f, 12f);
        [SerializeField, Min(0f)] float speakerTextGap = 6f;

        [Header("Next Indicator")]
        [SerializeField, Min(0f)] float indicatorBobDistance = 2f;
        [SerializeField, Min(0f)] float indicatorBobCyclesPerSecond = 2.5f;
        [SerializeField, Min(0f)] float indicatorTextGap = 6f;

        [Header("Speaker Tracking")]
        [SerializeField] RectTransform bubbleRoot;
        [SerializeField] RectTransform tail;
        [SerializeField, Min(0f)] float screenMargin = 8f;
        [SerializeField, Min(0f)] float tailEdgePadding = 18f;

        [Header("Choice Positioning")]
        [Tooltip("Space between the dialogue bubble and its choices.")]
        [SerializeField, Min(0f)] float choiceGap = 6f;

        [Header("Dialogue Screen Presentation")]
        [Tooltip("Optional. Automatically finds GameplayControls/PauseButton in the same HUD.")]
        [SerializeField] GameObject pauseControl;
        [Tooltip("Optional. Automatically finds QuestTracker in the same HUD.")]
        [SerializeField] GameObject questTrackerControl;
        [SerializeField, Range(0.01f, 1f)] float scrimScreenHeight = 0.25f;
        [SerializeField, Range(0f, 1f)] float scrimTopOpacity = 0.65f;

        readonly List<DialogueOptionView> options = new();
        readonly List<string> pages = new();

        Action advance;
        float visibleCharacters;
        int lastTypedCharacter;
        int visibleCharactersSinceBlip;
        bool typing;
        int pageIndex;
        Transform activeSpeaker;
        Renderer activeSpeakerRenderer;
        Vector2 speakerOffset;
        Vector2 authoredBubblePosition;
        Vector2 authoredTextSize;
        Vector2 indicatorBasePosition;
        Vector3 compactBubbleScale = Vector3.one;
        Quaternion compactBubbleRotation = Quaternion.identity;
        float textTopInset;
        Rect bodyPaintRect;
        Rect speakerPaintRect;
        Rect tailPaintRect;
        float tailCenterX;
        Canvas canvas;
        RectTransform bubbleBody;
        RectTransform indicatorRect;
        DialogueTopGradient screenGradient;
        bool dialogueSessionActive;
        bool presentationActive;
        bool pauseWasActive;
        bool questTrackerWasActive;

        public bool IsVisible => group != null && group.alpha > 0f;

        void Awake()
        {
            ResolveLayoutReferences();
            // The procedural overlay replaces the old, texture-based scrim at runtime.
            Transform oldScrim = transform.Find("Scrim");
            if (oldScrim != null)
                oldScrim.gameObject.SetActive(false);
            CaptureAuthoredLayout();
            ApplyCompactScale();

            // Template exists only as clone source. Never show placeholder copy.
            if (optionTemplate != null)
                optionTemplate.gameObject.SetActive(false);

            if (continueButton != null)
                continueButton.onClick.AddListener(Continue);
            Hide();
        }

        void ResolveLayoutReferences()
        {
            if (bubbleRoot == null)
                bubbleRoot = transform.Find("BubbleRoot") as RectTransform;
            if (bubbleRoot == null) return;
            bubbleBody = bubbleRoot.Find("Body") as RectTransform;
            if (tail == null) tail = bubbleRoot.Find("Tail") as RectTransform;
            // Hide the tail's rounded top edge underneath the body at the join.
            if (tail != null) tail.SetAsFirstSibling();
            if (bodyText != null && bodyText.transform.parent != bubbleBody)
                bodyText.transform.SetParent(bubbleBody, false);
            indicatorRect = nextIndicator != null ? nextIndicator.transform as RectTransform : null;
            if (indicatorRect != null && bubbleBody != null && indicatorRect.parent != bubbleBody)
                indicatorRect.SetParent(bubbleBody, false);
            canvas = GetComponentInParent<Canvas>();
            // This view owns tag sizing. Old fitters must not resize the padded frame
            // after the painted background and its text have been aligned.
            if (speakerTag != null)
            {
                LayoutGroup layout = speakerTag.GetComponent<LayoutGroup>();
                if (layout != null) layout.enabled = false;
                ContentSizeFitter fitter = speakerTag.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.enabled = false;
            }
        }

        // Refresh deliberately, rather than running layout continuously in Edit Mode.
        // This stores sprite alpha bounds for builds without enabling Read/Write.
        [ContextMenu("Refresh Artwork Layout")]
        public void RefreshArtworkLayout()
        {
            ResolveLayoutReferences();
#if UNITY_EDITOR
            bodyArtwork.Measure(bubbleBody != null ? bubbleBody.GetComponent<Image>() : null);
            speakerArtwork.Measure(speakerTag != null ? speakerTag.GetComponent<Image>() : null);
            tailArtwork.Measure(tail != null ? tail.GetComponent<Image>() : null);
            indicatorArtwork.Measure(indicatorRect != null ? indicatorRect.GetComponent<Image>() : null);
#endif
            CaptureAuthoredLayout();
            ApplyCompactScale();
            FitBubbleToPage(bodyText != null ? bodyText.text : string.Empty);
        }

        void OnDestroy()
        {
            SetScreenPresentation(false);
            if (screenGradient != null)
                Destroy(screenGradient.gameObject);
            if (continueButton != null)
                continueButton.onClick.RemoveListener(Continue);
        }

        void OnDisable() => SetScreenPresentation(false);

        void OnEnable()
        {
            if (dialogueSessionActive || IsVisible)
                SetScreenPresentation(true);
        }

        // The runner owns this hold so cutscene commands between lines cannot
        // briefly restore the exploration HUD while the conversation still runs.
        public void SetDialogueSessionActive(bool active)
        {
            if (active)
                EnsureActive();
            dialogueSessionActive = active;
            SetScreenPresentation(active || IsVisible);
        }

        void SetScreenPresentation(bool active)
        {
            if (active && !presentationActive)
            {
                Transform hudArea = transform.parent;
                if (pauseControl == null && hudArea != null)
                    pauseControl = hudArea.Find("GameplayControls/PauseButton")?.gameObject;
                if (questTrackerControl == null && hudArea != null)
                    questTrackerControl = hudArea.Find("QuestTracker")?.gameObject;
                pauseWasActive = pauseControl != null && pauseControl.activeSelf;
                questTrackerWasActive = questTrackerControl != null && questTrackerControl.activeSelf;
                if (pauseControl != null) pauseControl.SetActive(false);
                if (questTrackerControl != null) questTrackerControl.SetActive(false);
            }
            else if (!active && presentationActive)
            {
                if (pauseControl != null) pauseControl.SetActive(pauseWasActive);
                if (questTrackerControl != null) questTrackerControl.SetActive(questTrackerWasActive);
            }
            presentationActive = active;

            if (active && screenGradient == null && canvas != null)
            {
                // Root canvas deliberately bypasses safe-area insets: the gradient
                // must reach the physical top and both sides of the screen.
                var overlay = new GameObject("DialogueTopGradient", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(DialogueTopGradient));
                overlay.layer = canvas.rootCanvas.gameObject.layer;
                overlay.transform.SetParent(canvas.rootCanvas.transform, false);
                overlay.transform.SetAsFirstSibling();
                screenGradient = overlay.GetComponent<DialogueTopGradient>();
            }
            if (screenGradient != null)
            {
                screenGradient.Configure(scrimTopOpacity, scrimScreenHeight);
                screenGradient.gameObject.SetActive(active);
            }
        }

        void Update()
        {
            if (typing && bodyText != null)
            {
                visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
                int nextVisible = Mathf.Min(
                    Mathf.FloorToInt(visibleCharacters),
                    bodyText.textInfo.characterCount);
                bool playBlip = false;
                for (int index = lastTypedCharacter; index < nextVisible; index++)
                {
                    if (!bodyText.textInfo.characterInfo[index].isVisible)
                        continue;
                    visibleCharactersSinceBlip++;
                    if (visibleCharactersSinceBlip >= 2)
                    {
                        visibleCharactersSinceBlip = 0;
                        playBlip = true;
                    }
                }

                lastTypedCharacter = nextVisible;
                bodyText.maxVisibleCharacters = nextVisible;
                if (playBlip)
                    SunriseAudio.Play("dialogue_blip");
                if (bodyText.maxVisibleCharacters >= bodyText.textInfo.characterCount)
                    CompleteTyping();
            }

            AnimateNextIndicator();
        }

        void LateUpdate()
        {
            if (IsVisible)
            {
                FollowSpeaker();
                if (options.Count > 0)
                    PositionChoicesNearBubble();
            }
        }

        public void ShowLine(string speaker, string text, Transform talker, Vector2 talkerOffset, Action onAdvance)
        {
            EnsureActive();
            ShowGroup();
            ClearChoices();
            activeSpeaker = talker;
            activeSpeakerRenderer = FindSpeakerRenderer(talker);
            speakerOffset = talkerOffset;
            advance = onAdvance;
            speakerText.text = speaker ?? string.Empty;
            speakerTag.SetActive(!string.IsNullOrWhiteSpace(speaker));

            BuildPages(text ?? string.Empty);
            pageIndex = 0;
            ShowCurrentPage();
            continueButton.interactable = true;
            FollowSpeaker();
        }

        public void ShowChoices(IReadOnlyList<string> labels, Action<int> onChosen)
        {
            EnsureActive();
            ShowGroup();
            CompleteTyping();
            advance = null;
            pages.Clear();
            continueButton.interactable = false;
            SetIndicatorVisible(false);
            ClearChoices();

            for (int index = 0; index < labels.Count; index++)
            {
                int captured = index;
                DialogueOptionView option = Instantiate(optionTemplate, choicesRoot);
                option.name = $"Choice_{index + 1}";
                option.gameObject.SetActive(true);
                option.Bind(labels[index], () =>
                {
                    SunriseAudio.Play("dialogue_choice");
                    onChosen(captured);
                });
                options.Add(option);
            }

            Canvas.ForceUpdateCanvases();
            PrepareChoicesLayout();
            PositionChoicesNearBubble();
        }

        public void ClearChoices()
        {
            foreach (DialogueOptionView option in options)
                if (option != null)
                {
                    option.gameObject.SetActive(false);
                    Destroy(option.gameObject);
                }
            options.Clear();
            if (optionTemplate != null)
                optionTemplate.gameObject.SetActive(false);
        }

        void PrepareChoicesLayout()
        {
            if (choicesRoot == null || options.Count == 0)
                return;

            float width = 1f;
            float height = 0f;
            int activeCount = 0;
            foreach (DialogueOptionView option in options)
            {
                if (option == null || !option.gameObject.activeSelf ||
                    option.transform is not RectTransform optionRect)
                    continue;

                optionRect.ForceUpdateRectTransforms();
                width = Mathf.Max(width, optionRect.rect.width);
                height += optionRect.rect.height;
                activeCount++;
            }

            VerticalLayoutGroup layout = choicesRoot.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                width += layout.padding.left + layout.padding.right;
                height += layout.padding.top + layout.padding.bottom;
                if (activeCount > 1)
                    height += layout.spacing * (activeCount - 1);
            }

            choicesRoot.anchorMin = new Vector2(0f, 1f);
            choicesRoot.anchorMax = new Vector2(0f, 1f);
            choicesRoot.pivot = new Vector2(1f, 1f);
            choicesRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            choicesRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, height));
            LayoutRebuilder.ForceRebuildLayoutImmediate(choicesRoot);
        }

        void PositionChoicesNearBubble()
        {
            if (choicesRoot == null || bubbleRoot == null || options.Count == 0 ||
                bubbleRoot.parent is not RectTransform parent || choicesRoot.parent != parent)
                return;

            float parentWidth = parent.rect.width;
            float parentHeight = parent.rect.height;
            Rect paintBounds = RotatedPaintBounds();
            float bubbleWidth = paintBounds.width;
            float bubbleHeight = paintBounds.height;
            float bubbleLeft = bubbleRoot.localPosition.x + paintBounds.xMin - parent.rect.xMin;
            float bubbleTop = parent.rect.yMax - (bubbleRoot.localPosition.y + paintBounds.yMax);
            float bubbleRight = bubbleLeft + bubbleWidth;
            float bubbleBottom = bubbleTop + bubbleHeight;
            float choiceWidth = choicesRoot.rect.width;
            float choiceHeight = choicesRoot.rect.height;
            float maximumLeft = Mathf.Max(screenMargin, parentWidth - screenMargin - choiceWidth);
            float maximumTop = Mathf.Max(screenMargin, parentHeight - screenMargin - choiceHeight);

            // Figma layout: choices sit immediately to the dialogue's right.
            float left = bubbleRight + choiceGap;
            float top = Mathf.Clamp(bubbleTop, screenMargin, maximumTop);
            bool fitsRight = left + choiceWidth <= parentWidth - screenMargin;

            if (!fitsRight)
            {
                // Near the right edge, move the complete stack above the dialogue
                // instead of clamping it left and letting the cards overlap the bubble.
                left = bubbleLeft + (bubbleWidth - choiceWidth) * 0.5f;
                top = bubbleTop - choiceGap - choiceHeight;
                bool fitsAbove = top >= screenMargin;

                if (!fitsAbove)
                {
                    top = bubbleBottom + choiceGap;
                    bool fitsBelow = top + choiceHeight <= parentHeight - screenMargin;
                    if (!fitsBelow)
                    {
                        left = bubbleLeft - choiceGap - choiceWidth;
                        top = Mathf.Clamp(bubbleTop, screenMargin, maximumTop);
                    }
                }
            }

            left = Mathf.Clamp(left, screenMargin, maximumLeft);
            top = Mathf.Clamp(top, screenMargin, maximumTop);
            choicesRoot.anchoredPosition = new Vector2(
                Mathf.Round(left + choiceWidth),
                -Mathf.Round(top));
        }

        public void Hide()
        {
            advance = null;
            typing = false;
            pages.Clear();
            pageIndex = 0;
            activeSpeaker = null;
            activeSpeakerRenderer = null;
            RestoreAuthoredLayout();
            ClearChoices();
            SetIndicatorVisible(false);
            if (group == null)
                return;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            SetScreenPresentation(dialogueSessionActive);
        }

        void ShowGroup()
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            SetScreenPresentation(true);
        }

        // DialogueLayer is saved inactive in the HUD prefab so it cannot flash on screen
        // before Awake hides its CanvasGroup. Activating it here runs Awake first, then the
        // caller can safely reveal the prepared line or choices.
        void EnsureActive()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // This view starts inactive, so TMP can report a zero-width rect during the
            // first frame it is revealed. Restore the authored width before pagination.
            if (bodyText != null && authoredTextSize.x > 0f)
                bodyText.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    authoredTextSize.x);
            Canvas.ForceUpdateCanvases();
        }

        void Continue()
        {
            if (typing)
            {
                CompleteTyping();
                return;
            }

            SunriseAudio.Play("dialogue_advance");
            if (pageIndex + 1 < pages.Count)
            {
                pageIndex++;
                ShowCurrentPage();
                return;
            }

            Action callback = advance;
            advance = null;
            SetIndicatorVisible(false);
            callback?.Invoke();
        }

        void ShowCurrentPage()
        {
            string page = pageIndex >= 0 && pageIndex < pages.Count
                ? pages[pageIndex]
                : string.Empty;

            bodyText.text = page;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.overflowMode = TextOverflowModes.Truncate;
            FitBubbleToPage(page);
            bodyText.ForceMeshUpdate();
            visibleCharacters = 0f;
            lastTypedCharacter = 0;
            visibleCharactersSinceBlip = 0;
            bodyText.maxVisibleCharacters = 0;
            typing = bodyText.textInfo.characterCount > 0 && charactersPerSecond > 0f;
            SetIndicatorVisible(false);

            if (!typing)
                CompleteTyping();
        }

        void CompleteTyping()
        {
            if (bodyText == null)
                return;
            typing = false;
            bodyText.maxVisibleCharacters = int.MaxValue;
            // The indicator means that more text is waiting. It never appears on the final page.
            SetIndicatorVisible(pageIndex + 1 < pages.Count);
        }

        void BuildPages(string fullText)
        {
            pages.Clear();
            string clean = NormalizeWhitespace(fullText);
            if (string.IsNullOrEmpty(clean))
            {
                pages.Add(string.Empty);
                return;
            }

            List<string> sentences = SplitSentences(clean);
            string current = string.Empty;
            foreach (string sentence in sentences)
            {
                string candidate = string.IsNullOrEmpty(current)
                    ? sentence
                    : current + " " + sentence;

                if (CountWrappedLines(candidate) <= maximumLinesPerPage)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                {
                    pages.Add(current);
                    current = string.Empty;
                }

                AddLongSentencePages(sentence, ref current);
            }

            if (!string.IsNullOrEmpty(current))
                pages.Add(current);
            if (pages.Count == 0)
                pages.Add(clean);
        }

        void AddLongSentencePages(string sentence, ref string current)
        {
            string[] words = sentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                if (string.IsNullOrEmpty(current) || CountWrappedLines(candidate) <= maximumLinesPerPage)
                {
                    current = candidate;
                    continue;
                }

                pages.Add(current);
                current = word;
            }
        }

        int CountWrappedLines(string value)
        {
            if (bodyText == null || string.IsNullOrEmpty(value))
                return 1;
            if (authoredTextSize.x > 0f)
                bodyText.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    authoredTextSize.x);
            bodyText.text = value;
            bodyText.maxVisibleCharacters = int.MaxValue;
            // Pagination must measure every line, even if the previous page's
            // display rectangle would truncate the candidate being measured.
            TextOverflowModes displayOverflow = bodyText.overflowMode;
            bodyText.overflowMode = TextOverflowModes.Overflow;
            bodyText.ForceMeshUpdate();
            int lineCount = Mathf.Max(1, bodyText.textInfo.lineCount);
            bodyText.overflowMode = displayOverflow;
            return lineCount;
        }

        static List<string> SplitSentences(string value)
        {
            var result = new List<string>();
            var sentence = new StringBuilder();
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                sentence.Append(character);
                bool punctuation = character == '.' || character == '!' || character == '?';
                bool boundary = punctuation &&
                                (index + 1 >= value.Length || char.IsWhiteSpace(value[index + 1]));
                if (!boundary)
                    continue;

                string finished = sentence.ToString().Trim();
                if (finished.Length > 0)
                    result.Add(finished);
                sentence.Clear();
            }

            string remaining = sentence.ToString().Trim();
            if (remaining.Length > 0)
                result.Add(remaining);
            return result;
        }

        static string NormalizeWhitespace(string value)
        {
            var result = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            foreach (char character in value)
            {
                bool isSpace = char.IsWhiteSpace(character);
                if (isSpace)
                {
                    if (!previousWasSpace && result.Length > 0)
                        result.Append(' ');
                }
                else
                    result.Append(character);
                previousWasSpace = isSpace;
            }
            return result.ToString().Trim();
        }

        void CaptureAuthoredLayout()
        {
            if (bubbleRoot == null) return;
            authoredBubblePosition = bubbleRoot.anchoredPosition;
            // Absolute values keep a saved preview from applying scale/tilt twice.
            compactBubbleScale = new Vector3(visualScale, visualScale, 1f);
            compactBubbleRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
            tailCenterX = bodyWidth * preferredTailPosition;
            authoredTextSize = new Vector2(
                Mathf.Max(1f, Mathf.Min(textAreaSize.x, bodyWidth - bodyPadding.x - bodyPadding.z)),
                textAreaSize.y);
            if (bodyText != null)
            {
                bodyText.enableAutoSizing = false;
                bodyText.horizontalAlignment = HorizontalAlignmentOptions.Left;
                bodyText.verticalAlignment = VerticalAlignmentOptions.Top;
                bodyText.margin = Vector4.zero;
                bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, authoredTextSize.x);
            }
            FitBubbleToPage(bodyText != null ? bodyText.text : string.Empty);
        }

        void RestoreAuthoredLayout()
        {
            if (bubbleRoot == null) return;
            bubbleRoot.anchoredPosition = authoredBubblePosition;
            ApplyCompactScale();
            tailCenterX = bodyWidth * preferredTailPosition;
            FitBubbleToPage(bodyText != null ? bodyText.text : string.Empty);
        }

        float GetTextTopInset() => Mathf.Max(bodyPadding.y,
            speakerTag != null && speakerTag.activeSelf ? speakerBodyOverlap + speakerTextGap : 0f);

        void PositionIndicatorInsideBody()
        {
            if (indicatorRect == null || bubbleBody == null) return;
            Rect painted = new Rect(
                bodyWidth - bodyPadding.z - indicatorPaintedSize.x,
                bodyPaintRect.yMin + bodyPadding.w + indicatorBobDistance,
                indicatorPaintedSize.x, indicatorPaintedSize.y);
            painted.position -= bubbleBody.anchoredPosition;
            indicatorArtwork.Place(indicatorRect.GetComponent<Image>(), painted);
            indicatorBasePosition = indicatorRect.anchoredPosition;
        }

        void LayoutSpeakerTag()
        {
            speakerPaintRect = Rect.zero;
            if (speakerTag == null || speakerText == null) return;
            var tag = speakerTag.GetComponent<Image>();
            if (tag == null) return;
            speakerText.enableAutoSizing = false;
            speakerText.textWrappingMode = TextWrappingModes.NoWrap;
            speakerText.overflowMode = TextOverflowModes.Ellipsis;
            speakerText.alignment = TextAlignmentOptions.Center;
            speakerText.margin = Vector4.zero;
            Vector2 textSize = speakerText.GetPreferredValues(speakerText.text);
            float width = Mathf.Min(bodyWidth - speakerLeftInset, Mathf.Max(speakerMinimumSize.x,
                textSize.x + 2f * speakerTextPadding.x));
            float height = Mathf.Max(speakerMinimumSize.y, textSize.y + 2f * speakerTextPadding.y);
            speakerPaintRect = new Rect(speakerLeftInset, -speakerBodyOverlap, width, height);
            speakerArtwork.Place(tag, speakerPaintRect);
            Rect localPaint = speakerArtwork.LocalRect(tag);
            SetTextRect(speakerText.rectTransform, new Vector2(
                localPaint.xMin + speakerTextPadding.x, localPaint.yMax - speakerTextPadding.y),
                new Vector2(Mathf.Max(1f, width - 2f * speakerTextPadding.x),
                    Mathf.Max(1f, height - 2f * speakerTextPadding.y)));
        }

        void LayoutTail()
        {
            if (tail == null) return;
            tailCenterX = Mathf.Clamp(tailCenterX, tailEdgePadding, bodyWidth - tailEdgePadding);
            tailPaintRect = new Rect(tailCenterX - tailPaintedSize.x * 0.5f,
                bodyPaintRect.yMin + tailBodyOverlap - tailPaintedSize.y,
                tailPaintedSize.x, tailPaintedSize.y);
            tailArtwork.Place(tail.GetComponent<Image>(), tailPaintRect);
        }

        static void SetTextRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        void FitBubbleToPage(string page)
        {
            if (bubbleRoot == null || bubbleBody == null || bodyText == null) return;
            LayoutSpeakerTag();
            textTopInset = GetTextTopInset();
            float preferredTextHeight = Mathf.Ceil(bodyText.GetPreferredValues(page, authoredTextSize.x, 0f).y);
            float bottomInset = Mathf.Max(0f, bodyPadding.w);
            if (indicatorRect != null && pageIndex + 1 < pages.Count)
                bottomInset += indicatorPaintedSize.y + indicatorBobDistance * 2f + indicatorTextGap;
            float bodyHeight = Mathf.Max(minimumBodyHeight, preferredTextHeight + textTopInset + bottomInset);
            bodyPaintRect = new Rect(0f, -bodyHeight, bodyWidth, bodyHeight);
            bubbleRoot.anchorMin = bubbleRoot.anchorMax = bubbleRoot.pivot = new Vector2(0f, 1f);
            bubbleRoot.sizeDelta = new Vector2(bodyWidth, bodyHeight + tailPaintedSize.y - tailBodyOverlap);
            bodyArtwork.Place(bubbleBody.GetComponent<Image>(), bodyPaintRect);
            SetTextRect(bodyText.rectTransform,
                new Vector2(bodyPadding.x, -textTopInset) - bubbleBody.anchoredPosition,
                new Vector2(authoredTextSize.x, Mathf.Max(1f, preferredTextHeight)));
            LayoutTail();
            PositionIndicatorInsideBody();
        }

        void SetIndicatorVisible(bool visible)
        {
            if (nextIndicator != null)
                nextIndicator.SetActive(visible);
            if (!visible && indicatorRect != null)
                indicatorRect.anchoredPosition = indicatorBasePosition;
        }

        void AnimateNextIndicator()
        {
            if (nextIndicator == null || indicatorRect == null || !nextIndicator.activeSelf)
                return;

            float wave = Mathf.Sin(Time.unscaledTime * indicatorBobCyclesPerSecond * Mathf.PI * 2f);
            Vector2 position = indicatorBasePosition;
            position.y += Mathf.Round(wave * indicatorBobDistance);
            indicatorRect.anchoredPosition = position;
        }

        void FollowSpeaker()
        {
            if (activeSpeaker == null || bubbleRoot == null || tail == null)
                return;

            Camera worldCamera = Camera.main;
            RectTransform parent = bubbleRoot.parent as RectTransform;
            if (worldCamera == null || parent == null)
                return;

            Vector3 speakerTop = activeSpeakerRenderer != null
                ? new Vector3(activeSpeakerRenderer.bounds.center.x, activeSpeakerRenderer.bounds.max.y, activeSpeakerRenderer.bounds.center.z)
                : activeSpeaker.position;
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(speakerTop + (Vector3)speakerOffset);
            if (screenPoint.z < 0f)
                return;

            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, uiCamera, out Vector2 point))
                return;

            PositionAboveSpeaker(point);
        }

        // Parent-local coordinates, with the actual painted tip as the attachment.
        // All rotation and scale are included before clamping the card to the screen.
        public void PositionAboveSpeaker(Vector2 point)
        {
            if (bubbleRoot == null || tail == null || bubbleRoot.parent is not RectTransform parent) return;
            Rect safe = parent.rect;
            safe.xMin += screenMargin; safe.xMax -= screenMargin;
            safe.yMin += screenMargin; safe.yMax -= screenMargin;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                LayoutTail();
                Vector2 tip = TailTipInBubble();
                Vector2 desired = point - TransformBubbleOffset(tip);
                Rect bounds = RotatedPaintBounds();
                Vector2 clamped = new Vector2(
                    Mathf.Clamp(desired.x, safe.xMin - bounds.xMin, Mathf.Max(safe.xMin - bounds.xMin, safe.xMax - bounds.xMax)),
                    Mathf.Clamp(desired.y, safe.yMin - bounds.yMin, Mathf.Max(safe.yMin - bounds.yMin, safe.yMax - bounds.yMax)));
                bubbleRoot.localPosition = new Vector3(clamped.x, clamped.y, bubbleRoot.localPosition.z);
                if ((desired - clamped).sqrMagnitude < 0.0001f) return;
                Vector3 localTarget = Quaternion.Inverse(bubbleRoot.localRotation) * (Vector3)(point - clamped);
                float correctionX = localTarget.x / bubbleRoot.localScale.x - tip.x;
                float nextX = Mathf.Clamp(tailCenterX + correctionX, tailEdgePadding, bodyWidth - tailEdgePadding);
                if (Mathf.Abs(nextX - tailCenterX) < 0.001f) return;
                tailCenterX = nextX;
            }
        }

        Vector2 TailTipInBubble() => tail.anchoredPosition + tailArtwork.LocalTip(tail.GetComponent<Image>());

        Vector2 TransformBubbleOffset(Vector2 point) =>
            bubbleRoot.localRotation * Vector3.Scale(point, bubbleRoot.localScale);

        Rect RotatedPaintBounds()
        {
            Rect bounds = new Rect(TransformBubbleOffset(bodyPaintRect.min), Vector2.zero);
            IncludeRotatedRect(ref bounds, bodyPaintRect);
            if (speakerTag != null && speakerTag.activeSelf) IncludeRotatedRect(ref bounds, speakerPaintRect);
            if (tail != null) IncludeRotatedRect(ref bounds, tailPaintRect);
            return bounds;
        }

        void IncludeRotatedRect(ref Rect bounds, Rect painted)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 point = TransformBubbleOffset(new Vector2(
                    (corner & 1) == 0 ? painted.xMin : painted.xMax,
                    (corner & 2) == 0 ? painted.yMin : painted.yMax));
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, point.x), Mathf.Min(bounds.yMin, point.y),
                    Mathf.Max(bounds.xMax, point.x), Mathf.Max(bounds.yMax, point.y));
            }
        }

        void ApplyCompactScale()
        {
            if (bubbleRoot == null)
                return;

            bubbleRoot.localScale = compactBubbleScale;
            bubbleRoot.localRotation = compactBubbleRotation;
        }

        [Serializable]
        sealed class Artwork
        {
            [SerializeField] Sprite measuredSprite;
            [SerializeField] Rect alpha = new(0f, 0f, 1f, 1f);
            [SerializeField] Vector2 tip = new(0.5f, 0f);

            Rect Bounds(Image image) => image != null && image.sprite == measuredSprite ? alpha : new Rect(0f, 0f, 1f, 1f);

            public Rect LocalRect(Image image)
            {
                Rect a = Bounds(image);
                return Rect.MinMaxRect(Map(image, a.min).x, Map(image, a.min).y,
                    Map(image, a.max).x, Map(image, a.max).y);
            }

            public Vector2 LocalTip(Image image) => Map(image,
                image != null && image.sprite == measuredSprite ? tip : new Vector2(0.5f, 0f));

            Vector2 Map(Image image, Vector2 normalized)
            {
                Rect rect = image.rectTransform.rect;
                if (image.type != Image.Type.Sliced || image.sprite == null || image.sprite.border == Vector4.zero)
                    return rect.min + Vector2.Scale(normalized, rect.size);
                Vector2 pixels = image.sprite.rect.size;
                Vector4 border = image.sprite.border;
                float ppu = image.pixelsPerUnit * image.pixelsPerUnitMultiplier;
                return rect.min + new Vector2(
                    Slice(normalized.x * pixels.x, pixels.x, border.x, border.z, rect.width, ppu),
                    Slice(normalized.y * pixels.y, pixels.y, border.y, border.w, rect.height, ppu));
            }

            static float Slice(float pixel, float pixels, float low, float high, float size, float ppu)
            {
                float scale = Mathf.Min(1f, size * ppu / Mathf.Max(0.001f, low + high));
                float start = low / ppu * scale;
                float end = high / ppu * scale;
                if (pixel <= low && low > 0f) return pixel / low * start;
                if (pixel >= pixels - high && high > 0f) return size - (pixels - pixel) / high * end;
                return start + (pixel - low) / Mathf.Max(0.001f, pixels - low - high) * (size - start - end);
            }

            public void Place(Image image, Rect painted)
            {
                if (image == null) return;
                Rect a = Bounds(image);
                Vector2 size;
                if (image.type == Image.Type.Sliced && image.sprite != null && image.sprite.border != Vector4.zero)
                {
                    float ppu = image.pixelsPerUnit * image.pixelsPerUnitMultiplier;
                    Vector2 pixels = image.sprite.rect.size;
                    size = painted.size + new Vector2((1f - a.width) * pixels.x / ppu,
                        (1f - a.height) * pixels.y / ppu);
                }
                else size = new Vector2(painted.width / Mathf.Max(0.001f, a.width), painted.height / Mathf.Max(0.001f, a.height));
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.sizeDelta = size;
                image.preserveAspect = false;
                image.useSpriteMesh = false;
                rect.anchoredPosition = painted.min - LocalRect(image).min;
            }

#if UNITY_EDITOR
            public void Measure(Image image)
            {
                if (image == null || image.sprite == null) return;
                string path = UnityEditor.AssetDatabase.GetAssetPath(image.sprite);
                if (!System.IO.File.Exists(path)) return;
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!texture.LoadImage(System.IO.File.ReadAllBytes(path))) return;
                    Color32[] pixels = texture.GetPixels32();
                    Rect spriteRect = image.sprite.rect;
                    int left = (int)spriteRect.xMax, bottom = (int)spriteRect.yMax;
                    int right = -1, top = -1;
                    for (int y = (int)spriteRect.yMin; y < spriteRect.yMax; y++)
                        for (int x = (int)spriteRect.xMin; x < spriteRect.xMax; x++)
                            if (pixels[y * texture.width + x].a > 4)
                            {
                                left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                            }
                    if (right < left) return;
                    float sumX = 0f; int count = 0;
                    for (int x = left; x <= right; x++)
                        if (pixels[bottom * texture.width + x].a > 4) { sumX += x + 0.5f; count++; }
                    alpha = new Rect((left - spriteRect.x) / spriteRect.width, (bottom - spriteRect.y) / spriteRect.height,
                        (right + 1 - left) / spriteRect.width, (top + 1 - bottom) / spriteRect.height);
                    tip = new Vector2((sumX / Mathf.Max(1, count) - spriteRect.x) / spriteRect.width,
                        (bottom + 0.5f - spriteRect.y) / spriteRect.height);
                    measuredSprite = image.sprite;
                }
                finally { DestroyImmediate(texture); }
            }
#endif
        }

        static Renderer FindSpeakerRenderer(Transform talker)
        {
            if (talker == null)
                return null;
            if (talker.TryGetComponent(out Renderer own))
                return own;
            Transform visual = talker.Find("Visual");
            return visual != null ? visual.GetComponent<Renderer>() : talker.GetComponentInChildren<Renderer>();
        }
    }
}
