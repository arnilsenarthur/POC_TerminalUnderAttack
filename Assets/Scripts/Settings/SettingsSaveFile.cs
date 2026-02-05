using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace TUA.Settings
{
    [Serializable]
    internal sealed class SettingsSaveFile
    {
        [FormerlySerializedAs("Version")] public int version = 1;
        [FormerlySerializedAs("Items")] public List<SettingsSaveItem> items = new();
    }
}

