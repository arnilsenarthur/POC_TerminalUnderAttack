using System;
using UnityEngine;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Properties
        public int TickRate { get; private set; }
        public float TickDeltaTime => TickRate <= 0 ? 0f : 1f / TickRate;
        public uint NetworkTick { get; private set; }
        public uint LocalTick { get; private set; }
        public bool IsGameModeRunning { get; private set; }
        public string MatchInfoMessage { get; private set; }
        public bool MatchInfoShowTime { get; private set; }
        public float MatchInfoTimeSeconds { get; private set; }
        #endregion

        #region Static Events
        public static event Action<float> OnTickEvent;
        #endregion

        #region Public Methods
        public void Server_SetTickRate(int rate)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetTickRate can only be called on server side");
            Server_SetTickRateInternal(rate);
        }

        public void Server_SetGameModeRunning(bool running)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetGameModeRunning can only be called on server side");
            Server_SetGameModeRunningInternal(running);
        }

        public void Server_SetMatchInfo(string message, bool showTime, float timeSeconds)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetMatchInfo can only be called on server side");
            Server_SetMatchInfoInternal(message, showTime, timeSeconds);
        }

        public void Server_ClearMatchInfo()
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_ClearMatchInfo can only be called on server side");
            Server_SetMatchInfoInternal(string.Empty, false, 0f);
        }
        #endregion
    }
}
