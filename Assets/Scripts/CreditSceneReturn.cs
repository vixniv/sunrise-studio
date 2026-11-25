using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Put on one GameObject in CreditScene. After the credits have shown for Return After Seconds, or when the player taps
// once Skip Lock Seconds have passed, fades to black, loads the title screen and fades back in.
// On arrival it deletes the save, so Play on the title screen starts a new game instead of replaying the ending.
public sealed class CreditSceneReturn : MonoBehaviour
{
    [Tooltip("Scene loaded after the credits. Must be in the Build Profile scene list.")]
    [SerializeField] private string titleScene = "TitleScreen";
    [Tooltip("Seconds the credits stay on screen before going back to the title screen.")]
    [SerializeField, Min(0f)] private float returnAfterSeconds = 12f;
    [Tooltip("A tap goes back early. Taps are ignored for this many seconds, so the tap that closed the " +
             "thank-you card does not skip the credits.")]
    [SerializeField] private bool tapToSkip = true;
    [SerializeField, Min(0f)] private float skipLockSeconds = 2f;
    [Tooltip("Seconds of the fade to black before the title screen loads.")]
    [SerializeField, Min(0.05f)] private float fadeOutSeconds = 1f;
    [Tooltip("Seconds of the fade from black once the title screen has loaded.")]
    [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.75f;
    [Tooltip("Deletes the save when the credits start, so the finished story is not continued. " +
             "Turn off while testing this scene directly if you want to keep your save.")]
    [SerializeField] private bool deleteSaveOnArrival = true;

    private float shownSeconds;
    private bool leaving;

    private void Start()
    {
        if (deleteSaveOnArrival)
        {
            SaveSystem.DeleteSave();
            Debug.Log("CreditSceneReturn: story finished, save deleted.");
        }
    }

    private void Update()
    {
        if (leaving)
            return;

        shownSeconds += Time.unscaledDeltaTime;

        bool tapped = tapToSkip && shownSeconds >= skipLockSeconds &&
                      Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        if (tapped || shownSeconds >= returnAfterSeconds)
            ReturnToTitle();
    }

    public void ReturnToTitle()
    {
        if (leaving)
            return;

        string scene = string.IsNullOrWhiteSpace(titleScene) ? string.Empty : titleScene.Trim();
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"CreditSceneReturn: scene \"{scene}\" is not in the Build Profile scene list, or the name is misspelled.", this);
            enabled = false;
            return;
        }

        leaving = true;
        FadeToScene.Run(scene, fadeOutSeconds, fadeInSeconds);
    }

    // A black overlay of its own that survives the scene load. The title screen resets the shared
    // CircleTransition when it wakes, so that overlay would vanish at once instead of fading in.
    private sealed class FadeToScene : MonoBehaviour
    {
        private CanvasGroup group;

        public static void Run(string sceneName, float fadeOutSeconds, float fadeInSeconds)
        {
            var host = new GameObject("CreditReturnFade");
            DontDestroyOnLoad(host);

            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var fade = host.AddComponent<FadeToScene>();
            fade.group = host.AddComponent<CanvasGroup>();
            fade.group.alpha = 0f;
            fade.group.blocksRaycasts = true;

            var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(host.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = Color.black;

            fade.StartCoroutine(fade.Play(sceneName, fadeOutSeconds, fadeInSeconds));
        }

        private IEnumerator Play(string sceneName, float fadeOutSeconds, float fadeInSeconds)
        {
            yield return FadeTo(1f, fadeOutSeconds);

            // Same clean-up as Quit in the pause menu.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (CircleTransition.Instance != null)
                CircleTransition.Instance.ResetForTitleScreen();
            if (TimeTransition.Instance != null)
                TimeTransition.Instance.ResetForTitleScreen();
            if (TimeLoopResetTransition.Instance != null)
                TimeLoopResetTransition.Instance.ResetForTitleScreen();

            AsyncOperation loading = SceneManager.LoadSceneAsync(sceneName);
            while (loading != null && !loading.isDone)
                yield return null;
            yield return null;

            group.blocksRaycasts = false;
            yield return FadeTo(0f, fadeInSeconds);
            Destroy(gameObject);
        }

        private IEnumerator FadeTo(float target, float seconds)
        {
            float start = group.alpha;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, elapsed / seconds);
                yield return null;
            }
            group.alpha = target;
        }
    }
}
