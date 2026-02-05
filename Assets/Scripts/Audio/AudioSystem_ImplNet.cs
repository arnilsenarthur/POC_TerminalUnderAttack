using FishNet.Object;
using UnityEngine;

namespace TUA.Audio
{
    public partial class AudioSystem
    {
        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_PlaySound(string key, Vector3 position, bool is3D, float volumeMultiplier, int categoryInt)
        {
            var category = (AudioCategory)categoryInt;
            PlayLocal(key, is3D ? position : null, volumeMultiplier, category);
        }
    }
}
