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

        public static string GetPlayerColor(GamePlayer player)
        {
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
            if (!IsServerSide)
                return;

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
