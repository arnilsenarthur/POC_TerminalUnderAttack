using System;
using System.Collections.Generic;
using System.Linq;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Fields & Properties
        private readonly Dictionary<Uuid, Entity> _allEntities = new();
        private readonly Dictionary<Uuid, List<Entity>> _playerEntities = new();

        public IReadOnlyDictionary<Uuid, Entity> AllEntities => _allEntities;
        #endregion
        
        #region Events
        public static event Action<Entity> OnEntitySpawnEvent;
        public static event Action<Entity> OnEntityDespawnEvent;
        #endregion

        #region Entity Registration & Management

        public void RegisterEntity(Entity entity)
        {
            if (!entity)
            {
                Debug.LogWarning("[GameWorld] RegisterEntity called with null entity");
                return;
            }

            if (!entity.EntityUuid.IsValid)
            {
                Debug.LogWarning($"[GameWorld] RegisterEntity called with invalid UUID for entity '{entity.gameObject.name}' (Type: {entity.GetType().Name})");
                return;
            }

            var entityType = entity.GetType().Name;
            var entityName = entity.gameObject.name;

            if (_allEntities.ContainsKey(entity.EntityUuid))
            {
                if (_allEntities[entity.EntityUuid] == entity)
                {
                    Debug.LogWarning($"[GameWorld] Entity already registered: {entityType} '{entityName}' (UUID: {entity.EntityUuid})");
                    return;
                }

                var existingEntity = _allEntities[entity.EntityUuid];
                Debug.LogWarning($"[GameWorld] Entity with UUID {entity.EntityUuid} already registered with different instance. " +
                    $"Existing: {existingEntity?.GetType().Name} '{existingEntity?.gameObject.name}', " +
                    $"New: {entityType} '{entityName}'. Replacing.");
            }

            _allEntities[entity.EntityUuid] = entity;
            OnEntitySpawnEvent?.Invoke(entity);

            var ownerUuid = entity.OwnerPlayerUuid;
            if (!ownerUuid.IsValid) 
                return;
            
            if (!_playerEntities.ContainsKey(ownerUuid))
            {
                _playerEntities[ownerUuid] = new List<Entity>();
            }
            if (!_playerEntities[ownerUuid].Contains(entity))
            {
                _playerEntities[ownerUuid].Add(entity);
            }
        }

        public void UnregisterEntity(Entity entity)
        {
            if (!entity)
            {
                Debug.LogWarning("[GameWorld] UnregisterEntity called with null entity");
                return;
            }

            if (!entity.EntityUuid.IsValid)
            {
                Debug.LogWarning($"[GameWorld] UnregisterEntity called with invalid UUID for entity '{entity.gameObject.name}' (Type: {entity.GetType().Name})");
                return;
            }

            var entityType = entity.GetType().Name;
            var entityName = entity.gameObject.name;

            if (_allEntities.TryGetValue(entity.EntityUuid, out var registeredEntity) && registeredEntity == entity)
            {
                _allEntities.Remove(entity.EntityUuid);
                OnEntityDespawnEvent?.Invoke(entity);
            }
            else
            {
                Debug.LogWarning($"[GameWorld] Attempted to unregister entity {entityType} '{entityName}' (UUID: {entity.EntityUuid}) but it was not found in registry");
            }

            var ownerUuid = entity.OwnerPlayerUuid;
            if (!ownerUuid.IsValid || !_playerEntities.TryGetValue(ownerUuid, out var playerEntityList)) 
                return;
            
            playerEntityList.Remove(entity);
            if (playerEntityList.Count == 0)
            {
                _playerEntities.Remove(ownerUuid);
            }
        }

        public void UpdateEntityOwner(Entity entity, Uuid oldOwnerUuid, Uuid newOwnerUuid)
        {
            if (entity == null)
            {
                Debug.LogWarning("[GameWorld] UpdateEntityOwner called with null entity");
                return;
            }
            
            if (oldOwnerUuid.IsValid && _playerEntities.TryGetValue(oldOwnerUuid, out var oldList))
            {
                oldList.Remove(entity);
                if (oldList.Count == 0)
                {
                    _playerEntities.Remove(oldOwnerUuid);
                }
            }

            if (!newOwnerUuid.IsValid) 
                return;
            
            if (!_playerEntities.ContainsKey(newOwnerUuid))
                _playerEntities[newOwnerUuid] = new List<Entity>();
            
            if (!_playerEntities[newOwnerUuid].Contains(entity))
                _playerEntities[newOwnerUuid].Add(entity);
        }

        #endregion

        #region Entity Queries by UUID

        public Entity GetEntityByUuid(Uuid uuid)
        {
            if (!uuid.IsValid)
                return null;

            if (_allEntities.TryGetValue(uuid, out var entity) && entity && entity.IsSpawned)
            {
                return entity;
            }

            return null;
        }

        public T GetEntityByUuid<T>(Uuid uuid) where T : Entity
        {
            var entity = GetEntityByUuid(uuid);
            return entity as T;
        }

        #endregion

        #region Entity Queries by Player

        public IReadOnlyList<Entity> GetEntitiesOwnedByPlayer(GamePlayer gamePlayer)
        {
            if (gamePlayer == null || !gamePlayer.Uuid.IsValid)
                return new List<Entity>();

            if (_playerEntities.TryGetValue(gamePlayer.Uuid, out var entities))
            {
                return entities.Where(e => e && e.IsSpawned).ToList();
            }

            return new List<Entity>();
        }

        public IReadOnlyList<T> GetEntitiesOwnedByPlayer<T>(GamePlayer gamePlayer) where T : Entity
        {
            if (gamePlayer == null || !gamePlayer.Uuid.IsValid)
                return new List<T>();

            if (_playerEntities.TryGetValue(gamePlayer.Uuid, out var entities))
            {
                return entities.Where(e => e && e.IsSpawned && e is T).Cast<T>().ToList();
            }

            return new List<T>();
        }

        public Entity GetEntityOwnedByPlayer(GamePlayer gamePlayer)
        {
            if (gamePlayer == null || !gamePlayer.Uuid.IsValid)
                return null;

            return _playerEntities.TryGetValue(gamePlayer.Uuid, out var entities) ? entities.FirstOrDefault(e => e && e.IsSpawned) : null;
        }

        public T GetEntityOwnedByPlayer<T>(GamePlayer gamePlayer) where T : Entity
        {
            if (gamePlayer == null || !gamePlayer.Uuid.IsValid)
                return null;

            if (_playerEntities.TryGetValue(gamePlayer.Uuid, out var entities))
            {
                return entities.FirstOrDefault(e => e && e.IsSpawned && e is T) as T;
            }

            return null;
        }

        #endregion

        #region Entity Queries by Type

        public IReadOnlyList<T> GetEntities<T>() where T : Entity
        {
            var allValues = _allEntities.Values.ToList();
            
            var filtered = allValues.Where(e => 
            {
                if (!e)
                    return false;
  
                if (!e.IsSpawned)
                    return false;
 
                return e is T;
            }).Cast<T>().ToList();
            
            return filtered;
        }

        #endregion
    }
}
