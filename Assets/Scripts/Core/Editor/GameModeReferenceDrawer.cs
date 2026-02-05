using UnityEditor;
using UnityEngine;

namespace TUA.Core.Editor
{
    [CustomPropertyDrawer(typeof(GameMode), true)]
    public class GameModeReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var objectReference = property.objectReferenceValue;
            var gameMode = objectReference as GameMode;

            if (gameMode)
            {
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

                var halfWidth = position.width * 0.5f;
                var spacing = 5f;
                var idFieldRect = new Rect(position.x, position.y, halfWidth - spacing, position.height);
                var objectFieldRect = new Rect(position.x + halfWidth, position.y, halfWidth, position.height);

                
                var so = new SerializedObject(gameMode);
                var idProp = so.FindProperty("id");
                if (idProp != null)
                {
                    so.Update();
                    EditorGUI.BeginChangeCheck();
                    var newId = EditorGUI.TextField(idFieldRect, idProp.stringValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        idProp.stringValue = newId;
                        so.ApplyModifiedProperties();
                    }
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.TextField(idFieldRect, gameMode.Id ?? string.Empty);
                    EditorGUI.EndDisabledGroup();
                }

                
                EditorGUI.ObjectField(objectFieldRect, property, GUIContent.none);
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

