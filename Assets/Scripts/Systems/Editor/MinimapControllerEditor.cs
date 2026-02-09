using UnityEditor;
using UnityEngine;
using TUA.Systems;

namespace TUA.Systems.Editor
{
    [CustomEditor(typeof(MinimapController))]
    [CanEditMultipleObjects]
    public class MinimapControllerEditor : UnityEditor.Editor
    {
        #region Serialized Properties
        private SerializedProperty _navMeshSurfaceProp;
        private SerializedProperty _targetRenderTextureProp;
        private SerializedProperty _renderTextureWidthProp;
        private SerializedProperty _renderTextureHeightProp;
        private SerializedProperty _borderProp;
        private SerializedProperty _backgroundColorProp;
        private SerializedProperty _navMeshFillColorProp;
        private SerializedProperty _navMeshOutlineColorProp;
        private SerializedProperty _outlineWidthProp;
        #endregion

        #region Fields
        private Texture2D _previewTexture;
        private const float PreviewSize = 256f;
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            _navMeshSurfaceProp = serializedObject.FindProperty("navMeshSurface");
            _targetRenderTextureProp = serializedObject.FindProperty("targetRenderTexture");
            _renderTextureWidthProp = serializedObject.FindProperty("renderTextureWidth");
            _renderTextureHeightProp = serializedObject.FindProperty("renderTextureHeight");
            _borderProp = serializedObject.FindProperty("border");
            _backgroundColorProp = serializedObject.FindProperty("backgroundColor");
            _navMeshFillColorProp = serializedObject.FindProperty("navMeshFillColor");
            _navMeshOutlineColorProp = serializedObject.FindProperty("navMeshOutlineColor");
            _outlineWidthProp = serializedObject.FindProperty("outlineWidth");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var controller = (MinimapController)target;

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(10f);

            // Preview section
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            // Show preview
            if (_previewTexture != null)
            {
                var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(rect, _previewTexture, null, ScaleMode.ScaleToFit);
                
                EditorGUILayout.Space(5f);
                EditorGUILayout.LabelField($"Size: {_previewTexture.width}x{_previewTexture.height}", EditorStyles.miniLabel);
            }
            else if (controller.TargetRenderTexture != null)
            {
                var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(rect, controller.TargetRenderTexture, null, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.HelpBox("No preview available. Click 'Refresh' to generate one.", MessageType.Info);
            }

            EditorGUILayout.Space(5f);

            // Refresh button (after preview)
            if (GUILayout.Button("Refresh"))
            {
                if (Application.isPlaying)
                {
                    controller.Refresh();
                }
                else
                {
                    GeneratePreview(controller);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
        #endregion

        #region Private Methods
        private void GeneratePreview(MinimapController controller)
        {
            if (controller == null)
                return;

            controller.Refresh();

            // Get the render texture
            var renderTexture = controller.TargetRenderTexture;
            if (renderTexture == null)
            {
                Debug.LogWarning("MinimapController: No render texture available for preview.");
                return;
            }

            // Convert render texture to texture2D for preview
            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;

            _previewTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            _previewTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            _previewTexture.Apply();

            RenderTexture.active = previousActive;

            // Mark scene as dirty if in edit mode
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(controller);
            }
        }
        #endregion
    }
}
