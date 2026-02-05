using System;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Tick System Properties
        public int TickRate { get; private set; }
        public float TickDeltaTime => TickRate <= 0 ? 0f : 1f / TickRate;
        public uint NetworkTick { get; private set; }
        public uint LocalTick { get; private set; }
        public bool IsGameModeRunning { get; private set; }
        #endregion

        #region Tick System Events
        public static event Action<float> OnTickEvent;
        #endregion

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
    }
}
