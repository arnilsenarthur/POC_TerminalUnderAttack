using TUA.Core;
using TUA.Core.Interfaces;
using UnityEngine;

namespace TUA.Items
{
    [CreateAssetMenu(menuName = "TUA/DataDriveItem", fileName = "NewDataDriveItem")]
    public class DataDriveItem : Item
    {
        public override ItemStack GetDefaultItemStack()
        {
            return new DataDriveItemStack
            {
                item = Id,
            };
        }

        public override Color GetRenderColor(IInventoryHolder holder, ItemStack stack)
        {
            if (stack is DataDriveItemStack dataDriveStack)
                return dataDriveStack.targetColor;
            
            return Color.white;
        }

        public override string GetDisplayLabel(ItemStack stack)
        {
            var baseLabel = base.GetDisplayLabel(stack);
            if (stack is DataDriveItemStack dataDriveStack && !string.IsNullOrEmpty(dataDriveStack.targetName))
                return $"{dataDriveStack.targetName}_{baseLabel}";
            
            return baseLabel;
        }

        public override bool DisplayAsAimingWhenHeld(IInventoryHolder holder, ItemStack stack) => true;
    }
}