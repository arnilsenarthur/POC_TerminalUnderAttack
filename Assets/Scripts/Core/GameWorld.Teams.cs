using System.Collections.Generic;
using System.Linq;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Public Methods
        public List<Team> GetTeams()
        {
            return !_gameMode ? null : _gameMode.InternalGetTeams(this);
        }
        
        public List<GamePlayer> GetPlayersInTeam(string teamName)
        {
            if (string.IsNullOrEmpty(teamName) || AllPlayers == null)
                return new List<GamePlayer>();
            
            return AllPlayers.Where(p => p != null && p.TeamName == teamName).ToList();
        }
        
        public string GetPlayerTeam(GamePlayer player)
        {
            return player?.TeamName;
        }
        
        public void Server_SetPlayerTeam(GamePlayer player, string teamName)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetPlayerTeam can only be called on server side");
            
            if (player == null)
                return;
            
            Server_SetPlayerTeamInternal(player, teamName);
        }
        
        public List<GamePlayer> GetTeammates(GamePlayer player)
        {
            if (player == null)
                return new List<GamePlayer>();
            
            var teamName = GetPlayerTeam(player);
            return string.IsNullOrEmpty(teamName) ? new List<GamePlayer>() : GetPlayersInTeam(teamName).Where(p => p != null && p.Uuid != player.Uuid).ToList();
        }
        #endregion
    }
}
