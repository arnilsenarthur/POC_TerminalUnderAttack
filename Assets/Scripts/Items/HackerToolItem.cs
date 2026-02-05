using TUA.Core;
using TUA.Core.Interfaces;
using UnityEngine;

namespace TUA.Items
{
    [CreateAssetMenu(menuName = "TUA/HackerToolItem", fileName = "NewHackerToolItem")]
    public class HackerToolItem : Item
    {
        [Header("Aiming")]
        public bool aimable = true;

        public override ItemStack GetDefaultItemStack()
        {
            return new ItemStack
            {
                item = Id,
            };
        }

        public override bool IsAimable(IInventoryHolder holder, ItemStack stack) => aimable;
    }
}