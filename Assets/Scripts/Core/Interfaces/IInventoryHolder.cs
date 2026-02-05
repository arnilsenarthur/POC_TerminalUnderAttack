using System;

namespace TUA.Core.Interfaces
{
    public interface IInventoryHolder
    {
        event Action<Inventory> OnInventoryChangeEvent;
        Inventory Inventory { get; }
        void Server_SetInventory(Inventory inventory);
    }
}
