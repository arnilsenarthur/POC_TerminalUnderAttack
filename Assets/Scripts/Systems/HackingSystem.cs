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
        #region Methods
        private void _OnTick(float deltaTime)
        {
            if (GameWorld.Instance == null || !GameWorld.Instance.IsServerSide)
                return;
            foreach (var target in GameWorld.Instance.GetEntities<HackingTarget>())
            {
                if (target == null)
                    continue;
                if (target.IsHacked)
                    continue;
                var playersLookingAtTarget = _GetPlayersLookingAtTarget(target);
                bool isBeingHacked = playersLookingAtTarget.Count > 0 && !target.IsHacked;
                if (target.IsBeingHacked != isBeingHacked)
                    target.Server_SetIsBeingHacked(isBeingHacked);
                if (playersLookingAtTarget.Count > 0)
                {
                    float totalHackingSpeed = 0f;
                    foreach (var playerData in playersLookingAtTarget)
                    {
                        float distanceMultiplier = distanceMultiplierCurve.Evaluate(playerData.Distance / maxDetectionDistance);
                        totalHackingSpeed += hackingSpeed * distanceMultiplier;
                    }
                    float newProgress = Mathf.Clamp01(target.HackingProgress + totalHackingSpeed * deltaTime);
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
                    float newProgress = Mathf.Clamp01(target.HackingProgress - unhackingVelocity * deltaTime);
                    target.Server_SetHackingProgress(newProgress);
                }
            }
        }
        private HackingTarget _GetCurrentHackingTarget()
        {
            if (CameraSystem.Instance == null || CameraSystem.Instance.mainCamera == null)
                return null;
            Camera camera = CameraSystem.Instance.mainCamera;
            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, maxDetectionDistance, detectionLayerMask))
            {
                HackingTarget target = hit.collider.GetComponent<HackingTarget>();
                if (target != null)
                    return target;
            }
            return null;
        }
        private List<PlayerHackingData> _GetPlayersLookingAtTarget(HackingTarget target)
        {
            var playersLooking = new List<PlayerHackingData>();
            if (GameWorld.Instance == null)
                return playersLooking;
            foreach (var gamePlayer in GameWorld.Instance.AllPlayers)
            {
                if (gamePlayer == null)
                    continue;
                var playerEntities = GameWorld.Instance.GetEntitiesOwnedByPlayer<PlayerEntity>(gamePlayer);
                foreach (var playerEntity in playerEntities)
                {
                    if (playerEntity == null)
                        continue;
                    var selectedItem = playerEntity.Inventory?.GetSelectedItem();
                    if (selectedItem == null || string.IsNullOrEmpty(selectedItem.item))
                        continue;
                    Item item = null;
                    if (itemRegistry != null)
                        item = itemRegistry.GetEntry<Item>(selectedItem.item);
                    if (item is not HackerToolItem)
                        continue;
                    if (!playerEntity.IsAimReady)
                        continue;
                    if (!playerEntity.IsFiring)
                        continue;
                    if (_IsPlayerLookingAtTarget(playerEntity, target))
                    {
                        float distance = Vector3.Distance(playerEntity.transform.position, target.transform.position);
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
            }
            return playersLooking;
        }
        private bool _IsPlayerLookingAtTarget(PlayerEntity playerEntity, HackingTarget target)
        {
            playerEntity.GetCameraView(out var position, out var rotation, out _);
            var ray = new Ray(position, rotation * Vector3.forward);
            if (Physics.Raycast(ray, out var hit, maxDetectionDistance, detectionLayerMask))
            {
                if (hit.collider.gameObject == target.gameObject)
                    return true;
            }
            return false;
        }
        private void _CallOnTargetHacked(HackingTarget target, List<PlayerHackingData> players)
        {
            if (players == null || players.Count == 0)
                return;
            var feedSystem = FeedSystem.Instance;
            if (feedSystem == null)
                return;
            if (GameWorld.Instance == null)
                return;
            bool firstPlayer = true;
            foreach (var playerData in players)
            {
                if (!playerData.PlayerEntityUuid.IsValid)
                    continue;
                var playerEntity = GameWorld.Instance.GetEntityByUuid<PlayerEntity>(playerData.PlayerEntityUuid);
                if (playerEntity == null)
                    continue;
                var gamePlayer = playerEntity.GamePlayer;
                if (gamePlayer == null)
                    continue;
                string playerName = gamePlayer.Name ?? "Unknown";
                string targetName = target.unlocalizedName ?? "Target";
                string playerColor = FeedSystem.GetPlayerColor(gamePlayer);
                string targetColor = FeedSystem.ColorToHex(target.color);
                string player = $"<color={playerColor}>{playerName}</color>";
                string targetLabel = $"<color={targetColor}>{targetName}</color>";
                feedSystem.Server_AddFeedInfoLocalized("feed.hacked", 3f, player, targetLabel);
                if (firstPlayer && dataDriveItem != null)
                {
                    _ReplaceHackerToolWithDataDrive(playerEntity, target);
                    firstPlayer = false;
                }
            }
            Server_OnTargetHacked?.Invoke(target);
        }
        private void _ReplaceHackerToolWithDataDrive(PlayerEntity playerEntity, HackingTarget target)
        {
            if (playerEntity == null || target == null || dataDriveItem == null)
                return;
            var inventory = playerEntity.Inventory;
            if (inventory == null)
                return;
            int hackerToolSlot = -1;
            for (int i = 0; i < inventory.slots.Length; i++)
            {
                if (inventory.slots[i] == null || string.IsNullOrEmpty(inventory.slots[i].item))
                    continue;
                Item item = null;
                if (itemRegistry != null)
                    item = itemRegistry.GetEntry<Item>(inventory.slots[i].item);
                if (item is HackerToolItem)
                {
                    hackerToolSlot = i;
                    break;
                }
            }
            if (hackerToolSlot < 0)
                return;
            var newInventory = inventory.Copy();
            newInventory.slots[hackerToolSlot] = new DataDriveItemStack
            {
                item = dataDriveItem.Id,
                targetName = target.unlocalizedName ?? "Target",
                targetColor = target.color
            };
            playerEntity.Server_SetInventory(newInventory);
        }
        #endregion
        #region Events
        public Action<HackingTarget> Server_OnTargetHacked;
        #endregion
    }
}
