using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TUA.Misc.Editor
{
    [CustomPropertyDrawer(typeof(Uuid))]
    public class UuidPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.width -= 20;
            var a = property.FindPropertyRelative("high").intValue;
            var b = property.FindPropertyRelative("low").intValue;
            var str = new StringBuilder();
            str.Append(a.ToString("x8"));
            str.Append(b.ToString("x8"));
            var old = str.ToString();
            var changed = EditorGUI.TextField(position, label, old);

            if (changed != old && changed.Length == 16)
            {
                property.FindPropertyRelative("high").intValue = int.Parse(changed[..8], NumberStyles.HexNumber);
                property.FindPropertyRelative("low").intValue =
                    int.Parse(changed.Substring(8, 8), NumberStyles.HexNumber);
            }

            GUI.enabled = true;

            position.x += position.width;
            position.width = 20;
            GUI.enabled = !Application.isPlaying;
            if (GUI.Button(position, "N"))
            {
                property.FindPropertyRelative("high").intValue = new System.Random().Next(int.MinValue, int.MaxValue);
                property.FindPropertyRelative("low").intValue = new System.Random().Next(int.MinValue, int.MaxValue);
                property.serializedObject.ApplyModifiedProperties();
            }

            GUI.enabled = true;
        }
    }
}

