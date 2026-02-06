using System;
using System.Collections;
using System.Collections.Generic;

namespace TUA.Core
{
    [Serializable]
    public class Inventory : IEnumerable<ItemStack>
    {
        #region Fields
        public ItemStack[] slots;
        public int selectedSlot;
        #endregion

        #region Constructors
        public Inventory()
        {
            slots = Array.Empty<ItemStack>();
            selectedSlot = -1;
        }

        public Inventory(int slotCount)
        {
            slots = new ItemStack[slotCount];
            selectedSlot = -1;
        }
        #endregion

        #region Public Methods
        public bool CanSelectSlot(int slot)
        {
            return slot >= 0 && slot < slots.Length && !IsSlotEmpty(slot);
        }

        public bool IsSlotEmpty(int slot)
        {
            if (slot < 0 || slot >= slots.Length)
                return true;

            var stack = slots[slot];
            return string.IsNullOrEmpty(stack?.item);
        }

        public Inventory Copy()
        {
            var copy = new Inventory
            {
                selectedSlot = selectedSlot,
                slots = new ItemStack[slots.Length]
            };

            for (var i = 0; i < slots.Length; i++)
            {
                copy.slots[i] = slots[i]?.Copy();
            }

            return copy;
        }

        public ItemStack GetSelectedItem()
        {
            if (selectedSlot < 0 || selectedSlot >= slots.Length)
                return null;

            return slots[selectedSlot];
        }

        public bool AddItemAtSlot(int slot, ItemStack itemStack)
        {
            if (slot < 0)
                return false;

            if (slot >= slots.Length)
            {
                var newSlots = new ItemStack[slot + 1];
                for (var i = 0; i < slots.Length; i++)
                {
                    newSlots[i] = slots[i];
                }
                slots = newSlots;
            }

            slots[slot] = itemStack;
            return true;
        }

        public int AddItem(ItemStack itemStack)
        {
            var newSlots = new ItemStack[slots.Length + 1];
            for (var i = 0; i < slots.Length; i++)
            {
                newSlots[i] = slots[i];
            }
            newSlots[slots.Length] = itemStack;
            slots = newSlots;
            return slots.Length - 1;
        }

        public bool RemoveItemAtSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length)
                return false;

            var newSlots = new ItemStack[slots.Length - 1];
            for (var i = 0; i < slot; i++)
            {
                newSlots[i] = slots[i];
            }
            for (var i = slot + 1; i < slots.Length; i++)
            {
                newSlots[i - 1] = slots[i];
            }
            slots = newSlots;

            if (selectedSlot == slot)
            {
                selectedSlot = -1;
            }
            else if (selectedSlot > slot)
            {
                selectedSlot--;
            }

            return true;
        }

        public bool RemoveItem(string itemId)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i]?.item == itemId)
                {
                    return RemoveItemAtSlot(i);
                }
            }

            return false;
        }

        public bool SwapSlots(int slotA, int slotB)
        {
            if (slotA < 0 || slotA >= slots.Length || slotB < 0 || slotB >= slots.Length)
                return false;

            (slots[slotA], slots[slotB]) = (slots[slotB], slots[slotA]);
            return true;
        }

        public ItemStack GetItemAtSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length)
                return null;

            return slots[slot];
        }

        public int GetSlotCount()
        {
            return slots?.Length ?? 0;
        }
        #endregion

        #region Interface Implementation
        public IEnumerator<ItemStack> GetEnumerator()
        {
            return ((IEnumerable<ItemStack>)slots).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }
}
