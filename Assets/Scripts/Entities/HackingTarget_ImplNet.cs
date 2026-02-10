using FishNet.Object.Synchronizing;

namespace TUA.Entities
{
    public partial class HackingTarget
    {
        #region Private Fields
        private readonly SyncVar<float> _hackingProgress = new(0f);
        private readonly SyncVar<bool> _isHacked = new(false);
        private readonly SyncVar<bool> _isDelivered = new(false);
        private readonly SyncVar<bool> _isBeingHacked = new(false);
        #endregion

        #region Unity Callbacks
        private void Start()
        {
            HackingProgress = _hackingProgress.Value;
            IsHacked = _isHacked.Value;
            IsDelivered = _isDelivered.Value;
            IsBeingHacked = _isBeingHacked.Value;

            _hackingProgress.OnChange += (_, next, _) =>
            {
                HackingProgress = next;
                OnHackingProgressChangeEvent?.Invoke(next);
            };

            _isHacked.OnChange += (_, next, _) =>
            {
                IsHacked = next;
                OnIsHackedChangeEvent?.Invoke(next);
            };

            _isDelivered.OnChange += (_, next, _) =>
            {
                IsDelivered = next;
            };

            _isBeingHacked.OnChange += (_, next, _) =>
            {
                IsBeingHacked = next;
            };
        }
        #endregion

        #region Private Methods
        private void _Server_SetHackingProgressInternal(float progress)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetHackingProgress can only be called on server side");

            _hackingProgress.Value = progress;
        }

        private void _Server_SetIsHackedInternal(bool isHacked)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetIsHacked can only be called on server side");

            _isHacked.Value = isHacked;
        }

        private void _Server_SetIsDeliveredInternal(bool isDelivered)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetIsDelivered can only be called on server side");

            _isDelivered.Value = isDelivered;
        }

        private void _Server_SetIsBeingHackedInternal(bool isBeingHacked)
        {
            if (!IsServerSide)
                throw new System.InvalidOperationException("Server_SetIsBeingHacked can only be called on server side");

            _isBeingHacked.Value = isBeingHacked;
        }
        #endregion
    }
}
