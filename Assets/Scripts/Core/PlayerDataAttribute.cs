using System;
namespace TUA.Core
{
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class PlayerDataAttribute : Attribute
    {
        public string Id { get; }
        public PlayerDataAttribute(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("PlayerData ID cannot be null or whitespace.", nameof(id));
            Id = id;
        }
    }
}
