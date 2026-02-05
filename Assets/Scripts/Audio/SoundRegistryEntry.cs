using UnityEngine;
using UnityEngine.Serialization;

namespace TUA.Audio
{
    [System.Serializable]
    public sealed class SoundRegistryEntry
    {
        [FormerlySerializedAs("_key")] [SerializeField] private string key;
        [FormerlySerializedAs("_clip")] [SerializeField] private AudioClip clip;
        [FormerlySerializedAs("_defaultVolume")]
        [Tooltip("Default volume for this sound (0-5). This will be multiplied by the volume multiplier and category setting, then clamped to 1.0 for AudioSource.")]
        [Range(0f, 5f)]
        [SerializeField] private float defaultVolume;
        public string Key => key;
        public AudioClip Clip => clip;
        public float DefaultVolume => defaultVolume;
        
        public SoundRegistryEntry(string key, AudioClip clip, float defaultVolume = 1f)
        {
            this.key = key;
            this.clip = clip;
            this.defaultVolume = Mathf.Clamp(defaultVolume, 0f, 5f);
        }
    }
}
