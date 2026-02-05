using System;
using System.Collections.Generic;
using TUA.Misc;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Player Data Management
        private readonly Dictionary<Uuid, IPlayerData> _playerData = new();
        
        public IPlayerData Server_GetPlayerData(GamePlayer player)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_GetPlayerData can only be called on server side");
            
            if (player == null)
                return null;
            
            return _playerData.GetValueOrDefault(player.Uuid);
        }
        
        public IPlayerData Client_GetPlayerData(GamePlayer player)
        {
            if (IsServerSide)
                throw new InvalidOperationException("Client_GetPlayerData can only be called on client side");
            
            if (player == null)
                return null;
            
            return _clientPlayerDataSnapshots.GetValueOrDefault(player.Uuid);
        }
        
        private IPlayerData Server_GetPlayerDataSnapshot(GamePlayer player, GamePlayer requestingPlayer)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_GetPlayerDataSnapshot can only be called on server side");
            
            if (player == null)
                return null;
            
            var fullData = Server_GetPlayerData(player);
            if (fullData == null)
                return null;
            
            if (requestingPlayer == null || requestingPlayer.Uuid == player.Uuid)
                return fullData;
            
            return _gameMode ? _gameMode.GetPlayerDataSnapshot(player, fullData, requestingPlayer, this) : null;
        }
        
        public void Server_SetPlayerData(GamePlayer player, IPlayerData data)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetPlayerData can only be called on server side");
            
            if (player == null)
                return;
            
            var oldData = Server_GetPlayerData(player);
            _playerData[player.Uuid] = data;
            
            if (_gameMode)
                _gameMode.OnPlayerDataChanged(player, oldData, data, this);
            
            Server_BroadcastPlayerDataSnapshot(player);
        }
        
        public bool Server_HasPlayerData(GamePlayer player)
        {
            if (!IsServerSide || player == null)
                return false;
            return _playerData.ContainsKey(player.Uuid);
        }
        
        private void Server_RemovePlayerData(GamePlayer player)
        {
            if (player == null)
                return;
            _playerData.Remove(player.Uuid);
        }
        #endregion
    }
}
