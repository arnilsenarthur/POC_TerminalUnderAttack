using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TUA.Settings.Editor
{
    [CustomEditor(typeof(SettingsAsset))]
    public sealed class SettingsAssetEditor : UnityEditor.Editor
    {
        private const float Spacing = 4f;
        private const float RowSpacing = 2f;
        private const float ToggleW = 18f;
        private const float TypeW = 78f;

        private ReorderableList _list;

        private SerializedProperty _fileNameProp;
        private SerializedProperty _unlocalizedNameProp;
        private SerializedProperty _autoLoadProp;
        private SerializedProperty _autoSaveProp;
        private SerializedProperty _entriesProp;

        private void OnEnable()
        {
            _fileNameProp = serializedObject.FindProperty("fileName");
            _unlocalizedNameProp = serializedObject.FindProperty("unlocalizedName");
            _autoLoadProp = serializedObject.FindProperty("autoLoadOnEnable");
            _autoSaveProp = serializedObject.FindProperty("autoSaveOnChange");
            _entriesProp = serializedObject.FindProperty("entries");

            _list = new ReorderableList(serializedObject, _entriesProp, true, true, true, true);
            _list.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Entries");
            };
            _list.elementHeightCallback = idx =>
            {
                var element = _entriesProp.GetArrayElementAtIndex(idx);
                var h = EditorGUIUtility.singleLineHeight + RowSpacing;
                if (!element.isExpanded) 
                    return h;
                
                var typeProp = element.FindPropertyRelative("type");
                var type = (SettingType)typeProp.enumValueIndex;
                
                if (type == SettingType.Int || type == SettingType.Float)
                    h += (EditorGUIUtility.singleLineHeight * 2f) + RowSpacing; 

                h += EditorGUIUtility.singleLineHeight + RowSpacing; 
                h += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("provider"), true) + RowSpacing;

                if (type == SettingType.String)
                    h += EditorGUI.GetPropertyHeight(element.FindPropertyRelative("staticStringOptions"), true) + RowSpacing;

                return h;
            };

            _list.drawElementCallback = (rect, idx, _, _) =>
            {
                var element = _entriesProp.GetArrayElementAtIndex(idx);
                DrawEntry(rect, element);
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPersistenceSection();
            GUILayout.Space(Spacing);

            _list.DoLayoutList();

            GUILayout.Space(Spacing);
            DrawActionsSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPersistenceSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_unlocalizedNameProp);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("File", GUILayout.Width(30f));
                EditorGUILayout.PropertyField(_fileNameProp, GUIContent.none);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Auto", GUILayout.Width(30f));
                _autoLoadProp.boolValue = GUILayout.Toggle(_autoLoadProp.boolValue, "Load", GUILayout.Width(55f));
                _autoSaveProp.boolValue = GUILayout.Toggle(_autoSaveProp.boolValue, "Save", GUILayout.Width(55f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawActionsSection()
        {
            var asset = (SettingsAsset)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load"))
                    asset.LoadFromFile();
                if (GUILayout.Button("Save"))
                    asset.SaveToFile();
                if (GUILayout.Button("Reset All"))
                    asset.ResetAllToDefaults();
            }
        }

        private static void DrawEntry(Rect rect, SerializedProperty element)
        {
            rect.height = EditorGUIUtility.singleLineHeight;

            var visibleProp = element.FindPropertyRelative("visible");
            var typeProp = element.FindPropertyRelative("type");
            var keyProp = element.FindPropertyRelative("key");

            var defIntProp = element.FindPropertyRelative("defaultInt");
            var defFloatProp = element.FindPropertyRelative("defaultFloat");
            var defBoolProp = element.FindPropertyRelative("defaultBool");
            var defStringProp = element.FindPropertyRelative("defaultString");

            var intClampProp = element.FindPropertyRelative("intClamp");
            var intMinProp = element.FindPropertyRelative("intMin");
            var intMaxProp = element.FindPropertyRelative("intMax");
            var intStepProp = element.FindPropertyRelative("intStep");

            var floatClampProp = element.FindPropertyRelative("floatClamp");
            var floatMinProp = element.FindPropertyRelative("floatMin");
            var floatMaxProp = element.FindPropertyRelative("floatMax");
            var floatStepProp = element.FindPropertyRelative("floatStep");

            var providerProp = element.FindPropertyRelative("provider");
            var optionsProp = element.FindPropertyRelative("staticStringOptions");
            
            var bg = rect;
            bg.xMin -= 2f;
            bg.xMax += 2f;
            EditorGUI.DrawRect(bg, new Color(0f, 0f, 0f, element.isExpanded ? 0.06f : 0.03f));

            var prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            
            var foldoutRect = new Rect(rect.x, rect.y, 14f, rect.height);
            element.isExpanded = EditorGUI.Foldout(foldoutRect, element.isExpanded, GUIContent.none, true);

            var x = rect.x + 14f;
            var w = rect.width - 14f;

            var visRect = new Rect(x, rect.y, ToggleW, rect.height);
            x += ToggleW;
            w -= ToggleW;

            var typeRect = new Rect(x, rect.y, TypeW, rect.height);
            x += TypeW + 2f;
            w -= TypeW + 2f;
            
            var keyW = Mathf.Max(120f, w * 0.55f);
            var valW = Mathf.Max(80f, w - keyW - 2f);
            var keyRect = new Rect(x, rect.y, keyW, rect.height);
            var valRect = new Rect(x + keyW + 2f, rect.y, valW, rect.height);
            
            visibleProp.boolValue = GUI.Toggle(visRect, visibleProp.boolValue, GUIContent.none);
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

            var type = (SettingType)typeProp.enumValueIndex;

            
                var keyControl = $"tua_settings_key_{element.propertyPath}";
                GUI.SetNextControlName(keyControl);
                keyProp.stringValue = EditorGUI.DelayedTextField(keyRect, keyProp.stringValue);

                var keyFocused = GUI.GetNameOfFocusedControl() == keyControl;
                if (!keyFocused && string.IsNullOrWhiteSpace(keyProp.stringValue))
                    EditorGUI.LabelField(keyRect, "Key", EditorStyles.miniLabel);
                
            switch (type)
            {
                case SettingType.Label:
                    
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.TextField(valRect, "");
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.LabelField(valRect, "�", EditorStyles.miniLabel);
                    break;
                case SettingType.Int:
                    defIntProp.intValue = EditorGUI.IntField(valRect, defIntProp.intValue);
                    break;
                case SettingType.Float:
                    defFloatProp.floatValue = EditorGUI.FloatField(valRect, defFloatProp.floatValue);
                    break;
                case SettingType.Bool:
                    defBoolProp.boolValue = EditorGUI.Toggle(valRect, defBoolProp.boolValue);
                    break;
                case SettingType.String:
                    defStringProp.stringValue = EditorGUI.TextField(valRect, defStringProp.stringValue);
                    if (string.IsNullOrWhiteSpace(defStringProp.stringValue))
                        EditorGUI.LabelField(valRect, "Default", EditorStyles.miniLabel);
                    break;
            }
            
            if (element.isExpanded)
            {
                var r = rect;
                r.y += EditorGUIUtility.singleLineHeight + RowSpacing;
                r.height = EditorGUIUtility.singleLineHeight;
                
                if (type == SettingType.Int || type == SettingType.Float)
                {
                    var rangeLabelRect = new Rect(rect.x + 14f, r.y, rect.width - 14f, r.height);
                    r.y += EditorGUIUtility.singleLineHeight; 
                    var rangeFieldsRect = new Rect(rect.x + 14f, r.y, rect.width - 14f, r.height);

                    DrawRangeRow(rangeLabelRect, rangeFieldsRect, type,
                        intClampProp, intMinProp, intMaxProp, intStepProp,
                        floatClampProp, floatMinProp, floatMaxProp, floatStepProp);

                    r.y += EditorGUIUtility.singleLineHeight + RowSpacing;
                }
                
                var providerH = EditorGUI.GetPropertyHeight(providerProp, true);
                var providerRect = new Rect(rect.x + 14f, r.y, rect.width - 14f, providerH);
                EditorGUI.PropertyField(providerRect, providerProp, new GUIContent("Provider"), true);
                r.y += providerH + RowSpacing;

                if (type == SettingType.String)
                {
                    var optionsH = EditorGUI.GetPropertyHeight(optionsProp, true);
                    var optionsRect = new Rect(rect.x + 14f, r.y, rect.width - 14f, optionsH);
                    EditorGUI.PropertyField(optionsRect, optionsProp, new GUIContent("Options"), true);
                }
            }

            EditorGUI.indentLevel = prevIndent;
        }

        private static void DrawRangeRow(
            Rect labelRect,
            Rect fieldRect,
            SettingType type,
            SerializedProperty intClampProp,
            SerializedProperty intMinProp,
            SerializedProperty intMaxProp,
            SerializedProperty intStepProp,
            SerializedProperty floatClampProp,
            SerializedProperty floatMinProp,
            SerializedProperty floatMaxProp,
            SerializedProperty floatStepProp)
        {
            var hintStyle = EditorStyles.miniLabel;
            var lx = labelRect.x;
            var lh = labelRect.height;

            var clampW = 60f;
            var fieldW = Mathf.Max(60f, (labelRect.width - clampW - 6f) / 3f);
            lx += clampW + 2f;
            var minL = new Rect(lx, labelRect.y, fieldW, lh);
            lx += fieldW + 2f;
            var maxL = new Rect(lx, labelRect.y, fieldW, lh);
            lx += fieldW + 2f;
            var stepL = new Rect(lx, labelRect.y, fieldW, lh);

            EditorGUI.LabelField(minL, "min", hintStyle);
            EditorGUI.LabelField(maxL, "max", hintStyle);
            EditorGUI.LabelField(stepL, "step", hintStyle);
            
            var x = fieldRect.x;
            var h = fieldRect.height;

            var clampW2 = 60f;
            var fieldW2 = Mathf.Max(60f, (fieldRect.width - clampW2 - 6f) / 3f);

            var clampRect = new Rect(x, fieldRect.y, clampW2, h);
            x += clampW2 + 2f;
            var minRect = new Rect(x, fieldRect.y, fieldW2, h);
            x += fieldW2 + 2f;
            var maxRect = new Rect(x, fieldRect.y, fieldW2, h);
            x += fieldW2 + 2f;
            var stepRect = new Rect(x, fieldRect.y, fieldW2, h);

            switch (type)
            {
                case SettingType.Int:
                    intClampProp.boolValue = EditorGUI.ToggleLeft(clampRect, "Clamp", intClampProp.boolValue);
                    intMinProp.intValue = EditorGUI.IntField(minRect, intMinProp.intValue);
                    intMaxProp.intValue = EditorGUI.IntField(maxRect, intMaxProp.intValue);
                    intStepProp.intValue = Mathf.Max(1, EditorGUI.IntField(stepRect, intStepProp.intValue));
                    break;
                case SettingType.Float:
                    floatClampProp.boolValue = EditorGUI.ToggleLeft(clampRect, "Clamp", floatClampProp.boolValue);
                    floatMinProp.floatValue = EditorGUI.FloatField(minRect, floatMinProp.floatValue);
                    floatMaxProp.floatValue = EditorGUI.FloatField(maxRect, floatMaxProp.floatValue);
                    floatStepProp.floatValue = Mathf.Max(0f, EditorGUI.FloatField(stepRect, floatStepProp.floatValue));
                    break;
            }
        }
    }
}

