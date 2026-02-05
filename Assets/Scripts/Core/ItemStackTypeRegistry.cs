using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TUA.Core
{
    public static class ItemStackTypeRegistry
    {
        private static readonly Dictionary<string, Type> IDToType = new();
        private static readonly Dictionary<Type, string> TypeToId = new();
        private static readonly Dictionary<string, Func<ItemStack>> IDToFactory = new();
        private static bool _initialized;

        static ItemStackTypeRegistry()
        {
            _Initialize();
        }

        private static void _Initialize()
        {
            if (_initialized)
                return;

            var itemStackType = typeof(ItemStack);
            var allTypes = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => itemStackType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
                    allTypes.AddRange(types);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var loadedTypes = ex.Types.Where(t => t != null);
                    var types = loadedTypes
                        .Where(t => itemStackType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
                    allTypes.AddRange(types);
                }
                catch
                {
                    // ignored
                }
            }

            foreach (var type in allTypes)
            {
                var attribute = type.GetCustomAttribute<ItemStackTypeAttribute>();
                if (attribute == null)
                    continue;

                var id = attribute.Id;
                if (IDToType.ContainsKey(id))
                    throw new InvalidOperationException($"Duplicate ItemStackType ID '{id}' found on type '{type.FullName}'. ID already registered for '{IDToType[id].FullName}'.");

                IDToType[id] = type;
                TypeToId[type] = id;

                var constructor = type.GetConstructor(Type.EmptyTypes);
                if (constructor != null)
                {
                    IDToFactory[id] = () => (ItemStack)Activator.CreateInstance(type);
                }
                else
                {
                    Debug.LogWarning($"[ItemStackTypeRegistry] Type '{type.FullName}' with ID '{id}' does not have a parameterless constructor and cannot be instantiated.");
                }
            }

            _initialized = true;
        }

        public static string GetTypeId(Type type)
        {
            return type == null ? null : TypeToId.GetValueOrDefault(type);
        }

        public static string GetTypeId(ItemStack stack)
        {
            return stack == null ? null : GetTypeId(stack.GetType());
        }

        public static Type GetType(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            IDToType.TryGetValue(id, out var type);
            return type;
        }

        public static ItemStack CreateInstance(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            if (IDToFactory.TryGetValue(id, out var factory))
                return factory();

            var type = GetType(id);
            if (type != null)
                return (ItemStack)Activator.CreateInstance(type);

            return null;
        }

        public static bool IsRegistered(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && IDToType.ContainsKey(id);
        }

        public static bool IsRegistered(Type type)
        {
            return type != null && TypeToId.ContainsKey(type);
        }
    }
}
