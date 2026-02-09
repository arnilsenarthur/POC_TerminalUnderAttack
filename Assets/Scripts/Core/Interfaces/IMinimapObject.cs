using UnityEngine;

namespace TUA.Core.Interfaces
{
    public interface IMinimapObject
    {
        bool GetMinimapTarget(out Vector3 worldPos, out Color color, out string sprite, out float scale, out float rotationY);
    }
}
