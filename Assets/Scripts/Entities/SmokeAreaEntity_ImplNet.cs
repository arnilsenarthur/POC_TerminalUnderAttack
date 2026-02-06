using FishNet.Object.Synchronizing;
using TUA.Core;
using UnityEngine;

namespace TUA.Entities
{
    public partial class SmokeAreaEntity
    {
        #region Private Fields
        private readonly SyncVar<float> _spawnTime = new();
        private readonly SyncVar<float> _emissionDuration = new();
        private readonly SyncVar<float> _totalLifetime = new();
        private readonly SyncVar<float> _radius = new();
        #endregion

        #region Unity Callbacks
        public override void OnStartClient()
        {
            base.OnStartClient();
            _spawnTime.OnChange += _OnSpawnTimeChanged;
            _emissionDuration.OnChange += _OnEmissionDurationChanged;
            _totalLifetime.OnChange += _OnTotalLifetimeChanged;
            _radius.OnChange += _OnRadiusChanged;
            
            if (smokeParticleSystem != null)
            {
                smokeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            
            _SyncParticleSystem();
        }

        private void OnDestroy()
        {
            _spawnTime.OnChange -= _OnSpawnTimeChanged;
            _emissionDuration.OnChange -= _OnEmissionDurationChanged;
            _totalLifetime.OnChange -= _OnTotalLifetimeChanged;
            _radius.OnChange -= _OnRadiusChanged;
        }
        #endregion

        #region Private Methods
        private void _Server_SetSpawnTime(float value)
        {
            if (!IsServerSide)
                return;
            _spawnTime.Value = value;
        }

        private void _Server_SetEmissionDuration(float value)
        {
            if (!IsServerSide)
                return;
            _emissionDuration.Value = value;
        }

        private void _Server_SetTotalLifetime(float value)
        {
            if (!IsServerSide)
                return;
            _totalLifetime.Value = value;
        }

        private void _Server_SetRadius(float value)
        {
            if (!IsServerSide)
                return;
            _radius.Value = value;
        }

        private float GetSpawnTime() => _spawnTime.Value;
        private float GetEmissionDuration() => _emissionDuration.Value;
        private float GetTotalLifetime() => _totalLifetime.Value;
        private float GetRadius() => _radius.Value;

        private void _OnSpawnTimeChanged(float prev, float next, bool asServer)
        {
            if (asServer)
                return;

            _SyncParticleSystem();
        }

        private void _OnEmissionDurationChanged(float prev, float next, bool asServer)
        {
            if (asServer)
                return;

            _SyncParticleSystem();
        }

        private void _OnTotalLifetimeChanged(float prev, float next, bool asServer)
        {
            if (asServer)
                return;

            _SyncParticleSystem();
        }

        private void _OnRadiusChanged(float prev, float next, bool asServer)
        {
            if (asServer)
                return;

            transform.localScale = new Vector3(next, next, next);
        }
        #endregion
    }
}
