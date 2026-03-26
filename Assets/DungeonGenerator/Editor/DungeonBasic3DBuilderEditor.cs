#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DungeonGenerator.Editor
{
    [CustomEditor(typeof(DungeonBasic3DBuilder))]
    public class DungeonBasic3DBuilderEditor : UnityEditor.Editor
    {
        private int _manualSeed = 42;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            var builder = (DungeonBasic3DBuilder)target;

            if (GUILayout.Button("Build Dungeon"))
            {
                builder.BuildDungeon();
                EditorUtility.SetDirty(builder);
            }

            EditorGUILayout.Space();
            _manualSeed = EditorGUILayout.IntField("Build Seed", _manualSeed);

            if (GUILayout.Button("Build Dungeon (Specific Seed)"))
            {
                if (builder.settings == null)
                    builder.settings = new GeneratorSettings();

                builder.settings.useRandomSeed = false;
                builder.settings.seed = _manualSeed;

                builder.BuildDungeon();
                EditorUtility.SetDirty(builder);
            }

            if (GUILayout.Button("Clear Dungeon"))
            {
                builder.ClearDungeon();
                EditorUtility.SetDirty(builder);
            }
        }
    }
}
#endif
