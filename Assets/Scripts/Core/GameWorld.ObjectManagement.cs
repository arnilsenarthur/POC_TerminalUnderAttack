using System;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Server Object Spawning
        public GameObject Server_SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, GamePlayer owner = null, Transform parent = null)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SpawnObject can only be called on server side");
            
            return Server_SpawnObjectInternal(prefab, position, rotation, owner, parent);
        }

        public T Server_SpawnObject<T>(GameObject prefab, Vector3 position, Quaternion rotation, GamePlayer owner = null, Transform parent = null) where T : Component
        {
            var instance = Server_SpawnObject(prefab, position, rotation, owner, parent);
            return !instance ? null : instance.GetComponent<T>();
        }

        public bool Server_DespawnObject(GameObject instance, object despawnType = null)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_DespawnObject can only be called on server side");
            
            return Server_DespawnObjectInternal(instance, despawnType);
        }

        #endregion

        #region Server Ownership Management

        public bool Server_SetOwnership(GameObject instance, GamePlayer owner, bool includeNested = false)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetOwnership can only be called on server side");
            
            return Server_SetOwnershipInternal(instance, owner, includeNested);
        }

        public void Server_TransferOwnershipToServer(GamePlayer player)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_TransferOwnershipToServer can only be called on server side");
            
            Server_TransferOwnershipToServerInternal(player);
        }

        public void Server_RestorePlayerOwnership(GamePlayer player, object newConnection)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_RestorePlayerOwnership can only be called on server side");
            
            Server_RestorePlayerOwnershipInternal(player, newConnection);
        }

        public void Server_UpdateEntityOwnerUuid(object networkObject)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_UpdateEntityOwnerUuid can only be called on server side");
            
            Server_UpdateEntityOwnerUuidInternal(networkObject);
        }

        #endregion

        #region Object Queries by Player
        public GameObject[] GetObjectsOwnedByPlayer(GamePlayer gamePlayer)
        {
            if (gamePlayer == null)
                return Array.Empty<GameObject>();

            var entities = GetEntitiesOwnedByPlayer(gamePlayer);
            var gameObjects = new System.Collections.Generic.List<GameObject>();

            foreach (var entity in entities)
            {
                if (entity && entity.gameObject)
                {
                    gameObjects.Add(entity.gameObject);
                }
            }

            return gameObjects.ToArray();
        }

        public T[] GetObjectsOwnedByPlayer<T>(GamePlayer gamePlayer) where T : Component
        {
            if (gamePlayer == null)
                return Array.Empty<T>();

            var entities = GetEntitiesOwnedByPlayer<Entity>(gamePlayer);
            var components = new System.Collections.Generic.List<T>();

            foreach (var entity in entities)
            {
                if (entity)
                {
                    var component = entity.GetComponent<T>();
                    if (component)
                    {
                        components.Add(component);
                    }
                }
            }

            return components.ToArray();
        }

        public T GetObjectOwnedByPlayer<T>(GamePlayer gamePlayer) where T : Component
        {
            if (gamePlayer == null)
                return null;

            var entity = GetEntityOwnedByPlayer(gamePlayer);
            if (entity)
            {
                return entity.GetComponent<T>();
            }

            return null;
        }

        #endregion

        #region Player Queries from Objects

        public GamePlayer GetPlayerFromObject<T>(T component) where T : Component
        {
            if (!component)
                return null;

            if (component is Entity entity)
            {
                var gamePlayer = entity.GamePlayer;
                if (gamePlayer != null)
                    return gamePlayer;
            }

            return GetPlayerFromObjectInternal(component);
        }

        public GamePlayer GetPlayerFromObject(GameObject go)
        {
            if (!go)
                return null;

            var entity = go.GetComponent<Entity>();
            if (entity)
            {
                var gamePlayer = entity.GamePlayer;
                if (gamePlayer != null)
                    return gamePlayer;
            }

            return GetPlayerFromObjectInternal(go);
        }

        #endregion

        #region Private Helpers

        private void SetEntityOwnerUuid(GameObject obj, GamePlayer owner, bool includeChildren)
        {
            var ownerUuid = owner is { Uuid: { IsValid: true } } ? owner.Uuid : Uuid.Empty;

            if (includeChildren)
            {
                var entities = obj.GetComponentsInChildren<Entity>(true);
                foreach (var entity in entities)
                {
                    if (!entity) 
                        continue;
                    
                    var oldOwnerUuid = entity.OwnerPlayerUuid;
                    entity.Server_SetOwnerPlayerUuid(ownerUuid);
                    UpdateEntityOwner(entity, oldOwnerUuid, ownerUuid);
                }
            }
            else
            {
                var entity = obj.GetComponent<Entity>();
                if (!entity) 
                    return;
                
                var oldOwnerUuid = entity.OwnerPlayerUuid;
                entity.Server_SetOwnerPlayerUuid(ownerUuid);
                UpdateEntityOwner(entity, oldOwnerUuid, ownerUuid);
            }
        }

        #endregion
    }
}
