using TUA.Core;
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
            if (!GameWorld.Instance)
                return;
            
            var targetEntity = GameWorld.Instance.GetTargetEntity<Entity>();
            
            if (!targetEntity)
                return;
            
            targetEntity.GetCameraView(out var position, out var rotation, out var fieldOfView);
            mainCamera.transform.position = position;
            mainCamera.transform.rotation = rotation;
            mainCamera.fieldOfView = fieldOfView;
        }
        #endregion
    }
}
