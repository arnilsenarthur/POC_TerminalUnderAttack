using TUA.Core;
using TUA.Misc;
using TUA.Settings;
using TUA.Windowing;
using UnityEngine;

namespace TUA.Systems
{
    public class CameraSystem : SingletonBehaviour<CameraSystem>
    {
        #region Constants
        public const float CAMERA_SPEED_MULTIPLIER = 10f;
        #endregion

        #region Serialized Fields
        public Camera mainCamera;

        [Header("Look Settings")]
        [SerializeField] private SettingsAsset settings;
        [SerializeField] private string cameraSensitivityXKey = "cameraSensitivityX";
        [SerializeField] private string cameraSensitivityYKey = "cameraSensitivityY";
        [SerializeField] private string cameraInvertedXKey = "cameraInvertedX";
        [SerializeField] private string cameraInvertedYKey = "cameraInvertedY";
        [SerializeField] private float rotationSpeed = 100f;
        #endregion

        #region Fields
        private Vector2 _freeCameraRotation;
        #endregion

        #region Unity Callbacks
        public void LateUpdate()
        {
            if (!mainCamera)
                return;

            if (!GameWorld.Instance)
            {
                _HandleFreeCameraRotation();
                return;
            }

            var targetEntity = GameWorld.Instance.GetTargetEntity<Entity>();

            if (!targetEntity)
            {
                _HandleFreeCameraRotation();
                return;
            }

            targetEntity.GetCameraView(out var position, out var rotation, out var fieldOfView);

            mainCamera.transform.position = position;
            mainCamera.transform.rotation = rotation;
            mainCamera.fieldOfView = fieldOfView;
        }
        #endregion

        #region Private Methods
        private void _HandleFreeCameraRotation()
        {
            if (WindowManager.HasOpenWindow() || Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
                return;

            var mouseX = Input.GetAxisRaw("Mouse X");
            var mouseY = Input.GetAxisRaw("Mouse Y");

            if (Mathf.Abs(mouseX) < 0.0001f && Mathf.Abs(mouseY) < 0.0001f)
                return;

            var sensX = settings?.GetFloat(cameraSensitivityXKey, 1f) ?? 1f;
            var sensY = settings?.GetFloat(cameraSensitivityYKey, 1f) ?? 1f;
            var invX = settings?.GetBool(cameraInvertedXKey) ?? false;
            var invY = settings?.GetBool(cameraInvertedYKey) ?? false;

            var baseSpeed = rotationSpeed * CAMERA_SPEED_MULTIPLIER;
            var speedX = baseSpeed * sensX;
            var speedY = baseSpeed * sensY;

            var deltaTime = Time.deltaTime;
            var deltaYaw = mouseX * speedX * deltaTime * (invX ? -1f : 1f);
            var deltaPitch = -mouseY * speedY * deltaTime * (invY ? -1f : 1f);

            _freeCameraRotation.x += deltaYaw;
            _freeCameraRotation.y = Mathf.Clamp(_freeCameraRotation.y + deltaPitch, -89f, 89f);

            var yaw = _freeCameraRotation.x;
            var pitch = _freeCameraRotation.y;

            mainCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        #endregion
    }
}
