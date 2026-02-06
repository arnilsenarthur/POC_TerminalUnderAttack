using UnityEngine;

namespace TUA.Items
{
    [CreateAssetMenu(menuName = "TUA/GrenadeItem", fileName = "NewGrenadeItem")]
    public class GrenadeItem : GadgetItem
    {
        #region Serialized Fields
        [Header("Grenade Settings")]
        [Tooltip("Explosion damage radius in Unity units.")]
        public float explosionRadius = 5f;
        [Tooltip("Damage dealt at center of explosion.")]
        public float explosionDamage = 100f;
        [Tooltip("Damage falloff curve. X-axis is normalized distance (0 = center, 1 = edge). Y-axis is damage multiplier.")]
        public AnimationCurve damageFalloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [Tooltip("Explosion force applied to rigidbodies.")]
        public float explosionForce = 500f;
        #endregion
    }
}
