using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace TUA.Settings
{
    [CreateAssetMenu(menuName = "TUA/Settings/Providers/String List", fileName = "StringListProvider")]
    public class StringListProvider : SettingProvider
    {
        [FormerlySerializedAs("_options")] [SerializeField] protected List<string> options = new List<string>();
        [FormerlySerializedAs("_caseSensitive")] [SerializeField] private bool caseSensitive = true;
        [FormerlySerializedAs("_fallback")] [SerializeField] private string fallback = "";

        public virtual List<string> GetOptions()
        {
            return options;
        }

        public override string[] GetStringOptions(SettingsAsset settings, SettingEntry entry)
        {
            var opts = GetOptions();
            if (opts == null || opts.Count == 0)
                return Array.Empty<string>();
            return opts.ToArray();
        }

        public override bool Validate(SettingsAsset settings, SettingEntry entry, ref SettingValue value)
        {
            if (entry == null) 
                return true;
            
            if (entry.Type != SettingType.String) 
                return true;

            if (value.type != SettingType.String)
                value = SettingValue.FromString(entry.DefaultString);

            if (IsInList(value.stringValue))
                return true;

            var fallbackInternal = !string.IsNullOrWhiteSpace(this.fallback) && IsInList(this.fallback)
                ? fallback
                : (options != null && options.Count > 0 ? options[0] : value.stringValue);

            value = SettingValue.FromString(fallbackInternal);
            return true;
        }

        protected bool IsInList(string value)
        {
            var opts = GetOptions();
            if (opts == null || opts.Count == 0) return true;

            value ??= "";
            foreach (var t in opts)
            {
                if (caseSensitive)
                {
                    if (t == value) 
                        return true;
                }
                else
                {
                    if (string.Equals(t, value, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
