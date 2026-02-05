using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core
{
    public partial class GameWorld : SingletonNetBehaviour<GameWorld>
    {   
        [Header("Game Modes")]
        [SerializeField]
        private GameMode[] gameModes;

        [SerializeField]
        private string currentGameModeId;
        private GameMode _gameMode;
        
        private IGameSettings _gameSettings;

        public IReadOnlyList<GamePlayer> AllPlayers { get; private set; }
        public GamePlayer LocalGamePlayer { get; private set; }
        
        public void Server_SetGameSettings(IGameSettings gameSettings)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetGameSettings can only be called on server side");
            _gameSettings = gameSettings;
        }
        
        public Gs GetGameSettings<Gs>() where Gs : struct, IGameSettings
        {
            if (_gameSettings is Gs settings)
                return settings;
            return default(Gs);
        }

        public Entity GetTargetEntity<T>() where T : Entity
        {
            var localGamePlayer = LocalGamePlayer;
            if (localGamePlayer == null)
                return null;

            if(localGamePlayer.IsSpectator)
                return GetEntityByUuid(localGamePlayer.SpectatorTargetUuid) as T;

            var playerEntities = GetEntitiesOwnedByPlayer(localGamePlayer);
            if (playerEntities is { Count: > 0 })
                return playerEntities[0] as T;
            
            return null;
        }
        
        private GameMode GetConfiguredGameMode()
        {
            if (gameModes == null || gameModes.Length == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(currentGameModeId)) 
                return FindGameModeById(currentGameModeId);

            if (gameModes.Length != 1 || !gameModes[0]) 
                return null;
            
            Debug.LogWarning($"[GameWorld] `currentGameModeId` is empty; using the only configured GameMode ({gameModes[0].GetType().Name}, id='{gameModes[0].Id}').");
            return gameModes[0];
        }

        private GameMode FindGameModeById(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : gameModes?.Where(mode => mode).FirstOrDefault(mode => string.Equals(mode.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public void Server_AddPlayer(object connection, GamePlayer player)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_AddPlayer can only be called on server side");
            Server_AddPlayerInternal(connection, player);
        }

        public void Server_RemovePlayer(object connection)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_RemovePlayer can only be called on server side");
            Server_RemovePlayerInternal(connection);
        }

        public void Server_SetPlayerOnline(GamePlayer player, bool isOnline)
        {
            if (!IsServerSide || player == null)
                return;
            
            Server_SetPlayerOnlineInternal(player, isOnline);
        }

        public void Server_SetSpectatorTarget(GamePlayer player, Uuid targetEntityUuid)
        {
            if (!IsServerSide || player == null)
                return;
            
            Server_SetSpectatorTargetInternal(player, targetEntityUuid);
        }

        public void Client_RequestJoin(string username)
        {
            if (!IsClientSide)
            {
                Debug.LogWarning($"[GameWorld] Cannot join - IsClientSide: {IsClientSide}");
                return;
            }
            Client_RequestJoinInternal(username);
        }

        private static Uuid GenerateUuidFromString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return Uuid.New;

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                
            var high = BitConverter.ToInt32(hashBytes, 0);
            var low = BitConverter.ToInt32(hashBytes, 4);
                
            return new Uuid(high, low);
        }
    }
}