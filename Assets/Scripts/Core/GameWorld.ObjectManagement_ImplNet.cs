using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Private Methods
        private GameObject Server_SpawnObjectInternal(GameObject prefab, Vector3 position, Quaternion rotation, GamePlayer owner = null, Transform parent = null)
        {
            if (!prefab)
            {
                Debug.LogWarning("[GameWorld] Server_SpawnObjectInternal called with null prefab");
                return null;
            }

            object ownerConnection = null;
            if (owner != null && !TryGetConnection(owner, out ownerConnection))
            {
                Debug.LogWarning($"[GameWorld] Server_SpawnObjectInternal: Failed to get connection for owner {owner.Name} (UUID: {owner.Uuid})");
                return null;
            }
            var instance = !parent
                ? Instantiate(prefab, position, rotation)
                : Instantiate(prefab, position, rotation, parent);
            if (!instance.GetComponent<NetworkObject>())
            {
                Debug.LogError($"[GameWorld] Server_SpawnObjectInternal: Prefab '{prefab.name}' does not have a NetworkObject component. Destroying instance.");
                Destroy(instance);
                return null;
            }

            var networkManager = InstanceFinder.NetworkManager;
            if (networkManager && networkManager.ServerManager)
                networkManager.ServerManager.Spawn(instance, ownerConnection as NetworkConnection);
            else
                Debug.LogError($"[GameWorld] Server_SpawnObjectInternal: NetworkManager or ServerManager is null. Cannot spawn '{instance.name}'");

            if (owner != null)
                SetEntityOwnerUuid(instance, owner, true);

            return instance;
        }

        private bool Server_DespawnObjectInternal(GameObject instance, object despawnType)
        {
            if (!instance)
            {
                Debug.LogWarning("[GameWorld] Server_DespawnObjectInternal called with null instance");
                return false;
            }
            var despawnTypeEnum = despawnType as DespawnType? ?? DespawnType.Destroy;
            var networkObject = instance.GetComponent<NetworkObject>();
            if (!networkObject)
            {
                Debug.LogWarning($"[GameWorld] Server_DespawnObjectInternal: GameObject '{instance.name}' does not have a NetworkObject component");
                return false;
            }
            var networkManager = InstanceFinder.NetworkManager;
            if (networkManager && networkManager.ServerManager)
                networkManager.ServerManager.Despawn(networkObject, despawnTypeEnum);
            else
            {
                Debug.LogError($"[GameWorld] Server_DespawnObjectInternal: NetworkManager or ServerManager is null. Cannot despawn '{instance.name}'");
                return false;
            }
            return true;
        }

        private bool Server_SetOwnershipInternal(GameObject instance, GamePlayer owner, bool includeNested)
        {
            if (!instance)
            {
                Debug.LogWarning("[GameWorld] Server_SetOwnershipInternal called with null instance");
                return false;
            }
            object ownerConnection = null;
            if (owner != null && !TryGetConnection(owner, out ownerConnection))
            {
                Debug.LogWarning($"[GameWorld] Server_SetOwnershipInternal: Failed to get connection for owner {owner.Name} (UUID: {owner.Uuid})");
                return false;
            }
            if (includeNested)
            {
                var networkObjects = instance.GetComponentsInChildren<NetworkObject>(true);
                foreach (var no in networkObjects)
                {
                    if (ownerConnection == null)
                        no.RemoveOwnership();
                    else
                        no.GiveOwnership(ownerConnection as NetworkConnection);
                }
            }
            else
            {
                var networkObject = instance.GetComponent<NetworkObject>();
                if (!networkObject)
                {
                    Debug.LogWarning($"[GameWorld] Server_SetOwnershipInternal: GameObject '{instance.name}' does not have a NetworkObject component");
                    return false;
                }
                if (ownerConnection == null)
                    networkObject.RemoveOwnership();
                else
                    networkObject.GiveOwnership(ownerConnection as NetworkConnection);
            }
            return true;
        }

        private void Server_RestorePlayerOwnershipInternal(GamePlayer player, object newConnection)
        {
            if (player == null || !player.Uuid.IsValid || newConnection == null)
            {
                Debug.LogError("[Restore Player Ownership] Invalid player or connection");
                return;
            }
            var entities = GetEntitiesOwnedByPlayer(player);
            if (entities == null || entities.Count == 0)
            {
                Debug.LogWarning($"[Restore Player Ownership] No entities found for player {player.Name} (UUID: {player.Uuid})");
                return;
            }
            var objectsToRestore = new System.Collections.Generic.List<NetworkObject>();
            foreach (var entity in entities)
            {
                if (!entity || !((NetBehaviour)entity).IsSpawned)
                    continue;

                var networkObject = entity.NetworkObject;
                if (networkObject)
                    objectsToRestore.Add(networkObject);
            }
            if (objectsToRestore.Count == 0)
            {
                Debug.LogError($"[Restore Player Ownership] No NetworkObjects found for player {player.Name} (UUID: {player.Uuid}) to restore ownership");
                return;
            }
            foreach (var networkObject in objectsToRestore)
            {
                if (networkObject && networkObject.IsSpawned)
                    networkObject.GiveOwnership(newConnection as NetworkConnection);
            }
        }

        private void Server_UpdateEntityOwnerUuidInternal(object networkObject)
        {
            if (networkObject == null || !((networkObject as NetworkObject)?.IsSpawned ?? false))
            {
                if (networkObject == null)
                    Debug.LogWarning("[GameWorld] Server_UpdateEntityOwnerUuidInternal called with null networkObject");
                return;
            }
            var nob = networkObject as NetworkObject;
            if (!nob)
            {
                Debug.LogWarning("[GameWorld] Server_UpdateEntityOwnerUuidInternal: networkObject is not a NetworkObject");
                return;
            }
            var entity = nob.GetComponent<Entity>();
            if (!entity)
            {
                Debug.LogWarning($"[GameWorld] Server_UpdateEntityOwnerUuidInternal: NetworkObject '{nob.gameObject.name}' does not have an Entity component");
                return;
            }
            var owner = nob.Owner;
            if (owner == null || !owner.IsValid)
            {
                entity.Server_SetOwnerPlayerUuid(Uuid.Empty);
                return;
            }
            if (_connectionToPlayer.TryGetValue(owner, out var gamePlayer) && gamePlayer != null)
                entity.Server_SetOwnerPlayerUuid(gamePlayer.Uuid);
            else
            {
                var oldOwner = entity.OwnerPlayerUuid;
                entity.Server_SetOwnerPlayerUuid(Uuid.Empty);
                Debug.LogWarning($"[GameWorld] Updated entity '{entity.gameObject.name}' owner UUID: {oldOwner} -> Empty (connection not found in _connectionToPlayer)");
            }
        }

        private void Server_TransferOwnershipToServerInternal(GamePlayer player)
        {
            if (player == null || !player.Uuid.IsValid)
            {
                Debug.LogWarning("[GameWorld] Server_TransferOwnershipToServerInternal called with invalid player");
                return;
            }
            var entities = GetEntitiesOwnedByPlayer(player);
            if (entities == null || entities.Count == 0)
            {
                Debug.LogWarning($"[GameWorld] No entities found for player {player.Name} (UUID: {player.Uuid}) to transfer ownership");
                return;
            }

            foreach (var entity in entities)
            {
                if (!entity)
                    continue;
                var networkObject = entity.GetComponent<NetworkObject>();
                if (networkObject && networkObject.IsSpawned)
                    networkObject.RemoveOwnership();
            }
        }

        private bool TryGetConnection(GamePlayer player, out object connection)
        {
            connection = null;
            if (player == null || !player.Uuid.IsValid)
                return false;

            if (IsServerSide)
            {
                foreach (var kvp in _connectionToPlayer.Where(kvp => kvp.Value != null && kvp.Value.Uuid == player.Uuid))
                {
                    connection = kvp.Key;
                    return true;
                }
            }
            else if (IsClientSide)
            {
                var localConnection = InstanceFinder.ClientManager?.Connection;
                if (localConnection == null || !localConnection.IsValid)
                    return false;

                var localGamePlayer = LocalGamePlayer;
                if (localGamePlayer == null || (localGamePlayer != player && localGamePlayer.Uuid != player.Uuid))
                    return false;

                connection = localConnection;
                return true;
            }
            return false;
        }

        private GamePlayer GetPlayerFromObjectInternal<T>(T component) where T : Component
        {
            var networkObject = component.GetComponent<NetworkObject>();
            if (!networkObject)
                return null;

            var connection = networkObject.Owner;
            if (!connection.IsValid)
                return null;

            if (IsServerSide)
            {
                if (_connectionToPlayer.TryGetValue(connection, out var player))
                    return player;
            }
            else if (IsClientSide)
            {
                var localConnection = InstanceFinder.ClientManager?.Connection;
                if (localConnection != null && localConnection.IsValid && connection == localConnection)
                    return LocalGamePlayer;
            }
            return null;
        }

        private GamePlayer GetPlayerFromObjectInternal(GameObject go)
        {
            var networkObject = go.GetComponent<NetworkObject>();
            if (!networkObject)
                return null;

            var connection = networkObject.Owner;
            if (!connection.IsValid)
                return null;

            if (IsServerSide)
            {
                if (_connectionToPlayer.TryGetValue(connection, out var player))
                    return player;
            }
            else if (IsClientSide)
            {
                var localConnection = InstanceFinder.ClientManager?.Connection;
                if (localConnection != null && localConnection.IsValid && connection == localConnection)
                    return LocalGamePlayer;
            }
            return null;
        }
        #endregion
    }
}
