using FMODUnity;
using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonSound : MonoBehaviour
    {
        [Tooltip("Optional direct FMOD event override. When empty, plays the mapped FMOD event for Cue.")]
        [SerializeField] EventReference fmodEvent;
        [SerializeField] string cue = "ui_click";

        Button button;

        void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Play);
        }

        void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(Play);
        }

        void Play()
        {
            if (!fmodEvent.IsNull && SunriseAudio.TryPlayFmodEvent(fmodEvent))
                return;
            SunriseAudio.Play(cue);
        }

        public void SetCue(string value)
        {
            cue = value;
        }
    }
}
