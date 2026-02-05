using TUA.Core;
using TUA.Core.Interfaces;
using TUA.Entities;
using TUA.Misc;
using UnityEngine;

namespace TUA.Systems
{
    public class CameraSystem : SingletonBehaviour<CameraSystem>
    {
        #region Serialized Fields
        public Camera mainCamera;
        #endregion
        #region Unity Callbacks
        public void LateUpdate()
        {
            if (GameWorld.Instance == null)
                return;
            var targetEntity = GameWorld.Instance.GetTargetEntity<Entity>();
            if (targetEntity == null)
                return;
            if (targetEntity is not IPovHandler povHandler)
                return;
            povHandler.GetCameraView(out var position, out var rotation, out var fieldOfView);
            mainCamera.transform.position = position;
            mainCamera.transform.rotation = rotation;
            mainCamera.fieldOfView = fieldOfView;
        }
        #endregion
    }
}
