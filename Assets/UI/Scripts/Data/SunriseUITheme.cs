using UnityEngine;

namespace SunriseStudio.UI
{
    [CreateAssetMenu(
        fileName = "SunriseUITheme",
        menuName = "Sunrise Studio/UI Theme")]
    public sealed class SunriseUITheme : ScriptableObject
    {
        [Header("Core")]
        [SerializeField] Color mainBlue = new Color32(0x1F, 0x7B, 0xFF, 0xFF);
        [SerializeField] Color ink = new Color32(0x07, 0x0D, 0x2B, 0xFF);
        [SerializeField] Color secondaryInk = new Color32(0x14, 0x11, 0x1A, 0xFF);
        [SerializeField] Color paleIce = new Color32(0xE6, 0xF4, 0xFF, 0xFF);
        [SerializeField] Color paper = new Color32(0xF7, 0xF3, 0xEE, 0xFF);
        [SerializeField] Color glow = new Color32(0x35, 0xE0, 0xFF, 0xFF);
        [SerializeField] Color shadow = new Color32(0x10, 0x28, 0x6E, 0xFF);

        [Header("Time Phases")]
        [SerializeField] Color morning = new Color32(0xFF, 0xD1, 0x66, 0xFF);
        [SerializeField] Color afternoon = new Color32(0x6F, 0xB1, 0xFF, 0xFF);
        [SerializeField] Color evening = new Color32(0xB2, 0x8D, 0xFF, 0xFF);

        public Color MainBlue => mainBlue;
        public Color Ink => ink;
        public Color SecondaryInk => secondaryInk;
        public Color PaleIce => paleIce;
        public Color Paper => paper;
        public Color Glow => glow;
        public Color Shadow => shadow;
        public Color Morning => morning;
        public Color Afternoon => afternoon;
        public Color Evening => evening;

        public Color GetTimePhaseColor(TimePhase phase)
        {
            return phase switch
            {
                TimePhase.Morning => morning,
                TimePhase.Afternoon => afternoon,
                TimePhase.Evening => evening,
                _ => morning
            };
        }
    }
}
