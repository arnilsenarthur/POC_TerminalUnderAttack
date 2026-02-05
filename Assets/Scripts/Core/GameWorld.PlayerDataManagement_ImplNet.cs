using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Player Data Network Synchronization
        private readonly Dictionary<Uuid, IPlayerData> _clientPlayerDataSnapshots = new();
        
        private void Server_SendPlayerDataSnapshot(GamePlayer targetPlayer, GamePlayer requestingPlayer, NetworkConnection conn)
        {
            if (!IsServerSide || targetPlayer == null || requestingPlayer == null || conn == null || !conn.IsValid)
                return;

            var snapshot = Server_GetPlayerDataSnapshot(targetPlayer, requestingPlayer);
            if (snapshot == null)
                return;

            var writer = WriterPool.Retrieve(InstanceFinder.NetworkManager);
            try
            {
                var netWriter = new FishNetWriter(writer);
                snapshot.Serialize(netWriter);
                
                var segment = writer.GetArraySegment();
                var serializedData = new byte[segment.Count];
                Array.Copy(segment.Array!, segment.Offset, serializedData, 0, segment.Count);
                
                var typeId = PlayerDataTypeRegistry.GetTypeId(snapshot.GetType()) ?? "default";
                
                RpcClient_PlayerDataSnapshot(conn, targetPlayer.Uuid, typeId, serializedData);
            }
            finally
            {
                writer.Store();
            }
        }

        private void Server_BroadcastPlayerDataSnapshot(GamePlayer targetPlayer)
        {
            if (!IsServerSide || targetPlayer == null)
                return;

            foreach (var player in AllPlayers)
            {
                if (player is not { IsOnline: true } || player.IsSpectator)
                    continue;

                var conn = Server_GetConnectionForPlayer(player);
                if (conn != null && conn.IsValid)
                {
                    Server_SendPlayerDataSnapshot(targetPlayer, player, conn);
                }
            }
        }
        
        private void Server_SendAllPlayerDataSnapshotsToNewPlayer(GamePlayer newPlayer, NetworkConnection conn)
        {
            if (!IsServerSide || newPlayer == null || conn == null || !conn.IsValid)
                return;

            foreach (var playerDataType in PlayerDataTypeRegistry.GetAllRegisteredTypes())
            {
                foreach (var otherPlayer in AllPlayers)
                {
                    if (otherPlayer == null || !otherPlayer.IsOnline || otherPlayer.Uuid == newPlayer.Uuid)
                        continue;

                    var playerData = Server_GetPlayerData(otherPlayer);
                    if (playerData == null || playerData.GetType() != playerDataType) continue;
                    Server_SendPlayerDataSnapshot(otherPlayer, newPlayer, conn);
                    break;
                }
            }
        }
        
        private NetworkConnection Server_GetConnectionForPlayer(GamePlayer player)
        {
            if (!IsServerSide || player == null)
                return null;

            foreach (var kvp in _connectionToPlayer.Where(kvp => kvp.Value != null && kvp.Value.Uuid == player.Uuid))
            {
                if (kvp.Key is NetworkConnection { IsValid: true } conn)
                    return conn;
            }
            return null;
        }
        
        [TargetRpc]
        private void RpcClient_PlayerDataSnapshot(NetworkConnection conn, Uuid playerUuid, string typeId, byte[] serializedData)
        {
            if (string.IsNullOrWhiteSpace(typeId) || serializedData == null || serializedData.Length == 0)
                return;

            var playerDataType = PlayerDataTypeRegistry.GetType(typeId);
            if (playerDataType == null)
            {
                Debug.LogWarning($"[GameWorld] Unknown PlayerData type ID: {typeId}");
                return;
            }

            if (!(Activator.CreateInstance(playerDataType) is IPlayerData instance))
            {
                Debug.LogError($"[GameWorld] Failed to create instance of PlayerData type: {playerDataType.FullName}");
                return;
            }

            var reader = ReaderPool.Retrieve(serializedData, InstanceFinder.NetworkManager);
            try
            {
                var netReader = new FishNetReader(reader);
                var snapshot = instance.Deserialize(netReader);
                if (snapshot != null)
                {
                    _clientPlayerDataSnapshots[playerUuid] = snapshot;
                }
            }
            finally
            {
                reader.Store();
            }
        }
        #endregion
    }
}
