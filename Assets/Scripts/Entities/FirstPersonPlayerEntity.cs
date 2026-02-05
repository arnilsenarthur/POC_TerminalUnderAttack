using TUA.Core;
using TUA.Items;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace TUA.Entities
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonPlayerEntity : PlayerEntity
    {
        [Header("First Person Fix")]
        public Transform baseObject;
        public Vector3 baseObjectOffset = new(0f, 0f, -0.5f);
        [Tooltip("Offset for left hand IK target relative to camera (respects camera position and rotation). First-person only.")]
        public Vector3 leftHandTargetCameraOffset = Vector3.zero;

        public MultiAimConstraint bodyIkRig;
        private Transform _originalVisualItemParent;
        private Vector3 _originalLocalPosition;
        private Quaternion _originalLocalRotation;
        private bool _isVisualItemReparented;

        protected override void HandleMovement(float deltaTime)
        {
            base.HandleMovement(deltaTime);

            if (!IsLocalOwned || !CharacterController)
                return;

            var moveX = Input.GetAxis("Horizontal");
            var moveZ = Input.GetAxis("Vertical");

            var input = new Vector3(moveX, 0f, moveZ);
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            var speed = GetCurrentMoveSpeed();
            var desiredHorizontal = (transform.right * input.x + transform.forward * input.z) * speed;

            var currentHorizontal = new Vector3(Velocity.x, 0f, Velocity.z);
            Vector3 horizontal;

            if (CharacterController.isGrounded)
                horizontal = desiredHorizontal;
            else
            {
                
                if (input.sqrMagnitude < 0.0001f)
                    horizontal = currentHorizontal;
                else
                {
                    var t = 1f - Mathf.Exp(-10f * Mathf.Clamp01(airControl) * deltaTime);
                    horizontal = Vector3.Lerp(currentHorizontal, desiredHorizontal, t);
                }
            }

            var move = horizontal;
            move.y = Velocity.y;

            CharacterController.Move(move * deltaTime);
            Velocity = move;
        }

        protected override void UpdateIKTargets()
        {
            var targetEntity = GameWorld.Instance.GetTargetEntity<Entity>();
            if(targetEntity && targetEntity == this)
            {
                baseObject.localPosition = baseObjectOffset;
                bodyIkRig.weight = 0f;
                
                GetCameraView(out var cameraPosition, out var cameraRotation, out _);
                var cameraForward = cameraRotation * Vector3.forward;
                
                if (bodyIKTargetReference)
                {
                    var targetPosition = cameraPosition + cameraForward * 1f;
                    bodyIKTargetReference.position = targetPosition;
                    bodyIKTargetReference.rotation = Quaternion.LookRotation(cameraForward) * Quaternion.Euler(0f, -90f, -90f);
                }

                if (!leftHandIKTarget) 
                    return;
                
                var baseTargetPosition = cameraPosition + cameraForward * leftHandReachDistance;
                var offsetInCameraSpace = cameraRotation * leftHandTargetCameraOffset;
                var normalAimPosition = baseTargetPosition + offsetInCameraSpace;
                    
                WeaponItem currentWeaponItem = null;
                var selectedItem = Inventory?.GetSelectedItem();
                if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item) && itemRegistry)
                {
                    currentWeaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);
                }
                    
                var weaponScopePosition = currentWeaponItem && currentWeaponItem.hasScope 
                    ? currentWeaponItem.scopeTargetPosition 
                    : scopeTargetPosition;
                var scopedPosition = cameraPosition + cameraRotation * weaponScopePosition; 
                
                var leftTargetPosition = Vector3.Lerp(normalAimPosition, scopedPosition, ScopeBlend);
                    
                var recoilMultiplier = 1f - (ScopeBlend * 0.5f);
                leftTargetPosition -= cameraForward * CurrentRecoilKickback * recoilMultiplier;
                leftTargetPosition += Vector3.up * CurrentRecoilKickback * 0.5f * recoilMultiplier;
                    
                leftHandIKTarget.position = leftTargetPosition;
                var baseRotation = Quaternion.LookRotation(cameraForward) * Quaternion.Euler(0f, -90f, -90f);
                leftHandIKTarget.rotation = baseRotation;
            }
            else
            {
                bodyIkRig.weight = 1f;
            }
        }

        protected void LateUpdate()
        {
            var targetEntity = GameWorld.Instance.GetTargetEntity<Entity>();
            if(targetEntity && targetEntity == this)
            {
                baseObject.localPosition = baseObjectOffset;
                UpdateVisualItemScopePosition();
            }
        }
        
        private void UpdateVisualItemScopePosition()
        {
            if (!CurrentVisualItem)
                return;
            
            WeaponItem currentWeaponItem = null;
            var currentWeaponHasScope = false;
            var selectedItem = Inventory?.GetSelectedItem();
            if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item) && itemRegistry)
            {
                currentWeaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);
                if (currentWeaponItem)
                    currentWeaponHasScope = currentWeaponItem.hasScope;
            }
            
            if (!currentWeaponHasScope)
            {
                if (_isVisualItemReparented && heldItemParent)
                {
                    CurrentVisualItem.transform.SetParent(_originalVisualItemParent);
                    CurrentVisualItem.transform.localPosition = _originalLocalPosition;
                    CurrentVisualItem.transform.localRotation = _originalLocalRotation;
                    _isVisualItemReparented = false;
                }
                else if (heldItemParent)
                {
                    if (CurrentVisualItem.transform.parent != heldItemParent)
                        CurrentVisualItem.transform.SetParent(heldItemParent);
                    
                    CurrentVisualItem.transform.localPosition = Vector3.zero;
                    CurrentVisualItem.transform.localRotation = Quaternion.identity;
                }
                return;
            }
            
            if (ScopeBlend <= 0f)
            {
                if (_isVisualItemReparented && heldItemParent)
                {
                    CurrentVisualItem.transform.SetParent(_originalVisualItemParent);
                    CurrentVisualItem.transform.localPosition = Vector3.Lerp(CurrentVisualItem.transform.localPosition, _originalLocalPosition, Time.deltaTime * scopeLerpSpeed);
                    CurrentVisualItem.transform.localRotation = Quaternion.Lerp(CurrentVisualItem.transform.localRotation, _originalLocalRotation, Time.deltaTime * scopeLerpSpeed);

                    if (!(Vector3.Distance(CurrentVisualItem.transform.localPosition, _originalLocalPosition) <
                          0.001f) ||
                        !(Quaternion.Angle(CurrentVisualItem.transform.localRotation, _originalLocalRotation) <
                          0.1f)) return;
                    
                    CurrentVisualItem.transform.localPosition = _originalLocalPosition;
                    CurrentVisualItem.transform.localRotation = _originalLocalRotation;
                    _isVisualItemReparented = false;
                }
                else if (heldItemParent)
                {
                    if (CurrentVisualItem.transform.parent != heldItemParent)
                        CurrentVisualItem.transform.SetParent(heldItemParent);
               
                    CurrentVisualItem.transform.localPosition = Vector3.zero;
                    CurrentVisualItem.transform.localRotation = Quaternion.identity;
                }
                return;
            }
            
            if (!_isVisualItemReparented && heldItemParent)
            {
                _originalVisualItemParent = CurrentVisualItem.transform.parent;
                _originalLocalPosition = CurrentVisualItem.transform.localPosition;
                _originalLocalRotation = CurrentVisualItem.transform.localRotation;
                
                CurrentVisualItem.transform.SetParent(null);
                _isVisualItemReparented = true;
            }
            
            GetCameraView(out var cameraPosition, out var cameraRotation, out _);
            
            var weaponScopePosition = currentWeaponItem && currentWeaponItem.hasScope 
                ? currentWeaponItem.scopeTargetPosition 
                : scopeTargetPosition;
            
            var scopedWorldPosition = cameraPosition + cameraRotation * weaponScopePosition;
            
            var originalWorldPos = _originalVisualItemParent 
                ? _originalVisualItemParent.TransformPoint(_originalLocalPosition) 
                : transform.position;
            var originalWorldRot = _originalVisualItemParent 
                ? _originalVisualItemParent.rotation * _originalLocalRotation 
                : transform.rotation;
            
            var localLeftRotation = Quaternion.Euler(0f, 0f, 90f);
            var scopedWorldRotation = cameraRotation * localLeftRotation;
            
            var blendedPosition = Vector3.Lerp(originalWorldPos, scopedWorldPosition, ScopeBlend);
            var blendedRotation = Quaternion.Lerp(originalWorldRot, scopedWorldRotation, ScopeBlend);
            
            CurrentVisualItem.transform.position = blendedPosition;
            CurrentVisualItem.transform.rotation = blendedRotation;
        }

        protected override void HandleLook(float deltaTime)
        {
            base.HandleLook(deltaTime);
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            var yaw = ViewDirection.x;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public override void GetCameraView(out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            var viewDir = ViewDirection;
            var yaw = viewDir.x;
            var pitch = viewDir.y;

            var recoilOffset = GetCameraRecoilOffset();
            pitch -= recoilOffset.y;
            yaw += recoilOffset.x;

            position = transform.position + Vector3.up * headHeight;
            rotation = Quaternion.Euler(pitch, yaw, 0f);
            
            var defaultFOV = 60f;
            if (ScopeBlend > 0f && CurrentVisualItem)
            {
                var selectedItem = Inventory?.GetSelectedItem();
                if (selectedItem != null && !string.IsNullOrEmpty(selectedItem.item) && itemRegistry != null)
                {
                    var weaponItem = itemRegistry.GetEntry<WeaponItem>(selectedItem.item);
                    if (weaponItem && weaponItem.hasScope)
                    {
                        fieldOfView = Mathf.Lerp(defaultFOV, weaponItem.scopeFOV, ScopeBlend);
                        return;
                    }
                }
            }
            
            fieldOfView = defaultFOV;
        }
    }
}
