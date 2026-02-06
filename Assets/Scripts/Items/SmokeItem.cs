using UnityEngine;

namespace TUA.Items
{
    [CreateAssetMenu(menuName = "TUA/SmokeItem", fileName = "NewSmokeItem")]
    public class SmokeItem : GadgetItem
    {
        #region Serialized Fields
        [Header("Smoke Settings")]
        [Tooltip("Smoke cloud radius in Unity units.")]
        public float smokeRadius = 8f;
        [Tooltip("Smoke duration in seconds.")]
        public float smokeDuration = 15f;
        [Tooltip("Fuse time in seconds before smoke starts.")]
        public float smokeFuseTime = 1f;
        #endregion
    }
}
