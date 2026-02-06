using TUA.Core;
using TUA.Core.Interfaces;
using UnityEngine;

namespace TUA.Items
{
    public abstract class GadgetItem : Item
    {
        #region Serialized Fields
        [Header("Throwing")]
        [Tooltip("Initial throw velocity in Unity units/second.")]
        public float throwVelocity = 10f;
        [Tooltip("Drop force when throwing with right button in Unity units/second.")]
        public float dropForce = 2f;

        [Header("Visual/Audio")]
        [Tooltip("Prefab spawned when gadget explodes/flashes/smokes.")]
        public GameObject effectPrefab;
        [Tooltip("Sound key for throw sound.")]
        public string throwSoundKey;
        [Tooltip("Sound key for impact sound when hitting wall/ground.")]
        public string impactSoundKey;
        [Tooltip("Sound key for explosion/flash/smoke sound.")]
        public string effectSoundKey;
        #endregion

        #region Public Methods
        public override ItemStack GetDefaultItemStack()
        {
            return new GadgetItemStack
            {
                item = Id,
                count = 5,
            };
        }

        public override bool IsAimable(IInventoryHolder holder, ItemStack stack) => false;
        public override bool DisplayAsAimingWhenHeld(IInventoryHolder holder, ItemStack stack) => true;
        #endregion
    }
}
