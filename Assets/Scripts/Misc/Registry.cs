using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace TUA.Misc
{
    [MovedFrom("TUA.Core")]
    [CreateAssetMenu(menuName = "TUA/Registry", fileName = "NewRegistry")]
    public class Registry : ScriptableObject
    {
        [FormerlySerializedAs("_entries")] [SerializeField]
        private List<RegistrableScriptableObject> entries = new();

        public IReadOnlyList<RegistrableScriptableObject> Entries => entries;

        public RegistrableScriptableObject GetEntry(string id)
        {
            return string.IsNullOrEmpty(id) ? null : entries.FirstOrDefault(e => e && e.Id == id);
        }

        public T GetEntry<T>(string id) where T : RegistrableScriptableObject
        {
            return string.IsNullOrEmpty(id) ? null : entries.OfType<T>().FirstOrDefault(e => e.Id == id);
        }

        public bool TryGetEntry(string id, out RegistrableScriptableObject entry)
        {
            entry = GetEntry(id);
            return entry;
        }

        public bool TryGetEntry<T>(string id, out T entry) where T : RegistrableScriptableObject
        {
            entry = GetEntry<T>(id);
            return entry;
        }

        public bool HasEntry(string id)
        {
            return GetEntry(id);
        }
    }
}
