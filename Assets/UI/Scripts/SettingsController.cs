using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class SettingsController : MonoBehaviour
    {
        const string MusicVolumeKey = "Sunrise.Settings.MusicVolume";
        const string SfxVolumeKey = "Sunrise.Settings.SfxVolume";
        const string HapticsKey = "Sunrise.Settings.Haptics";

        [Header("Controls")]
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Toggle hapticsToggle;
        [SerializeField] Button closeButton;

        [Header("Events")]
        [SerializeField] FloatEvent onMusicVolumeChanged = new();
        [SerializeField] FloatEvent onSfxVolumeChanged = new();
        [SerializeField] BoolEvent onHapticsChanged = new();
        [SerializeField] UnityEvent onCloseRequested = new();

        SettingsData current = new();

        public SettingsData Current => current.Clone();
        public event Action CloseRequested;

        void OnEnable()
        {
            AddListeners();
            Load();
        }

        void OnDisable()
        {
            RemoveListeners();
        }

        public void Load()
        {
            current.musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            current.sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            current.hapticsEnabled = PlayerPrefs.GetInt(HapticsKey, 1) != 0;
            current.Sanitize();

            if (musicSlider != null)
                musicSlider.SetValueWithoutNotify(current.musicVolume);
            if (sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(current.sfxVolume);
            if (hapticsToggle != null)
                hapticsToggle.SetIsOnWithoutNotify(current.hapticsEnabled);

            BroadcastCurrentValues();
        }

        public void Save()
        {
            current.Sanitize();
            PlayerPrefs.SetFloat(MusicVolumeKey, current.musicVolume);
            PlayerPrefs.SetFloat(SfxVolumeKey, current.sfxVolume);
            PlayerPrefs.SetInt(HapticsKey, current.hapticsEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Apply(SettingsData settings)
        {
            if (settings == null)
                return;

            current = settings.Clone();
            current.Sanitize();

            if (musicSlider != null)
                musicSlider.SetValueWithoutNotify(current.musicVolume);
            if (sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(current.sfxVolume);
            if (hapticsToggle != null)
                hapticsToggle.SetIsOnWithoutNotify(current.hapticsEnabled);

            Save();
            BroadcastCurrentValues();
        }

        public void FocusCloseButton()
        {
            if (EventSystem.current != null && closeButton != null)
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        void AddListeners()
        {
            if (musicSlider != null)
                musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            if (hapticsToggle != null)
                hapticsToggle.onValueChanged.AddListener(HandleHapticsChanged);
            if (closeButton != null)
                closeButton.onClick.AddListener(HandleCloseClicked);
        }

        void RemoveListeners()
        {
            if (musicSlider != null)
                musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
            if (sfxSlider != null)
                sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
            if (hapticsToggle != null)
                hapticsToggle.onValueChanged.RemoveListener(HandleHapticsChanged);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        void HandleMusicChanged(float value)
        {
            current.musicVolume = Mathf.Clamp01(value);
            Save();
            SunriseAudio.SetMusicVolume(current.musicVolume);
            onMusicVolumeChanged.Invoke(current.musicVolume);
        }

        void HandleSfxChanged(float value)
        {
            current.sfxVolume = Mathf.Clamp01(value);
            Save();
            onSfxVolumeChanged.Invoke(current.sfxVolume);
        }

        void HandleHapticsChanged(bool enabled)
        {
            current.hapticsEnabled = enabled;
            Save();
            onHapticsChanged.Invoke(enabled);
        }

        void HandleCloseClicked()
        {
            Save();
            onCloseRequested.Invoke();
            CloseRequested?.Invoke();
        }

        void BroadcastCurrentValues()
        {
            SunriseAudio.SetMusicVolume(current.musicVolume);
            onMusicVolumeChanged.Invoke(current.musicVolume);
            onSfxVolumeChanged.Invoke(current.sfxVolume);
            onHapticsChanged.Invoke(current.hapticsEnabled);
        }
    }
}
