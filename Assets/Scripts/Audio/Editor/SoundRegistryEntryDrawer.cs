using UnityEditor;
using UnityEngine;

namespace TUA.Audio.Editor
{
    [CustomPropertyDrawer(typeof(SoundRegistryEntry))]
    public sealed class SoundRegistryEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var spacing = 5f;
            var totalSpacing = spacing * 2;
            var availableWidth = position.width - totalSpacing;
            var fieldWidth = availableWidth / 3f;

            var keyRect = new Rect(position.x, position.y, fieldWidth, position.height);
            var clipRect = new Rect(position.x + fieldWidth + spacing, position.y, fieldWidth, position.height);
            var volumeRect = new Rect(position.x + (fieldWidth + spacing) * 2, position.y, fieldWidth, position.height);

            // Draw fields
            var keyProp = property.FindPropertyRelative("key");
            var clipProp = property.FindPropertyRelative("clip");
            var volumeProp = property.FindPropertyRelative("defaultVolume");

            EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
            EditorGUI.PropertyField(clipRect, clipProp, GUIContent.none);
            EditorGUI.Slider(volumeRect, volumeProp, 0f, 5f, GUIContent.none);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
