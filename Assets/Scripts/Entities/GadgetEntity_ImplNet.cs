using FishNet.Object;
using TUA.Audio;
using TUA.Core;
using TUA.Items;
using TUA.Misc;
using TUA.UI;
using UnityEngine;

namespace TUA.Entities
{
    public partial class GadgetEntity
    {
        #region Network RPCs
        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_Initialize(string itemId)
        {
            if (IsServerSide)
                return;

            if (!string.IsNullOrEmpty(itemId) && itemRegistry != null)
                _gadgetItem = itemRegistry.GetEntry<GadgetItem>(itemId);
        }

        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_TriggerEffect()
        {
            if (_gadgetItem == null)
                return;

            if (!string.IsNullOrEmpty(_gadgetItem.effectSoundKey))
                AudioSystem.Instance?.PlayLocal(_gadgetItem.effectSoundKey, transform.position, 1f, AudioCategory.Gameplay);

            if (_gadgetItem.effectPrefab == null)
                return;

            var effect = Instantiate(_gadgetItem.effectPrefab, transform.position, transform.rotation);
            Destroy(effect, 10f);
        }

        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_FlashBlind(Uuid playerUuid, float duration)
        {
            var localPlayer = GameWorld.Instance?.LocalGamePlayer;
            if (localPlayer == null || localPlayer.Uuid != playerUuid)
                return;

            GameInterfaceController.Instance?.TriggerFlashEffect(duration);
        }

        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_SpawnSmoke(Vector3 position, float radius, float duration)
        {
            OnSmokeSpawnRequestEvent?.Invoke(this, position, radius, duration);
        }
        #endregion
    }
}
