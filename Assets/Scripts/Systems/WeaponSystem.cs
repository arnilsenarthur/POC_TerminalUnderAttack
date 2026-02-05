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
            {
                _UnregisterWeaponUser(weaponUser);
            }
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
            {
                Server_Shoot(weaponUser, origin, direction);
            }
            else if (weaponUser.IsLocalOwned)
            {
                weaponUser.Client_Shoot(origin, direction);
            }
        }
        private void _OnRequestToReload(IWeaponUser weaponUser)
        {
            if (weaponUser == null)
                return;
            if (weaponUser.IsServerSide)
            {
                Server_Reload(weaponUser);
            }
            else if (weaponUser.IsLocalOwned)
            {
                weaponUser.Client_Reload();
            }
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
            if (inventory == null)
                return false;
            var selectedItem = inventory.GetSelectedItem();
            if (selectedItem == null || !(selectedItem is WeaponItemStack weaponStack))
                return false;
            if (itemRegistry == null)
                return false;
            var weaponItem = itemRegistry.GetEntry<WeaponItem>(weaponStack.item);
            if (weaponItem == null)
                return false;
            var state = weaponUser.WeaponState;
            if (state.IsReloading)
                return false;
            if (weaponStack.ammo <= 0)
            {
                Server_Reload(weaponUser);
                return false;
            }
            float fireRate = weaponItem.fireRate > 0f ? weaponItem.fireRate : 0.1f;
            float timeSinceLastFire = Time.time - state.LastFireTime;
            if (timeSinceLastFire < fireRate)
                return false;
            weaponStack.ammo--;
            state.LastFireTime = Time.time;
            _SyncAmmo(weaponUser, weaponStack);
            float range = weaponItem.range > 0f ? weaponItem.range : defaultRange;
            float damage = weaponItem.damage > 0f ? weaponItem.damage : defaultDamage;
            
            // Apply recoil to weapon user (entity knows what weapon it's using)
            weaponUser.Server_ApplyRecoil();
            
            // Get movement velocity for firing error calculation
            Vector3 movementVelocity = weaponUser.Velocity;
            // Only use horizontal velocity for error calculation
            movementVelocity.y = 0f;
            
            // Calculate firing error based on velocity
            // Scale with velocity squared for stronger effect, then apply multiplier
            // This makes walking significantly reduce accuracy
            float firingError = 5 * movementVelocity.magnitude * weaponItem.firingErrorMultiplier * 0.8f;
            
            // Get recoil spread multiplier
            float recoilSpreadMultiplier = weaponUser.Server_GetRecoilSpreadMultiplier();
            
            // Get scope blend and apply spread multiplier when scoped
            float scopeBlend = weaponUser.GetScopeBlend();
            float scopeSpreadMultiplier = 1f;
            if (scopeBlend > 0f && weaponItem.hasScope)
            {
                // Lerp between normal spread (1.0) and scoped spread multiplier based on scope blend
                scopeSpreadMultiplier = Mathf.Lerp(1f, weaponItem.spreadAngleWhenScoping, scopeBlend);
            }
            
            // Get projectile count (default to 1 for normal weapons)
            int projectileCount = weaponItem.projectileCount > 0 ? weaponItem.projectileCount : 1;
            float spreadAngle = weaponItem.spreadAngle;
            
            // Shoot multiple projectiles if this is a shotgun
            Vector3 averageHitPoint = Vector3.zero;
            List<Vector3> hitPoints = new List<Vector3>();
            
            for (int i = 0; i < projectileCount; i++)
            {
                Vector3 shotDirection = direction;
                
                // Apply spread angle for all weapons (multiplied by recoil spread and scope spread)
                // This allows single-shot weapons to have random spread/miss chance
                if (spreadAngle > 0f)
                {
                    // Calculate random angle within spread cone (uniform distribution)
                    // Apply recoil spread multiplier and scope spread multiplier to adjust spread
                    float effectiveSpreadAngle = spreadAngle * recoilSpreadMultiplier * scopeSpreadMultiplier;
                    float randomAngle = Random.Range(0f, effectiveSpreadAngle * 0.5f);
                    float randomRotation = Random.Range(0f, 360f);
                    
                    // Create a random direction within the spread cone
                    // First, find perpendicular vectors to the direction
                    Vector3 up = Vector3.up;
                    Vector3 right = Vector3.Cross(direction, up).normalized;
                    if (right.magnitude < 0.1f)
                    {
                        // If direction is parallel to up, use forward instead
                        right = Vector3.Cross(direction, Vector3.forward).normalized;
                        up = Vector3.Cross(right, direction).normalized;
                    }
                    else
                    {
                        up = Vector3.Cross(right, direction).normalized;
                    }
                    
                    // Create a random point on a unit circle in the perpendicular plane
                    float angleRad = randomRotation * Mathf.Deg2Rad;
                    Vector3 perpendicularOffset = (right * Mathf.Cos(angleRad) + up * Mathf.Sin(angleRad)) * Mathf.Tan(randomAngle * Mathf.Deg2Rad);
                    
                    // Apply the offset to create the spread direction
                    shotDirection = (direction + perpendicularOffset).normalized;
                }
                
                // Apply firing error based on movement velocity and recoil (applies to all weapons)
                // Recoil increases spread even when not moving
                // Also apply scope spread multiplier when scoped
                float effectiveFiringError = firingError * recoilSpreadMultiplier * scopeSpreadMultiplier;
                if (effectiveFiringError > 0f && (movementVelocity.magnitude > 0.01f || recoilSpreadMultiplier > 1f))
                {
                    // Calculate random error angle based on velocity and recoil
                    float errorAngle = Random.Range(0f, effectiveFiringError);
                    float errorRotation = Random.Range(0f, 360f);
                    
                    // Use a perpendicular vector to the shot direction for the rotation axis
                    Vector3 right = Vector3.Cross(shotDirection, Vector3.up).normalized;
                    if (right.magnitude < 0.1f)
                        right = Vector3.Cross(shotDirection, Vector3.forward).normalized;
                    
                    // Apply random error in a cone around the shot direction
                    Quaternion errorRotationQuat = Quaternion.AngleAxis(errorAngle, right);
                    errorRotationQuat *= Quaternion.AngleAxis(errorRotation, shotDirection);
                    shotDirection = errorRotationQuat * shotDirection;
                }
                
                // Perform raycast for this projectile
                Vector3 hitPoint;
                if (Physics.Raycast(origin, shotDirection, out RaycastHit hit, range, hitLayers))
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
            
            // Average hit point for visual effect
            averageHitPoint /= projectileCount;
            
            // Get weapon exit point for muzzle flash (origin is already the bullet exit position)
            Vector3 weaponExit = origin;   
            // Spawn all shot effects together (unified to save RPC data)
            if (projectileCount > 1)
            {
                // For shotguns, spawn effects for each pellet
                foreach (Vector3 hitPoint in hitPoints)
                {
                    RpcClient_SpawnShotEffects(weaponUser.UserUuid, weaponExit, hitPoint);
                }
            }
            else
            {
                // For single projectile weapons, spawn one set of effects
                RpcClient_SpawnShotEffects(weaponUser.UserUuid, weaponExit, averageHitPoint);
            }
            return true;
        }
        public bool Server_Reload(IWeaponUser weaponUser)
        {
            if (weaponUser == null || !weaponUser.IsServerSide)
                return false;
            var inventory = weaponUser.Inventory;
            if (inventory == null)
                return false;
            var selectedItem = inventory.GetSelectedItem();
            if (selectedItem == null || selectedItem is not WeaponItemStack weaponStack)
                return false;
            if (itemRegistry == null)
                return false;
            var weaponItem = itemRegistry.GetEntry<WeaponItem>(weaponStack.item);
            if (weaponItem == null)
                return false;
            var state = weaponUser.WeaponState;
            if (state.IsReloading)
                return false;
            if (weaponStack.ammo >= weaponStack.maxAmmo)
                return false;
            state.IsReloading = true;
            state.ReloadStartTime = Time.time;
            
            // Send RPC to all clients to play reload animation
            RpcClient_PlayReloadAnimation(weaponUser.UserUuid, weaponItem.reloadClipName, weaponItem.reloadClipLength, weaponItem.reloadTime);
            
            // Set initial progress to 0
            weaponUser.Server_SetReloadProgress(0f);
            
            StartCoroutine(_ReloadCoroutine(weaponUser, weaponStack.item, inventory.selectedSlot, weaponItem));
            return true;
        }
        private System.Collections.IEnumerator _ReloadCoroutine(IWeaponUser weaponUser, string weaponItemId, int selectedSlot, WeaponItem weaponItem)
        {
            float reloadTime = weaponItem.reloadTime > 0f ? weaponItem.reloadTime : 1f;
            float startTime = Time.time;
            
            while (Time.time - startTime < reloadTime)
            {
                // Check if reload should be cancelled
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
                
                // Cancel if selected slot changed or item changed
                var selectedItem = inventory.GetSelectedItem();
                if (selectedItem == null || !(selectedItem is WeaponItemStack weaponStack) || 
                    weaponStack.item != weaponItemId || inventory.selectedSlot != selectedSlot)
                {
                    var state = weaponUser.WeaponState;
                    state.IsReloading = false;
                    weaponUser.Server_SetReloadProgress(0f);
                    yield break;
                }
                
                // Update progress
                float progress = (Time.time - startTime) / reloadTime;
                weaponUser.Server_SetReloadProgress(progress);
                
                yield return null;
            }
            
            // Final check before completing reload
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
            
            // Complete reload
            int ammoToReload = weaponItem.magazineSize > 0 ? weaponItem.magazineSize : weaponStack2.maxAmmo;
            weaponStack2.ammo = Mathf.Min(ammoToReload, weaponStack2.maxAmmo);
            _SyncAmmo(weaponUser, weaponStack2);
            
            // Reset progress to 0 to hide the reload bar
            weaponUser.Server_SetReloadProgress(0f);
        }
        #endregion
        #region Methods
        private void _GetShootingOriginAndDirection(IWeaponUser weaponUser, out Vector3 origin, out Vector3 direction)
        {
            // Get bullet exit position as origin
            origin = weaponUser.GetCurrentWeaponExit();
            
            // Get direction from camera view
            if (CameraSystem.Instance != null && CameraSystem.Instance.mainCamera != null)
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
            Debug.Log($"[WeaponSystem] _OnWeaponHit: Called - Shooter: {shooter?.UserUuid}, Collider: {hit.collider.name}, GameObject: {hit.collider.gameObject.name}, Layer: {hit.collider.gameObject.layer}, Base Damage: {damage}");
            
            // Check if this is a headshot
            bool isHeadshot = headLayer != 0 && ((1 << hit.collider.gameObject.layer) & headLayer) != 0;
            float finalDamage = damage;
            
            if (isHeadshot && weaponItem != null)
            {
                finalDamage *= weaponItem.headshotDamageMultiplier;
                Debug.Log($"[WeaponSystem] _OnWeaponHit: HEADSHOT detected! Multiplier: {weaponItem.headshotDamageMultiplier}, Final Damage: {finalDamage}");
            }
            
            var healthComponent = hit.collider.GetComponent<IHealth>();
            if (healthComponent != null)
            {
                Debug.Log($"[WeaponSystem] _OnWeaponHit: IHealth component found! Current Health: {healthComponent.CurrentHealth}/{healthComponent.MaxHealth}, Applying Damage: {finalDamage}");
                healthComponent.Server_TakeDamage(finalDamage);
                Debug.Log($"[WeaponSystem] _OnWeaponHit: Damage applied! New Health: {healthComponent.CurrentHealth}/{healthComponent.MaxHealth}");
            }
            else
            {
                Debug.LogWarning($"[WeaponSystem] _OnWeaponHit: NO IHealth component found on {hit.collider.name}! Cannot deal damage.");
                
                // Try to find IHealth on parent or root
                var parentHealth = hit.collider.GetComponentInParent<IHealth>();
                if (parentHealth != null)
                {
                    Debug.Log($"[WeaponSystem] _OnWeaponHit: Found IHealth on parent! Current Health: {parentHealth.CurrentHealth}/{parentHealth.MaxHealth}, Applying Damage: {finalDamage}");
                    parentHealth.Server_TakeDamage(finalDamage);
                    Debug.Log($"[WeaponSystem] _OnWeaponHit: Damage applied to parent! New Health: {parentHealth.CurrentHealth}/{parentHealth.MaxHealth}");
                }
                else
                {
                    Debug.LogWarning($"[WeaponSystem] _OnWeaponHit: No IHealth component found on parent either. Root: {hit.collider.transform.root.name}");
                }
            }
        }
        private void _OnWeaponMiss(IWeaponUser shooter, Vector3 endPoint)
        {
            Debug.Log($"[WeaponSystem] _OnWeaponMiss: Shooter: {shooter?.UserUuid}, End Point: {endPoint}");
        }
        
        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                Transform found = FindChildRecursive(child, name);
                if (found != null)
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
            if (newInventory.selectedSlot >= 0 && newInventory.selectedSlot < newInventory.slots.Length)
            {
                var selectedStack = newInventory.slots[newInventory.selectedSlot] as WeaponItemStack;
                if (selectedStack != null && selectedStack.item == weaponStack.item)
                {
                    selectedStack.ammo = weaponStack.ammo;
                    selectedStack.maxAmmo = weaponStack.maxAmmo;
                    weaponUser.Server_SetInventory(newInventory);
                }
            }
        }
        public void SpawnShotEffectsLocally(IWeaponUser weaponUser, Vector3 weaponExit, Vector3 hitPoint)
        {
            if (weaponUser == null)
                return;

            // If this is the local player's entity, get the gun exit ourselves for better accuracy
            Vector3 actualWeaponExit = weaponUser.GetCurrentWeaponExit();

            // Play shot sound
            if (AudioSystem.Instance != null && itemRegistry != null)
            {
                var inventory = weaponUser.Inventory;
                if (inventory != null)
                {
                    var selectedItem = inventory.GetSelectedItem();
                    if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item))
                    {
                        var weaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);
                        if (weaponItem != null && !string.IsNullOrEmpty(weaponItem.shotSoundKey))
                        {
                            AudioSystem.Instance.PlayLocal(weaponItem.shotSoundKey, actualWeaponExit, 1f, AudioCategory.SFX);
                        }
                    }
                }
            }

            // Spawn muzzle flash at weapon exit point
            if (muzzleFlashPrefab != null)
            {
                GameObject muzzleFlash = Instantiate(muzzleFlashPrefab);
                muzzleFlash.transform.position = actualWeaponExit;
                Vector3 direction = (hitPoint - actualWeaponExit).normalized;
                if (direction.magnitude > 0.1f)
                {
                    muzzleFlash.transform.rotation = Quaternion.LookRotation(direction);
                }
            }
            
            // Spawn bullet trace
            if (bulletTracePrefab != null && actualWeaponExit != hitPoint)
            {
            BulletTrace bulletTrace = Instantiate(bulletTracePrefab);
                bulletTrace.Initialize(actualWeaponExit, hitPoint);
            }
            
            // Spawn hit effect at hit point
            if (hitEffectPrefab != null)
            {
                GameObject hitEffect = Instantiate(hitEffectPrefab);
                hitEffect.transform.position = hitPoint;
            }
        }
        #endregion
    }
}
