using TUA.Misc;
using UnityEngine;

namespace TUA.Core
{
    public abstract partial class Entity
    {
        #region Properties
        public Uuid EntityUuid => GetEntityUuidNet();
        public Uuid OwnerPlayerUuid => GetOwnerPlayerUuidNet();

        public GamePlayer GamePlayer
        {
            get
            {
                if (!GameWorld.Instance)
                    return null;
                
                var ownerUuid = OwnerPlayerUuid;
                if (!ownerUuid.IsValid)
                    return null;
                
                foreach (var player in GameWorld.Instance.AllPlayers)
                {
                    if (player != null && player.Uuid == ownerUuid)
                        return player;
                }
                return null;
            }
        }
        public bool IsLocalOwned
        {
            get
            {
                if (!GameWorld.Instance)
                    return false;
                
                var localPlayer = GameWorld.Instance.LocalGamePlayer;
                if (localPlayer == null)
                    return false;
                
                var ownerUuid = OwnerPlayerUuid;
                return ownerUuid.IsValid && ownerUuid == localPlayer.Uuid;
            }
        }
        public bool IsOwned => OwnerPlayerUuid.IsValid;
        public new bool IsSpawned { get; private set; }
        #endregion

        #region Methods
        public void Server_SetOwnerPlayerUuid(Uuid playerUuid)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetOwnerPlayerUuid can only be called on server side");
            _Server_SetOwnerPlayerUuidInternal(playerUuid);
        }

        public void Server_SetEntityUuid(Uuid uuid)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetEntityUuid can only be called on server side");
            _Server_SetEntityUuidInternal(uuid);
        }

        public virtual void GetCameraView(out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            position = transform.position;
            rotation = transform.rotation;
            fieldOfView = 60f;
        }
        #endregion
    }
}
