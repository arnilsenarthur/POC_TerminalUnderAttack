using System;
using UnityEngine.Serialization;

namespace TUA.Settings
{
    [Serializable]
    internal sealed class SettingsSaveItem
    {
        [FormerlySerializedAs("Key")] public string key;
        [FormerlySerializedAs("Type")] public SettingType type;
        [FormerlySerializedAs("IntValue")] public int intValue;
        [FormerlySerializedAs("FloatValue")] public float floatValue;
        [FormerlySerializedAs("BoolValue")] public bool boolValue;
        [FormerlySerializedAs("StringValue")] public string stringValue;

        public static SettingsSaveItem From(string key, SettingValue value)
        {
            return new SettingsSaveItem
            {
                key = key,
                type = value.type,
                intValue = value.intValue,
                floatValue = value.floatValue,
                boolValue = value.boolValue,
                stringValue = value.stringValue
            };
        }

        public SettingValue ToValue()
        {
            switch (type)
            {
                case SettingType.Int: return SettingValue.FromInt(intValue);
                case SettingType.Float: return SettingValue.FromFloat(floatValue);
                case SettingType.Bool: return SettingValue.FromBool(boolValue);
                case SettingType.String: return SettingValue.FromString(stringValue);
                default: return default;
            }
        }
    }
}

