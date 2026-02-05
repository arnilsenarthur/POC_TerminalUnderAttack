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
            if (entry == null) return true;
            if (entry.Type != SettingType.String) return true;

            if (value.type != SettingType.String)
                value = SettingValue.FromString(entry.DefaultString);

            if (IsInList(value.stringValue))
                return true;

            var fallback = !string.IsNullOrWhiteSpace(this.fallback) && IsInList(this.fallback)
                ? this.fallback
                : (options != null && options.Count > 0 ? options[0] : value.stringValue);

            value = SettingValue.FromString(fallback);
            return true;
        }

        protected bool IsInList(string value)
        {
            var opts = GetOptions();
            if (opts == null || opts.Count == 0) return true;

            value ??= "";
            for (var i = 0; i < opts.Count; i++)
            {
                if (caseSensitive)
                {
                    if (opts[i] == value) return true;
                }
                else
                {
                    if (string.Equals(opts[i], value, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
