using System;
using System.Collections.Generic;
using FishNet.Serializing;
using TUA.Core;
using TUA.Entities;
using TUA.Items;
using TUA.Misc;
using UnityEngine;

namespace TUA
{
    public static class TUA_Serializers
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            // Register Uuid
            GenericWriter<Uuid>.SetWrite(WriteUuid);
            GenericReader<Uuid>.SetRead(ReadUuid);

            // Register PlayerState
            GenericWriter<PlayerState>.SetWrite(WritePlayerState);
            GenericReader<PlayerState>.SetRead(ReadPlayerState);

            // Register GamePlayer
            GenericWriter<GamePlayer>.SetWrite(WriteGamePlayer);
            GenericReader<GamePlayer>.SetRead(ReadGamePlayer);

            // Register Inventory & ItemStack
            GenericWriter<Inventory>.SetWrite(WriteInventory);
            GenericReader<Inventory>.SetRead(ReadInventory);
            GenericWriter<ItemStack>.SetWrite(WriteItemStack);
            GenericReader<ItemStack>.SetRead(ReadItemStack);

            // Register Sub-ItemStack types explicitly for direct RPC usage
            GenericWriter<WeaponItemStack>.SetWrite(WriteWeaponItemStack);
            GenericReader<WeaponItemStack>.SetRead(ReadWeaponItemStack);
            GenericWriter<DataDriveItemStack>.SetWrite(WriteDataDriveItemStack);
            GenericReader<DataDriveItemStack>.SetRead(ReadDataDriveItemStack);

            // Register derived ItemStack types for polymorphic serialization (via ItemStack serializer)
            RegisterWriteHandler("itemstack", (writer, stack) =>
            {
                writer.WriteString(stack.item ?? string.Empty);
            });
            RegisterReadHandler("itemstack", (reader) =>
            {
                return new ItemStack
                {
                    item = reader.ReadStringAllocated() ?? string.Empty
                };
            });

            RegisterWriteHandler("weapon", (writer, stack) =>
            {
                WriteWeaponItemStack(writer, (WeaponItemStack)stack);
            });
            RegisterReadHandler("weapon", (reader) =>
            {
                return ReadWeaponItemStack(reader);
            });

            RegisterWriteHandler("datadrive", (writer, stack) =>
            {
                WriteDataDriveItemStack(writer, (DataDriveItemStack)stack);
            });
            RegisterReadHandler("datadrive", (reader) =>
            {
                return ReadDataDriveItemStack(reader);
            });
        }

        #region Uuid
        private static void WriteUuid(Writer writer, Uuid value)
        {
            writer.WriteInt32(value.high);
            writer.WriteInt32(value.low);
        }

        private static Uuid ReadUuid(Reader reader)
        {
            return new Uuid(
                reader.ReadInt32(),
                reader.ReadInt32()
            );
        }
        #endregion

        #region GamePlayer
        private static void WriteGamePlayer(Writer writer, GamePlayer value)
        {
            bool hasValue = value != null;
            writer.WriteBoolean(hasValue);
            if (!hasValue)
                return;
            writer.WriteInt32(value.Uuid.high);
            writer.WriteInt32(value.Uuid.low);
            writer.WriteString(value.Name ?? string.Empty);
            writer.WriteBoolean(value.IsOnline);
            writer.WriteInt32(value.SpectatorTargetUuid.high);
            writer.WriteInt32(value.SpectatorTargetUuid.low);
        }

        private static GamePlayer ReadGamePlayer(Reader reader)
        {
            bool hasValue = reader.ReadBoolean();
            if (!hasValue)
                return null;
            int high = reader.ReadInt32();
            int low = reader.ReadInt32();
            string name = reader.ReadStringAllocated() ?? string.Empty;
            bool isOnline = reader.ReadBoolean();
            int spectatorHigh = reader.ReadInt32();
            int spectatorLow = reader.ReadInt32();
            var uuid = new Uuid(high, low);
            var player = new GamePlayer(uuid, name);
            player.SetIsOnline(isOnline);
            player.SetSpectatorTargetUuid(new Uuid(spectatorHigh, spectatorLow));
            return player;
        }
        #endregion

        #region Inventory & ItemStack
        private static readonly Dictionary<string, Action<Writer, ItemStack>> _writeHandlers = new Dictionary<string, Action<Writer, ItemStack>>();
        private static readonly Dictionary<string, Func<Reader, ItemStack>> _readHandlers = new Dictionary<string, Func<Reader, ItemStack>>();

        public static void RegisterWriteHandler(string id, Action<Writer, ItemStack> handler)
        {
            _writeHandlers[id] = handler;
        }

        public static void RegisterReadHandler(string id, Func<Reader, ItemStack> handler)
        {
            _readHandlers[id] = handler;
        }

        private static void WriteInventory(Writer writer, Inventory value)
        {
            bool hasValue = value != null;
            writer.WriteBoolean(hasValue);
            if (!hasValue)
                return;
            writer.WriteInt32(value.selectedSlot);
            writer.WriteInt32(value.slots?.Length ?? 0);
            if (value.slots != null)
            {
                for (int i = 0; i < value.slots.Length; i++)
                {
                    WriteItemStack(writer, value.slots[i]);
                }
            }
        }

        private static Inventory ReadInventory(Reader reader)
        {
            bool hasValue = reader.ReadBoolean();
            if (!hasValue)
                return null;
            int selectedSlot = reader.ReadInt32();
            int slotCount = reader.ReadInt32();
            var inventory = new Inventory(slotCount)
            {
                selectedSlot = selectedSlot
            };
            for (int i = 0; i < slotCount; i++)
            {
                inventory.slots[i] = ReadItemStack(reader);
            }
            return inventory;
        }

        private static void WriteItemStack(Writer writer, ItemStack value)
        {
            bool hasValue = value != null;
            writer.WriteBoolean(hasValue);
            if (!hasValue)
                return;

            string typeId = ItemStackTypeRegistry.GetTypeId(value);
            if (string.IsNullOrWhiteSpace(typeId))
            {
                typeId = "itemstack";
            }

            writer.WriteString(typeId);

            if (_writeHandlers.TryGetValue(typeId, out Action<Writer, ItemStack> handler))
            {
                handler(writer, value);
            }
            else
            {
                writer.WriteString(value.item ?? string.Empty);
            }
        }

        private static ItemStack ReadItemStack(Reader reader)
        {
            bool hasValue = reader.ReadBoolean();
            if (!hasValue)
                return null;

            string typeId = reader.ReadStringAllocated();
            if (string.IsNullOrWhiteSpace(typeId))
            {
                return null;
            }

            if (_readHandlers.TryGetValue(typeId, out Func<Reader, ItemStack> handler))
            {
                return handler(reader);
            }

            // Fallback
            var type = ItemStackTypeRegistry.GetType(typeId);
            if (type != null)
            {
                var stack = ItemStackTypeRegistry.CreateInstance(typeId);
                if (stack != null)
                {
                    stack.item = reader.ReadStringAllocated() ?? string.Empty;
                    return stack;
                }
            }

            return new ItemStack
            {
                item = reader.ReadStringAllocated() ?? string.Empty
            };
        }

        private static void WriteWeaponItemStack(Writer writer, WeaponItemStack value)
        {
            writer.WriteString(value.item ?? string.Empty);
            writer.WriteInt32(value.ammo);
            writer.WriteInt32(value.maxAmmo);
        }

        private static WeaponItemStack ReadWeaponItemStack(Reader reader)
            {
            return new WeaponItemStack
            {
                item = reader.ReadStringAllocated() ?? string.Empty,
                ammo = reader.ReadInt32(),
                maxAmmo = reader.ReadInt32()
            };
        }

        private static void WriteDataDriveItemStack(Writer writer, DataDriveItemStack value)
        {
            writer.WriteString(value.item ?? string.Empty);
            writer.WriteString(value.targetName ?? string.Empty);
            writer.WriteColor32(new Color32(
                (byte)(value.targetColor.r * 255),
                (byte)(value.targetColor.g * 255),
                (byte)(value.targetColor.b * 255),
                (byte)(value.targetColor.a * 255)
            ));
        }

        private static DataDriveItemStack ReadDataDriveItemStack(Reader reader)
        {
            var dataDriveStack = new DataDriveItemStack
            {
                item = reader.ReadStringAllocated() ?? string.Empty,
                targetName = reader.ReadStringAllocated() ?? string.Empty
            };
            var color32 = reader.ReadColor32();
            dataDriveStack.targetColor = new Color(
                color32.r / 255f,
                color32.g / 255f,
                color32.b / 255f,
                color32.a / 255f
            );
            return dataDriveStack;
            }
        #endregion

        #region PlayerState
        private static void WritePlayerState(Writer writer, PlayerState value)
        {
            writer.WriteVector2(value.viewDirection);
            writer.WriteVector3(value.position);
            writer.WriteVector3(value.velocity);
            writer.WriteBoolean(value.isAiming);
            writer.WriteBoolean(value.isFiring);
            writer.WriteBoolean(value.isSneaking);
            writer.WriteBoolean(value.isScoped);
            writer.WriteVector2(value.recoilComputed);
            writer.WriteSingle(value.reloadProgress);
        }

        private static PlayerState ReadPlayerState(Reader reader)
        {
            return new PlayerState
            {
                viewDirection = reader.ReadVector2(),
                position = reader.ReadVector3(),
                velocity = reader.ReadVector3(),
                isAiming = reader.ReadBoolean(),
                isFiring = reader.ReadBoolean(),
                isSneaking = reader.ReadBoolean(),
                isScoped = reader.ReadBoolean(),
                recoilComputed = reader.ReadVector2(),
                reloadProgress = reader.ReadSingle()
            };
        }
        #endregion
    }
}
