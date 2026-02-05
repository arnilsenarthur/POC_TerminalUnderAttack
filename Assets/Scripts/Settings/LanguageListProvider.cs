using TUA.I18n;
using UnityEngine;

namespace TUA.Settings
{
    [CreateAssetMenu(menuName = "TUA/Settings/Providers/Language List", fileName = "LanguageListProvider")]
    public class LanguageListProvider : SettingProvider
    {
        public override string[] GetStringOptions(SettingsAsset settings, SettingEntry entry)
        {
            LocalizationManager.RefreshAvailableLanguages();
            var languages = LocalizationManager.GetAvailableLanguageNames();
            if (languages != null && languages.Count != 0)
                return languages.ToArray();
            
            Debug.LogWarning("[LanguageListProvider] No languages found. Make sure language files exist in Resources/Languages/");
            return new[] { "English" };
        }

        public override void ApplyValue(SettingValue value)
        {
            if (value.type != SettingType.String) return;
            var strValue = value.stringValue;
            
            var key = LocalizationManager.GetLanguageKeyFromName(strValue);
            if (LocalizationManager.CurrentLanguage != key)
            {
                LocalizationManager.LoadLanguage(key);
            }
        }
    }
}
