using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TUA.Core
{
    public static class PlayerDataTypeRegistry
    {
        private static readonly Dictionary<string, Type> IDToType = new();
        private static readonly Dictionary<Type, string> TypeToId = new();
        private static bool _initialized;

        static PlayerDataTypeRegistry()
        {
            _Initialize();
        }

        private static void _Initialize()
        {
            if (_initialized)
                return;

            var iPlayerDataType = typeof(IPlayerData);
            var allTypes = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => iPlayerDataType.IsAssignableFrom(t) && t.IsValueType && !t.IsAbstract);
                    allTypes.AddRange(types);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var loadedTypes = ex.Types.Where(t => t != null);
                    var types = loadedTypes
                        .Where(t => iPlayerDataType.IsAssignableFrom(t) && t.IsValueType && !t.IsAbstract);
                    allTypes.AddRange(types);
                }
                catch
                {
                    // Ignore assembly load errors
                }
            }

            // Register types with PlayerDataAttribute
            foreach (var type in allTypes)
            {
                var attribute = type.GetCustomAttribute<PlayerDataAttribute>();
                if (attribute == null)
                    continue;

                var id = attribute.Id;
                if (IDToType.TryGetValue(id, out var value))
                    throw new InvalidOperationException($"Duplicate PlayerData ID '{id}' found on type '{type.FullName}'. ID already registered for '{value.FullName}'.");

                IDToType[id] = type;
                TypeToId[type] = id;
                Debug.Log($"[PlayerDataTypeRegistry] Registered PlayerData type: {type.FullName} with ID '{id}'");
            }

            _initialized = true;
        }

        public static string GetTypeId(Type type)
        {
            if (type == null)
                return null;

            if (!_initialized)
                _Initialize();

            return TypeToId.GetValueOrDefault(type);
        }

        public static Type GetType(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            if (!_initialized)
                _Initialize();

            IDToType.TryGetValue(id, out Type type);
            return type;
        }

        public static bool IsRegistered(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && IDToType.ContainsKey(id);
        }

        public static bool IsRegistered(Type type)
        {
            return type != null && TypeToId.ContainsKey(type);
        }

        public static IEnumerable<Type> GetAllRegisteredTypes()
        {
            if (!_initialized)
                _Initialize();
            
            return IDToType.Values;
        }
    }
}
