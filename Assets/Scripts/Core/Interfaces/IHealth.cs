using System;

namespace TUA.Core.Interfaces
{
    public interface IHealth
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        event Action<float, float> OnHealthChangeEvent;
        event Action OnDeathEvent;
        void Server_SetHealth(float health);
        void Server_SetMaxHealth(float maxHealth);
        void Server_TakeDamage(float damage);
    }
}
