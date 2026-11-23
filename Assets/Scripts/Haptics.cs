using System.Runtime.InteropServices;
using UnityEngine;

namespace SunriseStudio
{
    // Values must match the switch in Assets/Plugins/iOS/SunriseHaptics.mm.
    public enum HapticStyle
    {
        Light = 0,
        Medium = 1,
        Heavy = 2,
        Soft = 3,
        Rigid = 4,
    }

    /// <summary>
    /// iPhone tap feedback via UIImpactFeedbackGenerator.
    /// Respects the Haptics toggle in the config menu. Does nothing in the Editor
    /// or on other platforms.
    /// </summary>
    public static class Haptics
    {
        public const string PrefsKey = "Sunrise.Settings.Haptics";

        static bool? enabled;

        public static bool Enabled
        {
            get
            {
                enabled ??= PlayerPrefs.GetInt(PrefsKey, 1) != 0;
                return enabled.Value;
            }
            set => enabled = value;
        }

        /// <summary>Light tap for UI button presses.</summary>
        public static void ButtonTap() => Impact(HapticStyle.Light);

        public static void Impact(HapticStyle style)
        {
            if (!Enabled)
                return;

#if UNITY_IOS && !UNITY_EDITOR
            SunriseHaptics_Impact((int)style);
#endif
        }

        /// <summary>Warms up the Taptic Engine so the next Impact fires with less latency.</summary>
        public static void Prepare(HapticStyle style = HapticStyle.Light)
        {
            if (!Enabled)
                return;

#if UNITY_IOS && !UNITY_EDITOR
            SunriseHaptics_Prepare((int)style);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            enabled = null;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void SunriseHaptics_Impact(int style);

        [DllImport("__Internal")]
        static extern void SunriseHaptics_Prepare(int style);
#endif
    }
}
