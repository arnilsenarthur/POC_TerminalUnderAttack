using UnityEngine;

namespace TUA.Items
{
    [CreateAssetMenu(menuName = "TUA/FlashItem", fileName = "NewFlashItem")]
    public class FlashItem : GadgetItem
    {
        #region Serialized Fields
        [Header("Flash Settings")]
        [Tooltip("Blind duration in seconds for players looking at flash.")]
        public float blindDuration = 3f;
        [Tooltip("Maximum angle in degrees from flash direction that players can be blinded.")]
        public float blindAngle = 45f;
        [Tooltip("Flash radius in Unity units.")]
        public float flashRadius = 10f;
        [Tooltip("Fuse time in seconds before flash.")]
        public float flashFuseTime = 1f;
        #endregion
    }
}
