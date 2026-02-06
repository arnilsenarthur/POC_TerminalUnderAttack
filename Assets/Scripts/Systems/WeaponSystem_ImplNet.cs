using FishNet.Object;
using FishNet.Transporting;
using TUA.Core;
using TUA.Core.Interfaces;
using TUA.Misc;
using UnityEngine;

namespace TUA.Systems
{
    public partial class WeaponSystem
    {
        [ObserversRpc]
        private void RpcClient_SpawnShotEffects(Uuid shooterEntityUuid, Vector3 weaponExit, Vector3 hitPoint, bool actuallyHit)
        {
            if (!GameWorld.Instance) 
                return;
            
            var entity = GameWorld.Instance.GetEntityByUuid(shooterEntityUuid);
            if (entity is IWeaponUser weaponUser)
                SpawnShotEffectsLocally(weaponUser, weaponExit, hitPoint, actuallyHit);
        }

        [ObserversRpc(ExcludeServer = false, BufferLast = true)]
        private void RpcClient_PlayReloadAnimation(Uuid shooterUuid, string reloadClipName, float reloadClipLength, float reloadTime, Channel channel = Channel.Reliable)
        {
            if (!GameWorld.Instance)
                return;
            
            var entity = GameWorld.Instance.GetEntityByUuid(shooterUuid);
            if (entity is IWeaponUser weaponUser)
                weaponUser.Client_PlayReloadAnimation(reloadClipName, reloadClipLength, reloadTime);
        }

        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_SpawnDeathEffects(Vector3 position, Vector3 normal)
        {
            SpawnDeathEffectsLocally(position, normal);
        }
    }
}
