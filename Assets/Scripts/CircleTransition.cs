using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CircleTransition : MonoBehaviour
{
    public static CircleTransition Instance { get; private set; }

    [Header("References")]
    public RectTransform circle;
    public CanvasGroup canvasGroup;
    [Tooltip("Optional full-screen black Image with a CanvasGroup, used when a scene that is already black fades into the next scene.")]
    [SerializeField] private CanvasGroup blackFadeGroup;

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
    string blackEntryScene;
    float blackEntryOpenDuration;

    public bool IsTransitioning => transitioning;
    public bool IsBlack => blackFadeGroup != null && blackFadeGroup.alpha >= 0.999f;

    // A cutscene in the newly loaded room may lengthen this one map-entry reveal.
    // The usual openDuration stays unchanged for later visits and other transitions.
    public bool SetCurrentBlackEntryOpenDuration(string sceneName, float seconds)
    {
        if (blackEntryScene == null || !string.Equals(blackEntryScene, sceneName, System.StringComparison.Ordinal) ||
            float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
            return false;

        blackEntryOpenDuration = seconds;
        return true;
    }

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

        if (blackFadeGroup != null)
        {
            blackFadeGroup.alpha = 0f;
            blackFadeGroup.blocksRaycasts = false;
        }

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
        float volume = 1f,
        System.Action whileCovered = null)
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
            audioSource.PlayOneShot(sound, volume * SunriseAudio.SfxVolume);

        StartCoroutine(TransitionRoutine(sceneName, whileCovered));
    }

    // The current scene has already faded to black. Keep it covered during the load,
    // then fade the new scene in without a circle reveal.
    public void LoadSceneFromBlack(string sceneName, float fadeInDuration, System.Action whileCovered = null)
    {
        if (transitioning)
            return;

        if (blackFadeGroup == null)
        {
            Debug.LogError("CircleTransition needs a Black Fade Group for a full-screen fade. Using its circle transition instead.", this);
            LoadScene(sceneName);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"CircleTransition: scene \"{sceneName}\" is not in the Build Profile scene list, or the name is misspelled.", this);
            return;
        }

        StartCoroutine(BlackFadeSceneRoutine(sceneName, Mathf.Max(0.05f, fadeInDuration), whileCovered));
    }

    // Used when entering a room from SchoolMap: fade fully to black, load the room,
    // then fade back in. Other scene transitions can continue using the circle.
    public void LoadSceneWithBlackFade(string sceneName, AudioClip sound = null, float volume = 1f)
    {
        if (transitioning)
            return;

        if (blackFadeGroup == null)
        {
            Debug.LogError("CircleTransition needs a Black Fade Group for map entry. Using its circle transition instead.", this);
            LoadScene(sceneName, sound, volume);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"CircleTransition: scene \"{sceneName}\" is not in the Build Profile scene list, or the name is misspelled.", this);
            return;
        }

        if (sound != null)
            audioSource.PlayOneShot(sound, volume * SunriseAudio.SfxVolume);

        StartCoroutine(BlackFadeEntryRoutine(sceneName));
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

    public bool CoverBlack(System.Action whileCovered, float holdSeconds)
    {
        if (transitioning || blackFadeGroup == null)
            return false;
        StartCoroutine(CoverBlackRoutine(whileCovered, holdSeconds));
        return true;
    }

    public void ResetForTitleScreen()
    {
        StopAllCoroutines();
        transitioning = false;
        blackEntryScene = null;
        if (circle != null) circle.localScale = Vector3.zero;
        if (blackFadeGroup != null)
        {
            blackFadeGroup.alpha = 0f;
            blackFadeGroup.blocksRaycasts = false;
        }
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
    }

    // Scene cameras may begin at an authored position far from the door spawn.
    // Cut only their first activation; retain each Brain's normal blend afterward.
    public void PrimeCameraForScene(Scene scene)
    {
        var restore = new List<(CinemachineBrain brain, CinemachineBlendDefinition blend)>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (CinemachineBrain brain in root.GetComponentsInChildren<CinemachineBrain>(true))
        {
            if (brain == null || !brain.isActiveAndEnabled) continue;
            restore.Add((brain, brain.DefaultBlend));
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        SnapCameraToTarget(scene);
        if (restore.Count > 0)
            StartCoroutine(RestoreCameraBlends(restore));
    }

    // The player is placed at a door (or teleported during a covered cutscene) before
    // the fade reveals the room. Update Cinemachine without its saved damping state so
    // the camera does not travel from the scene's authored position on the first frames.
    public void SnapCameraToTarget(Scene scene)
    {
        CinemachineCore.ResetCameraState();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (CinemachineVirtualCameraBase camera in root.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
            if (camera != null && camera.isActiveAndEnabled)
                camera.CancelDamping(true);
    }

    // A newly loaded camera can continue easing from its authored position even
    // after DoorSpawn places Anin. Let it reach the door spawn while the screen
    // is fully black, then restore the room's authored camera feel for gameplay.
    public IEnumerator SettleCameraBeforeReveal(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
            yield break;

        var composers = new List<(CinemachinePositionComposer component, Vector3 damping)>();
        var confiners = new List<(CinemachineVirtualCameraBase camera, CinemachineConfiner2D component, float damping, float slowingDistance)>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (CinemachineVirtualCameraBase camera in root.GetComponentsInChildren<CinemachineVirtualCameraBase>(true))
        {
            if (camera == null || !camera.isActiveAndEnabled) continue;
            if (camera.TryGetComponent(out CinemachinePositionComposer composer))
            {
                composers.Add((composer, composer.Damping));
                composer.Damping = Vector3.zero;
            }
            if (camera.TryGetComponent(out CinemachineConfiner2D confiner) && confiner.isActiveAndEnabled)
            {
                confiners.Add((camera, confiner, confiner.Damping, confiner.SlowingDistance));
                confiner.Damping = 0f;
                confiner.SlowingDistance = 0f;
            }
        }

        try
        {
            // Confiner2D bakes its shape over several frames. On a phone, the
            // first two frames can finish before the camera bounds are ready.
            // Keep the room covered until its active confiners have baked for
            // this device's actual aspect ratio.
            yield return new WaitForEndOfFrame();
            float deadline = Time.realtimeSinceStartup + 5f;
            bool allBaked;
            do
            {
                allBaked = true;
                foreach (var entry in confiners)
                {
                    if (entry.camera == null || entry.component == null ||
                        entry.component.BoundingShape2D == null)
                        continue;

                    if (!entry.component.BoundingShapeIsBaked &&
                        !entry.component.BakeBoundingShape(entry.camera, 0.02f))
                        allBaked = false;
                }
                if (!allBaked && Time.realtimeSinceStartup < deadline)
                    yield return new WaitForEndOfFrame();
            }
            while (!allBaked && Time.realtimeSinceStartup < deadline);

            if (!allBaked)
                Debug.LogWarning($"CircleTransition: camera bounds were not ready before revealing {sceneName}.", this);

            SnapCameraToTarget(scene);
            yield return new WaitForEndOfFrame();
            SnapCameraToTarget(scene);
            yield return new WaitForEndOfFrame();
        }
        finally
        {
            foreach (var entry in composers)
                if (entry.component != null) entry.component.Damping = entry.damping;
            foreach (var entry in confiners)
                if (entry.component != null)
                {
                    entry.component.Damping = entry.damping;
                    entry.component.SlowingDistance = entry.slowingDistance;
                }
        }
    }

    IEnumerator RestoreCameraBlends(List<(CinemachineBrain brain, CinemachineBlendDefinition blend)> restore)
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        // DoorSpawn calls PrimeCameraForScene during the scene load. Do not
        // restore the authored blend while the new room is still being shown:
        // mobile confiner baking can take longer than these two frames.
        while (transitioning || (TimeTransition.Instance != null && TimeTransition.Instance.IsTransitioning))
            yield return null;
        foreach (var entry in restore)
            if (entry.brain != null) entry.brain.DefaultBlend = entry.blend;
    }

    IEnumerator CoverBlackRoutine(System.Action whileCovered, float holdSeconds)
    {
        transitioning = true;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        float startAlpha = blackFadeGroup.alpha;
        for (float elapsed = 0f; elapsed < closeDuration; elapsed += Time.unscaledDeltaTime)
        {
            blackFadeGroup.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / closeDuration));
            yield return null;
        }
        blackFadeGroup.alpha = 1f;
        whileCovered?.Invoke();
        if (holdSeconds > 0f) yield return new WaitForSecondsRealtime(holdSeconds);
        for (float elapsed = 0f; elapsed < openDuration; elapsed += Time.unscaledDeltaTime)
        {
            blackFadeGroup.alpha = 1f - Mathf.Clamp01(elapsed / openDuration);
            yield return null;
        }
        blackFadeGroup.alpha = 0f;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        transitioning = false;
    }

    // Fades the full screen to or from black without loading a scene. Yarn can
    // change time, move actors, and play audio while the screen stays black.
    public bool FadeBlack(bool toBlack)
    {
        if (transitioning || blackFadeGroup == null)
            return false;

        StartCoroutine(FadeBlackRoutine(toBlack));
        return true;
    }

    IEnumerator FadeBlackRoutine(bool toBlack)
    {
        transitioning = true;
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;

        float startAlpha = blackFadeGroup.alpha;
        float endAlpha = toBlack ? 1f : 0f;
        float duration = toBlack ? closeDuration : openDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            blackFadeGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        blackFadeGroup.alpha = endAlpha;
        if (!toBlack && canvasGroup != null)
            canvasGroup.blocksRaycasts = false;
        transitioning = false;
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

    IEnumerator TransitionRoutine(string sceneName, System.Action whileCovered)
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

        AsyncOperation loading =
            SceneManager.LoadSceneAsync(sceneName);

        while (!loading.isDone)
            yield return null;

        yield return SettleCameraBeforeReveal(sceneName);

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

    IEnumerator BlackFadeSceneRoutine(string sceneName, float fadeInDuration, System.Action whileCovered)
    {
        transitioning = true;
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;

        circle.localScale = Vector3.zero;
        blackFadeGroup.alpha = 1f;

        whileCovered?.Invoke();

        AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName);
        while (!loading.isDone)
            yield return null;

        yield return SettleCameraBeforeReveal(sceneName);

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            blackFadeGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        blackFadeGroup.alpha = 0f;
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = false;
        transitioning = false;
    }

    IEnumerator BlackFadeEntryRoutine(string sceneName)
    {
        transitioning = true;
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;

        if (circle != null)
            circle.localScale = Vector3.zero;
        float startAlpha = blackFadeGroup.alpha;
        float elapsed = 0f;
        while (elapsed < closeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            blackFadeGroup.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / closeDuration));
            yield return null;
        }
        blackFadeGroup.alpha = 1f;

        blackEntryScene = sceneName;
        blackEntryOpenDuration = openDuration;
        AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName);
        while (!loading.isDone)
            yield return null;
        yield return SettleCameraBeforeReveal(sceneName);

        float revealDuration = blackEntryOpenDuration;
        blackEntryScene = null;
        elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            blackFadeGroup.alpha = 1f - Mathf.Clamp01(elapsed / revealDuration);
            yield return null;
        }
        blackFadeGroup.alpha = 0f;
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
