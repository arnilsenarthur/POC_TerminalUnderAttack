using FishNet.Object;
using TUA.Systems;
using UnityEngine;

namespace TUA.Systems
{
    public partial class FeedSystem
    {
        [ObserversRpc(ExcludeServer = false, BufferLast = true)]
        private void RpcServer_AddFeedItem(string message, float duration)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("RpcServer_AddFeedItem can only be called on server side");
            OnFeedItemAddEvent?.Invoke(message, duration);
        }
        [ObserversRpc(ExcludeServer = false, BufferLast = true)]
        private void RpcServer_AddFeedItemLocalized(string key, string[] args, float duration)
        {
            // ObserversRpc is called on all observers (clients), so we just invoke the event
            // The server-side check is done in the public Server_AddFeedInfoLocalized method
            OnFeedLocalizedItemAddEvent?.Invoke(key, args, duration);
        }
    }
}
