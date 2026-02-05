using FishNet.Connection;
using FishNet.Object.Synchronizing;
using TUA.Core.Interfaces;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core
{
    public abstract partial class Entity : NetBehaviour, IPovHandler
    {
        private readonly SyncVar<Uuid> _entityUuid = new();
        private readonly SyncVar<Uuid> _ownerPlayerUuid = new();

        protected Uuid GetEntityUuidNet() => _entityUuid.Value;
        protected Uuid GetOwnerPlayerUuidNet() => _ownerPlayerUuid.Value;

        public override void OnStartServer()
        {
            base.OnStartServer();
            IsSpawned = true;
            if (!_entityUuid.Value.IsValid)
            {
                var newUuid = Uuid.New;
                _entityUuid.Value = newUuid;
            }
            
            if (!_entityUuid.Value.IsValid)
            {
                var entityType = GetType().Name;
                Debug.LogError($"[Entity] Failed to generate valid UUID for {entityType} '{gameObject.name}' on server!");
                return;
            }
            
            if (GameWorld.Instance)
            {
                GameWorld.Instance.RegisterEntity(this);
                var nob = GetComponent<FishNet.Object.NetworkObject>();
                if (nob)
                    GameWorld.Instance.Server_UpdateEntityOwnerUuid(nob);
            }
            else
            {
                var entityType = GetType().Name;
                Debug.LogWarning($"[Entity] GameWorld.Instance is null when starting server for {entityType} '{gameObject.name}'");
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            IsSpawned = true;
            
            _entityUuid.OnChange += (_, next, asServer) =>
            {
                if (!asServer && next.IsValid && GameWorld.Instance != null)
                    GameWorld.Instance.RegisterEntity(this);
            };
            
            if (GameWorld.Instance && _entityUuid.Value.IsValid)
                GameWorld.Instance.RegisterEntity(this);
            else if (!GameWorld.Instance)
            {
                var entityType = GetType().Name;
                Debug.LogWarning($"[Entity] GameWorld.Instance is null when starting client for {entityType} '{gameObject.name}'");
            }
        }
        
        public override void OnStopServer()
        {
            if (GameWorld.Instance)
                GameWorld.Instance.UnregisterEntity(this);
            IsSpawned = false;
            base.OnStopServer();
        }
        
        public override void OnStopClient()
        {
            if (GameWorld.Instance)
                GameWorld.Instance.UnregisterEntity(this);
            IsSpawned = false;
            base.OnStopClient();
        }
        
        public override void OnOwnershipServer(NetworkConnection prevOwner)
        {
            base.OnOwnershipServer(prevOwner);
            var oldOwnerUuid = _ownerPlayerUuid.Value;
            if (GameWorld.Instance == null) 
                return;
            
            var nob = GetComponent<FishNet.Object.NetworkObject>();
            if (nob)
                GameWorld.Instance.Server_UpdateEntityOwnerUuid(nob);
            
            var newOwnerUuid = _ownerPlayerUuid.Value;
            GameWorld.Instance.UpdateEntityOwner(this, oldOwnerUuid, newOwnerUuid);
        }
        
        protected void _Server_SetOwnerPlayerUuidInternal(Uuid playerUuid)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("_Server_SetOwnerPlayerUuidInternal can only be called on server side");
            _ownerPlayerUuid.Value = playerUuid;
        }
        
        protected void _Server_SetEntityUuidInternal(Uuid uuid)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("_Server_SetEntityUuidInternal can only be called on server side");
            _entityUuid.Value = uuid;
        }
    }
}
