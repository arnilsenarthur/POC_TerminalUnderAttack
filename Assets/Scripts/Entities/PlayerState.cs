using UnityEngine;

namespace TUA.Entities
{
    [System.Serializable]
    public struct PlayerState
    {
        public Vector2 viewDirection;
        public Vector3 position;
        public Vector3 velocity;
        public bool isAiming;
        public bool isFiring;
        public bool isSneaking;
        public bool isScoped;
        public Vector2 recoilComputed;
        public float reloadProgress;
    }
}
