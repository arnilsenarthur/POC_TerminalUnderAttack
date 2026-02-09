using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TUA.Core
{
    public partial class GameWorld
    {
        #region Public Methods
        public List<Team> GetTeams()
        {
            if (_gameMode)
                return _gameMode.InternalGetTeams(this);

            if (_teams.Count == 0)
                return null;

            return _teams
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Name))
                .Select(t => new Team(t.Name, t.Color))
                .ToList();
        }
        
        public List<GamePlayer> GetPlayersInTeam(string teamName)
        {
            if (string.IsNullOrEmpty(teamName) || AllPlayers == null)
                return new List<GamePlayer>();
            
            return AllPlayers.Where(p => p != null && string.Equals(p.TeamName, teamName, System.StringComparison.OrdinalIgnoreCase)).ToList();
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

        public Color GetTeamColor(string teamName)
        {
            if (string.IsNullOrEmpty(teamName))
                return Color.gray;

            var teams = GetTeams();
            if (teams != null)
            {
                for (var i = 0; i < teams.Count; i++)
                {
                    var team = teams[i];
                    if (team == null || !string.Equals(team.Name, teamName, System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    return team.Color;
                }
            }

            return Color.gray;
        }
        #endregion
    }
}
