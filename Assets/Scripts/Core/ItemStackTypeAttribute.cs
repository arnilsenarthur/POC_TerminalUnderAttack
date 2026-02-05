using System;

namespace TUA.Core
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ItemStackTypeAttribute : Attribute
    {
        public string Id { get; }

        public ItemStackTypeAttribute(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ItemStackType ID cannot be null or whitespace.", nameof(id));
            
            Id = id;
        }
    }
}
