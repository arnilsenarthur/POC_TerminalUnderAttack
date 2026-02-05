using System.Collections.Generic;
using TUA.Misc;
using UnityEngine;
using UnityEngine.Serialization;

namespace TUA.Core
{
    public interface IPlayerData
    {
        void Serialize(INetWriter writer);
        IPlayerData Deserialize(INetReader reader);
    }
    
    public interface IGameSettings
    {
    }
    
    public abstract class GameMode : MonoBehaviour
    {
        [FormerlySerializedAs("_id")]
        [Header("Game Mode")]
        [SerializeField]
        private string id;
        public string Id => id;
        
        public virtual void OnWorldStart(GameWorld gameWorld)
        {
        }
        
        public virtual void OnWorldEnd(GameWorld gameWorld)
        {
        }
        
        public virtual void OnPlayerJoined(GamePlayer player, GameWorld gameWorld)
        {
        }
        
        public virtual GamePlayer AcceptNewPlayer(Uuid playerUuid, string username, GameWorld gameWorld)
        {
            return new GamePlayer(playerUuid, username);
        }
        
        public virtual void OnTick(float deltaTime, GameWorld gameWorld)
        {
        }
        
        public virtual void OnPlayerDataChanged(GamePlayer player, IPlayerData oldData, IPlayerData newData, GameWorld gameWorld)
        {
        }
        
        public abstract IPlayerData GetPlayerDataSnapshot(GamePlayer player, IPlayerData fullData, GamePlayer requestingPlayer, GameWorld gameWorld);
        
        public virtual List<Team> GetTeams(GameWorld gameWorld)
        {
            return null;
        }
    }
    public abstract class GameMode<Pd, Gs> : GameMode where Pd : struct, IPlayerData where Gs : struct, IGameSettings
    {
        public Gs GameSettings { get; internal set; }
        
        public virtual void OnWorldStart(GameWorld gameWorld, Gs gameSettings)
        {
            GameSettings = gameSettings;
            base.OnWorldStart(gameWorld);
        }
        
        public override void OnWorldEnd(GameWorld gameWorld)
        {
        }
        
        public override void OnPlayerJoined(GamePlayer player, GameWorld gameWorld)
        {
        }
        
        public override void OnTick(float deltaTime, GameWorld gameWorld)
        {
        }
        
        public virtual void OnPlayerDataChanged(GamePlayer player, Pd oldData, Pd newData, GameWorld gameWorld)
        {
        }
        
        public virtual Pd GetPlayerDataSnapshot(GamePlayer player, Pd fullData, GamePlayer requestingPlayer, GameWorld gameWorld)
        {
            if (requestingPlayer != null && requestingPlayer.Uuid == player.Uuid)
                return fullData;
            return default;
        }
        
        public override void OnPlayerDataChanged(GamePlayer player, IPlayerData oldData, IPlayerData newData, GameWorld gameWorld)
        {
            if (oldData is Pd typedOldData && newData is Pd typedNewData)
            {
                OnPlayerDataChanged(player, typedOldData, typedNewData, gameWorld);
            }
        }
        
        public override IPlayerData GetPlayerDataSnapshot(GamePlayer player, IPlayerData fullData, GamePlayer requestingPlayer, GameWorld gameWorld)
        {
            if (fullData is Pd typedFullData)
            {
                Pd snapshot = GetPlayerDataSnapshot(player, typedFullData, requestingPlayer, gameWorld);
                return snapshot;
            }
            return null;
        }
        
        public override List<Team> GetTeams(GameWorld gameWorld)
        {
            return null;
        }
    }
}
