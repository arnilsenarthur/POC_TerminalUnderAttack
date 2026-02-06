using System;
using TUA.Audio;
using TUA.Core;
using TUA.Core.Interfaces;
using TUA.Items;
using TUA.Misc;
using TUA.Settings;
using TUA.Windowing;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;

namespace TUA.Entities
{
    [RequireComponent(typeof(CharacterController))]
    public abstract partial class PlayerEntity : Entity, IWeaponUser, IHealth
    {
        #region Serialized Fields
        [Header("Movement")]
        public float walkSpeed = 5f;
        public float stealthSpeed = 2f;
        public float gravity = 9.81f;
        public float jumpHeight = 2f;

        [Header("Movement Physics")]
        [Tooltip("Small downward velocity applied while grounded to keep the controller snapped to ground and prevent grounded flicker.")]
        public float groundStickVelocity = 2f;
        [Range(0f, 1f)]
        [Tooltip("0 = no air control (preserve momentum). 1 = full air control (same as ground).")]
        public float airControl = 0.25f;

        [Header("Look")]
        public float rotationSpeed = 100f;

        [FormerlySerializedAs("_settings")]
        [Header("Look Settings")]
        [SerializeField] private SettingsAsset settings;
        [FormerlySerializedAs("_cameraSensitivityXKey")] [SerializeField] private string cameraSensitivityXKey = "cameraSensitivityX";
        [FormerlySerializedAs("_cameraSensitivityYKey")] [SerializeField] private string cameraSensitivityYKey = "cameraSensitivityY";
        [FormerlySerializedAs("_cameraInvertedXKey")] [SerializeField] private string cameraInvertedXKey = "cameraInvertedX";
        [FormerlySerializedAs("_cameraInvertedYKey")] [SerializeField] private string cameraInvertedYKey = "cameraInvertedY";

        [Header("Body")]
        public float headHeight = 1.6f;

        [Header("Animator")]
        public Animator animator;
        public string walkingXParameter = "WalkingX";
        public string walkingZParameter = "WalkingZ";
        public string airborneParameter = "Airborne";

        [Header("Animation / Aiming")]
        public float aimInTime = 0.20f;
        [Tooltip("Seconds for aim-out blend.")]
        public float aimOutTime = 0.15f;
        [Tooltip("If true, the player can only shoot once aim-in is complete.")]
        public bool requireAimToShoot = true;

        [Header("Aim IK")]
        public Rig aimIKRig;
        [Tooltip("RigBuilder component that needs to be rebuilt when IK targets change.")]
        public RigBuilder rigBuilder;
        [Tooltip("Maximum distance forward from chest for left hand IK target (arm reach).")]
        public float leftHandReachDistance = 0.4f;
        [Tooltip("Height offset from ground for chest position (as fraction of headHeight).")]
        public float chestHeightRatio = 0.65f;
        public Transform leftHandIKTarget;
        public Transform rightHandIKTarget;
        public TwoBoneIKConstraint rightHandIKRig;
        public string rightHandIKTargetName = "RigAimRightTarget";
        public string bulletExitName = "BulletExit";

        [Header("Visual Item")]
        public Registry itemRegistry;
        public Transform heldItemParent;

        [Header("Inverse Kinematics")]
        [Tooltip("Body IK target reference for aim direction.")]
        public Transform bodyIKTargetReference;
        public AnimationCurve aimTargetZOffsetCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve aimTargetYOffsetCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Scope Settings")]
        [Tooltip("Position relative to camera when scoped. This will be transformed by camera to fix the gun relative to camera.")]
        public Vector3 scopeTargetPosition = new Vector3(0f, -0.1f, 0.5f);
        [Tooltip("Lerp speed for transitioning between normal aiming and scoped states.")]
        public float scopeLerpSpeed = 8f;
        #endregion
        
        #region Fields
        protected GameObject CurrentVisualItem;
        private readonly WeaponState _weaponState = new();
        private float _aimBlend;
        private float _aimReadyAt;
        
        protected bool IsScoped;
        protected float ScopeBlend;
        private AudioPlayback _currentReloadSound;
        
        protected float CurrentRecoilVertical;
        protected float CurrentRecoilHorizontal;
        protected float CurrentRecoilKickback;
        protected float CurrentRecoilRotationX;
        
        private float _smoothedCameraRecoilVertical;
        private float _smoothedCameraRecoilHorizontal;
        
        protected Vector2 SyncedRecoilOffset = Vector2.zero;
        private float _armsIKWeight = 1f;
        protected CharacterController CharacterController;
        #endregion

        #region  Properties
        private Vector2 _viewDirection;
        public Vector2 ViewDirection 
        { 
            get => _viewDirection;
            set
            {
                if (IsLocalOwned)
                {
                    _viewDirection = value;
                    OnViewDirectionChangeEvent?.Invoke(value);
                }
            }
        }

        protected Vector3 InternalVelocity;
        public Vector3 Velocity 
        { 
            get => InternalVelocity;
            set
            {
                if (InternalVelocity == value)
                    return;
                    
                Client_UpdateVelocity(value);
            }
        }

        private bool _isAiming;
        public bool IsAiming 
        { 
            get => _isAiming;
            set
            {
                if (_isAiming == value)
                    return;
                    
                Client_UpdateAiming(value);
            }
        }

        private bool _isFiring;
        public bool IsFiring 
        { 
            get => _isFiring;
            set
            {
                if (_isFiring == value)
                    return;
                    
                Client_UpdateFiring(value);
            }
        }

        public bool IsAimReady => IsAimReadyInternal();
        private bool _isSneaking;
        
        public bool IsSneaking 
        { 
            get => _isSneaking;
            set
            {
                if (_isSneaking == value)
                    return;
                    
                Client_UpdateSneaking(value);
            }
        }
        
        public float ReloadProgress { get; private set; }
        protected Vector3 Position { get; private set; }
        #endregion

        #region Events
        public event Action<Vector2> OnViewDirectionChangeEvent;
        public event Action<Vector3> OnPositionChangeEvent;
        public event Action<Vector3> OnVelocityChangeEvent;
        public event Action<bool> OnIsAimingChangeEvent;
        public event Action<bool> OnIsFiringChangeEvent;
        public event Action<bool> OnIsSneakingChangeEvent;
        public event Action<float> OnReloadProgressChangeEvent;
        #endregion
        
        #region Unity Callbacks
        public void OnEnable()
        {
            CharacterController = GetComponent<CharacterController>();
        }
        
        protected virtual void OnDestroy()
        {
            // Clean up visual item when player is destroyed
            // This is important because the gun can be unparented (e.g., when scoped in first person)
            // and would otherwise remain in the scene as an orphaned object
            if (CurrentVisualItem != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(CurrentVisualItem);
                }
                else
                {
                    DestroyImmediate(CurrentVisualItem);
                }
                CurrentVisualItem = null;
            }
            
            // Cancel any active reload sounds
            _CancelReloadSound();
        }
        
        public void Update()
        {
            var deltaTime = Time.deltaTime;
            
            if (IsLocalOwned)
            { 
                HandleInventoryInput();
                HandleAimingInput();
                HandleSneakInput();
                HandleFiringStateInput();
                HandleGadgetInput();
                HandleMovement(deltaTime);
                HandleLook(deltaTime);
            }
            else
            {
                HandleRemoteMovement(deltaTime);
            }

            UpdateLocomotionAnimation();
            UpdateRecoil(deltaTime);
            
            if (IsScoped)
            {
                ScopeBlend = Mathf.Lerp(ScopeBlend, 1f, scopeLerpSpeed * deltaTime);
            }
            else
            {
                ScopeBlend = Mathf.Lerp(ScopeBlend, 0f, scopeLerpSpeed * deltaTime);
            }
            
            UpdateAimingBlend(deltaTime);
            
            var targetEntity = GameWorld.Instance?.GetTargetEntity<Entity>();
            var isFirstPersonSpectated = (this is FirstPersonPlayerEntity) && (targetEntity && targetEntity == this);
            
            if (!isFirstPersonSpectated)
            {
                var yaw = ViewDirection.x;
                var pitch = ViewDirection.y;
                
                var headPosition = transform.position + Vector3.up * headHeight;
                var yawRotation = Quaternion.Euler(0f, yaw, 0f);
                var offset = new Vector3(0.2f, aimTargetYOffsetCurve.Evaluate(pitch), aimTargetZOffsetCurve.Evaluate(pitch));
                
                var targetPosition = headPosition + yawRotation * offset;
                if (bodyIKTargetReference)
                {
                    bodyIKTargetReference.position = targetPosition;
                    bodyIKTargetReference.rotation = Quaternion.Euler(pitch, yaw, 0f) * Quaternion.Euler(0f, -90f, -90f);
                }

                if (leftHandIKTarget)
                {
                    var chestPosition = transform.position + Vector3.up * (headHeight * chestHeightRatio);
                    
                    var directionToTarget = (targetPosition - chestPosition).normalized;
                    var leftTargetPosition = chestPosition + directionToTarget * leftHandReachDistance;
                    
                    var bodyForward = transform.forward;
                    var bodyUp = transform.up;
                    leftTargetPosition -= bodyForward * CurrentRecoilKickback;
                    leftTargetPosition += bodyUp * (CurrentRecoilKickback * 0.5f);
                    
                    leftHandIKTarget.position = leftTargetPosition;
                    var recoilPitch = pitch - CurrentRecoilRotationX;
                    var recoilYaw = yaw + CurrentRecoilHorizontal;
                    var baseRotation = Quaternion.Euler(recoilPitch, recoilYaw, 0f) * Quaternion.Euler(0f, -90f, -90f);
                    leftHandIKTarget.rotation = baseRotation;
                }
            }

            UpdateIKTargets();
            UpdateRightHandIKTarget();
            
            if (IsLocalOwned)
            {
                HandleWeaponInput();
            }
        }

        private void UpdateLocomotionAnimation()
        {
            if (!animator)
                return;

            if (string.IsNullOrEmpty(walkingXParameter) || string.IsNullOrEmpty(walkingZParameter))
                return;

            if (!string.IsNullOrEmpty(airborneParameter) && CharacterController)
            {
                var airborne = !CharacterController.isGrounded;
                animator.SetBool(airborneParameter, airborne);
            }
            
            var planar = Velocity;
            planar.y = 0f;
            var local = transform.InverseTransformDirection(planar);

            var moveSpeed = Mathf.Max(0.001f, GetCurrentMoveSpeed());
            var v = new Vector2(local.x, local.z) / moveSpeed;
            
            var mag = v.magnitude;
            if (mag > 1f)
                v /= mag;

            animator.SetFloat(walkingXParameter, v.x);
            animator.SetFloat(walkingZParameter, v.y);
        }
        #endregion

        protected virtual void HandleMovement(float deltaTime)
        {
            if (!IsLocalOwned || !CharacterController)
                return;
            
            var grounded = CharacterController.isGrounded;
            var vel = Velocity;
            if (grounded && vel.y <= 0f)
                vel.y = -Mathf.Abs(groundStickVelocity);
            
            if (!IsWindowOpen())
            {
                if (grounded && Input.GetButtonDown("Jump"))
                    vel.y = Mathf.Sqrt(jumpHeight * 2f * gravity);
            }
            
            if (!grounded || vel.y > 0f)
                vel.y -= gravity * deltaTime;
            
            Velocity = vel;
        }

        /// <summary>
        /// Checks if any window (pause menu, settings, etc.) is currently open.
        /// When windows are open, only player input is disabled - the game world continues running normally.
        /// This is important for multiplayer where the world must continue even when a player has their menu open.
        /// </summary>
        protected bool IsWindowOpen()
        {
            return WindowManager.HasOpenWindow();
        }

        protected virtual void HandleRemoteMovement(float deltaTime)
        {
            if (IsLocalOwned || !CharacterController)
                return;

            var syncedVelocity = Velocity;
            CharacterController.Move(syncedVelocity * deltaTime);

            if (Position != Vector3.zero)
            {
                var distance = Vector3.Distance(transform.position, Position);
                if (distance > 0.1f) 
                {
                    var lerpSpeed = Mathf.Min(distance * 5f, 20f); 
                    transform.position = Vector3.Lerp(transform.position, Position, deltaTime * lerpSpeed);
                }
            }

            var syncedYaw = ViewDirection.x;
            var currentEuler = transform.rotation.eulerAngles;
            transform.rotation = Quaternion.Euler(currentEuler.x, syncedYaw, currentEuler.z);
        }

        protected float GetCurrentMoveSpeed()
        {
            return IsSneaking ? stealthSpeed : walkSpeed;
        }

        protected virtual void HandleLook(float deltaTime)
        {
            if (!IsLocalOwned)
                return;

            if (IsWindowOpen() || Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
                return;

            var mouseX = Input.GetAxisRaw("Mouse X");
            var mouseY = Input.GetAxisRaw("Mouse Y");

            if (Mathf.Abs(mouseX) < 0.0001f && Mathf.Abs(mouseY) < 0.0001f)
                return;

            var sensX = settings?.GetFloat(cameraSensitivityXKey, 1f) ?? 1f;
            var sensY = settings?.GetFloat(cameraSensitivityYKey, 1f) ?? 1f;
            var invX = settings?.GetBool(cameraInvertedXKey) ?? false;
            var invY = settings?.GetBool(cameraInvertedYKey) ?? false;

            var baseSpeed = rotationSpeed * 10f;
            var speedX = baseSpeed * sensX;
            var speedY = baseSpeed * sensY;

            var deltaYaw = mouseX * speedX * deltaTime * (invX ? -1f : 1f);
            var deltaPitch = -mouseY * speedY * deltaTime * (invY ? -1f : 1f);

            var newYaw = _viewDirection.x + deltaYaw;
            var newPitch = Mathf.Clamp(_viewDirection.y + deltaPitch, -89f, 89f);

            _viewDirection = new Vector2(newYaw, newPitch);
            OnViewDirectionChangeEvent?.Invoke(_viewDirection);
        }

        private bool IsSelectedItemAimable()
        {
            var selected = Inventory?.GetSelectedItem();
            if (selected == null || string.IsNullOrEmpty(selected.item))
                return false;

            if (itemRegistry)
            {
                var item = itemRegistry.GetEntry<Item>(selected.item);
                if (item)
                    return item.IsAimable(this, selected);
            }
            
            return selected is WeaponItemStack;
        }

        private bool IsSelectedItemDisplayedAsAiming()
        {
            var selected = Inventory?.GetSelectedItem();
            if (selected == null || string.IsNullOrEmpty(selected.item))
                return false;

            if (!itemRegistry)
                return false;

            var item = itemRegistry.GetEntry<Item>(selected.item);
            return item && item.DisplayAsAimingWhenHeld(this, selected);
        }

        private void HandleAimingInput()
        {
            if (!IsLocalOwned)
                return;
            if (IsWindowOpen())
                return;

            // Check if current weapon has scope
            var currentWeaponHasScope = false;
            var selectedItem = Inventory?.GetSelectedItem();
            if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item) && itemRegistry)
            {
                var weaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);
                if (weaponItem)
                    currentWeaponHasScope = weaponItem.hasScope;
            }

            if (currentWeaponHasScope)
            {
                if (!IsAiming)
                {
                    IsAiming = true;
                    ApplyAimingState(true);
                }
                
                if (_weaponState.IsReloading)
                {
                    if (IsScoped)
                        Client_UpdateScoping(false);
                }
                else
                {
                    var wantScope = Input.GetButton("Fire2") || Input.GetMouseButton(1);
                    if (IsScoped != wantScope)
                    {
                        Client_UpdateScoping(wantScope);
                    }
                }
            }
            else
            {
                if (IsScoped)
                    Client_UpdateScoping(false);
            
                if (!IsSelectedItemAimable())
                {
                    if (!IsAiming) 
                        return;
                    
                    IsAiming = false;
                    ApplyAimingState(false);
                    return;
                }

                var wantAim = Input.GetButton("Fire2") || Input.GetMouseButton(1);
                if (wantAim == IsAiming)
                    return;

                IsAiming = wantAim;
                ApplyAimingState(wantAim);
            }
        }

        private void HandleFiringStateInput()
        {
            if (!IsLocalOwned)
                return;
            
            if (IsWindowOpen())
                return;

            var wantFiring = Input.GetButton("Fire1");
            if (wantFiring == IsFiring)
                return;

            IsFiring = wantFiring;
        }

        private void HandleSneakInput()
        {
            if (!IsLocalOwned)
                return;
            
            if (IsWindowOpen())
                return;

var inputSneak = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftShift);
            
            var wantSneak = inputSneak || (IsSelectedItemAimable() && IsAiming);

            if (wantSneak == IsSneaking)
                return;

            IsSneaking = wantSneak;
        }

        private void HandleGadgetInput()
        {
            if (!IsLocalOwned)
                return;
                
            if (IsWindowOpen())
                return;

            var selectedItem = Inventory?.GetSelectedItem();
            if (selectedItem is not GadgetItemStack gadgetStack)
                return;

            if (gadgetStack.count <= 0)
                return;

            var isDrop = false;
            if (Input.GetMouseButtonDown(1))
                isDrop = true;
            else if (!Input.GetMouseButtonDown(0))
                return;

            GetCameraView(out var cameraPosition, out var cameraRotation, out _);
            var cameraForward = cameraRotation * Vector3.forward;
            var throwOrigin = cameraPosition;
            if (IsServerSide)
            {
                OnRequestToThrowGadgetEvent?.Invoke(this, throwOrigin, cameraForward, isDrop);
            }
            else
            {
                Client_ThrowGadget(throwOrigin, cameraForward, isDrop);
                OnRequestToThrowGadgetEvent?.Invoke(this, throwOrigin, cameraForward, isDrop);
            }
        }

        private bool IsAimReadyInternal()
        {
            if (!IsSelectedItemAimable())
                return true;

            if (!IsAiming)
                return false;
            
            if (Time.time < _aimReadyAt)
                return false;
            
            return _aimBlend >= 0.99f;
        }

        private void UpdateAimingBlend(float deltaTime)
        {
            var visualAim = (IsSelectedItemAimable() && IsAiming) || IsSelectedItemDisplayedAsAiming();
            var target = visualAim ? 1f : 0f;
            
            var duration = (target > _aimBlend) ? Mathf.Max(0.001f, aimInTime) : Mathf.Max(0.001f, aimOutTime);
            var step = deltaTime / duration;
            _aimBlend = Mathf.MoveTowards(_aimBlend, target, step);

            var isReloading = WeaponState.IsReloading || (ReloadProgress > 0f && ReloadProgress < 1f);
            const float ikLerpSpeed = 3f;
            
            var targetWeight = isReloading ? 0f : 1f;
            _armsIKWeight = Mathf.MoveTowards(_armsIKWeight, targetWeight, ikLerpSpeed * deltaTime);
            
            if (isReloading && _armsIKWeight > 0.01f)
                _armsIKWeight = Mathf.Max(0f, _armsIKWeight - ikLerpSpeed * deltaTime * 2f);

            if (aimIKRig)
                aimIKRig.weight = _armsIKWeight * _aimBlend;
        }

        private void UpdateRecoil(float deltaTime)
        {
            WeaponItem weaponItem = null;
            var selectedItem = Inventory?.GetSelectedItem();
            if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item) && itemRegistry)
                weaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);
            
            var recoverySpeed = weaponItem ? weaponItem.recoilRecoverySpeed : 5f;

            CurrentRecoilVertical = Mathf.MoveTowards(CurrentRecoilVertical, 0f, recoverySpeed * deltaTime);
            CurrentRecoilHorizontal = Mathf.MoveTowards(CurrentRecoilHorizontal, 0f, recoverySpeed * deltaTime);
            CurrentRecoilKickback = Mathf.MoveTowards(CurrentRecoilKickback, 0f, recoverySpeed * deltaTime);
            CurrentRecoilRotationX = Mathf.MoveTowards(CurrentRecoilRotationX, 0f, recoverySpeed * deltaTime);
            
            var cameraRecoilLerpSpeed = 15f;
            _smoothedCameraRecoilVertical = Mathf.Lerp(_smoothedCameraRecoilVertical, CurrentRecoilVertical, cameraRecoilLerpSpeed * deltaTime);
            _smoothedCameraRecoilHorizontal = Mathf.Lerp(_smoothedCameraRecoilHorizontal, CurrentRecoilHorizontal, cameraRecoilLerpSpeed * deltaTime);
        }
        
        public void Server_ApplyRecoil()
        {
            WeaponItem weaponItem = null;
            var selectedItem = Inventory?.GetSelectedItem();
            if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item) && itemRegistry)
                weaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);

            if (!weaponItem)
                return;

            var recoilMultiplier = 1f;
            if (ScopeBlend > 0f && weaponItem.hasScope)
                recoilMultiplier = Mathf.Lerp(1f, weaponItem.recoilMultiplierWhenScoping, ScopeBlend);

            CurrentRecoilVertical += weaponItem.recoilVertical * recoilMultiplier;
            CurrentRecoilVertical = Mathf.Min(CurrentRecoilVertical, weaponItem.maxRecoil);

            CurrentRecoilHorizontal += UnityEngine.Random.Range(-weaponItem.recoilHorizontal, weaponItem.recoilHorizontal) * recoilMultiplier;
            CurrentRecoilHorizontal = Mathf.Clamp(CurrentRecoilHorizontal, -weaponItem.maxRecoil, weaponItem.maxRecoil);

            CurrentRecoilKickback += weaponItem.recoilKickback * recoilMultiplier;
            CurrentRecoilKickback = Mathf.Min(CurrentRecoilKickback, weaponItem.maxKickback);

            CurrentRecoilRotationX += weaponItem.recoilVertical * recoilMultiplier;
            CurrentRecoilRotationX = Mathf.Min(CurrentRecoilRotationX, weaponItem.maxRecoil);
        }
        
        public float Server_GetRecoilSpreadMultiplier()
        {
            WeaponItem weaponItem = null;
            var selectedItem = Inventory?.GetSelectedItem();
            if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item) && itemRegistry)
                weaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);
 
            if (!weaponItem || CurrentRecoilVertical <= 0f)
                return 1f;

            var recoilRatio = CurrentRecoilVertical / Mathf.Max(1f, weaponItem.maxRecoil);
            return 1f + (weaponItem.recoilSpreadMultiplier - 1f) * recoilRatio;
        }
        
        public float GetScopeBlend()
        {
            return ScopeBlend;
        }

        public Vector2 GetCameraRecoilOffset()
        {
            if (IsLocalOwned)
                return new Vector2(_smoothedCameraRecoilHorizontal, _smoothedCameraRecoilVertical);
            
            return SyncedRecoilOffset;
        }

        public void Client_PlayReloadAnimation(string reloadClipName, float reloadClipLength, float reloadTime)
        {
            if (!animator)
                return;
  
            if (string.IsNullOrEmpty(reloadClipName)) 
                return;
            
            var weaponItem = itemRegistry.GetEntry<WeaponItem>(Inventory.GetSelectedItem().item);
            if (_currentReloadSound != null)
            {
                _currentReloadSound.Cancel();
                _currentReloadSound = null;
            }

            var weaponTransform = CurrentVisualItem != null ? CurrentVisualItem.transform : transform;
            _currentReloadSound = AudioSystem.Instance.PlayAudio(weaponItem.reloadSoundKey, 1f, AudioCategory.Gameplay);
            _currentReloadSound.Follow(weaponTransform);
            
            const float crossFadeTime = 0.25f;
            var effectiveReloadTime = Mathf.Max(0.001f, reloadTime - crossFadeTime);
            var animationSpeed = reloadClipLength > 0f ? reloadClipLength / effectiveReloadTime : 1f;
            
            animator.speed = animationSpeed;
            animator.CrossFade(reloadClipName, crossFadeTime, 1);
        }

        protected virtual void UpdateIKTargets()
        {
        }

        private void UpdateRightHandIKTarget()
        {
            Transform foundRightTarget = null;
            if (CurrentVisualItem && !string.IsNullOrEmpty(rightHandIKTargetName))
                foundRightTarget = CurrentVisualItem.transform.Find(rightHandIKTargetName);
   
            if (rightHandIKRig)
            {
                var hasValidTargets = foundRightTarget && leftHandIKTarget && rightHandIKTarget;
                var baseWeight = hasValidTargets ? _armsIKWeight : 0f;
                var scopedWeight = Mathf.Lerp(1f, 0.3f, ScopeBlend);
                rightHandIKRig.weight = baseWeight * scopedWeight;
            }
            
            if (foundRightTarget && leftHandIKTarget && rightHandIKTarget && CurrentVisualItem)
            {
                var leftPos = leftHandIKTarget.position;
                if (float.IsNaN(leftPos.x) || float.IsNaN(leftPos.y) || float.IsNaN(leftPos.z))
                    return;
                
                var localRightPosition = foundRightTarget.localPosition;
                var localRightRotation = foundRightTarget.localRotation;
                
                if (float.IsNaN(localRightPosition.x) || float.IsNaN(localRightPosition.y) || float.IsNaN(localRightPosition.z))
                    return;
                
                var visualItemTransform = CurrentVisualItem.transform;
                var worldRightPosition = visualItemTransform.TransformPoint(localRightPosition);
                
                if (float.IsNaN(worldRightPosition.x) || float.IsNaN(worldRightPosition.y) || float.IsNaN(worldRightPosition.z))
                    return;
                
                rightHandIKTarget.position = worldRightPosition;
                var worldRightRotation = visualItemTransform.rotation * localRightRotation;
                rightHandIKTarget.rotation = worldRightRotation;
            }
        }

        private void OnViewDirectionChanged(Vector2 _)
        {
        }

        private void OnPositionChanged(Vector3 next)
        {
            if (!IsLocalOwned && next != Vector3.zero)
                transform.position = next;
        }

        private void OnVelocityChanged(Vector3 _)
        {
        }

        private void OnAimingChanged(bool next)
        {
            if (IsLocalOwned)
                return;
            
            ApplyAimingState(next);
        }

        private void OnSneakingChanged(bool _)
        {
        }

        private void OnHealthChanged(float health, float maxHealth)
        {
            OnHealthChangeEvent?.Invoke(health, maxHealth);
        }

        private void Server_SetHealth(float health)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetHealth can only be called on server side");
            
            Server_SetHealthInternal(health);
        }

        private void Server_SetMaxHealth(float maxHealth)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetMaxHealth can only be called on server side");
            
            Server_SetMaxHealthInternal(maxHealth);
        }

        private void Server_TakeDamage(float damage)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_TakeDamage can only be called on server side");
            
            var newHealth = Mathf.Max(0f, CurrentHealth - damage);
            Server_SetHealth(newHealth);
        }
        
        public void Server_SetReloadProgress(float progress)
        {
             if (!IsServerSide)
                throw new InvalidOperationException("Server_SetReloadProgress can only be called on server side");
             
             Server_SetReloadProgressInternal(progress);
        }
        
        internal void _CancelReloadSound()
        {
            if (_currentReloadSound != null)
            {
                _currentReloadSound.Cancel();
                _currentReloadSound = null;
            }
        }

        public override void GetCameraView(out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            position = transform.position + Vector3.up * headHeight;
            rotation = Quaternion.Euler(ViewDirection.y, ViewDirection.x, 0f);
            fieldOfView = 60f;
        }
        
        #region IWeaponUser Implementation
        public event Action<IWeaponUser, Vector3, Vector3> OnClientSpawnShotEffects;
        public event Action<IWeaponUser, Vector3, Vector3> OnRequestToShootEvent;
        public event Action<IWeaponUser> OnRequestToReloadEvent;
        public event Action<IWeaponUser, Vector3, Vector3, bool> OnRequestToThrowGadgetEvent;
        
        public WeaponState WeaponState => _weaponState;
        bool IWeaponUser.IsLocalOwned => IsLocalOwned;
        bool IWeaponUser.IsServerSide => IsServerSide;
        Uuid IWeaponUser.UserUuid => EntityUuid;
        bool IWeaponUser.IsValidAndSpawned => IsSpawned;

        public Vector3 GetCurrentWeaponExit()
        {
            if (!CurrentVisualItem)
                return transform.position;
                
            var bulletExit = CurrentVisualItem.transform.Find(bulletExitName);
            return !bulletExit ? transform.position : bulletExit.position;
        }
        public void RaiseClientSpawnShotEffects(Vector3 weaponExit, Vector3 hitPoint)
        {
            OnClientSpawnShotEffects?.Invoke(this, weaponExit, hitPoint);
        }
        #endregion
                    
        #region IInventory Implementation
        public event Action<Inventory> OnInventoryChangeEvent;
        public Inventory Inventory { get; private set; }
        #endregion

        #region IHealth Implementation

        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public event Action<float, float> OnHealthChangeEvent;
        public event Action OnDeathEvent;

        float IHealth.CurrentHealth => CurrentHealth;
        float IHealth.MaxHealth => MaxHealth;

        event Action<float, float> IHealth.OnHealthChangeEvent
        {
            add => OnHealthChangeEvent += value;
            remove => OnHealthChangeEvent -= value;
        }

        event Action IHealth.OnDeathEvent
        {
            add => OnDeathEvent += value;
            remove => OnDeathEvent -= value;
        }

        void IHealth.Server_SetHealth(float health)
        {
            Server_SetHealth(health);
        }

        void IHealth.Server_SetMaxHealth(float maxHealth)
        {
            Server_SetMaxHealth(maxHealth);
        }

        void IHealth.Server_TakeDamage(float damage)
        {
            Server_TakeDamage(damage);
        }

        #endregion

        #region Inventory Management

        private void OnInventoryChangedHandler(Inventory next)
        {
            // Cancel reload if inventory changed (item or slot change)
            if (IsServerSide && _weaponState.IsReloading)
            {
                _weaponState.IsReloading = false;
                Server_SetReloadProgress(0f);
            }

            // Cancel reload sound when inventory changes
            if (_currentReloadSound != null)
            {
                _currentReloadSound.Cancel();
                _currentReloadSound = null;
            }

            OnInventoryChangeEvent?.Invoke(next);
            UpdateVisualItem();
        }

        public void Server_SetInventory(Inventory inventory)
        {
            if (!IsServerSide)
                throw new InvalidOperationException("Server_SetInventory can only be called on server side");
            Server_SetInventoryInternal(inventory ?? new Inventory());
        }

        private void HandleInventoryInput()
        {
            if (IsWindowOpen())
                return;
            for (var i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    Client_RequestSelectSlot(i);
                    break;
                }
            }
        }

        private void HandleWeaponInput()
        {
            if (IsWindowOpen())
                return;
            // Check if weapon is automatic
            var isAutomatic = false;
            var selectedItem = Inventory?.GetSelectedItem();
            if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item) && itemRegistry != null)
            {
                var weaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);
                if (weaponItem != null)
                {
                    isAutomatic = weaponItem.automatic;
                }
            }

            // For automatic weapons: fire while holding button. For semi-automatic: fire only on button down
            var shouldFire = isAutomatic ? Input.GetButton("Fire1") : Input.GetButtonDown("Fire1");
            
            if (shouldFire)
            {
                if (requireAimToShoot && !IsAimReadyInternal())
                    return;

                // Get BulletExit position after IK resolution (position is now correct)
                _GetShootingOriginAndDirection(out var origin, out var direction);
                
                if (IsServerSide)
                {
                OnRequestToShootEvent?.Invoke(this, origin, direction);
                }
                else
                {
                     // Send RPC to server
                     Client_Shoot(origin, direction);
                     // Invoke locally for prediction?
                     OnRequestToShootEvent?.Invoke(this, origin, direction);
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (IsServerSide)
            {
                OnRequestToReloadEvent?.Invoke(this);
            }
                else
                {
                     Client_Reload();
                     OnRequestToReloadEvent?.Invoke(this);
                }
            }
        }
        
        // Client methods that internally call RPCs
        public void Client_Shoot(Vector3 origin, Vector3 direction)
        {
            if (!IsClientSide)
                throw new InvalidOperationException("Client_Shoot can only be called on client side");
            if (!IsLocalOwned)
                throw new InvalidOperationException("Client_Shoot can only be called for locally owned player");
            RpcClient_Shoot(origin, direction);
        }

        public void Client_Reload()
        {
            if (!IsClientSide)
                throw new InvalidOperationException("Client_Reload can only be called on client side");
            if (!IsLocalOwned)
                throw new InvalidOperationException("Client_Reload can only be called for locally owned player");
            RpcClient_Reload();
        }

        public void Client_ThrowGadget(Vector3 origin, Vector3 direction, bool isDrop)
        {
            if (!IsClientSide)
                throw new InvalidOperationException("Client_ThrowGadget can only be called on client side");
            if (!IsLocalOwned)
                throw new InvalidOperationException("Client_ThrowGadget can only be called for locally owned player");
            RpcClient_ThrowGadget(origin, direction, isDrop);
        }
        
        // Private implementations to update state from client and call RPC
        // Client_Update methods now only update local state
        // State syncing is handled by tick-based PlayerState system
        private void Client_UpdateViewDirection(float yaw, float pitch)
        {
            if (!IsClientSide || !IsLocalOwned)
                return;
            
            var value = new Vector2(yaw, pitch);
            _viewDirection = value;
            OnViewDirectionChangeEvent?.Invoke(value);
            // State is synced via tick-based PlayerState system
        }

        private void Client_UpdateVelocity(Vector3 velocity)
        {
            if (!IsClientSide || !IsLocalOwned)
                return;
            
            InternalVelocity = velocity;
            OnVelocityChangeEvent?.Invoke(velocity);
            OnVelocityChanged(velocity);
            // State is synced via tick-based PlayerState system
        }
        
        private void Client_UpdateAiming(bool isAiming)
        {
            if (!IsClientSide || !IsLocalOwned)
                return;
            
            _isAiming = isAiming;
            OnIsAimingChangeEvent?.Invoke(isAiming);
            OnAimingChanged(isAiming);
            // State is synced via tick-based PlayerState system
        }

        private void Client_UpdateScoping(bool isScoped)
        {
            if (!IsClientSide || !IsLocalOwned)
                return;
            
            IsScoped = isScoped;
            // State is synced via tick-based PlayerState system
        }
        
        private void Client_UpdateFiring(bool isFiring)
        {
            if (!IsClientSide || !IsLocalOwned)
                return;
            
            _isFiring = isFiring;
            OnIsFiringChangeEvent?.Invoke(isFiring);
            // State is synced via tick-based PlayerState system
        }
        
        private void Client_UpdateSneaking(bool isSneaking)
        {
            if (!IsClientSide || !IsLocalOwned)
                return;
            
            _isSneaking = isSneaking;
            OnIsSneakingChangeEvent?.Invoke(isSneaking);
            OnSneakingChanged(isSneaking);
            // State is synced via tick-based PlayerState system
        }
        
        private void Client_RequestSelectSlot(int slotIndex)
        {
            if (!IsClientSide)
                throw new InvalidOperationException("Client_RequestSelectSlot can only be called on client side");
            if (!IsLocalOwned)
                throw new InvalidOperationException("Client_RequestSelectSlot can only be called for locally owned player");
            RpcClient_SelectSlot(slotIndex);
        }

        private void _GetShootingOriginAndDirection(out Vector3 origin, out Vector3 direction)
        {
            // Get BulletExit position by manually transforming it like UpdateRightHandIKTarget does
            if (CurrentVisualItem != null)
            {
                var bulletExit = CurrentVisualItem.transform.Find(bulletExitName);
                if (bulletExit != null)
                {
                    // Get local position and transform it to world space using the visual item's transform
                    var localBulletExitPosition = bulletExit.localPosition;
                    var visualItemTransform = CurrentVisualItem.transform;
                    origin = visualItemTransform.TransformPoint(localBulletExitPosition);
                }
                else
                {
                    origin = transform.position;
                }
            }
            else
            {
                origin = transform.position;
            }
            
            // Get direction from camera view
            if (this is ThirdPersonPlayerEntity)
            {
                // For third person: cast ray from camera, if it hits something beyond the player, aim at that point
                GetCameraView(out var cameraPosition, out var cameraRotation, out _);
                var cameraForward = cameraRotation * Vector3.forward;
                
                // Cast ray from camera
                var ray = new Ray(cameraPosition, cameraForward);
                if (Physics.Raycast(ray, out var hit, float.MaxValue))
                {
                    var hitPoint = hit.point;
                    var playerPosition = transform.position;
                    
                    // Check if hit point is between camera and player
                    var cameraToPlayer = playerPosition - cameraPosition;
                    var cameraToHit = hitPoint - cameraPosition;
                    
                    // If hit is closer than player, ignore it (something blocking between camera and player)
                    if (cameraToHit.magnitude < cameraToPlayer.magnitude)
                    {
                        // Use camera forward direction as-is
                        direction = cameraForward;
                    }
                    else
                    {
                        // Hit is beyond player - aim from player towards hit point
                        direction = (hitPoint - origin).normalized;
                    }
                }
                else
                {
                    // No hit - use camera forward
                    direction = cameraForward;
                }
            }
            else
            {
                // For first person, use camera view directly (accounts for recoil)
                GetCameraView(out _, out var rotation, out _);
                direction = rotation * Vector3.forward;
            }
        }

        public void UpdateVisualItem()
        {
            
            if (CurrentVisualItem != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(CurrentVisualItem);
                }
                else
                {
                    DestroyImmediate(CurrentVisualItem);
                }
                CurrentVisualItem = null;
            }

            var inventory = Inventory;
            if (inventory == null)
                return;

            var selectedItemStack = inventory.GetSelectedItem();
            if (selectedItemStack == null || string.IsNullOrEmpty(selectedItemStack.item))
                return;

            
            if (itemRegistry == null)
                return;

            var item = itemRegistry.GetEntry<Item>(selectedItemStack.item);
            if (item == null)
                return;

            GameObject prefabToUse = null;

            if (item.visualPrefab != null)
            {
                prefabToUse = item.visualPrefab;
            }

            if (prefabToUse == null)
                return;

            
            if (heldItemParent == null)
                heldItemParent = transform;

            CurrentVisualItem = Instantiate(prefabToUse, heldItemParent);
            CurrentVisualItem.transform.localPosition = Vector3.zero;
            CurrentVisualItem.transform.localRotation = Quaternion.identity;
        }
        
        private void ApplyAimingState(bool isAiming)
        {
            if (isAiming)
                _aimReadyAt = Time.time + Mathf.Max(0f, aimInTime);
            else
                _aimReadyAt = float.PositiveInfinity;
        }
        #endregion
    }
}
