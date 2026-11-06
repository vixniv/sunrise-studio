using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SunriseAudioLibrary", menuName = "Sunrise/Audio Library")]
public sealed class SunriseAudioLibrary : ScriptableObject
{
    [Serializable]
    public sealed class Cue
    {
        public string key;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;

        [NonSerialized] int lastIndex = -1;

        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0)
                return null;
            if (clips.Length == 1)
                return clips[0];

            int index = UnityEngine.Random.Range(0, clips.Length - 1);
            if (lastIndex >= 0 && index >= lastIndex)
                index++;
            lastIndex = index;
            return clips[index];
        }
    }

    [Serializable]
    public sealed class SceneSurface
    {
        public string sceneName;
        public FootstepSurface surface;
    }

    public Cue[] cues;
    public SceneSurface[] sceneSurfaces;

    public AudioClip GetClip(string key, out float volume)
    {
        volume = 1f;
        if (string.IsNullOrWhiteSpace(key) || cues == null)
            return null;

        for (int i = 0; i < cues.Length; i++)
        {
            Cue cue = cues[i];
            if (cue == null || !string.Equals(cue.key, key, StringComparison.OrdinalIgnoreCase))
                continue;
            volume = cue.volume;
            return cue.PickClip();
        }
        return null;
    }

    public FootstepSurface GetSurface(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || sceneSurfaces == null)
            return null;

        for (int i = 0; i < sceneSurfaces.Length; i++)
        {
            SceneSurface entry = sceneSurfaces[i];
            if (entry != null && string.Equals(entry.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                return entry.surface;
        }
        return null;
    }
}
