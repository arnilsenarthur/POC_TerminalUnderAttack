using FishNet.Object;

namespace TUA.Misc
{
    public abstract partial class NetBehaviour : NetworkBehaviour
    {
        public bool IsServerSide => IsServerInitialized;
        public bool IsClientSide => IsClientInitialized;
    }
}
