using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;

namespace TUA.Settings
{
    [CreateAssetMenu(menuName = "TUA/Settings/Settings Asset", fileName = "NewSettings")]
    public sealed class SettingsAsset : ScriptableObject
    {
         [FormerlySerializedAs("_unlocalizedName")]
         [Header("Display")]
         [Tooltip("Shown in the settings UI tab header. If empty, falls back to the asset name.")]
         [SerializeField] private string unlocalizedName = "";

        [FormerlySerializedAs("_fileName")]
        [Tooltip("File name (relative to Application.persistentDataPath). Leave empty to use asset name + .json")]
        [SerializeField] private string fileName = "";
        [FormerlySerializedAs("_autoLoadOnEnable")] [SerializeField] private bool autoLoadOnEnable = true;
        [FormerlySerializedAs("_autoSaveOnChange")] [SerializeField] private bool autoSaveOnChange = true;

        [FormerlySerializedAs("_entries")]
        [Header("Entries")]
        [SerializeField] private List<SettingEntry> entries = new();

        [NonSerialized] private Dictionary<string, SettingEntry> _entryByKey;
        [NonSerialized] private Dictionary<string, SettingValue> _values;
        [NonSerialized] private Dictionary<string, Action<SettingChanged>> _perKeyHandlers;
        [NonSerialized] private bool _initialized;

        public IReadOnlyList<SettingEntry> Entries => entries;

         public string UnlocalizedName => string.IsNullOrWhiteSpace(unlocalizedName) ? name : unlocalizedName;

        public event Action<SettingChanged> OnAnyChangeEvent;

         
         public event Action<string> OnLoadEvent;

         
         public event Action<string> OnSaveEvent;

         
         public event Action<string> OnResetAllEvent;

        private void OnEnable()
        {
            _initialized = false;
            EnsureInitialized();
            if (autoLoadOnEnable)
                LoadFromFile();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(unlocalizedName))
                unlocalizedName = "";

            entries ??= new List<SettingEntry>();
            foreach (var t in entries)
                t?.OnValidateFixup();

            _initialized = false;
        }

        public void Subscribe(string key, Action<SettingChanged> handler)
        {
            if (string.IsNullOrWhiteSpace(key) || handler == null)
                return;
            
            EnsureInitialized();
            _perKeyHandlers.TryGetValue(key, out var existing);
            _perKeyHandlers[key] = existing + handler;
        }

        public void Unsubscribe(string key, Action<SettingChanged> handler)
        {
            if (string.IsNullOrWhiteSpace(key) || handler == null)
                return;
            
            EnsureInitialized();
            if (!_perKeyHandlers.TryGetValue(key, out var existing)) 
                return;
            
            existing -= handler;
            if (existing == null) 
                _perKeyHandlers.Remove(key);
            
            else _perKeyHandlers[key] = existing;
        }

        public bool TryGetEntry(string key, out SettingEntry entry)
        {
            EnsureInitialized();
            return _entryByKey.TryGetValue(key, out entry);
        }

        public bool HasKey(string key)
        {
            EnsureInitialized();
            return _entryByKey.ContainsKey(key);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            return !TryGetTypedEntry(key, SettingType.Int, out var entry) ? defaultValue : GetValueValidated(key, entry).intValue;
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            return !TryGetTypedEntry(key, SettingType.Float, out var entry) ? defaultValue : GetValueValidated(key, entry).floatValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            return !TryGetTypedEntry(key, SettingType.Bool, out var entry) ? defaultValue : GetValueValidated(key, entry).boolValue;
        }

        public string GetString(string key, string defaultValue = null)
        {
            return !TryGetTypedEntry(key, SettingType.String, out var entry) ? defaultValue : GetValueValidated(key, entry).stringValue;
        }

        public bool SetInt(string key, int value) => SetValue(key, SettingValue.FromInt(value));
        public bool SetFloat(string key, float value) => SetValue(key, SettingValue.FromFloat(value));
        public bool SetBool(string key, bool value) => SetValue(key, SettingValue.FromBool(value));
        public bool SetString(string key, string value) => SetValue(key, SettingValue.FromString(value));

        public bool ResetToDefault(string key)
        {
            if (!TryGetEntry(key, out var entry) || !entry.IsKeyed)
                return false;
            
            return SetValue(key, entry.GetDefaultValue());
        }

        public void ResetAllToDefaults()
        {
            var path = GetFilePath();
            Debug.Log($"[Settings] Reset all defaults ({name}) � file: {path}", this);

            EnsureInitialized();
            foreach (var kvp in _entryByKey)
            {
                var e = kvp.Value;
                if (e == null || !e.IsKeyed) continue;
                SetValue(kvp.Key, e.GetDefaultValue());
            }

            OnResetAllEvent?.Invoke(path);
        }

        public string[] GetStringOptions(string key)
        {
            return !TryGetTypedEntry(key, SettingType.String, out var entry) ? null : GetStringOptions(entry);
        }

        private string[] GetStringOptions(SettingEntry entry)
        {
            if (entry is not { Type: SettingType.String }) 
                return null;

            var provider = entry.Provider;
            if (provider)
            {
                var opts = provider.GetStringOptions(this, entry);
                if (opts != null && opts.Length > 0) 
                    return opts;
            }

            var staticOpts = entry.StringOptions;
            if (staticOpts == null || staticOpts.Length == 0) 
                return null;
            
            return staticOpts;
        }

        public void LoadFromFile()
        {
            EnsureInitialized();

            var path = GetFilePath();
            Debug.Log($"[Settings] Load ({name}) � file: {path}", this);
            
            if (!File.Exists(path))
            {
                InitializeValuesFromDefaults();
                OnLoadEvent?.Invoke(path);
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var save = JsonUtility.FromJson<SettingsSaveFile>(json);
                if (save == null || save.items == null)
                {
                    InitializeValuesFromDefaults();
                    return;
                }

                InitializeValuesFromDefaults();

                foreach (var item in save.items)
                {
                    if (string.IsNullOrWhiteSpace(item.key)) 
                        continue;
                    
                    if (!TryGetEntry(item.key, out var entry))
                        continue;
                    
                    if (!entry.IsKeyed) 
                        continue;
                    
                    var val = item.ToValue();
                    if (val.type != entry.Type) 
                        continue;
                    
                    _values[item.key] = val;
                    GetValueValidated(item.key, entry);
                }

                 OnLoadEvent?.Invoke(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load settings from '{path}': {ex.Message}", this);
                InitializeValuesFromDefaults();
                 OnLoadEvent?.Invoke(path);
            }
        }

        public void SaveToFile()
        {
            EnsureInitialized();
            
            try
            {
                 var path = GetFilePath();
                 Debug.Log($"[Settings] Save ({name}) � file: {path}", this);

                var save = new SettingsSaveFile
                {
                    version = 1,
                    items = new List<SettingsSaveItem>(_values.Count)
                };

                foreach (var kvp in _values)
                {
                    if (!TryGetEntry(kvp.Key, out var entry) || entry == null || !entry.IsKeyed) continue;
                    if (entry.Type == SettingType.Label) continue;
                    
                    var val = GetValueValidated(kvp.Key, entry);
                    save.items.Add(SettingsSaveItem.From(kvp.Key, val));
                }

                 var json = JsonUtility.ToJson(save, prettyPrint: true);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.persistentDataPath);
                File.WriteAllText(path, json);

                 OnSaveEvent?.Invoke(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save settings: {ex.Message}", this);
            }
        }

        private bool SetValue(string key, SettingValue newValue)
        {
            EnsureInitialized();
            if (!TryGetEntry(key, out var entry) || entry == null || !entry.IsKeyed) return false;
            if (newValue.type != entry.Type) return false;

            var oldValue = GetValueValidated(key, entry);
            
            newValue = Validate(entry, newValue);
            if (newValue.Equals(oldValue))
                return false;

            _values[key] = newValue;
            RaiseChanged(key, entry.Type, oldValue, newValue);

            if (autoSaveOnChange)
                SaveToFile();

            return true;
        }

        private SettingValue GetValueValidated(string key, SettingEntry entry)
        {
            EnsureInitialized();

            if (!_values.TryGetValue(key, out var value))
            {
                value = entry.GetDefaultValue();
                _values[key] = value;
            }

            var validated = Validate(entry, value);
            if (!validated.Equals(value))
            {
                _values[key] = validated;
                RaiseChanged(key, entry.Type, value, validated);
                if (autoSaveOnChange)
                    SaveToFile();
            }

            return validated;
        }

        private SettingValue Validate(SettingEntry entry, SettingValue value)
        {
            if (entry == null) return value;
            if (value.type != entry.Type) value = entry.GetDefaultValue();

            var provider = entry.Provider;
            if (provider != null)
            {
                provider.Validate(this, entry, ref value);
                if (value.type != entry.Type)
                    value = entry.GetDefaultValue();
            }

             
             if (entry.Type == SettingType.Int)
             {
                 var v = value.intValue;

                 if (entry.IntClamp)
                     v = Mathf.Clamp(v, entry.IntMin, entry.IntMax);

                 var step = Mathf.Max(1, entry.IntStep);
                 var baseValue = entry.IntClamp ? entry.IntMin : 0;
                 if (step > 1)
                 {
                     v = baseValue + Mathf.RoundToInt((v - baseValue) / (float)step) * step;
                     if (entry.IntClamp)
                         v = Mathf.Clamp(v, entry.IntMin, entry.IntMax);
                 }

                 value = SettingValue.FromInt(v);
             }
             else if (entry.Type == SettingType.Float)
             {
                 var v = value.floatValue;

                 if (entry.FloatClamp)
                     v = Mathf.Clamp(v, entry.FloatMin, entry.FloatMax);

                 var step = Mathf.Max(0f, entry.FloatStep);
                 var baseValue = entry.FloatClamp ? entry.FloatMin : 0f;
                 if (step > 0.000001f)
                 {
                     v = baseValue + Mathf.Round((v - baseValue) / step) * step;
                     if (entry.FloatClamp)
                         v = Mathf.Clamp(v, entry.FloatMin, entry.FloatMax);
                 }

                 value = SettingValue.FromFloat(v);
             }

            if (entry.Type == SettingType.String)
            {
                var opts = GetStringOptions(entry);
                if (opts != null && opts.Length > 0)
                {
                    var cur = value.stringValue ?? "";
                    var found = false;
                    for (var i = 0; i < opts.Length; i++)
                    {
                        if (opts[i] == cur) { found = true; break; }
                    }

                    if (!found)
                        value = SettingValue.FromString(opts[0]);
                }
            }

            return value;
        }

        private bool TryGetTypedEntry(string key, SettingType type, out SettingEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (!TryGetEntry(key, out entry) || entry == null) return false;
            return entry.Type == type;
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            _entryByKey = new Dictionary<string, SettingEntry>(StringComparer.Ordinal);
            _values = new Dictionary<string, SettingValue>(StringComparer.Ordinal);
            _perKeyHandlers = new Dictionary<string, Action<SettingChanged>>(StringComparer.Ordinal);

            if (entries == null) entries = new List<SettingEntry>();

            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                e.OnValidateFixup();

                if (!e.IsKeyed) continue;

                if (_entryByKey.ContainsKey(e.Key))
                {
                    Debug.LogError($"Duplicate setting key '{e.Key}' in SettingsAsset '{name}'. Keys must be unique.", this);
                    continue;
                }

                _entryByKey.Add(e.Key, e);
            }

            InitializeValuesFromDefaults();
            _initialized = true;
        }

        private void InitializeValuesFromDefaults()
        {
            _values.Clear();
            foreach (var kvp in _entryByKey)
            {
                var e = kvp.Value;
                if (e == null || !e.IsKeyed) continue;
                _values[kvp.Key] = e.GetDefaultValue();
            }
        }

        private void RaiseChanged(string key, SettingType type, SettingValue oldValue, SettingValue newValue)
        {
            var evt = new SettingChanged(key, type, oldValue, newValue);
            OnAnyChangeEvent?.Invoke(evt);
            if (_perKeyHandlers != null && _perKeyHandlers.TryGetValue(key, out var handlers))
                handlers?.Invoke(evt);
        }

        private string GetFilePath()
        {
            var file = string.IsNullOrWhiteSpace(fileName) ? $"{name}.json" : fileName;
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                file += ".json";
            return Path.Combine(Application.persistentDataPath, file);
        }
    }
}

