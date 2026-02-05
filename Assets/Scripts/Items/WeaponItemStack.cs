using System;
using TUA.Core;

namespace TUA.Items
{
    [ItemStackType("weapon")]
    [Serializable]
    public class WeaponItemStack : ItemStack
    {
        public int ammo;
        public int maxAmmo;
    }
}

