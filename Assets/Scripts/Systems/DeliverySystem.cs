using TUA.Core;
using TUA.Core.Interfaces;
using TUA.Entities;
using TUA.Items;
using TUA.Misc;
using UnityEngine;

namespace TUA.Systems
{
    public class DeliverySystem : SingletonBehaviour<DeliverySystem>
    {
        #region Serialized Fields
        [Header("Delivery Settings")]
        public Transform deliveryPoint;
        public float radius = 5f;
        public HackerToolItem hackerToolItem;
        #endregion
        
        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            GameWorld.OnTickEvent += _OnTick;
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            GameWorld.OnTickEvent -= _OnTick;
        }
        private void OnDrawGizmos()
        {
            if (!deliveryPoint)
                return;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(deliveryPoint.position, radius);
        }
        #endregion
        
        #region Methods
        private void _OnTick(float deltaTime)
        {
            if (!GameWorld.Instance.IsServerSide)
                return;
            
            if (!deliveryPoint || !hackerToolItem)
                return;
            
            if (!GameWorld.Instance || !GameWorld.Instance.IsServerInitialized)
                return;
            
            var allEntities = GameWorld.Instance.GetEntities<Entity>();
            foreach (var entity in allEntities)
            {
                if (!entity)
                    continue;
                
                var inventoryHolder = entity as IInventoryHolder;
                if (inventoryHolder == null)
                    continue;
                
                var distance = Vector3.Distance(entity.transform.position, deliveryPoint.position);
                if (distance > radius)
                    continue;
                
                _Server_ReplaceDataDriveWithHackerTool(inventoryHolder);
            }
        }
        
        private void _Server_ReplaceDataDriveWithHackerTool(IInventoryHolder inventoryHolder)
        {
            if (inventoryHolder == null || !hackerToolItem) 
                return;

            var inventory = inventoryHolder.Inventory;
            if (inventory == null) 
                return;

            var dataDriveSlot = -1;
            DataDriveItemStack dataDriveItem = null;
            
            for (var i = 0; i < inventory.slots.Length; i++)
            {
                if (inventory.slots[i] == null || string.IsNullOrEmpty(inventory.slots[i].item))
                    continue;

                if (inventory.slots[i] is not DataDriveItemStack) 
                    continue;
                
                dataDriveSlot = i;
                dataDriveItem = inventory.slots[i] as DataDriveItemStack;
                break;
            }
            
            if (dataDriveSlot < 0) return;

            var feed = FeedSystem.Instance;
            if (feed != null && inventoryHolder is PlayerEntity playerEntity)
            {
                var color = FeedSystem.GetPlayerColor(playerEntity.GamePlayer);
                var diskColor = FeedSystem.ColorToHex(dataDriveItem!.targetColor);
                var player = $"<color={color}>{playerEntity.GamePlayer.Name}</color>";
                var disk = $"<color={diskColor}>{dataDriveItem.targetName}</color>";
                feed.Server_AddFeedInfoLocalized("feed.delivered_disk", 3f, player, disk);
            }
            
            var newInventory = inventory.Copy();
            newInventory.slots[dataDriveSlot] = new ItemStack
            {
                item = hackerToolItem.Id
            };
            
            inventoryHolder.Server_SetInventory(newInventory);
        }
        #endregion
    }
}
