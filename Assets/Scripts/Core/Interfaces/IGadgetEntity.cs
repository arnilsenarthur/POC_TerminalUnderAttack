using System;
using TUA.Core;
using TUA.Misc;
using UnityEngine;

namespace TUA.Core.Interfaces
{
    public interface IGadgetEntity
    {
        Uuid EntityUuid { get; }
        bool IsServerSide { get; }
        bool IsValidAndSpawned { get; }
        event Action<IGadgetEntity, Vector3, float, float> OnSmokeSpawnRequestEvent;
        event Action<IGadgetEntity, GamePlayer, GamePlayer> OnKillEvent;
    }
}
