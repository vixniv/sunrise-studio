using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CircleTransition : MonoBehaviour
{
    public static CircleTransition Instance { get; private set; }

    [Header("References")]
    public RectTransform circle;
    public CanvasGroup canvasGroup;

    [Header("Timing")]
    [Min(0.05f)]
    public float closeDuration = 0.35f;

    [Min(0.05f)]
    public float openDuration = 0.35f;

    [Header("Audio")]
    [Tooltip("Plays sounds passed to LoadScene, such as a door. Left empty, one is added at runtime. " +
             "It lives on this persistent object, so the sound is not cut off when the scene unloads.")]
    public AudioSource audioSource;

    bool transitioning;

    public bool IsTransitioning => transitioning;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (circle != null)
            circle.localScale = Vector3.zero;

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = false;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D
        }
    }

    // sound, when given, plays as the circle starts closing.
    public void LoadScene(
        string sceneName,
        AudioClip sound = null,
        float volume = 1f)
    {
        if (transitioning)
            return;

        if (circle == null)
        {
            Debug.LogError(
                "CircleTransition has no Circle assigned.",
                this);
            return;
        }

        // LoadSceneAsync returns null for these, which would leave the screen covered.
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"CircleTransition: scene \"{sceneName}\" is not in the Build Profile scene list, or the name is misspelled.",
                this);
            return;
        }

        if (sound != null)
            audioSource.PlayOneShot(sound, volume);

        StartCoroutine(TransitionRoutine(sceneName));
    }

    // Closes the circle, runs whileCovered, waits holdSeconds, then opens it again without loading a scene.
    // Returns false when a transition is already running or there is no circle.
    public bool Cover(System.Action whileCovered, float holdSeconds)
    {
        if (transitioning || circle == null)
            return false;

        StartCoroutine(CoverRoutine(whileCovered, holdSeconds));
        return true;
    }

    IEnumerator CoverRoutine(System.Action whileCovered, float holdSeconds)
    {
        transitioning = true;

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;

        float coverScale = CalculateCoverScale();

        yield return AnimateCircle(
            0f,
            coverScale,
            closeDuration);

        whileCovered?.Invoke();

        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);

        yield return AnimateCircle(
            coverScale,
            0f,
            openDuration);

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = false;

        transitioning = false;
    }

    IEnumerator TransitionRoutine(string sceneName)
    {
        transitioning = true;

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;

        float coverScale = CalculateCoverScale();

        yield return AnimateCircle(
            0f,
            coverScale,
            closeDuration);

        AsyncOperation loading =
            SceneManager.LoadSceneAsync(sceneName);

        while (!loading.isDone)
            yield return null;

        // Let the newly loaded scene finish one frame of setup.
        yield return null;

        coverScale = CalculateCoverScale();
        circle.localScale = Vector3.one * coverScale;

        yield return AnimateCircle(
            coverScale,
            0f,
            openDuration);

        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = false;

        transitioning = false;
    }

    IEnumerator AnimateCircle(
        float startScale,
        float endScale,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float scale = Mathf.Lerp(
                startScale,
                endScale,
                eased);

            circle.localScale = Vector3.one * scale;

            yield return null;
        }

        circle.localScale = Vector3.one * endScale;
    }

    float CalculateCoverScale()
    {
        Canvas canvas = circle.GetComponentInParent<Canvas>();

        if (canvas == null)
            return 20f;

        RectTransform canvasRect =
            canvas.GetComponent<RectTransform>();

        float width = canvasRect.rect.width;
        float height = canvasRect.rect.height;
        float diagonal =
            Mathf.Sqrt(width * width + height * height);

        float circleDiameter =
            Mathf.Max(1f, circle.rect.width);

        return diagonal / circleDiameter * 1.1f;
    }
}
