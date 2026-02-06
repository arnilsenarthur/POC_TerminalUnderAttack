using TUA.Core;
using UnityEngine;

namespace TUA.Entities
{
    public partial class SmokeAreaEntity : Entity
    {
        #region Serialized Fields
        [Header("Smoke Settings")]
        [Tooltip("Particle system that creates the smoke effect.")]
        public ParticleSystem smokeParticleSystem;
        #endregion

        #region Private Fields
        private bool _emissionStopped;
        #endregion

        #region Unity Callbacks
        private void Awake()
        {
            if (smokeParticleSystem == null)
                smokeParticleSystem = GetComponentInChildren<ParticleSystem>();
            
            if (smokeParticleSystem != null)
            {
                smokeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _SetEmissionEnabled(true);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _Server_SetSpawnTime(Time.time);
            
            if (smokeParticleSystem != null)
            {
                smokeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _SetEmissionEnabled(true);
                _emissionStopped = false;
                smokeParticleSystem.Play();
            }
        }

        public void Update()
        {
            if (!IsSpawned)
                return;

            var spawnTime = GetSpawnTime();
            var emissionDuration = GetEmissionDuration();
            var totalLifetime = GetTotalLifetime();

            if (spawnTime <= 0f || emissionDuration <= 0f || totalLifetime <= 0f)
                return;

            var elapsedTime = Time.time - spawnTime;

            if (IsServerSide)
            {
                if (!_emissionStopped && elapsedTime >= GetEmissionDuration())
                {
                    _StopEmission();
                    _emissionStopped = true;
                }

                if (elapsedTime >= GetTotalLifetime())
                {
                    if (GameWorld.Instance)
                        GameWorld.Instance.Server_DespawnObject(gameObject);
                }
            }
            else
            {
                if (!_emissionStopped && elapsedTime >= GetEmissionDuration())
                {
                    _StopEmission();
                    _emissionStopped = true;
                }
            }
        }
        #endregion

        #region Public Methods
        public void Server_Initialize(float emissionDuration, float totalLifetime, float radius)
        {
            if (!IsServerSide)
                return;

            _Server_SetEmissionDuration(emissionDuration);
            _Server_SetTotalLifetime(totalLifetime);
            _Server_SetRadius(radius);
            _Server_SetSpawnTime(Time.time);
            transform.localScale = new Vector3(radius, radius, radius);

            if (smokeParticleSystem != null)
            {
                smokeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _SetEmissionEnabled(true);
                _emissionStopped = false;
                smokeParticleSystem.Play();
            }
        }
        #endregion


        #region Private Methods
        private void _SyncParticleSystem()
        {
            if (smokeParticleSystem == null)
                return;

            var spawnTime = GetSpawnTime();
            var emissionDuration = GetEmissionDuration();
            var totalLifetime = GetTotalLifetime();

            if (spawnTime <= 0f || emissionDuration <= 0f || totalLifetime <= 0f)
                return;

            var elapsedTime = Time.time - spawnTime;

            if (elapsedTime < 0f)
                elapsedTime = 0f;

            if (elapsedTime >= GetEmissionDuration())
            {
                _StopEmission();
                _emissionStopped = true;
            }
            else
            {
                smokeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _SetEmissionEnabled(true);
                _emissionStopped = false;
                
                if (elapsedTime > 0.01f)
                {
                    smokeParticleSystem.Simulate(elapsedTime, true, false);
                }
                
                smokeParticleSystem.Play();
            }
        }

        private void _SetEmissionEnabled(bool enabled)
        {
            if (smokeParticleSystem == null)
                return;

            var emission = smokeParticleSystem.emission;
            emission.enabled = enabled;
        }

        private void _StopEmission()
        {
            if (smokeParticleSystem == null)
                return;

            _SetEmissionEnabled(false);
        }
        #endregion
    }
}
