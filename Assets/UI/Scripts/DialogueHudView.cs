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
        Vector2 authoredTailPosition;
        Vector2 authoredBubbleSize;
        Vector2 authoredBodySize;
        Vector2 authoredTextSize;
        Vector2 authoredTextPosition;
        Vector2 indicatorBasePosition;
        readonly Vector3[] speakerTagCorners = new Vector3[4];
        Vector3 compactBubbleScale = Vector3.one;
        Quaternion compactBubbleRotation = Quaternion.identity;
        float textTopInset;
        float bubbleExtraHeight;
        float tailOverlap;
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
            if (bubbleRoot == null)
                bubbleRoot = transform.Find("BubbleRoot") as RectTransform;
            if (tail == null && bubbleRoot != null)
                tail = bubbleRoot.Find("Tail") as RectTransform;
            if (bubbleRoot != null)
                bubbleBody = bubbleRoot.Find("Body") as RectTransform;
            EnsureBodyTextIsInsideBody();
            if (nextIndicator != null)
                indicatorRect = nextIndicator.transform as RectTransform;
            if (indicatorRect != null && bubbleBody != null)
            {
                // The indicator belongs to the resized body, not the root plus tail.
                indicatorRect.SetParent(bubbleBody, false);
                indicatorRect.anchorMin = indicatorRect.anchorMax = new Vector2(1f, 0f);
                indicatorRect.pivot = new Vector2(0.5f, 0.5f);
                indicatorRect.localScale = Vector3.one;
                indicatorRect.localRotation = Quaternion.identity;
            }

            canvas = GetComponentInParent<Canvas>();
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

        void EnsureBodyTextIsInsideBody()
        {
            if (bodyText == null || bubbleBody == null)
                return;

            RectTransform textRect = bodyText.rectTransform;
            if (textRect.parent == bubbleBody)
                return;

            // Some HUD prefab revisions saved BodyText beside Body. That makes its
            // position independent from the panel being resized. Reparent it while
            // retaining the authored top-left layout values.
            Vector2 anchorMin = textRect.anchorMin;
            Vector2 anchorMax = textRect.anchorMax;
            Vector2 pivot = textRect.pivot;
            Vector2 position = textRect.anchoredPosition;
            Vector2 size = textRect.sizeDelta;
            Quaternion rotation = textRect.localRotation;
            Vector3 scale = textRect.localScale;

            textRect.SetParent(bubbleBody, false);
            textRect.anchorMin = anchorMin;
            textRect.anchorMax = anchorMax;
            textRect.pivot = pivot;
            textRect.anchoredPosition = position;
            textRect.sizeDelta = size;
            textRect.localRotation = rotation;
            textRect.localScale = scale;
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
            float bubbleWidth = bubbleRoot.rect.width * Mathf.Abs(bubbleRoot.localScale.x);
            float bubbleHeight = bubbleRoot.rect.height * Mathf.Abs(bubbleRoot.localScale.y);
            float bubbleLeft = bubbleRoot.anchoredPosition.x - bubbleRoot.pivot.x * bubbleWidth;
            float bubbleTop = -bubbleRoot.anchoredPosition.y -
                              (1f - bubbleRoot.pivot.y) * bubbleHeight;
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
            if (bubbleRoot != null)
            {
                authoredBubblePosition = bubbleRoot.anchoredPosition;
                authoredBubbleSize = bubbleRoot.sizeDelta;
                compactBubbleScale = bubbleRoot.localScale * visualScale;
                compactBubbleRotation = bubbleRoot.localRotation *
                    Quaternion.Euler(0f, 0f, tiltDegrees);
            }
            if (tail != null)
                authoredTailPosition = tail.anchoredPosition;
            if (bubbleBody != null)
                authoredBodySize = bubbleBody.sizeDelta;
            if (bodyText != null)
            {
                RectTransform textRect = bodyText.rectTransform;
                textRect.anchorMin = new Vector2(0f, 1f);
                textRect.anchorMax = new Vector2(0f, 1f);
                textRect.pivot = new Vector2(0f, 1f);
                textRect.localScale = Vector3.one;
                textRect.localRotation = Quaternion.identity;
                authoredTextPosition = textRect.anchoredPosition;
                authoredTextSize = bodyText.rectTransform.sizeDelta;
                if (authoredTextSize.x <= 0f)
                    authoredTextSize.x = textAreaSize.x;
                if (authoredTextSize.y <= 0f)
                    authoredTextSize.y = textAreaSize.y;
                if (bubbleBody != null)
                {
                    float bodyWidth = bubbleBody.rect.width;
                    float left = Mathf.Max(Mathf.Max(0f, bodyPadding.x), authoredTextPosition.x);
                    float right = Mathf.Max(0f, bodyPadding.z);
                    left = Mathf.Clamp(left, 0f, Mathf.Max(0f, bodyWidth - right - 1f));
                    authoredTextPosition.x = left;
                    authoredTextSize.x = Mathf.Min(authoredTextSize.x, Mathf.Max(1f, bodyWidth - left - right));
                }
                bodyText.enableAutoSizing = false;
                bodyText.horizontalAlignment = HorizontalAlignmentOptions.Left;
                bodyText.verticalAlignment = VerticalAlignmentOptions.Top;
                bodyText.margin = Vector4.zero;
                textRect.anchoredPosition = authoredTextPosition;
                bodyText.rectTransform.sizeDelta = authoredTextSize;
            }
            if (tail != null && bubbleBody != null)
            {
                tailOverlap = authoredBodySize.y + authoredTailPosition.y;
                // Only include the part of the tail that extends below the body. The
                // authored BubbleRoot currently contains extra empty height, so using
                // BubbleRoot - Body would make short dialogue appear much too tall.
                bubbleExtraHeight = Mathf.Max(0f, tail.rect.height - tailOverlap);
            }
            else if (bubbleRoot != null && bubbleBody != null)
                bubbleExtraHeight = Mathf.Max(0f, authoredBubbleSize.y - authoredBodySize.y);
            PositionIndicatorInsideBody();
        }

        void RestoreAuthoredLayout()
        {
            if (bubbleRoot != null)
            {
                bubbleRoot.anchoredPosition = authoredBubblePosition;
                bubbleRoot.sizeDelta = authoredBubbleSize;
                bubbleRoot.localScale = compactBubbleScale;
                bubbleRoot.localRotation = compactBubbleRotation;
            }
            if (bubbleBody != null)
                bubbleBody.sizeDelta = authoredBodySize;
            if (bodyText != null)
            {
                bodyText.rectTransform.anchoredPosition = authoredTextPosition;
                bodyText.rectTransform.sizeDelta = authoredTextSize;
            }
            if (tail != null)
                tail.anchoredPosition = authoredTailPosition;
            PositionIndicatorInsideBody();
        }

        float GetTextTopInset()
        {
            float inset = Mathf.Max(Mathf.Max(0f, bodyPadding.y), -authoredTextPosition.y);
            if (speakerTag == null || !speakerTag.activeSelf || bubbleBody == null ||
                speakerTag.transform is not RectTransform tagRect)
                return inset;

            tagRect.GetWorldCorners(speakerTagCorners);
            for (int index = 0; index < speakerTagCorners.Length; index++)
            {
                float tagBottom = bubbleBody.InverseTransformPoint(speakerTagCorners[index]).y;
                inset = Mathf.Max(inset, bubbleBody.rect.yMax - tagBottom + speakerTextGap);
            }
            return inset;
        }

        void PositionIndicatorInsideBody()
        {
            if (indicatorRect == null || bubbleBody == null)
                return;

            // Leave room for the complete icon AND the lowest point of its bob.
            indicatorBasePosition = new Vector2(
                -Mathf.Max(0f, bodyPadding.z) - indicatorRect.rect.width * 0.5f,
                Mathf.Max(0f, bodyPadding.w) + indicatorRect.rect.height * 0.5f + indicatorBobDistance);
            indicatorRect.anchoredPosition = indicatorBasePosition;
        }

        void FitBubbleToPage(string page)
        {
            if (bubbleRoot == null || bubbleBody == null || bodyText == null)
                return;

            float textWidth = authoredTextSize.x > 0f
                ? authoredTextSize.x
                : bodyText.rectTransform.rect.width;
            // Speaker text can resize its tag through a layout group this frame.
            Canvas.ForceUpdateCanvases();
            textTopInset = GetTextTopInset();
            float preferredTextHeight = Mathf.Ceil(bodyText.GetPreferredValues(page, textWidth, 0f).y);
            float bottomInset = Mathf.Max(0f, bodyPadding.w);
            if (indicatorRect != null && pageIndex + 1 < pages.Count)
                bottomInset += indicatorRect.rect.height + indicatorBobDistance * 2f + indicatorTextGap;
            float bodyHeight = Mathf.Max(
                minimumBodyHeight,
                preferredTextHeight + textTopInset + bottomInset);

            bubbleBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);
            RectTransform textRect = bodyText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(authoredTextPosition.x, -textTopInset);
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
            textRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(1f, preferredTextHeight));
            float bubbleHeight = bodyHeight + bubbleExtraHeight;
            bubbleRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);

            if (tail != null)
            {
                Vector2 tailPosition = tail.anchoredPosition;
                tailPosition.y = -bodyHeight + tailOverlap;
                tail.anchoredPosition = tailPosition;
            }

            PositionIndicatorInsideBody();

            textRect.ForceUpdateRectTransforms();
            bubbleBody.ForceUpdateRectTransforms();
            bubbleRoot.ForceUpdateRectTransforms();
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

            float speakerX = point.x - parent.rect.xMin;
            float speakerYFromTop = parent.rect.yMax - point.y;
            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(bubbleRoot.localScale.x));
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(bubbleRoot.localScale.y));
            float bubbleWidth = bubbleRoot.rect.width * scaleX;
            float bubbleHeight = bubbleRoot.rect.height * scaleY;
            float parentWidth = parent.rect.width;
            float parentHeight = parent.rect.height;

            float bubbleLeft = speakerX - authoredTailPosition.x * scaleX;
            bubbleLeft = Mathf.Clamp(bubbleLeft, screenMargin, Mathf.Max(screenMargin, parentWidth - bubbleWidth - screenMargin));

            float tailTipFromTop = (-tail.anchoredPosition.y + tail.rect.height) * scaleY;
            float bubbleTop = speakerYFromTop - tailTipFromTop;
            bubbleTop = Mathf.Clamp(bubbleTop, screenMargin, Mathf.Max(screenMargin, parentHeight - bubbleHeight - screenMargin));

            bubbleRoot.anchoredPosition = new Vector2(Mathf.Round(bubbleLeft), -Mathf.Round(bubbleTop));
            float tailX = Mathf.Clamp(
                (speakerX - bubbleLeft) / scaleX,
                tailEdgePadding,
                bubbleRoot.rect.width - tailEdgePadding);
            tail.anchoredPosition = new Vector2(Mathf.Round(tailX), tail.anchoredPosition.y);
        }

        void ApplyCompactScale()
        {
            if (bubbleRoot == null)
                return;

            bubbleRoot.localScale = compactBubbleScale;
            bubbleRoot.localRotation = compactBubbleRotation;
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
