using System;
using UnityEngine;

namespace SunriseStudio.UI
{
    [CreateAssetMenu(
        fileName = "InventoryItem",
        menuName = "Sunrise Studio/UI Data/Inventory Item")]
    public sealed class InventoryItemData : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [TextArea(2, 5)] [SerializeField] string description;
        [SerializeField] Sprite icon;
        [SerializeField] InteractionAction useAction;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public InteractionAction UseAction => useAction;
    }

    [Serializable]
    public sealed class InventoryItemStack
    {
        [SerializeField] InventoryItemData item;
        [Min(0)] [SerializeField] int quantity;

        public InventoryItemData Item => item;
        public int Quantity => quantity;

        public InventoryItemStack(InventoryItemData item, int quantity)
        {
            this.item = item;
            this.quantity = Mathf.Max(0, quantity);
        }
    }
}
