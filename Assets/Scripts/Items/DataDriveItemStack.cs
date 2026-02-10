using System;
using TUA.Core;
using TUA.Misc;
using UnityEngine;

namespace TUA.Items
{
    [ItemStackType("datadrive")]
    [Serializable]
    public class DataDriveItemStack : ItemStack
    {
        public string targetName;
        public Color targetColor;
        public Uuid targetUuid;
    }
}
