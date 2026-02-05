using System;
using TUA.Core;
using UnityEngine;

namespace TUA.Items
{
    [ItemStackType("datadrive")]
    [Serializable]
    public class DataDriveItemStack : ItemStack
    {
        public string targetName;
        public Color targetColor;
    }
}

