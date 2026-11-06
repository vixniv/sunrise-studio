using System;
using UnityEngine;

namespace SunriseStudio.UI
{
    [Serializable]
    public sealed class SettingsData
    {
        [Range(0f, 1f)] public float musicVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        public bool hapticsEnabled = true;

        public SettingsData Clone()
        {
            return new SettingsData
            {
                musicVolume = musicVolume,
                sfxVolume = sfxVolume,
                hapticsEnabled = hapticsEnabled
            };
        }

        public void Sanitize()
        {
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
        }
    }
}
