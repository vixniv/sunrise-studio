using System;
using UnityEngine;

namespace SunriseStudio.UI
{
    [Serializable]
    public sealed class InteractionAction
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [TextArea(2, 5)] [SerializeField] string description;
        [Min(0)] [SerializeField] int timeCostMinutes;
        [SerializeField] Sprite icon;
        [SerializeField] bool locked;
        [SerializeField] string lockedReason;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int TimeCostMinutes => timeCostMinutes;
        public Sprite Icon => icon;
        public bool Locked => locked;
        public string LockedReason => lockedReason;

        public InteractionAction() { }

        public InteractionAction(
            string id,
            string displayName,
            string description,
            int timeCostMinutes,
            Sprite icon,
            bool locked = false,
            string lockedReason = "")
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.timeCostMinutes = Mathf.Max(0, timeCostMinutes);
            this.icon = icon;
            this.locked = locked;
            this.lockedReason = lockedReason;
        }
    }
}
