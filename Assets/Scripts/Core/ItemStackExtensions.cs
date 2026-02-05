using System.Reflection;

namespace TUA.Core
{
    public static class ItemStackExtensions
    {
        public static ItemStack Copy(this ItemStack source)
        {
            if (source == null)
                return null;

            var typeId = ItemStackTypeRegistry.GetTypeId(source);
            if (string.IsNullOrWhiteSpace(typeId))
                typeId = "itemstack";

            var copy = ItemStackTypeRegistry.CreateInstance(typeId) ?? new ItemStack();

            var sourceType = source.GetType();
            var copyType = copy.GetType();

            var fields = sourceType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var copyField = copyType.GetField(field.Name, BindingFlags.Public | BindingFlags.Instance);
                if (copyField == null || copyField.FieldType != field.FieldType) 
                    continue;
                
                var value = field.GetValue(source);
                copyField.SetValue(copy, value);
            }

            var properties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (!property.CanRead || !property.CanWrite)
                    continue;

                var copyProperty = copyType.GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);
                if (copyProperty == null || copyProperty.PropertyType != property.PropertyType ||
                    !copyProperty.CanWrite) continue;
                
                var value = property.GetValue(source);
                copyProperty.SetValue(copy, value);
            }

            return copy;
        }
    }
}
