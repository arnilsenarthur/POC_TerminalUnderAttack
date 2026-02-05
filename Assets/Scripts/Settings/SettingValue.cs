using System;
using UnityEngine.Serialization;

namespace TUA.Settings
{
    [Serializable]
    public struct SettingValue : IEquatable<SettingValue>
    {
        [FormerlySerializedAs("Type")] public SettingType type;
        [FormerlySerializedAs("IntValue")] public int intValue;
        [FormerlySerializedAs("FloatValue")] public float floatValue;
        [FormerlySerializedAs("BoolValue")] public bool boolValue;
        [FormerlySerializedAs("StringValue")] public string stringValue;

        public static SettingValue FromInt(int v) => new SettingValue { type = SettingType.Int, intValue = v };
        public static SettingValue FromFloat(float v) => new SettingValue { type = SettingType.Float, floatValue = v };
        public static SettingValue FromBool(bool v) => new SettingValue { type = SettingType.Bool, boolValue = v };
        public static SettingValue FromString(string v) => new SettingValue { type = SettingType.String, stringValue = v };

        public bool Equals(SettingValue other)
        {
            if (type != other.type) return false;
            switch (type)
            {
                case SettingType.Int:
                    return intValue == other.intValue;
                case SettingType.Float:
                    return Math.Abs(floatValue - other.floatValue) < 0.00001f;
                case SettingType.Bool:
                    return boolValue == other.boolValue;
                case SettingType.String:
                    return stringValue == other.stringValue;
                default:
                    return true;
            }
        }

        public override bool Equals(object obj) => obj is SettingValue other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)type;
                hash = (hash * 397) ^ intValue;
                hash = (hash * 397) ^ floatValue.GetHashCode();
                hash = (hash * 397) ^ boolValue.GetHashCode();
                hash = (hash * 397) ^ (stringValue != null ? stringValue.GetHashCode() : 0);
                return hash;
            }
        }
    }
}

