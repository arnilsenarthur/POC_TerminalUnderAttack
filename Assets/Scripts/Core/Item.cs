using TUA.Core.Interfaces;
using TUA.I18n;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core
{
    public abstract class Item : RegistrableScriptableObject
    {
        #region Serialized Fields
        public Sprite sprite;
        public GameObject visualPrefab;
        #endregion

        #region Public Methods
        public virtual ItemStack GetDefaultItemStack()
        {
            return new ItemStack
            {
                item = Id,
            };
        }

        public virtual Color GetRenderColor(IInventoryHolder holder, ItemStack stack)
        {
            return Color.white;
        }

        public virtual string GetDisplayLabel(ItemStack stack)
        {
            string itemId = !string.IsNullOrEmpty(stack?.item) ? stack.item : this.Id;
            if (string.IsNullOrEmpty(itemId))
                return LocalizationManager.Get("item.unknown");
            string locKey = $"item.{itemId.ToLowerInvariant()}";
            return LocalizationManager.Get(locKey);
        }

        public virtual bool IsAimable(IInventoryHolder holder, ItemStack stack) => false;
        public virtual bool DisplayAsAimingWhenHeld(IInventoryHolder holder, ItemStack stack) => false;
        #endregion
    }
}
