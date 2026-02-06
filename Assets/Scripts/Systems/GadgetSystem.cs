using System;
using System.Collections.Generic;
using TUA.Audio;
using TUA.Core;
using TUA.Core.Interfaces;
using TUA.Entities;
using TUA.Items;
using TUA.Misc;
using UnityEngine;

namespace TUA.Systems
{
    public partial class GadgetSystem : SingletonNetBehaviour<GadgetSystem>
    {

        #region Serialized Fields
        [Header("Prefabs")]
        public GameObject grenadePrefab;
        public GameObject flashPrefab;
        public GameObject smokePrefab;
        [Header("Smoke")]
        public GameObject smokeAreaEntityPrefab;
        [Header("References")]
        public Registry itemRegistry;
        #endregion

        #region Fields
        private readonly HashSet<IGadgetEntity> _registeredGadgetEntities = new();
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
            foreach (var gadgetEntity in _registeredGadgetEntities)
            {
                if (gadgetEntity != null)
                {
                    gadgetEntity.OnSmokeSpawnRequestEvent -= _OnSmokeSpawnRequest;
                    gadgetEntity.OnKillEvent -= _OnKillEvent;
                }
            }
            _registeredGadgetEntities.Clear();
            foreach (var weaponUser in _registeredWeaponUsers)
            {
                if (weaponUser != null)
                {
                    weaponUser.OnRequestToThrowGadgetEvent -= _OnRequestToThrowGadget;
                }
            }
            _registeredWeaponUsers.Clear();
        }

        #endregion

        #region Public Methods
        public bool Server_ThrowGadget(IWeaponUser weaponUser, Vector3 origin, Vector3 direction, bool isDrop)
        {
            if (!_ValidateThrowRequest(weaponUser, out var inventory, out var selectedSlot, out var gadgetStack, out var gadgetItem))
                return false;

            if (!_GetGadgetPrefab(gadgetItem, out var prefab))
                return false;

            var throwVelocity = _CalculateThrowVelocity(weaponUser, direction, gadgetItem, isDrop);
            var spawnPosition = _CalculateSpawnPosition(origin, direction);
            var spawnRotation = Quaternion.LookRotation(throwVelocity.normalized);

            if (!_SpawnGadgetEntity(prefab, spawnPosition, spawnRotation, weaponUser.UserUuid, gadgetItem, throwVelocity))
                return false;

            _UpdateInventoryAfterThrow(weaponUser, inventory, selectedSlot, gadgetStack);

            _PlayThrowSound(gadgetItem, origin);

            return true;
        }

        public void Server_SpawnSmokeArea(Vector3 position, float radius, float duration)
        {
            if (!IsServerSide)
                return;

            if (smokeAreaEntityPrefab == null)
                return;

            var gameWorld = GameWorld.Instance;
            if (gameWorld == null)
                return;

            var smokeArea = gameWorld.Server_SpawnObject<SmokeAreaEntity>(
                smokeAreaEntityPrefab,
                position,
                Quaternion.identity
            );

            if (smokeArea != null)
            {
                smokeArea.Server_Initialize(duration, duration + 5f, radius);
            }
        }
        #endregion

        #region Private Methods
        private void _OnEntitySpawn(Entity entity)
        {
            if (entity is IGadgetEntity gadgetEntity)
                _RegisterGadgetEntity(gadgetEntity);
            if (entity is IWeaponUser weaponUser)
                _RegisterWeaponUser(weaponUser);
        }

        private void _OnEntityDespawn(Entity entity)
        {
            if (entity is IGadgetEntity gadgetEntity)
                _UnregisterGadgetEntity(gadgetEntity);
            if (entity is IWeaponUser weaponUser)
                _UnregisterWeaponUser(weaponUser);
        }

        private void _RegisterGadgetEntity(IGadgetEntity gadgetEntity)
        {
            if (gadgetEntity == null || _registeredGadgetEntities.Contains(gadgetEntity))
                return;

            _registeredGadgetEntities.Add(gadgetEntity);
            gadgetEntity.OnSmokeSpawnRequestEvent += _OnSmokeSpawnRequest;
            gadgetEntity.OnKillEvent += _OnKillEvent;
        }

        private void _UnregisterGadgetEntity(IGadgetEntity gadgetEntity)
        {
            if (gadgetEntity == null)
                return;

            _registeredGadgetEntities.Remove(gadgetEntity);
            gadgetEntity.OnSmokeSpawnRequestEvent -= _OnSmokeSpawnRequest;
            gadgetEntity.OnKillEvent -= _OnKillEvent;
        }

        private void _OnSmokeSpawnRequest(IGadgetEntity gadgetEntity, Vector3 position, float radius, float duration)
        {
            if (!IsServerSide)
                return;

            Server_SpawnSmokeArea(position, radius, duration);
        }

        private void _OnKillEvent(IGadgetEntity gadgetEntity, GamePlayer killer, GamePlayer victim)
        {
            if (!IsServerSide)
                return;

            if (killer != null || victim != null)
                FeedSystem.InvokeKillEvent(killer, victim);
        }

        private void _RegisterWeaponUser(IWeaponUser weaponUser)
        {
            if (weaponUser == null || _registeredWeaponUsers.Contains(weaponUser))
                return;

            _registeredWeaponUsers.Add(weaponUser);
            weaponUser.OnRequestToThrowGadgetEvent += _OnRequestToThrowGadget;
        }

        private void _UnregisterWeaponUser(IWeaponUser weaponUser)
        {
            if (weaponUser == null)
                return;

            _registeredWeaponUsers.Remove(weaponUser);
            weaponUser.OnRequestToThrowGadgetEvent -= _OnRequestToThrowGadget;
        }

        private void _OnRequestToThrowGadget(IWeaponUser weaponUser, Vector3 origin, Vector3 direction, bool isDrop)
        {
            if (weaponUser == null)
                return;

            Server_ThrowGadget(weaponUser, origin, direction, isDrop);
        }

        private bool _ValidateThrowRequest(IWeaponUser weaponUser, out Inventory inventory, out int selectedSlot, out GadgetItemStack gadgetStack, out GadgetItem gadgetItem)
        {
            inventory = null;
            selectedSlot = -1;
            gadgetStack = null;
            gadgetItem = null;

            if (weaponUser == null || !weaponUser.IsServerSide)
                return false;

            if (!weaponUser.UserUuid.IsValid)
                return false;

            if (!weaponUser.IsValidAndSpawned)
                return false;

            inventory = weaponUser.Inventory;
            if (inventory == null)
                return false;

            selectedSlot = inventory.selectedSlot;
            if (selectedSlot < 0 || selectedSlot >= inventory.slots.Length)
                return false;

            var selectedItem = inventory.slots[selectedSlot];
            if (selectedItem is not GadgetItemStack stack)
                return false;

            gadgetStack = stack;
            if (gadgetStack.count <= 0)
                return false;

            if (!itemRegistry)
                return false;

            var item = itemRegistry.GetEntry<GadgetItem>(gadgetStack.item);
            if (item == null)
                return false;

            gadgetItem = item;
            return true;
        }

        private bool _GetGadgetPrefab(GadgetItem gadgetItem, out GameObject prefab)
        {
            prefab = null;

            if (gadgetItem is GrenadeItem)
                prefab = grenadePrefab;
            else if (gadgetItem is FlashItem)
                prefab = flashPrefab;
            else if (gadgetItem is SmokeItem)
                prefab = smokePrefab;

            return prefab != null;
        }

        private Vector3 _CalculateThrowVelocity(IWeaponUser weaponUser, Vector3 direction, GadgetItem gadgetItem, bool isDrop)
        {
            var throwDirection = direction.normalized;
            var throwerVelocity = weaponUser.Velocity;
            var force = isDrop ? gadgetItem.dropForce : gadgetItem.throwVelocity;
            return throwDirection * force + throwerVelocity;
        }

        private Vector3 _CalculateSpawnPosition(Vector3 origin, Vector3 direction)
        {
            var forwardOffset = direction.normalized * 0.5f;
            return origin + forwardOffset;
        }

        private bool _SpawnGadgetEntity(GameObject prefab, Vector3 position, Quaternion rotation, Uuid throwerUuid, GadgetItem gadgetItem, Vector3 velocity)
        {
            var gameWorld = GameWorld.Instance;
            if (gameWorld == null)
                return false;

            GamePlayer thrower = null;
            if (gameWorld.AllPlayers != null)
            {
                foreach (var player in gameWorld.AllPlayers)
                {
                    if (player != null && player.Uuid == throwerUuid)
                    {
                        thrower = player;
                        break;
                    }
                }
            }

            var gadgetEntity = gameWorld.Server_SpawnObject<GadgetEntity>(prefab, position, rotation, thrower);
            if (gadgetEntity == null)
                return false;

            gadgetEntity.Server_Initialize(gadgetItem, velocity, throwerUuid);
            return true;
        }

        private void _UpdateInventoryAfterThrow(IWeaponUser weaponUser, Inventory inventory, int selectedSlot, GadgetItemStack gadgetStack)
        {
            if (weaponUser == null || inventory == null)
                return;

            if (selectedSlot < 0 || selectedSlot >= inventory.slots.Length)
                return;

            var newInventory = inventory.Copy();
            if (newInventory.slots[selectedSlot] is GadgetItemStack updatedStack && updatedStack.item == gadgetStack.item)
            {
                updatedStack.count--;
                if (updatedStack.count <= 0)
                {
                    newInventory.RemoveItemAtSlot(selectedSlot);
                }
            }

            weaponUser.Server_SetInventory(newInventory);
        }

        private void _PlayThrowSound(GadgetItem gadgetItem, Vector3 position)
        {
            if (string.IsNullOrEmpty(gadgetItem.throwSoundKey))
                return;

            var audioSystem = AudioSystem.Instance;
            if (audioSystem != null)
                audioSystem.PlayBroadcast(gadgetItem.throwSoundKey, position, 1f, AudioCategory.Gameplay);
        }

        #endregion
    }
}
