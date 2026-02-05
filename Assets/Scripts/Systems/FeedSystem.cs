using TUA.Core;
using TUA.Misc;
using UnityEngine;

namespace TUA.Systems
{
    public partial class FeedSystem : SingletonNetBehaviour<FeedSystem>
    {
        #region Static Events
        public static event System.Action<string, float> OnFeedItemAddEvent;
        public static event System.Action<string, string[], float> OnFeedLocalizedItemAddEvent;
        #endregion
        
        #region Methods
        public void Server_AddFeedInfo(string message, float duration = 3f)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_AddFeedInfo can only be called on server side");
            
            RpcServer_AddFeedItem(message, duration);
        }
        
        public void Server_AddFeedInfoLocalized(string key, float duration = 3f, params string[] args)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_AddFeedInfoLocalized can only be called on server side");
            
            RpcServer_AddFeedItemLocalized(key, args, duration);
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
    }
}
