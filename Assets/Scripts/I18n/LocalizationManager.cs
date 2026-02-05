using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;

namespace TUA.I18n
{
    public static class LocalizationManager
    {
        #region Static Fields
        private static readonly Dictionary<string, string> CurrentTranslations = new();
        private static readonly Dictionary<string, string> AvailableLanguages = new();
        #endregion
        
        #region Static Properties
        public static string CurrentLanguage { get; private set; } = "en_US";
        #endregion
        
        #region Static Events
        public static event Action OnLanguageChangeEvent;
        #endregion
        
        #region Static Methods
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            _RefreshAvailableLanguages();
            _LoadLanguage(CurrentLanguage);
        }
        
        public static void RefreshAvailableLanguages()
        {
            _RefreshAvailableLanguages();
        }
        
        private static void _RefreshAvailableLanguages()
        {
            AvailableLanguages.Clear();
            var languageAssets = Resources.LoadAll<LanguageAsset>("Languages");
            if (languageAssets == null || languageAssets.Length == 0)
            {
                Debug.LogWarning("[LocalizationManager] No language files found in Resources/Languages/. Make sure files have .lang extension and are imported as LanguageAssets.");
                return;
            }
            
            foreach (var langAsset in languageAssets)
            {
                if (!langAsset || string.IsNullOrEmpty(langAsset.languageKey) || string.IsNullOrEmpty(langAsset.languageName))
                    continue;
                AvailableLanguages[langAsset.languageKey] = langAsset.languageName;
            }
            
            if (AvailableLanguages.Count == 0)
                Debug.LogWarning("[LocalizationManager] Found language files but none contained valid language data.");
        }
        
        public static List<string> GetAvailableLanguageNames()
        {
            return AvailableLanguages.Values.ToList();
        }
        
        public static string GetLanguageKeyFromName(string name)
        {
            foreach (var kvp in AvailableLanguages)
            {
                if (kvp.Value == name) return kvp.Key;
            }
            return "en_US";
        }
        
        public static void LoadLanguage(string langKey)
        {
            _LoadLanguage(langKey);
        }
        
        private static void _LoadLanguage(string langKey)
        {
            var langAsset = Resources.Load<LanguageAsset>($"Languages/{langKey}");
            if (!langAsset || string.IsNullOrEmpty(langAsset.content))
            {
                Debug.LogWarning($"[LocalizationManager] Language file '{langKey}' not found.");
                return;
            }
            
            CurrentTranslations.Clear();
            CurrentLanguage = langKey;
            
            using (var reader = new StringReader(langAsset.content))
            {
                while (reader.ReadLine() is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("name="))
                        continue;
                    
                    var splitIndex = line.IndexOf('=');
                    if (splitIndex <= 0)
                        continue;
                    
                    string key = line.Substring(0, splitIndex).Trim();
                    string value = line.Substring(splitIndex + 1).Trim();
                    CurrentTranslations[key] = value;
                }
            }
            OnLanguageChangeEvent?.Invoke();
        }
        
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            return CurrentTranslations.TryGetValue(key, out var value) ? value : $"<{key}>";
        }
        
        public static string Get(string key, params object[] args)
        {
            var template = Get(key);
            if (template.StartsWith("<") && template.EndsWith(">") && args != null && args.Length > 0)
            {
                var argStrings = args.Select(arg => $"[{arg}]").ToArray();
                return $"<{key}, {string.Join(", ", argStrings)}>";
            }
            
            if (string.IsNullOrEmpty(template)) 
                return "";
            
            if (args == null || args.Length == 0) 
                return template;
            
            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }
        
        #endregion
    }
}
