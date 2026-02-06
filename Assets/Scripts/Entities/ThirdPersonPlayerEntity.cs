using UnityEngine;

namespace TUA.Entities
{
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonPlayerEntity : PlayerEntity
    {
        [Header("Third Person")]
        public float bodyRotationSpeed = 5f;

        [Header("Third Person Camera")]
        [Tooltip("Distance from the shoulder pivot to the camera.")]
        public float cameraDistance = 5f;
        [Tooltip("Minimum allowed camera distance from the shoulder pivot.")]
        public float minCameraDistance = 1.2f;
        [Tooltip("Maximum allowed camera distance from the shoulder pivot.")]
        public float maxCameraDistance = 5f;
        [Header("Third Person Camera Collision")]
        [Tooltip("If > 0, uses a spherecast so the camera doesn't clip through obstacles.")]
        public float cameraCollisionRadius = 0.25f;
        [Tooltip("Small buffer to keep camera slightly away from surfaces.")]
        public float cameraCollisionBuffer = 0.08f;
        [Tooltip("Layers which block the third-person camera.")]
        public LayerMask cameraCollisionMask = ~0;
        [Tooltip("Base height of the shoulder pivot.")]
        public float cameraHeight = 2f;
        [Tooltip("Shoulder pivot offset in local space (x=right shoulder, y=extra height, z=forward).")]
        public Vector3 shoulderOffset = new Vector3(0.45f, 0f, 0.15f);

        protected override void HandleMovement(float deltaTime)
        {
            base.HandleMovement(deltaTime);

            if (!IsLocalOwned || !CharacterController)
                return;

            // When window is open, ignore input but continue applying movement (player keeps falling/moving)
            Vector3 input;
            if (IsWindowOpen())
            {
                // No input when paused - player continues with current velocity
                input = Vector3.zero;
            }
            else
            {
                // Read input normally when not paused
                var moveX = Input.GetAxis("Horizontal");
                var moveZ = Input.GetAxis("Vertical");
                input = new Vector3(moveX, 0f, moveZ);
                if (input.sqrMagnitude > 1f)
                    input.Normalize();
            }
            
            // Continue rotating body even when paused (for visual consistency)
            var yaw = ViewDirection.x;
            var targetRot = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, deltaTime * bodyRotationSpeed);

            var currentHorizontal = new Vector3(Velocity.x, 0f, Velocity.z);
            Vector3 horizontal;

            // Normal movement when not paused
            var speed = GetCurrentMoveSpeed();
            var desiredHorizontal = (transform.forward * input.z + transform.right * input.x) * speed;

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

            // Always apply movement - player continues falling/moving even when paused
            CharacterController.Move(move * deltaTime);
            Velocity = move;
        }

        public override void GetCameraView(out Vector3 position, out Quaternion rotation, out float fieldOfView)
        {
            var viewDir = ViewDirection;
            var yaw = viewDir.x;
            var pitch = viewDir.y;

            var recoilOffset = GetCameraRecoilOffset();
            pitch -= recoilOffset.y;
            yaw += recoilOffset.x;
            
            var yawRot = Quaternion.Euler(0f, yaw, 0f);
            var pivotLocal = new Vector3(shoulderOffset.x, cameraHeight + shoulderOffset.y, shoulderOffset.z);
            var pivotWorld = transform.position + yawRot * pivotLocal;

            var direction = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

            var desiredDistance = Mathf.Clamp(cameraDistance, minCameraDistance, maxCameraDistance);
            var backDir = -direction.normalized;
            var distance = desiredDistance;

            if (cameraCollisionRadius > 0f)
            {
                var castDistance = desiredDistance + Mathf.Max(0f, cameraCollisionBuffer);
                if (Physics.SphereCast(
                        pivotWorld,
                        cameraCollisionRadius,
                        backDir,
                        out var hit,
                        castDistance,
                        cameraCollisionMask,
                        QueryTriggerInteraction.Ignore))
                {
                    distance = Mathf.Clamp(hit.distance - Mathf.Max(0f, cameraCollisionBuffer), 0.1f, desiredDistance);
                }
            }

            position = pivotWorld + backDir * distance;
            rotation = Quaternion.LookRotation(direction, Vector3.up);
            fieldOfView = 60f;
        }
    }
}
