using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using TUA.Core;
using UnityEngine;

namespace TUA.Entities
{
    public partial class PlayerEntity
    {
        #region Private Fields
        private readonly SyncVar<Inventory> _inventory = new(
            new Inventory(),
            new SyncTypeSettings
            {
                WritePermission = WritePermission.ServerOnly,
                ReadPermission = ReadPermission.Observers,
                SendRate = 0f,
            });
        private readonly SyncVar<float> _health = new(
            100f,
            new SyncTypeSettings
            {
                WritePermission = WritePermission.ServerOnly,
                ReadPermission = ReadPermission.Observers,
                SendRate = 0f,
            });
        private readonly SyncVar<float> _maxHealth = new(
            100f,
            new SyncTypeSettings
            {
                WritePermission = WritePermission.ServerOnly,
                ReadPermission = ReadPermission.Observers,
                SendRate = 0f,
            });
        private readonly SyncVar<float> _reloadProgress = new(
            0f,
            new SyncTypeSettings
            {
                WritePermission = WritePermission.ServerOnly,
                ReadPermission = ReadPermission.Observers,
                SendRate = 0f,
            });
        #endregion

        #region Unity Callbacks
        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_health.Value <= 0f)
                _health.Value = 100f;
            if (_maxHealth.Value <= 0f)
                _maxHealth.Value = 100f;
            CurrentHealth = _health.Value;
            MaxHealth = _maxHealth.Value;
            Inventory = _inventory.Value;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            CurrentHealth = _health.Value;
            MaxHealth = _maxHealth.Value;
            Inventory = _inventory.Value;

            GameWorld.OnTickEvent += _OnTick;

            _inventory.OnChange += (_, next, _) =>
            {
                Inventory = next;
                OnInventoryChangedHandler(next);
            };
            _health.OnChange += (_, next, _) =>
            {
                CurrentHealth = next;
                OnHealthChanged(CurrentHealth, MaxHealth);
            };
            _maxHealth.OnChange += (_, next, _) =>
            {
                MaxHealth = next;
                OnHealthChanged(CurrentHealth, MaxHealth);
            };
            _reloadProgress.OnChange += (_, next, _) =>
            {
                ReloadProgress = next;
                OnReloadProgressChangeEvent?.Invoke(next);

                if (next <= 0)
                    _CancelReloadSound();
            };

            UpdateVisualItem();
            ApplyAimingState(IsAiming);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            GameWorld.OnTickEvent -= _OnTick;
        }
        #endregion

        #region Private Methods
        private void _OnTick(float deltaTime)
        {
            if (IsLocalOwned)
            {
                var state = new PlayerState
                {
                    viewDirection = ViewDirection,
                    position = transform.position,
                    velocity = Velocity,
                    isAiming = IsAiming,
                    isFiring = IsFiring,
                    isSneaking = IsSneaking,
                    isScoped = IsScoped,
                    recoilComputed = GetCameraRecoilOffset(),
                    reloadProgress = 0f
                };
                RpcServer_UpdatePlayerState(state);
            }
        }

        private void Server_SetHealthInternal(float health)
        {
            var previousHealth = CurrentHealth;
            health = Mathf.Clamp(health, 0f, MaxHealth);
            _health.Value = health;
            CurrentHealth = health;

            if (previousHealth > 0f && health <= 0f)
                OnDeathEvent?.Invoke();
        }

        private void Server_SetMaxHealthInternal(float maxHealth)
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            _maxHealth.Value = maxHealth;
            MaxHealth = maxHealth;
            _health.Value = Mathf.Clamp(_health.Value, 0f, maxHealth);
            CurrentHealth = _health.Value;
        }

        private void Server_SetInventoryInternal(Inventory inventory)
        {
            _inventory.Value = inventory;
            Inventory = inventory;
        }

        private void Server_SetReloadProgressInternal(float progress)
        {
            _reloadProgress.Value = Mathf.Clamp01(progress);
        }

        [ServerRpc(RequireOwnership = true)]
        private void RpcServer_UpdatePlayerState(PlayerState state, Channel channel = Channel.Unreliable)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("RpcServer_UpdatePlayerState can only be called on server side");

            RpcClient_PlayerStateUpdate(state, channel);
        }

        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_PlayerStateUpdate(PlayerState state, Channel channel = Channel.Unreliable)
        {
            if (IsLocalOwned)
                return;

            var viewDirChanged = _viewDirection != state.viewDirection;
            var positionChanged = Position != state.position;
            var velocityChanged = InternalVelocity != state.velocity;
            var aimingChanged = _isAiming != state.isAiming;
            var firingChanged = _isFiring != state.isFiring;
            var sneakingChanged = _isSneaking != state.isSneaking;

            if (viewDirChanged)
            {
                var currentYaw = _viewDirection.x;
                var currentPitch = _viewDirection.y;
                var targetYaw = state.viewDirection.x;
                var targetPitch = state.viewDirection.y;

                var yawDiff = Mathf.DeltaAngle(currentYaw, targetYaw);
                var pitchDiff = targetPitch - currentPitch;

                var lerpSpeed = 20f;
                var newYaw = Mathf.LerpAngle(currentYaw, currentYaw + yawDiff, Time.deltaTime * lerpSpeed);
                var newPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * lerpSpeed);

                _viewDirection = new Vector2(newYaw, newPitch);
                OnViewDirectionChangeEvent?.Invoke(_viewDirection);
                OnViewDirectionChanged(_viewDirection);
            }

            Position = state.position;
            InternalVelocity = state.velocity;
            _isAiming = state.isAiming;
            _isFiring = state.isFiring;
            _isSneaking = state.isSneaking;
            IsScoped = state.isScoped;
            SyncedRecoilOffset = state.recoilComputed;

            if (positionChanged)
                transform.position = state.position;

            if (positionChanged)
            {
                OnPositionChangeEvent?.Invoke(state.position);
                OnPositionChanged(state.position);
            }

            if (velocityChanged)
            {
                OnVelocityChangeEvent?.Invoke(state.velocity);
                OnVelocityChanged(state.velocity);
            }

            if (aimingChanged)
            {
                OnIsAimingChangeEvent?.Invoke(state.isAiming);
                OnAimingChanged(state.isAiming);
            }

            if (firingChanged)
            {
                OnIsFiringChangeEvent?.Invoke(state.isFiring);
            }

            if (sneakingChanged)
            {
                OnIsSneakingChangeEvent?.Invoke(state.isSneaking);
                OnSneakingChanged(state.isSneaking);
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void RpcClient_SelectSlot(int slotIndex)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("RpcClient_SelectSlot can only be called on server side");
            var inventory = _inventory.Value;

            if (inventory == null)
                return;

            if (!inventory.CanSelectSlot(slotIndex))
                return;

            var newInventory = inventory.Copy();
            newInventory.selectedSlot = slotIndex;
            _inventory.Value = newInventory;
        }

        [ServerRpc(RequireOwnership = true)]
        private void RpcClient_ThrowGadget(Vector3 origin, Vector3 direction, bool isDrop)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("RpcClient_ThrowGadget can only be called on server side");

            OnRequestToThrowGadgetEvent?.Invoke(this, origin, direction, isDrop);
        }

        [ServerRpc(RequireOwnership = true)]
        private void RpcClient_Shoot(Vector3 origin, Vector3 direction)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("RpcClient_Shoot can only be called on server side");
            OnRequestToShootEvent?.Invoke(this, origin, direction);
        }

        [ServerRpc(RequireOwnership = true)]
        private void RpcClient_Reload()
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("RpcClient_Reload can only be called on server side");
            OnRequestToReloadEvent?.Invoke(this);
        }
        #endregion
    }
}
