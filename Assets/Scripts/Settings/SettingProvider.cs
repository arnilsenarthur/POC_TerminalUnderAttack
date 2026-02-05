using UnityEngine;

namespace TUA.Settings
{
    public abstract class SettingProvider : ScriptableObject
    {
        public virtual string[] GetStringOptions(SettingsAsset settings, SettingEntry entry) => null;

        public virtual bool Validate(SettingsAsset settings, SettingEntry entry, ref SettingValue value) => true;

        public virtual void ApplyValue(SettingValue value) { }
    }
}
