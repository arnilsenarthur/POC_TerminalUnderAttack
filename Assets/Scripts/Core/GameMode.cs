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

    public struct ScoreboardPlayerEntry
    {
        public Uuid PlayerUuid;
        public string PlayerName;
        public int Kills;
    }
    
    public class ScoreboardSection
    {
        public string TeamName;
        public string Header;
        public string StatLine;
        public Color TeamColor;
        public List<ScoreboardPlayerEntry> Players = new();
    }

    public class MatchResultsData
    {
        public string Title;
        public string ObjectiveLine;
    }
    
    public interface IGameSettings
    {
    }
    
    public abstract class GameMode : MonoBehaviour
    {
        #region Serialized Fields
        [FormerlySerializedAs("_id")]
        [Header("Game Mode")]
        [SerializeField]
        private string id;
        #endregion

        #region Properties
        public string Id => id;
        #endregion

        #region Public Methods
        public abstract IPlayerData GetPlayerDataSnapshot(GamePlayer player, IPlayerData fullData, GamePlayer requestingPlayer, GameWorld gameWorld);
        
        public virtual void BuildScoreboard(GameWorld gameWorld, List<ScoreboardSection> sections)
        {
        }

        public virtual void BuildMatchResults(GameWorld gameWorld, MatchResultsData results)
        {
        }
        
        public virtual string GetMatchInfoText(GameWorld world)
        {
            if (world == null)
                return string.Empty;
            return world.MatchInfoKey ?? string.Empty;
        }

        public virtual bool IsMatchOver(GameWorld world)
        {
            return false;
        }

        public virtual Uuid OnGetNextSpectateTarget(GamePlayer spectator, GameWorld gameWorld)
        {
            return default;
        }

        public virtual Uuid OnGetPrevSpectateTarget(GamePlayer spectator, GameWorld gameWorld)
        {
            return default;
        }
        #endregion

        #region Internal Methods
        internal virtual void InternalOnWorldStart(GameWorld gameWorld)
        {
        }

        internal virtual void InternalOnWorldEnd(GameWorld gameWorld)
        {
        }

        internal virtual void InternalOnPlayerJoined(GamePlayer player, GameWorld gameWorld)
        {
        }

        internal virtual GamePlayer InternalAcceptNewPlayer(Uuid playerUuid, string username, GameWorld gameWorld)
        {
            return new GamePlayer(playerUuid, username);
        }

        internal virtual void InternalOnTick(float deltaTime, GameWorld gameWorld)
        {
        }

        internal virtual void InternalOnPlayerDataChanged(GamePlayer player, IPlayerData oldData, IPlayerData newData, GameWorld gameWorld)
        {
        }

        internal virtual List<Team> InternalGetTeams(GameWorld gameWorld)
        {
            return null;
        }

        internal virtual void InternalSetGameSettings(IGameSettings gameSettings)
        {
        }
        #endregion
    }

    public abstract class GameMode<Pd, Gs> : GameMode where Pd : struct, IPlayerData where Gs : struct, IGameSettings
    {
        #region Properties
        public Gs GameSettings { get; internal set; }
        #endregion

        #region Public Methods
        public virtual void OnWorldStart(GameWorld gameWorld, Gs gameSettings)
        {
            GameSettings = gameSettings;
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
        
        public virtual void OnPlayerDataChanged(GamePlayer player, Pd oldData, Pd newData, GameWorld gameWorld)
        {
        }
        
        public virtual Pd GetPlayerDataSnapshot(GamePlayer player, Pd fullData, GamePlayer requestingPlayer, GameWorld gameWorld)
        {
            if (requestingPlayer != null && requestingPlayer.Uuid == player.Uuid)
                return fullData;
            return default;
        }
        
        public virtual List<Team> GetTeams(GameWorld gameWorld)
        {
            return null;
        }

        public override Uuid OnGetNextSpectateTarget(GamePlayer spectator, GameWorld gameWorld)
        {
            return base.OnGetNextSpectateTarget(spectator, gameWorld);
        }

        public override Uuid OnGetPrevSpectateTarget(GamePlayer spectator, GameWorld gameWorld)
        {
            return base.OnGetPrevSpectateTarget(spectator, gameWorld);
        }
        #endregion

        #region Internal Methods
        internal override void InternalOnWorldStart(GameWorld gameWorld)
        {
            var settings = gameWorld.GetGameSettings<Gs>();
            OnWorldStart(gameWorld, settings);
        }

        internal override void InternalOnWorldEnd(GameWorld gameWorld)
        {
            OnWorldEnd(gameWorld);
        }

        internal override void InternalOnPlayerJoined(GamePlayer player, GameWorld gameWorld)
        {
            OnPlayerJoined(player, gameWorld);
        }

        internal override GamePlayer InternalAcceptNewPlayer(Uuid playerUuid, string username, GameWorld gameWorld)
        {
            return AcceptNewPlayer(playerUuid, username, gameWorld);
        }

        internal override void InternalOnTick(float deltaTime, GameWorld gameWorld)
        {
            OnTick(deltaTime, gameWorld);
        }

        internal override void InternalOnPlayerDataChanged(GamePlayer player, IPlayerData oldData, IPlayerData newData, GameWorld gameWorld)
        {
            if (oldData is Pd typedOldData && newData is Pd typedNewData)
                OnPlayerDataChanged(player, typedOldData, typedNewData, gameWorld);
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
        
        internal override List<Team> InternalGetTeams(GameWorld gameWorld)
        {
            return GetTeams(gameWorld);
        }

        internal override void InternalSetGameSettings(IGameSettings gameSettings)
        {
            if (gameSettings is Gs settings)
                GameSettings = settings;
        }
        #endregion
    }
}
