#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DungeonGenerator.Editor
{
    [CustomEditor(typeof(DungeonGraphGizmoPreview))]
    public class DungeonGraphGizmoPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            var preview = (DungeonGraphGizmoPreview)target;

            if (GUILayout.Button("Regenerate Preview"))
            {
                preview.Regenerate();
                EditorUtility.SetDirty(preview);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Use Current Time As Seed"))
            {
                if (preview.settings == null)
                    preview.settings = new GeneratorSettings();

                preview.settings.useRandomSeed = false;
                preview.settings.seed = System.DateTime.Now.Millisecond;
                preview.Regenerate();

                EditorUtility.SetDirty(preview);
                SceneView.RepaintAll();
            }
        }
    }
}
#endif
