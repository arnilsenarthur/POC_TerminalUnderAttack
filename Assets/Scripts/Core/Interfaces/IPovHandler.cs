using UnityEngine;

namespace TUA.Core.Interfaces
{
    public interface IPovHandler
    {
        void GetCameraView(out Vector3 position, out Quaternion rotation, out float fieldOfView);
    }
}
