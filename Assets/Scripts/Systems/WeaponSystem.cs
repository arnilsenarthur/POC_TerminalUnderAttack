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
        [Header("Hit Effects")]
        [Tooltip("Sound key to play when a bullet hits an IHealth entity")]
        public string playerHitSoundKey;
        [Header("Death Effects")]
        [Tooltip("VFX prefab to spawn when a bullet kills an IHealth entity")]
        public GameObject playerDeathVfxPrefab;
        [Header("References")]
        public Registry itemRegistry;
        #endregion

        #region Private Fields
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
       
        #region Public Methods
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
            var hitFlags = new List<bool>();
            
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
                
                bool actuallyHit = Physics.Raycast(origin, shotDirection, out var hit, range, hitLayers);
                Vector3 hitPoint = actuallyHit ? hit.point : origin + shotDirection * range;
                
                if (actuallyHit)
                    _OnWeaponHit(weaponUser, hit, damage, weaponItem, origin, range);
  
                hitPoints.Add(hitPoint);
                hitFlags.Add(actuallyHit);
                averageHitPoint += hitPoint;
            }
            
            averageHitPoint /= projectileCount;
            var averageActuallyHit = hitFlags.Count > 0 && hitFlags.Contains(true);
            
             var weaponExit = origin;   
            if (projectileCount > 1)
            {
                for (var i = 0; i < hitPoints.Count; i++)
                {
                    RpcClient_SpawnShotEffects(weaponUser.UserUuid, weaponExit, hitPoints[i], hitFlags[i]);
                }
            }
            else
            {
                RpcClient_SpawnShotEffects(weaponUser.UserUuid, weaponExit, averageHitPoint, averageActuallyHit);
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

        public void SpawnShotEffectsLocally(IWeaponUser weaponUser, Vector3 weaponExit, Vector3 hitPoint, bool actuallyHit)
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
                        AudioSystem.Instance.PlayLocal(weaponItem.shotSoundKey, actualWeaponExit, 1f, AudioCategory.Gameplay);
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

            if (actuallyHit && hitEffectPrefab != null)
            {
                var hitEffect = Instantiate(hitEffectPrefab);
                hitEffect.transform.position = hitPoint;
            }
        }

        public void SpawnDeathEffectsLocally(Vector3 position, Vector3 normal)
        {
            if (playerDeathVfxPrefab != null)
            {
                var deathVfx = Instantiate(playerDeathVfxPrefab);
                deathVfx.transform.position = position;

                if (normal.magnitude > 0.1f)
                {
                    deathVfx.transform.rotation = Quaternion.LookRotation(normal);
                }
            }
        }
        #endregion

        #region Private Methods
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
        
        private void _GetShootingOriginAndDirection(IWeaponUser weaponUser, out Vector3 origin, out Vector3 direction)
        {
            origin = weaponUser.GetCurrentWeaponExit();

            if (CameraSystem.Instance && CameraSystem.Instance.mainCamera)
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
        
        private void _OnWeaponHit(IWeaponUser shooter, RaycastHit hit, float damage, WeaponItem weaponItem, Vector3 origin, float range)
        {
            var isHeadshot = headLayer != 0 && ((1 << hit.collider.gameObject.layer) & headLayer) != 0;
            var finalDamage = damage;

            if (weaponItem != null && range > 0f && weaponItem.distanceDamageFalloffCurve != null && weaponItem.distanceDamageFalloffCurve.length > 0)
            {
                var distance = hit.distance;
                var normalizedDistance = Mathf.Clamp01(distance / range);
                var distanceMultiplier = weaponItem.distanceDamageFalloffCurve.Evaluate(normalizedDistance);
                finalDamage *= distanceMultiplier;
            }
            
            if (isHeadshot && weaponItem)
                finalDamage *= weaponItem.headshotDamageMultiplier;
            
            var healthComponent = hit.collider.GetComponent<IHealth>();
            healthComponent ??= hit.collider.GetComponentInParent<IHealth>();

            if (healthComponent != null)
            {
                var healthBeforeDamage = healthComponent.CurrentHealth;
                healthComponent.Server_TakeDamage(finalDamage);

                if (!string.IsNullOrEmpty(playerHitSoundKey) && AudioSystem.Instance)
                    AudioSystem.Instance.PlayBroadcast(playerHitSoundKey, hit.point, 1f, AudioCategory.Gameplay);

                if (healthBeforeDamage > 0f && healthComponent.CurrentHealth <= 0f)
                {
                    RpcClient_SpawnDeathEffects(hit.point, hit.normal);

                    GamePlayer killer = null;
                    GamePlayer victim = null;

                    if (shooter is Entity shooterEntity)
                        killer = shooterEntity.GamePlayer;

                        if (healthComponent is Entity victimEntity)
                            victim = victimEntity.GamePlayer;
                        else if (hit.collider != null)
                        {
                            var hitEntity = hit.collider.GetComponent<Entity>() ?? hit.collider.GetComponentInParent<Entity>();
                            if (hitEntity != null)
                                victim = hitEntity.GamePlayer;
                        }

                    if (killer != null || victim != null)
                        FeedSystem.InvokeKillEvent(killer, victim);
                }
            }

            var rb = hit.collider.GetComponent<Rigidbody>();
            if (rb != null && weaponItem != null && weaponItem.hitForce > 0f)
            {
                var direction = (hit.point - origin).normalized;
                rb.AddForceAtPosition(direction * weaponItem.hitForce, hit.point, ForceMode.Impulse);
            }
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
                selectedStack.item != weaponStack.item)
                return;
            
            selectedStack.ammo = weaponStack.ammo;
            selectedStack.maxAmmo = weaponStack.maxAmmo;
            weaponUser.Server_SetInventory(newInventory);
        }
        #endregion
    }
}
