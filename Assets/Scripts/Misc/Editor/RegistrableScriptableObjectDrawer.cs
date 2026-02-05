using UnityEngine;
using UnityEditor;

namespace TUA.Misc.Editor
{
    [CustomPropertyDrawer(typeof(RegistrableScriptableObject), true)]
    public class RegistrableScriptableObjectDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var objectReference = property.objectReferenceValue;
            var registrableObject = objectReference as RegistrableScriptableObject;

            if (registrableObject)
            {
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

                var halfWidth = position.width * 0.5f;
                var spacing = 5f;

                var objectFieldRect = new Rect(position.x, position.y, halfWidth - spacing, position.height);
                var idFieldRect = new Rect(position.x + halfWidth, position.y, halfWidth, position.height);

                EditorGUI.ObjectField(objectFieldRect, property, GUIContent.none);

                var serializedObject = new SerializedObject(registrableObject);
                var idProperty = serializedObject.FindProperty("id");

                if (idProperty != null)
                {
                    EditorGUI.BeginChangeCheck();
                    var newId = EditorGUI.TextField(idFieldRect, idProperty.stringValue);
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        idProperty.stringValue = newId;
                        serializedObject.ApplyModifiedProperties();
                    }
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.TextField(idFieldRect, registrableObject.Id ?? "");
                    EditorGUI.EndDisabledGroup();
                }
            }
            else
            {
                EditorGUI.ObjectField(position, property, label);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
