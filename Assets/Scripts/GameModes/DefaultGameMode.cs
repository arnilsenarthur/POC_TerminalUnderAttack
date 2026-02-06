using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TUA.Core;
using TUA.Entities;
using TUA.Misc;
using UnityEngine;

namespace TUA.GameModes
{
    [PlayerData("default")]
    [Serializable]
    public struct DefaultPlayerData : IPlayerData
    {
        public void Serialize(INetWriter writer)
        {
        }

        public IPlayerData Deserialize(INetReader reader)
        {
            return this;
        }
    }

    public struct DefaultGameSettings : IGameSettings
    {
    }

    public class DefaultGameMode : GameMode<DefaultPlayerData, DefaultGameSettings>
    {
        #region Serialized Fields
        [Header("References")]
        public GameObject firstPersonPlayerPrefab;
        public GameObject thirdPersonPlayerPrefab;
        public Item[] defaultItems;
        #endregion

        #region Public Methods
        public override void OnWorldStart(GameWorld gameWorld, DefaultGameSettings gameSettings)
        {
            Debug.Log("DefaultGameMode OnWorldStart");
            SpawnPlayerEntity(null, gameWorld);
        }

        public override GamePlayer AcceptNewPlayer(Uuid playerUuid, string username, GameWorld gameWorld)
        {
            if (!gameWorld || !gameWorld.IsServerSide)
                return null;

            var gamePlayer = new GamePlayer(playerUuid, username);
            var playerData = new DefaultPlayerData();
            gameWorld.Server_SetPlayerData(gamePlayer, playerData);
            var allPlayers = gameWorld.AllPlayers;
            var invadersCount = allPlayers.Count(p => p.TeamName == "invaders");
            var guardsCount = allPlayers.Count(p => p.TeamName == "guards");
            var assignedTeam = invadersCount <= guardsCount ? "invaders" : "guards";
            gameWorld.Server_SetPlayerTeam(gamePlayer, assignedTeam);
            return gamePlayer;
        }

        public override void OnPlayerJoined(GamePlayer player, GameWorld gameWorld)
        {
            if (player == null || gameWorld == null)
                return;

            if (string.Equals(player.Name, "spec", StringComparison.OrdinalIgnoreCase))
            {
                var targetUuid = OnGetNextSpectateTarget(player, gameWorld);
                if (targetUuid.IsValid)
                    gameWorld.Server_SetSpectatorTarget(player, targetUuid);
                return;
            }

            SpawnPlayerEntity(player, gameWorld);
        }

        public override List<Team> GetTeams(GameWorld gameWorld)
        {
            return new List<Team>
            {
                new("invaders"),
                new("guards")
            };
        }

        public override Uuid OnGetNextSpectateTarget(GamePlayer spectator, GameWorld gameWorld)
        {
            if (spectator == null || gameWorld == null)
                return default;

            var allPlayers = gameWorld.AllPlayers;
            if (allPlayers == null || allPlayers.Count == 0)
                return default;

            var currentTargetUuid = spectator.SpectatorTargetUuid;
            var currentIndex = -1;
            var validTargetUuids = new List<Uuid>();

            foreach (var player in allPlayers)
            {
                if (player == null || !player.IsOnline || player.Uuid == spectator.Uuid)
                    continue;

                var entity = gameWorld.GetEntityOwnedByPlayer<Entity>(player);
                if (entity != null)
                {
                    var entityUuid = entity.EntityUuid;
                    validTargetUuids.Add(entityUuid);
                    if (entityUuid == currentTargetUuid)
                        currentIndex = validTargetUuids.Count - 1;
                }
            }

            if (validTargetUuids.Count == 0)
                return default;

            if (currentIndex < 0)
                currentIndex = -1;

            var nextIndex = (currentIndex + 1) % validTargetUuids.Count;
            return validTargetUuids[nextIndex];
        }

        public override Uuid OnGetPrevSpectateTarget(GamePlayer spectator, GameWorld gameWorld)
        {
            if (spectator == null || gameWorld == null)
                return default;

            var allPlayers = gameWorld.AllPlayers;
            if (allPlayers == null || allPlayers.Count == 0)
                return default;

            var currentTargetUuid = spectator.SpectatorTargetUuid;
            var currentIndex = -1;
            var validTargetUuids = new List<Uuid>();

            foreach (var player in allPlayers)
            {
                if (player == null || !player.IsOnline || player.Uuid == spectator.Uuid)
                    continue;

                var entity = gameWorld.GetEntityOwnedByPlayer<Entity>(player);
                if (entity != null)
                {
                    var entityUuid = entity.EntityUuid;
                    validTargetUuids.Add(entityUuid);
                    if (entityUuid == currentTargetUuid)
                        currentIndex = validTargetUuids.Count - 1;
                }
            }

            if (validTargetUuids.Count == 0)
                return default;

            if (currentIndex < 0)
                currentIndex = validTargetUuids.Count;

            var prevIndex = currentIndex <= 0 ? validTargetUuids.Count - 1 : currentIndex - 1;
            return validTargetUuids[prevIndex];
        }
        #endregion

        #region Private Methods
        private void SpawnPlayerEntity(GamePlayer player, GameWorld gameWorld)
        {
            var pe = gameWorld.Server_SpawnObject<PlayerEntity>(firstPersonPlayerPrefab, Vector3.zero, Quaternion.identity, player);

            pe.Server_SetInventory(new Inventory
            {
                slots = defaultItems.Select(item => item.GetDefaultItemStack()).ToArray(),
            });

            pe.OnDeathEvent += () =>
            {
                if (!pe || !pe.gameObject)
                    return;

                var deadEntityUuid = pe.EntityUuid;
                foreach (var spectatorPlayer in gameWorld.AllPlayers)
                {
                    if (spectatorPlayer == null || !spectatorPlayer.IsSpectator)
                        continue;

                    if (spectatorPlayer.SpectatorTargetUuid == deadEntityUuid)
                    {
                        var nextTargetUuid = OnGetNextSpectateTarget(spectatorPlayer, gameWorld);
                        if (nextTargetUuid.IsValid)
                            gameWorld.Server_SetSpectatorTarget(spectatorPlayer, nextTargetUuid);
                    }
                }

                gameWorld.Server_DespawnObject(pe.gameObject);
                StartCoroutine(RespawnPlayerAfterDelay(player, gameWorld, 3f));
            };
        }

        private IEnumerator RespawnPlayerAfterDelay(GamePlayer player, GameWorld gameWorld, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (gameWorld && gameWorld.IsServerSide)
                SpawnPlayerEntity(player, gameWorld);
        }
        #endregion
    }
}
