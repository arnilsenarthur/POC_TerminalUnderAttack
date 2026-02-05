using System;
using TUA.Misc;
namespace TUA.Core
{
    [Serializable]
    public class GamePlayer
    {
        public Uuid Uuid { get; internal set; }
        public string Name { get; internal set; }
        public bool IsOnline { get; internal set; }
        public Uuid SpectatorTargetUuid { get; internal set; }
        public string TeamName { get; internal set; }
        public bool IsSpectator => SpectatorTargetUuid.IsValid;
        
        public GamePlayer()
        {
            Uuid = Uuid.New;
            Name = string.Empty;
            IsOnline = false;
            SpectatorTargetUuid = default;
            TeamName = null;
        }
        
        public GamePlayer(Uuid uuid, string name)
        {
            Uuid = uuid;
            Name = name;
            IsOnline = true;
            SpectatorTargetUuid = default;
            TeamName = null;
        }
        
        public void SetIsOnline(bool isOnline)
        {
            IsOnline = isOnline;
        }
        
        public void SetSpectatorTargetUuid(Uuid spectatorTargetUuid)
        {
            SpectatorTargetUuid = spectatorTargetUuid;
        }
        
        internal void SetTeamName(string teamName)
        {
            TeamName = teamName;
        }
    }
}
