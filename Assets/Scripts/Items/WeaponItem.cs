using TUA.Core;
using TUA.Core.Interfaces;
using UnityEngine;

namespace TUA.Items
{
    [CreateAssetMenu(menuName = "TUA/WeaponItem", fileName = "NewWeaponItem")]
    public class WeaponItem : Item
    {
        [Header("Aiming")]
        public bool aimable = true;
        [Tooltip("If true, weapon always aims when selected, and right mouse button activates scope.")]
        public bool hasScope;
        
        [Header("Scope Settings")]
        [Tooltip("Position relative to camera when scoped. This will be transformed by camera to fix the gun relative to camera.")]
        public Vector3 scopeTargetPosition = new(0f, -0.1f, 0.5f);
        [Tooltip("Field of view when scoped. Lower values = more zoom.")]
        public float scopeFOV = 30f;
        [Tooltip("Spread angle multiplier when scoped. 1.0 = normal spread, 0.5 = half spread (more accurate), 0.0 = no spread.")]
        public float spreadAngleWhenScoping = 0.5f;
        [Tooltip("Recoil multiplier when scoped. 1.0 = normal recoil, 0.5 = half recoil (more stable), 0.0 = no recoil.")]
        public float recoilMultiplierWhenScoping = 0.5f;

        [Header("Combat Stats")]
        [Tooltip("Damage dealt per hit (damage points).")]
        public float damage;
        [Tooltip("Maximum range in Unity units (meters).")]
        public float range;
        [Tooltip("Time between shots in seconds.")]
        public float fireRate;
        [Tooltip("If true, weapon will fire continuously while holding fire button. If false, fires once per button press (semi-automatic).")]
        public bool automatic;
        [Tooltip("Damage multiplier for headshots. Default is 1.5 (50% bonus damage).")]
        public float headshotDamageMultiplier = 1.5f;

        [Header("Ammunition")]
        public int defaultAmmo;
        public int maxAmmo;
        public int magazineSize;
        [Tooltip("Reload time in seconds.")]
        public float reloadTime;
        [Tooltip("Animation clip name to play when reloading.")]
        public string reloadClipName;
        [Tooltip("Duration of the reload animation clip in seconds. Used to calculate animation speed.")]
        public float reloadClipLength = 1f;

        [Header("Audio")]
        [Tooltip("Sound key for weapon shot sound. Played on client when shooting.")]
        public string shotSoundKey;
        [Tooltip("Sound key for weapon reload sound. Played on client when reloading.")]
        public string reloadSoundKey;

        [Header("Accuracy")]
        [Tooltip("Firing error multiplier in degrees per (Unity unit/second). Multiplied by movement velocity magnitude to add inaccuracy. Applies to all weapons. Example: 0.5 means 0.5 degrees of error per unit/second of movement speed.")]
        public float firingErrorMultiplier;

        [Header("Multi-Projectile / Shotgun")]
        [Tooltip("Number of projectiles per shot. 1 = single projectile (normal weapon), >1 = shotgun spread.")]
        public int projectileCount = 1;
        [Tooltip("Spread angle in degrees for multi-projectile weapons (shotguns).")]
        public float spreadAngle;

        [Header("Recoil")]
        [Tooltip("Vertical recoil force in degrees. How much the weapon kicks upward per shot.")]
        public float recoilVertical = 1f;
        [Tooltip("Horizontal recoil force in degrees. Random horizontal kick per shot.")]
        public float recoilHorizontal = 0.3f;
        [Tooltip("Kickback distance in Unity units. How far the weapon moves backward (Z axis) per shot.")]
        public float recoilKickback = 0.02f;
        [Tooltip("Maximum kickback distance in Unity units. Prevents weapon from going inside the player.")]
        public float maxKickback = 0.15f;
        [Tooltip("Recoil recovery speed per second. Higher values = faster recovery.")]
        public float recoilRecoverySpeed = 5f;
        [Tooltip("Maximum recoil accumulation before recovery starts. Prevents infinite recoil buildup.")]
        public float maxRecoil = 10f;
        [Tooltip("Spread multiplier while in recoil. 1.0 = normal spread, 2.0 = double spread during recoil.")]
        public float recoilSpreadMultiplier = 1.5f;

        public override ItemStack GetDefaultItemStack()
        {
            return new WeaponItemStack
            {
                item = Id,
                ammo = defaultAmmo,
                maxAmmo = maxAmmo,
            };
        }

        public override bool IsAimable(IInventoryHolder holder, ItemStack stack) => aimable;
    }
}