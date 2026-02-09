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
        private readonly SyncVar<string> _matchInfoMessage = new();
        private readonly SyncVar<bool> _matchInfoShowTime = new();
        private readonly SyncVar<float> _matchInfoTimeSeconds = new();
        #endregion
        
        #region Unity Callbacks
        private void Start()
        {
            TickRate = _tickRate.Value;
            IsGameModeRunning = _gameModeRunning.Value;
            MatchInfoMessage = _matchInfoMessage.Value;
            MatchInfoShowTime = _matchInfoShowTime.Value;
            MatchInfoTimeSeconds = _matchInfoTimeSeconds.Value;
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
            _matchInfoMessage.OnChange += (_, next, _) =>
            {
                MatchInfoMessage = next;
            };
            _matchInfoShowTime.OnChange += (_, next, _) =>
            {
                MatchInfoShowTime = next;
            };
            _matchInfoTimeSeconds.OnChange += (_, next, _) =>
            {
                MatchInfoTimeSeconds = next;
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
                _matchInfoMessage.Value = string.Empty;
                _matchInfoMessage.UpdateSendRate(0f);
                _matchInfoShowTime.Value = false;
                _matchInfoShowTime.UpdateSendRate(0f);
                _matchInfoTimeSeconds.Value = 0f;
                _matchInfoTimeSeconds.UpdateSendRate(0f);
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

        private void Server_SetMatchInfoInternal(string message, bool showTime, float timeSeconds)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetMatchInfoInternal can only be called on server side");

            _matchInfoMessage.Value = message ?? string.Empty;
            _matchInfoMessage.UpdateSendRate(0f);
            _matchInfoShowTime.Value = showTime;
            _matchInfoShowTime.UpdateSendRate(0f);
            _matchInfoTimeSeconds.Value = Mathf.Max(0f, timeSeconds);
            _matchInfoTimeSeconds.UpdateSendRate(0f);
        }

        private void Server_UpdateTeamsFromGameModeInternal()
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_UpdateTeamsFromGameModeInternal can only be called on server side");

            if (!_gameMode)
                return;

            var teams = _gameMode.InternalGetTeams(this);
            if (teams == null || teams.Count == 0)
                return;

            _teams.Clear();
            for (var i = 0; i < teams.Count; i++)
            {
                var t = teams[i];
                if (t == null || string.IsNullOrWhiteSpace(t.Name))
                    continue;
                _teams.Add(new Team(t.Name, t.Color));
            }
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
            {
                _gameMode?.InternalOnTick(tickDelta, this);
            }
            
            OnTickEvent?.Invoke(tickDelta);
        }
        #endregion
    }
}
