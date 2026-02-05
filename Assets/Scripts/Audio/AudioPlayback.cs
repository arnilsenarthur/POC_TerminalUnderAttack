using System.Collections;
using UnityEngine;

namespace TUA.Audio
{
    public sealed class AudioPlayback
    {
        private readonly AudioSystem _system;
        private readonly AudioSource _source;
        private Coroutine _followCoroutine;
        private bool _isCancelled;

        public string SoundKey { get; private set; }

        internal AudioPlayback(AudioSystem system, AudioSource source, string soundKey)
        {
            _system = system;
            _source = source;
            SoundKey = soundKey;
        }
        
        public AudioPlayback At(Vector3 position)
        {
            if (_isCancelled || !_source)
                return this;

            _source.spatialBlend = 1f;
            _source.transform.position = position;
            return this;
        }
        
        public AudioPlayback Follow(Transform transform)
        {
            if (_isCancelled || !_source)
                return this;
            
            if (_followCoroutine != null && _system)
            {
                _system.StopCoroutine(_followCoroutine);
            }

            if (!transform)
            {
                Cancel();
                return this;
            }

            _source.spatialBlend = 1f;
            _source.transform.position = transform.position;
            _followCoroutine = _system.StartCoroutine(_FollowTransform(transform));
            return this;
        }
        
        public void Cancel()
        {
            if (_isCancelled)
                return;

            _isCancelled = true;

            if (_followCoroutine != null && _system)
            {
                _system.StopCoroutine(_followCoroutine);
                _followCoroutine = null;
            }

            if (!_source) 
                return;
            
            _source.Stop();
            _system?.ReturnAudioSource(_source);
        }

        private IEnumerator _FollowTransform(Transform transform)
        {
            while (!_isCancelled && _source && _source.isPlaying && transform)
            {
                _source.transform.position = transform.position;
                yield return null;
            }

            if (!transform && !_isCancelled && _source)
                Cancel();

            _followCoroutine = null;
        }
        
        public AudioSource Source => _source;
        
        public bool IsCancelled => _isCancelled;
        
        internal void _MarkCancelled()
        {
            _isCancelled = true;
            
            if (_followCoroutine != null && _system)
            {
                _system.StopCoroutine(_followCoroutine);
                _followCoroutine = null;
            }
            
            if (_source)
                _source.Stop();
        }
    }
}
