using System;
using System.Collections.Generic;
using TUA.Core;
using TUA.Entities;
using TUA.Items;
using TUA.Misc;
using UnityEngine;

namespace TUA.Systems
{
    public class HackingSystem : SingletonBehaviour<HackingSystem>
    {
        #region Serialized Fields
        [Header("Hacking Settings")]
        public float hackingSpeed = 0.05f;
        public float unhackingVelocity = 0.02f;
        public float maxDetectionDistance = 10f;
        public LayerMask detectionLayerMask = -1;
        public AnimationCurve distanceMultiplierCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public Registry itemRegistry;
        [Header("Data Drive")]
        public DataDriveItem dataDriveItem;
        #endregion
        
        #region Properties
        public HackingTarget CurrentHackingTarget { get; private set; }
        #endregion

        #region Events
        public Action<HackingTarget> OnTargetHackedEvent;
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
        
        public void Update()
        {
            CurrentHackingTarget = _GetCurrentHackingTarget();
        }
        #endregion
        
        #region Private Methods
        private void _OnTick(float deltaTime)
        {
            if (!GameWorld.Instance || !GameWorld.Instance.IsServerSide)
                return;

            foreach (var target in GameWorld.Instance.GetEntities<HackingTarget>())
            {
                if (!target)
                    continue;
                
                if (target.IsHacked)
                    continue;
                
                var playersLookingAtTarget = _GetPlayersLookingAtTarget(target);
                var isBeingHacked = playersLookingAtTarget.Count > 0 && !target.IsHacked;

                if (target.IsBeingHacked != isBeingHacked)
                    target.Server_SetIsBeingHacked(isBeingHacked);
                
                if (playersLookingAtTarget.Count > 0)
                {
                    var totalHackingSpeed = 0f;
                    foreach (var playerData in playersLookingAtTarget)
                    {
                        var distanceMultiplier = distanceMultiplierCurve.Evaluate(playerData.Distance / maxDetectionDistance);
                        totalHackingSpeed += hackingSpeed * distanceMultiplier;
                    }
                    
                    var newProgress = Mathf.Clamp01(target.HackingProgress + totalHackingSpeed * deltaTime);
                    target.Server_SetHackingProgress(newProgress);
                    
                    if (newProgress >= 1f)
                    {
                    _CallOnTargetHacked(target, playersLookingAtTarget);
                    target.Server_SetIsHacked(true);
                    target.Server_SetIsBeingHacked(false);
                    }
                }
                else
                {
                    var newProgress = Mathf.Clamp01(target.HackingProgress - unhackingVelocity * deltaTime);
                    target.Server_SetHackingProgress(newProgress);
                }
            }
        }
        
        private HackingTarget _GetCurrentHackingTarget()
        {
            if (!Camera.main)
                return null;
            
            var cam = Camera.main;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, maxDetectionDistance, detectionLayerMask))
            {
                var target = hit.collider.GetComponent<HackingTarget>();
                if (target)
                    return target;
            }
            return null;
        }
        
        private List<PlayerHackingData> _GetPlayersLookingAtTarget(HackingTarget target)
        {
            var playersLooking = new List<PlayerHackingData>();
            if (!GameWorld.Instance)
                return playersLooking;

            foreach (var gamePlayer in GameWorld.Instance.AllPlayers)
            {
                if (gamePlayer == null)
                    continue;

                var playerEntities = GameWorld.Instance.GetEntitiesOwnedByPlayer<PlayerEntity>(gamePlayer);
                foreach (var playerEntity in playerEntities)
                {
                    if (!playerEntity)
                        continue;
                    
                    var selectedItem = playerEntity.Inventory?.GetSelectedItem();
                    if (selectedItem == null || string.IsNullOrEmpty(selectedItem.item))
                        continue;
                    
                    Item item = null;
                    if (itemRegistry)
                        item = itemRegistry.GetEntry<Item>(selectedItem.item);
                    
                    if (item is not HackerToolItem)
                        continue;
                    
                    if (!playerEntity.IsAimReady)
                        continue;
                    
                    if (!playerEntity.IsFiring)
                        continue;

                    if (!_IsPlayerLookingAtTarget(playerEntity, target)) 
                        continue;
                    
                    var distance = Vector3.Distance(playerEntity.transform.position, target.transform.position);
                    if (distance <= maxDetectionDistance)
                    {
                        playersLooking.Add(new PlayerHackingData
                        {
                            PlayerEntityUuid = playerEntity.EntityUuid,
                            Distance = distance
                        });
                    }
                }
            }
            return playersLooking;
        }
        
        private bool _IsPlayerLookingAtTarget(PlayerEntity playerEntity, HackingTarget target)
        {
            playerEntity.GetCameraView(out var position, out var rotation, out _);
            var ray = new Ray(position, rotation * Vector3.forward);
            if (!Physics.Raycast(ray, out var hit, maxDetectionDistance, detectionLayerMask)) 
                return false;
            
            return hit.collider.gameObject == target.gameObject;
        }
        
        private void _CallOnTargetHacked(HackingTarget target, List<PlayerHackingData> players)
        {
            if (players == null || players.Count == 0)
                return;
            
            if (FeedSystem.Instance == null)
                return;
            
            if (!GameWorld.Instance)
                return;
            
            var firstPlayer = true;
            foreach (var playerData in players)
            {
                if (!playerData.PlayerEntityUuid.IsValid)
                    continue;
                
                var playerEntity = GameWorld.Instance.GetEntityByUuid<PlayerEntity>(playerData.PlayerEntityUuid);
                if (!playerEntity)
                    continue;
                
                var gamePlayer = playerEntity.GamePlayer;
                if (gamePlayer == null)
                    continue;
                
                var playerName = gamePlayer.Name ?? "Unknown";
                var targetName = target.unlocalizedName ?? "Target";
                var playerColor = FeedSystem.GetPlayerColor(gamePlayer);
                var targetColor = FeedSystem.ColorToHex(target.color);
                var player = $"<color={playerColor}>{playerName}</color>";
                var targetLabel = $"<color={targetColor}>{targetName}</color>";
                FeedSystem.Instance.Server_AddFeedInfoLocalized("feed.hacked", 3f, FeedMessageType.Objective, player, targetLabel);

                if (!firstPlayer || !dataDriveItem) 
                    continue;
                
                _ReplaceHackerToolWithDataDrive(playerEntity, target);
                firstPlayer = false;
            }
            OnTargetHackedEvent?.Invoke(target);
        }

        private void _ReplaceHackerToolWithDataDrive(PlayerEntity playerEntity, HackingTarget target)
        {
            if (!playerEntity || !target || !dataDriveItem)
                return;
            
            var inventory = playerEntity.Inventory;
            if (inventory == null)
                return;
            
            var hackerToolSlot = -1;
            for (var i = 0; i < inventory.slots.Length; i++)
            {
                if (inventory.IsSlotEmpty(i))
                    continue;
                
                Item item = null;
                if (itemRegistry)
                    item = itemRegistry.GetEntry<Item>(inventory.slots[i].item);

                if (item is not HackerToolItem) 
                    continue;
                
                hackerToolSlot = i;
                break;
            }
            
            if (hackerToolSlot < 0)
                return;
            
            var newInventory = inventory.Copy();
            newInventory.slots[hackerToolSlot] = new DataDriveItemStack
            {
                item = dataDriveItem.Id,
                targetName = target.unlocalizedName ?? "Target",
                targetColor = target.color,
                targetUuid = target.EntityUuid
            };
            playerEntity.Server_SetInventory(newInventory);
        }
        #endregion
    }
}
