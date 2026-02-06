using FishNet.Object;

namespace TUA.Systems
{
    public partial class FeedSystem
    {
        [ObserversRpc(ExcludeServer = false, BufferLast = true)]
        private void RpcServer_AddFeedItem(string message, float duration, int messageType)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("RpcServer_AddFeedItem can only be called on server side");

            OnFeedItemAddEvent?.Invoke(message, duration);
        }

        [ObserversRpc(ExcludeServer = false, BufferLast = true)]
        private void RpcServer_AddFeedItemLocalized(string key, string[] args, float duration, int messageType)
        {
            OnFeedLocalizedItemAddEvent?.Invoke(key, args, duration);
        }
    }
}
