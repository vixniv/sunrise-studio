using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonSound : MonoBehaviour
    {
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
            SunriseAudio.Play(cue);
        }

        public void SetCue(string value)
        {
            cue = value;
        }
    }
}
