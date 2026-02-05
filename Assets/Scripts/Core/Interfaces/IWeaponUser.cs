using System;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core.Interfaces
{
    public interface IWeaponUser : IInventoryHolder
    {
        WeaponState WeaponState { get; }
        bool IsLocalOwned { get; }
        bool IsServerSide { get; }
        Vector3 Velocity { get; }
        Uuid UserUuid { get; }
        bool IsValidAndSpawned { get; }
        Vector3 GetCurrentWeaponExit();
        void Client_Shoot(Vector3 origin, Vector3 direction);
        void Client_Reload();
        void Server_SetReloadProgress(float progress);
        event Action<IWeaponUser, Vector3, Vector3> OnRequestToShootEvent;
        event Action<IWeaponUser> OnRequestToReloadEvent;
        void Server_ApplyRecoil();
        float Server_GetRecoilSpreadMultiplier();
        float GetScopeBlend();
        void Client_PlayReloadAnimation(string reloadClipName, float reloadClipLength, float reloadTime);
    }
}
