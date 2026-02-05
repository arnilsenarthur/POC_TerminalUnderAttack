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
        [Header("References")]
        public GameObject firstPersonPlayerPrefab;
        public GameObject thirdPersonPlayerPrefab;
        public Item[] defaultItems;

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
            SpawnPlayerEntity(player, gameWorld);
        }
        
        private void SpawnPlayerEntity(GamePlayer player, GameWorld gameWorld)
        {
            var pe = gameWorld.Server_SpawnObject<PlayerEntity>(firstPersonPlayerPrefab, Vector3.zero, Quaternion.identity, player);
            
            pe.Server_SetInventory(new Inventory{
                slots = defaultItems.Select(item => item.GetDefaultItemStack()).ToArray(),
            });
            
            pe.OnDeathEvent += () =>
            {
                if (!pe || !pe.gameObject)
                    return;
                
                gameWorld.Server_DespawnObject(pe.gameObject);
                StartCoroutine(RespawnPlayerAfterDelay(player, gameWorld, 3f));
            };
        }
        
        private IEnumerator RespawnPlayerAfterDelay(GamePlayer player, GameWorld gameWorld, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (player != null && gameWorld && gameWorld.IsServerSide)
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
    }
}
