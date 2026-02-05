using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace TUA.Settings
{
    [Serializable]
    public sealed class SettingEntry
    {
        [FormerlySerializedAs("_visible")] [SerializeField] private bool visible = true;
        [FormerlySerializedAs("_type")] [SerializeField] private SettingType type = SettingType.Int;

        [FormerlySerializedAs("_key")] [SerializeField] private string key;

        [FormerlySerializedAs("_defaultInt")]
        [Header("Defaults")]
        [SerializeField] private int defaultInt;
        [FormerlySerializedAs("_defaultFloat")] [SerializeField] private float defaultFloat;
        [FormerlySerializedAs("_defaultBool")] [SerializeField] private bool defaultBool;
        [FormerlySerializedAs("_defaultString")] [SerializeField] private string defaultString;

        [FormerlySerializedAs("_intClamp")]
        [Header("Int (Clamp/Step)")]
        [SerializeField] private bool intClamp;
        [FormerlySerializedAs("_intMin")] [SerializeField] private int intMin;
        [FormerlySerializedAs("_intMax")] [SerializeField] private int intMax = 100;
        [FormerlySerializedAs("_intStep")]
        [Min(1)]
        [SerializeField] private int intStep = 1;

        [FormerlySerializedAs("_floatClamp")]
        [Header("Float (Clamp/Step)")]
        [SerializeField] private bool floatClamp;
        [FormerlySerializedAs("_floatMin")] [SerializeField] private float floatMin;
        [FormerlySerializedAs("_floatMax")] [SerializeField] private float floatMax = 1f;
        [FormerlySerializedAs("_floatStep")]
        [Min(0f)]
        [SerializeField] private float floatStep;

        [FormerlySerializedAs("_staticStringOptions")]
        [Header("String Dropdown")]
        [Tooltip("Used when providers return no options.")]
        [SerializeField] private List<string> staticStringOptions = new();

        [FormerlySerializedAs("_provider")]
        [Header("Providers (optional)")]
        [SerializeField] private SettingProvider provider;

        public bool Visible => visible;
        public SettingType Type => type;
        public string Key => key;

        public int DefaultInt => defaultInt;
        public float DefaultFloat => defaultFloat;
        public bool DefaultBool => defaultBool;
        public string DefaultString => defaultString;

        public bool IntClamp => intClamp;
        public int IntMin => intMin;
        public int IntMax => intMax;
        public int IntStep => intStep;

        public bool FloatClamp => floatClamp;
        public float FloatMin => floatMin;
        public float FloatMax => floatMax;
        public float FloatStep => floatStep;

        public string[] StringOptions => staticStringOptions?.ToArray() ?? Array.Empty<string>();
        public SettingProvider Provider => provider;

        public bool IsKeyed => !string.IsNullOrWhiteSpace(key);

        public SettingValue GetDefaultValue()
        {
            switch (type)
            {
                case SettingType.Int: return SettingValue.FromInt(defaultInt);
                case SettingType.Float: return SettingValue.FromFloat(defaultFloat);
                case SettingType.Bool: return SettingValue.FromBool(defaultBool);
                case SettingType.String: return SettingValue.FromString(defaultString);
                default: return default;
            }
        }

        public void OnValidateFixup()
        {
            key ??= "";
            defaultString ??= "";
            if (staticStringOptions == null) staticStringOptions = new List<string>();

            if (intMax < intMin)
                (intMin, intMax) = (intMax, intMin);

            if (floatMax < floatMin)
                (floatMin, floatMax) = (floatMax, floatMin);
            if (floatStep < 0f) floatStep = 0f;

            if (type == SettingType.Int && intClamp)
                defaultInt = Mathf.Clamp(defaultInt, intMin, intMax);
            if (type == SettingType.Float && floatClamp)
                defaultFloat = Mathf.Clamp(defaultFloat, floatMin, floatMax);

            if (type == SettingType.String && staticStringOptions.Count > 0 && string.IsNullOrEmpty(defaultString))
                defaultString = staticStringOptions[0];
        }
    }
}

