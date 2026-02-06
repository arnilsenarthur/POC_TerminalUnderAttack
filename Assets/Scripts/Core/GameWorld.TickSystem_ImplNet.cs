using System;
using FishNet;
using FishNet.Managing.Timing;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Private Fields
        private TimeManager _timeManager;
        private readonly SyncVar<int> _tickRate = new(64);
        private readonly SyncVar<bool> _gameModeRunning = new();
        #endregion
        
        #region Unity Callbacks
        private void Start()
        {
            TickRate = _tickRate.Value;
            IsGameModeRunning = _gameModeRunning.Value;
            _tickRate.OnChange += (_, next, asServer) =>
            {
                TickRate = next;
                if (_timeManager && next > 0 && asServer)
                    _timeManager.SetTickRate((ushort)next);
            };
            _gameModeRunning.OnChange += (_, next, _) =>
            {
                IsGameModeRunning = next;
            };
        }

        private void Update()
        {
            if (_timeManager)
            {
                NetworkTick = _timeManager.Tick;
                LocalTick = _timeManager.LocalTick;
            }
        }
        #endregion

        #region Private Methods
        private void InitializeTickSystem()
        {
            if (_timeManager)
            {
                Debug.LogWarning("[GameWorld] Tick system already initialized!");
                return;
            }
            
            var networkManager = InstanceFinder.NetworkManager;
            if (!networkManager)
            {
                Debug.LogError("[GameWorld] NetworkManager not found! Tick system cannot initialize.");
                return;
            }
            
            _timeManager = networkManager.TimeManager;
            if (!_timeManager)
            {
                Debug.LogError("[GameWorld] TimeManager not found! Tick system cannot initialize.");
                return;
            }
            
            if (IsServerSide)
            {
                _tickRate.UpdateSendRate(0f);
                _tickRate.OnChange += (_, next, asServer) =>
                {
                    if (_timeManager && next > 0 && asServer)
                        _timeManager.SetTickRate((ushort)next);
                };
                _gameModeRunning.Value = false;
                _gameModeRunning.UpdateSendRate(0f);
                _timeManager.SetTickRate((ushort)_tickRate.Value);
            }
            _timeManager.OnTick += TimeManager_OnTick;
        }

        private void CleanupTickSystem()
        {
            if (_timeManager)
            {
                _timeManager.OnTick -= TimeManager_OnTick;
                _timeManager = null;
            }
        }

        private void Server_SetTickRateInternal(int rate)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetTickRateInternal can only be called on server side");
            
            if (rate <= 0)
            {
                Debug.LogWarning($"[GameWorld] Invalid tick rate: {rate}. Must be greater than 0.");
                return;
            }
            
            rate = Mathf.Clamp(rate, 1, 128);
            _tickRate.Value = rate;
            _tickRate.UpdateSendRate(0f);
            
            if (_timeManager)
                _timeManager.SetTickRate((ushort)rate);
        }

        private void Server_SetGameModeRunningInternal(bool running)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetGameModeRunningInternal can only be called on server side");
            
            _gameModeRunning.Value = running;
            _gameModeRunning.UpdateSendRate(0f);
        }

        private void TimeManager_OnTick()
        {
            var dt = _timeManager ? _timeManager.TickDelta : 0d;
            
            if (dt <= 0d)
                return;
            
            if (!_gameModeRunning.Value)
                return;
            
            var tickDelta = (float)dt;
            if (IsServerSide)
                _gameMode?.InternalOnTick(tickDelta, this);
            
            OnTickEvent?.Invoke(tickDelta);
        }
        #endregion
    }
}
