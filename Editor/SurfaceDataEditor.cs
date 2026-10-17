#if !HAS_VENEER
using UnityEditor;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// The fallback Inspector of <see cref="SurfaceData"/>, used only while Veneer is not in the project: Unity's
    /// default drawing for the settings and the surface groups, the short explanation, and the Compile button, so the
    /// surfaces can still be authored and compiled. With the kit installed, <c>SurfaceDataStudioEditor</c> (the studio-look
    /// Inspector in the <c>Studio</c> folder) replaces it.
    /// </summary>
    [CustomEditor(typeof(SurfaceData))]
    internal sealed class SurfaceDataEditor : UnityEditor.Editor
    {
        private const string GroupsInfo =
            "Every group is a surface (e.g. \"Grass\", \"Wood\", \"Snow\"). Its name becomes its SurfaceGroups " +
            "member on Compile, so names must be unique. Its Textures must be the exact same Texture2D asset " +
            "used by the surface's material (or Terrain layer) - SurfaceEngine matches a hit against them to " +
            "identify the surface, so they aren't just previews. Press Compile after any change - and after " +
            "renaming or reordering groups.";

        private static readonly Color CompileButtonColor = new(0.4f, 0.75f, 0.4f);

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(GroupsInfo, MessageType.Info);
            EditorGUILayout.Space(8);

            var buttonColor = GUI.backgroundColor;
            GUI.backgroundColor = CompileButtonColor;
            if (GUILayout.Button("Compile", GUILayout.Height(34)))
            {
                SurfaceCompiler.Compile((SurfaceData)target);
            }

            GUI.backgroundColor = buttonColor;
        }
    }
}
#endif
