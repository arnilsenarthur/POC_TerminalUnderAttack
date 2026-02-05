using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace TUA.Misc
{
    [MovedFrom("TUA.Core")]
    public abstract class RegistrableScriptableObject : ScriptableObject
    {
        [FormerlySerializedAs("_id")] [SerializeField]
        private string id;

        public string Id => id;

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = name;
            }
        }
    }
}
