using System.Collections.Generic;
using TUA.Audio;
using TUA.Core;
using TUA.Core.Interfaces;
using TUA.Items;
using TUA.Misc;
using UnityEngine;

namespace TUA.Systems
{
    public partial class WeaponSystem : SingletonNetBehaviour<WeaponSystem>
    {
        #region Serialized Fields
        [Header("Settings")]
        public LayerMask hitLayers = -1;
        [Tooltip("Layer mask for head colliders. Hits on this layer will receive headshot damage multiplier.")]
        public LayerMask headLayer;
        public float defaultDamage = 10f;
        public float defaultRange = 100f;
        [Header("Visual Effects")]
        public BulletTrace bulletTracePrefab;
        public GameObject hitEffectPrefab;
        public GameObject muzzleFlashPrefab;
        [Header("References")]
        public Registry itemRegistry;
        #endregion
        #region Fields
        private readonly HashSet<IWeaponUser> _registeredWeaponUsers = new();
        #endregion
        
        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            GameWorld.OnEntitySpawnEvent += _OnEntitySpawn;
            GameWorld.OnEntityDespawnEvent += _OnEntityDespawn;
        }
        
        protected override void OnDisable()
        {
            base.OnDisable();
            GameWorld.OnEntitySpawnEvent -= _OnEntitySpawn;
            GameWorld.OnEntityDespawnEvent -= _OnEntityDespawn;
            foreach (var weaponUser in _registeredWeaponUsers)
            {
                if (weaponUser != null)
                {
                    weaponUser.OnRequestToShootEvent -= _OnRequestToShoot;
                    weaponUser.OnRequestToReloadEvent -= _OnRequestToReload;
                }
            }
            _registeredWeaponUsers.Clear();
        }
        #endregion
       
        #region Methods
        private void _OnEntitySpawn(Entity entity)
        {
            if (entity is IWeaponUser weaponUser)
            {
                _RegisterWeaponUser(weaponUser);
            }
        }
        
        private void _OnEntityDespawn(Entity entity)
        {
            if (entity is IWeaponUser weaponUser)
                _UnregisterWeaponUser(weaponUser);
        }
        
        private void _RegisterWeaponUser(IWeaponUser weaponUser)
        {
            if (weaponUser == null || _registeredWeaponUsers.Contains(weaponUser))
                return;
            
            _registeredWeaponUsers.Add(weaponUser);
            weaponUser.OnRequestToShootEvent += _OnRequestToShoot;
            weaponUser.OnRequestToReloadEvent += _OnRequestToReload;
        }
        
        private void _UnregisterWeaponUser(IWeaponUser weaponUser)
        {
            if (weaponUser == null || !_registeredWeaponUsers.Contains(weaponUser))
                return;
            
            _registeredWeaponUsers.Remove(weaponUser);
            weaponUser.OnRequestToShootEvent -= _OnRequestToShoot;
            weaponUser.OnRequestToReloadEvent -= _OnRequestToReload;
        }
        
        private void _OnRequestToShoot(IWeaponUser weaponUser, Vector3 origin, Vector3 direction)
        {
            if (weaponUser == null)
                return;
            if (weaponUser.IsServerSide)
                Server_Shoot(weaponUser, origin, direction);
            else if (weaponUser.IsLocalOwned)
                weaponUser.Client_Shoot(origin, direction);
        }
        
        private void _OnRequestToReload(IWeaponUser weaponUser)
        {
            if (weaponUser == null)
                return;
            if (weaponUser.IsServerSide)
                Server_Reload(weaponUser);
            else if (weaponUser.IsLocalOwned)
                weaponUser.Client_Reload();
        }
        #endregion
        
        #region Server API
        public bool Server_Shoot(IWeaponUser weaponUser, Vector3 origin, Vector3 direction)
        {
            if (weaponUser == null || !weaponUser.IsServerSide)
                return false;
            
            if (!weaponUser.UserUuid.IsValid)
            {
                Debug.LogWarning($"[WeaponSystem] Server_Shoot: User UUID is invalid.");
                return false;
            }
            
            if (!weaponUser.IsValidAndSpawned)
            {
                Debug.LogWarning($"[WeaponSystem] Server_Shoot: User is not spawned.");
                return false;
            }
            
            var inventory = weaponUser.Inventory;

            var selectedItem = inventory?.GetSelectedItem();
            if (selectedItem == null || !(selectedItem is WeaponItemStack weaponStack))
                return false;
            
            if (!itemRegistry)
                return false;
            
            var weaponItem = itemRegistry.GetEntry<WeaponItem>(weaponStack.item);
            if (!weaponItem)
                return false;
            
            var state = weaponUser.WeaponState;
            if (state.IsReloading)
                return false;
            
            if (weaponStack.ammo <= 0)
            {
                Server_Reload(weaponUser);
                return false;
            }
            
            var fireRate = weaponItem.fireRate > 0f ? weaponItem.fireRate : 0.1f;
            var timeSinceLastFire = Time.time - state.LastFireTime;
            if (timeSinceLastFire < fireRate)
                return false;
            
            weaponStack.ammo--;
            state.LastFireTime = Time.time;
            _SyncAmmo(weaponUser, weaponStack);
            var range = weaponItem.range > 0f ? weaponItem.range : defaultRange;
            var damage = weaponItem.damage > 0f ? weaponItem.damage : defaultDamage;
            weaponUser.Server_ApplyRecoil();
            
            var movementVelocity = weaponUser.Velocity;
            movementVelocity.y = 0f;
            
            var firingError = 5 * movementVelocity.magnitude * weaponItem.firingErrorMultiplier * 0.8f;
            var recoilSpreadMultiplier = weaponUser.Server_GetRecoilSpreadMultiplier();
            
            var scopeBlend = weaponUser.GetScopeBlend();
            var scopeSpreadMultiplier = 1f;
            if (scopeBlend > 0f && weaponItem.hasScope)
            {
                scopeSpreadMultiplier = Mathf.Lerp(1f, weaponItem.spreadAngleWhenScoping, scopeBlend);
            }
            
            var projectileCount = weaponItem.projectileCount > 0 ? weaponItem.projectileCount : 1;
            var spreadAngle = weaponItem.spreadAngle;
            var averageHitPoint = Vector3.zero;
            var hitPoints = new List<Vector3>();
            
            for (var i = 0; i < projectileCount; i++)
            {
                var shotDirection = direction;
                if (spreadAngle > 0f)
                {
                    var effectiveSpreadAngle = spreadAngle * recoilSpreadMultiplier * scopeSpreadMultiplier;
                    var randomAngle = Random.Range(0f, effectiveSpreadAngle * 0.5f);
                    var randomRotation = Random.Range(0f, 360f);
                    
                    var up = Vector3.up;
                    var right = Vector3.Cross(direction, up).normalized;
                    if (right.magnitude < 0.1f)
                    {
                        right = Vector3.Cross(direction, Vector3.forward).normalized;
                    }

                    up = Vector3.Cross(right, direction).normalized;

                    var angleRad = randomRotation * Mathf.Deg2Rad;
                    var perpendicularOffset = (right * Mathf.Cos(angleRad) + up * Mathf.Sin(angleRad)) * Mathf.Tan(randomAngle * Mathf.Deg2Rad);
                    shotDirection = (direction + perpendicularOffset).normalized;
                }

                var effectiveFiringError = firingError * recoilSpreadMultiplier * scopeSpreadMultiplier;
                if (effectiveFiringError > 0f && (movementVelocity.magnitude > 0.01f || recoilSpreadMultiplier > 1f))
                {
                    var errorAngle = Random.Range(0f, effectiveFiringError);
                    var errorRotation = Random.Range(0f, 360f);
                    
                    var right = Vector3.Cross(shotDirection, Vector3.up).normalized;
                    if (right.magnitude < 0.1f)
                        right = Vector3.Cross(shotDirection, Vector3.forward).normalized;
                    
                    var errorRotationQuat = Quaternion.AngleAxis(errorAngle, right);
                    errorRotationQuat *= Quaternion.AngleAxis(errorRotation, shotDirection);
                    shotDirection = errorRotationQuat * shotDirection;
                }
                
                Vector3 hitPoint;
                if (Physics.Raycast(origin, shotDirection, out var hit, range, hitLayers))
                {
                    hitPoint = hit.point;
                    Debug.Log($"[WeaponSystem] Server_Shoot: Raycast HIT - Origin: {origin}, Direction: {shotDirection}, Hit Point: {hitPoint}, Collider: {hit.collider.name} (Layer: {hit.collider.gameObject.layer}), Distance: {hit.distance:F2}m");
                    _OnWeaponHit(weaponUser, hit, damage, weaponItem);
                }
                else
                {
                    hitPoint = origin + shotDirection * range;
                    Debug.Log($"[WeaponSystem] Server_Shoot: Raycast MISS - Origin: {origin}, Direction: {shotDirection}, Max Range: {range}m, End Point: {hitPoint}");
                    _OnWeaponMiss(weaponUser, hitPoint);
                }
                
                hitPoints.Add(hitPoint);
                averageHitPoint += hitPoint;
            }
            
            averageHitPoint /= projectileCount;
            
             var weaponExit = origin;   
            if (projectileCount > 1)
            {
                foreach (var hitPoint in hitPoints)
                {
                    RpcClient_SpawnShotEffects(weaponUser.UserUuid, weaponExit, hitPoint);
                }
            }
            else
            {
                RpcClient_SpawnShotEffects(weaponUser.UserUuid, weaponExit, averageHitPoint);
            }
            return true;
        }
        
        public bool Server_Reload(IWeaponUser weaponUser)
        {
            if (weaponUser is not { IsServerSide: true })
                return false;
            
            var inventory = weaponUser.Inventory;

            var selectedItem = inventory?.GetSelectedItem();
            if (selectedItem is not WeaponItemStack weaponStack)
                return false;
            
            if (!itemRegistry)
                return false;
            
            var weaponItem = itemRegistry.GetEntry<WeaponItem>(weaponStack.item);
            if (!weaponItem)
                return false;
            
            var state = weaponUser.WeaponState;
            if (state.IsReloading)
                return false;
            
            if (weaponStack.ammo >= weaponStack.maxAmmo)
                return false;
            
            state.IsReloading = true;
            state.ReloadStartTime = Time.time;
            
            RpcClient_PlayReloadAnimation(weaponUser.UserUuid, weaponItem.reloadClipName, weaponItem.reloadClipLength, weaponItem.reloadTime);
            
            weaponUser.Server_SetReloadProgress(0f);
            StartCoroutine(_ReloadCoroutine(weaponUser, weaponStack.item, inventory.selectedSlot, weaponItem));
            return true;
        }
        
        private System.Collections.IEnumerator _ReloadCoroutine(IWeaponUser weaponUser, string weaponItemId, int selectedSlot, WeaponItem weaponItem)
        {
            var reloadTime = weaponItem.reloadTime > 0f ? weaponItem.reloadTime : 1f;
            var startTime = Time.time;
            
            while (Time.time - startTime < reloadTime)
            {
                if (weaponUser == null || !weaponUser.IsServerSide)
                {
                    weaponUser?.Server_SetReloadProgress(0f);
                    yield break;
                }
                
                var inventory = weaponUser.Inventory;
                if (inventory == null)
                {
                    weaponUser.Server_SetReloadProgress(0f);
                    yield break;
                }
                
                var selectedItem = inventory.GetSelectedItem();
                if (selectedItem == null || !(selectedItem is WeaponItemStack weaponStack) || 
                    weaponStack.item != weaponItemId || inventory.selectedSlot != selectedSlot)
                {
                    var state = weaponUser.WeaponState;
                    state.IsReloading = false;
                    weaponUser.Server_SetReloadProgress(0f);
                    yield break;
                }
                
                var progress = (Time.time - startTime) / reloadTime;
                weaponUser.Server_SetReloadProgress(progress);
                yield return null;
            }
            
            if (weaponUser == null || !weaponUser.IsServerSide)
            {
                weaponUser?.Server_SetReloadProgress(0f);
                yield break;
            }
            
            var state2 = weaponUser.WeaponState;
            state2.IsReloading = false;
            
            var inventory2 = weaponUser.Inventory;
            if (inventory2 == null)
            {
                weaponUser.Server_SetReloadProgress(0f);
                yield break;
            }
            
            var selectedItem2 = inventory2.GetSelectedItem();
            if (selectedItem2 == null || !(selectedItem2 is WeaponItemStack weaponStack2) || 
                weaponStack2.item != weaponItemId || inventory2.selectedSlot != selectedSlot)
            {
                weaponUser.Server_SetReloadProgress(0f);
                yield break;
            }
            
            var ammoToReload = weaponItem.magazineSize > 0 ? weaponItem.magazineSize : weaponStack2.maxAmmo;
            weaponStack2.ammo = Mathf.Min(ammoToReload, weaponStack2.maxAmmo);
            _SyncAmmo(weaponUser, weaponStack2);
            
            weaponUser.Server_SetReloadProgress(0f);
        }
        #endregion
        
        #region Methods
        private void _GetShootingOriginAndDirection(IWeaponUser weaponUser, out Vector3 origin, out Vector3 direction)
        {
            origin = weaponUser.GetCurrentWeaponExit();

            if (CameraSystem.Instance && CameraSystem.Instance.mainCamera )
            {
                direction = CameraSystem.Instance.mainCamera.transform.forward;
                return;
            }
            
            if (weaponUser is IPovHandler povHandler)
            {
                povHandler.GetCameraView(out _, out var rotation, out _);
                direction = rotation * Vector3.forward;
                return;
            }
            direction = (weaponUser as MonoBehaviour)?.transform.forward ?? Vector3.forward;
        }
        
        private void _OnWeaponHit(IWeaponUser shooter, RaycastHit hit, float damage, WeaponItem weaponItem)
        {
            var isHeadshot = headLayer != 0 && ((1 << hit.collider.gameObject.layer) & headLayer) != 0;
            var finalDamage = damage;
            
            if (isHeadshot && weaponItem)
                finalDamage *= weaponItem.headshotDamageMultiplier;
            
            var healthComponent = hit.collider.GetComponent<IHealth>();
            if (healthComponent != null)
                healthComponent.Server_TakeDamage(finalDamage);
            else
            {
                var parentHealth = hit.collider.GetComponentInParent<IHealth>();
                parentHealth?.Server_TakeDamage(finalDamage);
            }
        }
        private void _OnWeaponMiss(IWeaponUser shooter, Vector3 endPoint)
        {
        }
        
        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                
                var found = FindChildRecursive(child, name);
                if (found)
                    return found;
            }
            return null;
        }
        
        private void _SyncAmmo(IWeaponUser weaponUser, WeaponItemStack weaponStack)
        {
            if (weaponUser == null || weaponStack == null)
                return;
            
            var inventory = weaponUser.Inventory;
            if (inventory == null)
                return;
            
            var newInventory = inventory.Copy();
            if (newInventory.selectedSlot < 0 || newInventory.selectedSlot >= newInventory.slots.Length)
                return;

            if (newInventory.slots[newInventory.selectedSlot] is not WeaponItemStack selectedStack ||
                selectedStack.item != weaponStack.item) return;
            
            selectedStack.ammo = weaponStack.ammo;
            selectedStack.maxAmmo = weaponStack.maxAmmo;
            weaponUser.Server_SetInventory(newInventory);
        }
        
        public void SpawnShotEffectsLocally(IWeaponUser weaponUser, Vector3 weaponExit, Vector3 hitPoint)
        {
            if (weaponUser == null)
                return;

            var actualWeaponExit = weaponUser.GetCurrentWeaponExit();

            if (AudioSystem.Instance && itemRegistry)
            {
                var inventory = weaponUser.Inventory;
                var selectedItem = inventory?.GetSelectedItem();
                if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item))
                {
                    var weaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);
                    if (weaponItem && !string.IsNullOrEmpty(weaponItem.shotSoundKey))
                    {
                        AudioSystem.Instance.PlayLocal(weaponItem.shotSoundKey, actualWeaponExit, 1f, AudioCategory.SFX);
                    }
                }
            }

            if (muzzleFlashPrefab)
            {
                var muzzleFlash = Instantiate(muzzleFlashPrefab);
                muzzleFlash.transform.position = actualWeaponExit;
                var direction = (hitPoint - actualWeaponExit).normalized;
                if (direction.magnitude > 0.1f)
                    muzzleFlash.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            if (bulletTracePrefab && actualWeaponExit != hitPoint)
            {
                var bulletTrace = Instantiate(bulletTracePrefab);
                bulletTrace.Initialize(actualWeaponExit, hitPoint);
            }

            if (hitEffectPrefab == null) 
                return;
            
            var hitEffect = Instantiate(hitEffectPrefab);
            hitEffect.transform.position = hitPoint;
        }
        #endregion
    }
}
