using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Private Fields
        private readonly SyncList<GamePlayer> _allPlayers = new();
        private readonly Dictionary<object, GamePlayer> _connectionToPlayer = new();
        private Uuid _localPlayerUuid;
        #endregion
        
        #region Unity Callbacks
        protected override void Awake()
        {
            base.Awake();
            AllPlayers = _allPlayers;
            _allPlayers.OnChange += (_, _, _, _, _) =>
            {
                if (!IsClientSide || !_localPlayerUuid.IsValid)
                    return;
                
                foreach (var player in _allPlayers.Where(player => player != null && player.Uuid == _localPlayerUuid))
                {
                    LocalGamePlayer = player;
                    return;
                }

                LocalGamePlayer = null;
            };
            var networkManager = InstanceFinder.NetworkManager;
            if (!networkManager) 
                return;

            if (!networkManager.ServerManager) 
                return;
            
            networkManager.ServerManager.OnServerConnectionState += _OnServerConnectionState;
            networkManager.ServerManager.OnRemoteConnectionState += _OnRemoteConnectionState;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            var networkManager = InstanceFinder.NetworkManager;
            if (!networkManager) 
                return;

            if (!networkManager.ServerManager) 
                return;
            
            networkManager.ServerManager.OnServerConnectionState -= _OnServerConnectionState;
            networkManager.ServerManager.OnRemoteConnectionState -= _OnRemoteConnectionState;
            if (networkManager.ServerManager.Objects != null)
                networkManager.ServerManager.Objects.OnPreDestroyClientObjects -= _OnPreDestroyClientObjects;
            }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            if (!IsServerSide)
                InitializeTickSystem();
            _allPlayers.OnChange += _OnPlayersChanged;
            var playerName = PlayerPrefs.GetString("TUA.PlayerName", string.Empty);
            
            if (string.IsNullOrEmpty(playerName))
            {
                var random = new System.Random();
                var playerNumber = random.Next(100, 1000);
                playerName = $"Player_{playerNumber}";
            }
            if (!string.IsNullOrEmpty(playerName))
                Client_RequestJoin(playerName);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            CleanupTickSystem();
            _allPlayers.OnChange -= _OnPlayersChanged;
        }
        #endregion

        #region Public Methods
        [ServerRpc(RequireOwnership = false)]
        private void RpcClient_RequestSpectatorTarget(int targetUuidHigh, int targetUuidLow, NetworkConnection conn = null)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("RpcClient_RequestSpectatorTarget can only be called on server side");

            if (conn == null || !_connectionToPlayer.TryGetValue(conn, out var player))
                return;

            var targetUuid = new Uuid(targetUuidHigh, targetUuidLow);
            Server_SetSpectatorTargetInternal(player, targetUuid);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RpcClient_RequestJoin(string uuidString, string username, NetworkConnection conn = null)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("RpcClient_RequestJoin can only be called on server side");

            if (conn == null || !conn.IsValid)
            {
                Debug.LogWarning("[GameWorld] Invalid connection in join request");
                return;
            }

            if (!Uuid.TryParse(uuidString, out var playerUuid))
            {
                Debug.LogWarning($"[GameWorld] Invalid UUID string from connection {conn.ClientId}: {uuidString}");
                return;
            }

            if (string.IsNullOrEmpty(username) || username.Length > 50)
            {
                Debug.LogWarning($"[GameWorld] Invalid username from connection {conn.ClientId}: {username}");
                return;
            }

            var expectedUuid = GenerateUuidFromString(username);
            if (playerUuid != expectedUuid)
            {
                Debug.LogWarning($"[GameWorld] UUID mismatch for username '{username}' from connection {conn.ClientId}. Expected: {expectedUuid}, Got: {playerUuid}");
                playerUuid = expectedUuid;
            }

            var existingPlayer = _allPlayers.FirstOrDefault(player => player != null && player.Uuid == playerUuid);
            GamePlayer gamePlayer;

            if (existingPlayer != null)
            {
                existingPlayer.Name = username;
                existingPlayer.IsOnline = true;
                gamePlayer = existingPlayer;
            }
            else
                gamePlayer = new GamePlayer(playerUuid, username);
            Server_AddPlayerInternal(conn, gamePlayer);
        }

        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_PlayerTeamChanged(Uuid playerUuid, string teamName)
        {
            foreach (var gp in _allPlayers.Where(gp => gp != null && gp.Uuid == playerUuid))
            {
                gp.SetTeamName(teamName);
                return;
            }
        }
        #endregion

        #region Private Methods
        private void _OnServerConnectionState(ServerConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    if (!gameObject.activeSelf)
                        gameObject.SetActive(true);
                    InitializeTickSystem();
                    if (!_gameMode)
                    {
                        _gameMode = GetConfiguredGameMode();
                        if (!_gameMode)
                            Debug.LogError($"[GameWorld] Could not resolve a GameMode. Assign `gameModes` on GameWorld and set `currentGameModeId` to match one of them.");
                        else
                        {
                            if (_gameSettings != null)
                                _gameMode.InternalSetGameSettings(_gameSettings);
                            _gameMode.InternalOnWorldStart(this);
                            Server_SetGameModeRunning(true);
                        }
                    }
                    break;
                case LocalConnectionState.Stopped:
                    Server_SetGameModeRunning(false);
                    _gameMode?.InternalOnWorldEnd(this);
                    _gameMode = null;
                    CleanupTickSystem();
                    break;
            }
        }

        private void _OnRemoteConnectionState(object connection, RemoteConnectionStateArgs args)
        {
            if (!IsServerSide)
                return;

            switch (args.ConnectionState)
            {
                case RemoteConnectionState.Started:
                    break;
                case RemoteConnectionState.Stopped:
                    if (connection != null && _connectionToPlayer.TryGetValue(connection, out var player))
                    {
                        Server_TransferOwnershipToServer(player);
                        Server_SetPlayerOnline(player, false);
                        _connectionToPlayer.Remove(connection);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void _OnPreDestroyClientObjects(object connection)
        {
            if (!IsServerSide)
                return;

            if (connection is not NetworkConnection { IsValid: true } netConn) 
                return;
            
            var objectsToTransfer = (from nob in netConn.Objects where nob && nob.IsSpawned let entity = nob.GetComponent<Entity>() where entity select nob).ToList();
            foreach (var nob in objectsToTransfer)
                nob.RemoveOwnership();
        }
        
        private void _OnPlayersChanged(SyncListOperation op, int index, GamePlayer oldItem, GamePlayer newItem, bool asServer)
        {
        }
        
        private void Server_AddPlayerInternal(object connection, GamePlayer player)
        {
            if (player == null || connection is not NetworkConnection { IsValid: true } netConn)
                return;
            
            object oldConnection = null;
            for (var i = 0; i < _allPlayers.Count; i++)
            {
                if (_allPlayers[i] == null || _allPlayers[i].Uuid != player.Uuid) 
                    continue;
                
                foreach (var kvp in _connectionToPlayer.Where(kvp => kvp.Value != null && kvp.Value.Uuid == player.Uuid))
                {
                    oldConnection = kvp.Key;
                    break;
                }
                
                player.IsOnline = true;
                _allPlayers[i] = player;
                _connectionToPlayer[connection] = player;
                if (oldConnection != null && oldConnection != connection)
                    _connectionToPlayer.Remove(oldConnection);
                Server_RestorePlayerOwnershipInternal(player, connection);
                return;
            }
            player.IsOnline = true;
            _allPlayers.Add(player);
            _connectionToPlayer[connection] = player;
            
            Server_SendAllPlayerDataSnapshotsToNewPlayer(player, netConn);
            _gameMode?.InternalOnPlayerJoined(player, this);
        }
        
        private void Server_RemovePlayerInternal(object connection)
        {
            if (connection == null || !(connection is NetworkConnection netConn) || !netConn.IsValid)
                return;
            
            if (!_connectionToPlayer.TryGetValue(connection, out var player))
                return;
            
            Server_SetPlayerOnline(player, false);
            Server_RemovePlayerData(player);
            _connectionToPlayer.Remove(connection);
        }
        
        private void Server_SetPlayerOnlineInternal(GamePlayer player, bool isOnline)
        {
            if (player == null)
                return;
            
            for (var i = 0; i < _allPlayers.Count; i++)
            {
                if (_allPlayers[i] == null || _allPlayers[i].Uuid != player.Uuid) 
                    continue;
                
                player.IsOnline = isOnline;
                _allPlayers[i] = player;
                return;
            }
        }
        
        private void Server_SetSpectatorTargetInternal(GamePlayer player, Uuid targetEntityUuid)
        {
            if (player == null)
                return;
            
            for (var i = 0; i < _allPlayers.Count; i++)
            {
                if (_allPlayers[i] == null || _allPlayers[i].Uuid != player.Uuid) 
                    continue;
                
                player.SpectatorTargetUuid = targetEntityUuid;
                _allPlayers[i] = player;
                return;
            }
        }
        
        private void Client_RequestJoinInternal(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                Debug.LogWarning("[GameWorld] Cannot join with empty username");
                return;
            }
            
            if (!IsSpawned)
            {
                Debug.LogWarning("[GameWorld] NetworkObject is not spawned yet. Join request will be queued.");
                StartCoroutine(DelayedJoinRequest(username));
                return;
            }
            
            var playerUuid = GenerateUuidFromString(username);
            var uuidString = playerUuid.ToString();
            _localPlayerUuid = playerUuid;
            RpcClient_RequestJoin(uuidString, username);
        }
        
        private IEnumerator DelayedJoinRequest(string username)
        {
            yield return new WaitForSeconds(0.5f);
            if (IsClientSide && !IsServerSide && IsSpawned)
            {
                if (!string.IsNullOrEmpty(username))
                {
                    var playerUuid = GenerateUuidFromString(username);
                    var uuidString = playerUuid.ToString();
                    _localPlayerUuid = playerUuid;
                    RpcClient_RequestJoin(uuidString, username);
                }
            }
            else
                Debug.LogWarning($"[GameWorld] Still not ready after delay. IsClientSide: {IsClientSide}, IsServerSide: {IsServerSide}, IsSpawned: {IsSpawned}");
        }

        private void Server_SetPlayerTeamInternal(GamePlayer player, string teamName)
        {
            if (player == null)
                return;
            
            for (var i = 0; i < _allPlayers.Count; i++)
            {
                if (_allPlayers[i] == null || _allPlayers[i].Uuid != player.Uuid) 
                    continue;
                
                player.SetTeamName(teamName);
                _allPlayers[i] = player;
                RpcClient_PlayerTeamChanged(player.Uuid, teamName);
                return;
            }
        }
        #endregion
    }
}
