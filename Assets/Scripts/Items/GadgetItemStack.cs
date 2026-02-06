using System;
using TUA.Core;

namespace TUA.Items
{
    [ItemStackType("gadget")]
    [Serializable]
    public class GadgetItemStack : ItemStack
    {
        public int count = 1;
    }
}
