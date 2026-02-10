using System;
using TUA.Core;
using TUA.Misc;
using UnityEngine;
using TUA.Audio;

namespace TUA.Systems
{
    public enum FeedMessageType
    {
        Unknown = 0,
        Objective = 10,
        Kill = 20
    }

    public partial class FeedSystem : SingletonNetBehaviour<FeedSystem>
    {
        #region Serialized Fields
        [Header("Feed Sounds")]
        public string soundFeedKey;
        public string soundKillKey;
        public string soundObjectiveKey;
        #endregion

        #region Static Events
        public static event System.Action<string, float> OnFeedItemAddEvent;
        public static event System.Action<string, string[], float> OnFeedLocalizedItemAddEvent;
        public static event System.Action<GamePlayer, GamePlayer> OnKillEvent;
        #endregion

        #region Unity Callbacks
        protected override void OnEnable()
        {
            base.OnEnable();
            OnKillEvent += _OnKillEvent;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            OnKillEvent -= _OnKillEvent;
        }
        #endregion

        #region Public Methods
        public void Server_AddFeedInfo(string message, float duration = 3f, FeedMessageType messageType = FeedMessageType.Unknown)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_AddFeedInfo can only be called on server side");

            RpcServer_AddFeedItem(message, duration, (int)messageType);
            _PlayFeedSound(messageType);
        }

        public void Server_AddFeedInfoLocalized(string key, float duration = 3f, params string[] args)
        {
            Server_AddFeedInfoLocalized(key, duration, FeedMessageType.Unknown, args);
        }

        public void Server_AddFeedInfoLocalized(string key, float duration, FeedMessageType messageType, params string[] args)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_AddFeedInfoLocalized can only be called on server side");

            RpcServer_AddFeedItemLocalized(key, args, duration, (int)messageType);
            _PlayFeedSound(messageType);
        }

        public void Server_AddKillMessage(GamePlayer killer, GamePlayer victim, float duration = 3f)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_AddKillMessage can only be called on server side");

            var killerName = killer != null ? killer.Name : "Unknown";
            var victimName = victim != null ? victim.Name : "Unknown";

            Server_AddFeedInfoLocalized("feed.killed", duration, FeedMessageType.Kill, killerName, victimName);
        }

        public static void InvokeKillEvent(GamePlayer killer, GamePlayer victim)
        {
            OnKillEvent?.Invoke(killer, victim);
        }

        public static string GetPlayerColor(GamePlayer player)
        {
            var world = GameWorld.Instance;
            var teamName = player?.TeamName;
            if (world != null && !string.IsNullOrWhiteSpace(teamName))
            {
                var teams = world.GetTeams();
                if (teams != null)
                {
                    for (var i = 0; i < teams.Count; i++)
                    {
                        var team = teams[i];
                        if (team == null || string.IsNullOrWhiteSpace(team.Name))
                            continue;

                        if (!string.Equals(team.Name, teamName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (team.Color.a > 0f)
                            return ColorToHex(team.Color);
                        break;
                    }
                }

                var fallback = world.GetTeamColor(teamName);
                if (fallback.a > 0f && fallback != Color.gray)
                    return ColorToHex(fallback);
            }

            return "#FFD700";
        }

        public static string ColorToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }
        #endregion

        #region Private Methods
        private void _OnKillEvent(GamePlayer killer, GamePlayer victim)
        {
            Server_AddKillMessage(killer, victim);
        }

        private void _PlayFeedSound(FeedMessageType messageType)
        {
            string soundKey = null;

            switch (messageType)
            {
                case FeedMessageType.Kill:
                    soundKey = soundKillKey;
                    break;
                case FeedMessageType.Objective:
                    soundKey = soundObjectiveKey;
                    break;
                case FeedMessageType.Unknown:
                default:
                    soundKey = soundFeedKey;
                    break;
            }

            if (!string.IsNullOrEmpty(soundKey) && AudioSystem.Instance)
                AudioSystem.Instance.PlayGlobalFollowingCamera(soundKey, 1f, AudioCategory.Feed);
        }
        #endregion
    }
}
