using System;
using System.Collections;
using System.Collections.Generic;

namespace TUA.Core
{
    [Serializable]
    public class Inventory : IEnumerable<ItemStack>
    {
        public ItemStack[] slots;
        public int selectedSlot;

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

        public bool CanSelectSlot(int slot)
        {
            return slot >= 0 && slot < slots.Length && slots[slot] != null && !IsSlotEmpty(slot);
        }

        public bool IsSlotEmpty(int slot)
        {
            if (slot < 0 || slot >= slots.Length)
                return true;
            
            var stack = slots[slot];
            return stack == null || string.IsNullOrEmpty(stack.item);
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
                if (slots[i] == null)
                    continue;

                copy.slots[i] = slots[i].Copy();
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
            if (slot < 0 || slot >= slots.Length)
                return false;

            slots[slot] = itemStack;
            return true;
        }

        public int AddItem(ItemStack itemStack)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (IsSlotEmpty(i))
                {
                    slots[i] = itemStack;
                    return i;
                }
            }

            return -1;
        }

        public bool RemoveItemAtSlot(int slot)
        {
            if (slot < 0 || slot >= slots.Length)
                return false;

            slots[slot] = null;

            if (selectedSlot == slot)
            {
                selectedSlot = -1;
            }

            return true;
        }

        public bool RemoveItem(string itemId)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].item == itemId)
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

        public IEnumerator<ItemStack> GetEnumerator()
        {
            return ((IEnumerable<ItemStack>)slots).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
