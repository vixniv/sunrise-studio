using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

// <<freeze_frame>>: a camera shutter. The screen flashes white, then the world stops (Time.timeScale = 0)
// and stays stopped until <<loop_end>> releases it or the scene unloads. Dialogue keeps running while frozen:
// the DialogBubble, ChoiceWheel and CircleTransition all run on unscaled time.
// Put it on a full-screen white Image in a Screen Space - Overlay canvas, and assign it to YarnCommands' Freeze Frame.
public class FreezeFrame : MonoBehaviour
{
    [Tooltip("Full-screen white Image. Leave empty to use the Image on this object.")]
    [SerializeField] private Image flash;
    [Tooltip("Seconds for the flash to fade from white to clear.")]
    [SerializeField, Min(0.05f)] private float flashDuration = 0.6f;
    [Tooltip("Seconds to hold on the frozen frame before the dialogue continues.")]
    [SerializeField, Min(0f)] private float holdSeconds = 1f;
    [Tooltip("Stops the world after the flash. Off: only the flash plays.")]
    [SerializeField] private bool stopTime = true;
    [Tooltip("Optional shutter sound.")]
    [SerializeField] private AudioClip shutterSound;
    [Tooltip("Plays the shutter sound. Leave empty to use an Audio Source on this object.")]
    [SerializeField] private AudioSource audioSource;

    private bool frozen;
    private bool playing;

    private void Awake()
    {
        if (flash == null) flash = GetComponent<Image>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        SetFlashAlpha(0f);
    }

    // Finishes once the flash and the hold are over; the world stays frozen after that.
    public YarnTask Play()
    {
        if (!playing) StartCoroutine(PlayRoutine());
        return YarnTask.WaitUntil(() => !playing || this == null);
    }

    // Starts the world again. Safe to call when nothing is frozen.
    public void Release()
    {
        if (!frozen) return;
        frozen = false;
        Time.timeScale = 1f;
    }

    private IEnumerator PlayRoutine()
    {
        playing = true;
        if (shutterSound != null && audioSource != null) audioSource.PlayOneShot(shutterSound);
        if (stopTime)
        {
            frozen = true;
            Time.timeScale = 0f;
        }

        for (float elapsed = 0f; elapsed < flashDuration; elapsed += Time.unscaledDeltaTime)
        {
            SetFlashAlpha(1f - Mathf.SmoothStep(0f, 1f, elapsed / flashDuration));
            yield return null;
        }
        SetFlashAlpha(0f);

        if (holdSeconds > 0f) yield return new WaitForSecondsRealtime(holdSeconds);
        playing = false;
    }

    private void SetFlashAlpha(float alpha)
    {
        if (flash == null) return;
        flash.enabled = alpha > 0f;
        Color color = flash.color;
        color.a = alpha;
        flash.color = color;
    }

    // Time.timeScale outlives the scene, so a frozen world is started again on unload.
    private void OnDestroy() => Release();
}
