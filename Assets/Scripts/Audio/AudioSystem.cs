using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TUA.Misc;
using TUA.Settings;
using UnityEngine;

namespace TUA.Audio
{
    public partial class AudioSystem : SingletonNetBehaviour<AudioSystem>
    {
        #region Serialized Fields
        [Header("Settings")]
        [Tooltip("Sound registry containing all available sound clips.")]
        public SoundRegistry soundRegistry;
        
        [Tooltip("Settings asset for audio volume settings.")]
        public SettingsAsset audioSettings;
        
        [Header("Audio Source Pool")]
        [Tooltip("Initial pool size for audio sources.")]
        [Min(1)]
        public int initialPoolSize = 10;
        
        [Tooltip("Maximum pool size. If exceeded, oldest sources will be reused.")]
        [Min(1)]
        public int maxPoolSize = 50;
        #endregion

        #region Fields
        private readonly Queue<AudioSource> _audioSourcePool = new();
        private readonly List<AudioSource> _activeSources = new();
        private readonly Dictionary<string, List<AudioPlayback>> _playbacksByKey = new();
        private GameObject _poolParent;
        #endregion

        #region Unity Callbacks
        protected override void Awake()
        {
            base.Awake();
            _poolParent = new GameObject("AudioSourcePool");
            _poolParent.transform.SetParent(transform);
            
            // Pre-populate pool
            for (var i = 0; i < initialPoolSize; i++)
            {
                var source = _CreateAudioSource();
                _audioSourcePool.Enqueue(source);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_poolParent)
                Destroy(_poolParent);
        }
        #endregion

        #region Public API
        public AudioPlayback PlayAudio(string key, float volumeMultiplier = 1f, AudioCategory category = AudioCategory.General)
        {
            if (!soundRegistry)
            {
                Debug.LogWarning("[AudioSystem] Sound registry not assigned!");
                return null;
            }

            var clip = soundRegistry.GetClip(key);
            if (!clip)
            {
                Debug.LogWarning($"[AudioSystem] Sound '{key}' not found in registry!");
                return null;
            }

            var defaultVolume = soundRegistry.GetDefaultVolume(key);
            var categoryVolume = GetCategoryVolume(category);
            var finalVolume = defaultVolume * volumeMultiplier * categoryVolume;
            finalVolume = Mathf.Clamp01(finalVolume);

            var source = _GetAudioSource();
            source.clip = clip;
            source.volume = finalVolume;
            source.spatialBlend = 0f;
            
            source.Play();
            
            var playback = new AudioPlayback(this, source, key);
            _RegisterPlayback(key, playback);
            StartCoroutine(_ReturnToPoolWhenFinished(source, playback));
            
            return playback;
        }

        public AudioSource PlayLocal(string key, Vector3? position = null, float volumeMultiplier = 1f, AudioCategory category = AudioCategory.General)
        {
            var playback = PlayAudio(key, volumeMultiplier, category);
            if (playback == null)
                return null;

            if (position.HasValue)
            {
                playback.At(position.Value);
            }

            return playback.Source;
        }

        public void PlayBroadcast(string key, Vector3? position = null, float volumeMultiplier = 1f, AudioCategory category = AudioCategory.General)
        {
            if (!IsServerSide)
            {
                Debug.LogWarning("[AudioSystem] PlayBroadcast can only be called on server side!");
                return;
            }

            RpcClient_PlaySound(key, position ?? Vector3.zero, position.HasValue, volumeMultiplier, (int)category);
        }

        public float GetCategoryVolume(AudioCategory category)
        {
            if (!audioSettings)
                return 1f;

            string key = $"audio.volume.{category.ToString().ToLowerInvariant()}";
            return audioSettings.GetFloat(key, 1f);
        }

        public void CancelSound(string key)
        {
            if (string.IsNullOrEmpty(key) || !_playbacksByKey.TryGetValue(key, out var playbacks))
                return;

            var playbacksCopy = new List<AudioPlayback>(playbacks);
            foreach (var playback in playbacksCopy)
            {
                playback.Cancel();
            }
        }
        
        public void CancelPlayback(AudioPlayback playback)
        {
            if (playback == null)
                return;

            playback.Cancel();
        }
        
        internal void ReturnAudioSource(AudioSource source)
        {
            if (!source)
                return;

            if (_activeSources.Contains(source))
                _activeSources.Remove(source);

            source.Stop();
            source.clip = null;
            _audioSourcePool.Enqueue(source);
        }
        #endregion

        #region Private Methods
        private AudioSource _CreateAudioSource()
        {
            var go = new GameObject("AudioSource");
            go.transform.SetParent(_poolParent.transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private AudioSource _GetAudioSource()
        {
            AudioSource source;
            
            if (_audioSourcePool.Count > 0)
                source = _audioSourcePool.Dequeue();
            else if (_activeSources.Count < maxPoolSize)
                source = _CreateAudioSource();
            else
            {
                source = _activeSources[0];
                _activeSources.RemoveAt(0);
                
                AudioPlayback foundPlayback = null;
                foreach (var playbacks in _playbacksByKey.Select(kvp => kvp.Value))
                {
                    for (int i = playbacks.Count - 1; i >= 0; i--)
                    {
                        var playback = playbacks[i];
                        if (playback != null && playback.Source == source && !playback.IsCancelled)
                        {
                            foundPlayback = playback;
                            break;
                        }
                    }
                    if (foundPlayback != null)
                        break;
                }
                
                if (foundPlayback != null)
                {
                    Debug.LogWarning($"[AudioSystem] _GetAudioSource: Stopping playback '{foundPlayback.SoundKey}' because source is being reused");
                    foundPlayback._MarkCancelled();
                    _UnregisterPlayback(foundPlayback);
                }
                
                source.Stop();
                source.clip = null;
            }

            _activeSources.Add(source);
            return source;
        }

        private IEnumerator _ReturnToPoolWhenFinished(AudioSource source, AudioPlayback playback)
        {
            while (source && source.isPlaying && !playback.IsCancelled)
            {
                yield return null;
            }

            if (playback.IsCancelled || !source) 
                yield break;
            
            _UnregisterPlayback(playback);
            ReturnAudioSource(source);
        }

        private void _RegisterPlayback(string key, AudioPlayback playback)
        {
            if (string.IsNullOrEmpty(key) || playback == null)
                return;

            if (!_playbacksByKey.TryGetValue(key, out var list))
            {
                list = new List<AudioPlayback>();
                _playbacksByKey[key] = list;
            }

            list.Add(playback);
        }

        private void _UnregisterPlayback(AudioPlayback playback)
        {
            if (playback == null || string.IsNullOrEmpty(playback.SoundKey))
                return;

            if (!_playbacksByKey.TryGetValue(playback.SoundKey, out var list)) 
                return;
            
            list.Remove(playback);
            if (list.Count == 0)
            {
                _playbacksByKey.Remove(playback.SoundKey);
            }
        }
        #endregion
    }
}
