using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace TUA.Audio
{
    [CreateAssetMenu(menuName = "TUA/Audio/Sound Registry", fileName = "NewSoundRegistry")]
    public sealed class SoundRegistry : ScriptableObject
    {
        [FormerlySerializedAs("_entries")] [SerializeField] 
        private List<SoundRegistryEntry> entries = new();

        private Dictionary<string, SoundRegistryEntry> _entryByKey;

        private void OnEnable()
        {
            _entryByKey = null;
        }

        private void EnsureInitialized()
        {
            if (_entryByKey != null)
                return;

            _entryByKey = new Dictionary<string, SoundRegistryEntry>();
            if (entries == null) 
                return;
            
            foreach (var entry in entries.Where(entry => entry != null && !string.IsNullOrEmpty(entry.Key)).Where(entry => !_entryByKey.TryAdd(entry.Key, entry)))
            {
                Debug.LogWarning($"[SoundRegistry] Duplicate key '{entry.Key}' in registry '{name}'. Skipping duplicate.", this);
            }
        }

        public SoundRegistryEntry GetEntry(string key)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(key))
                return null;
            
            _entryByKey.TryGetValue(key, out var entry);
            return entry;
        }

        public AudioClip GetClip(string key)
        {
            var entry = GetEntry(key);
            return entry?.Clip;
        }

        public float GetDefaultVolume(string key)
        {
            var entry = GetEntry(key);
            return entry?.DefaultVolume ?? 1f;
        }

        public bool HasEntry(string key)
        {
            EnsureInitialized();
            return !string.IsNullOrEmpty(key) && _entryByKey.ContainsKey(key);
        }

        public IReadOnlyList<SoundRegistryEntry> GetAllEntries()
        {
            EnsureInitialized();
            return entries ?? new List<SoundRegistryEntry>();
        }
    }
}
