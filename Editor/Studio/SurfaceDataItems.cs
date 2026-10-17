using System;
using Veneer;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SurfaceSystem
{
    /// <summary>
    /// The pieces of the <see cref="SurfaceData"/> Inspector that are not plain fields, registered with Veneer so the default layout (and
    /// a user's copy of it) can place them: the state pill, the Compile row, the validation hints and the "pool type missing" hint, plus
    /// the widget of the <c>groups</c> list (<see cref="SurfaceGroupsView"/>). They can be moved or hidden in the Inspector Designer, not changed.
    /// </summary>
    internal static class SurfaceDataItems
    {
        /// <summary>Id of the header state pill (header badge).</summary>
        public const string StateId = "surface.data-state";

        /// <summary>Id of the Compile button row (after the header, outside the tabs).</summary>
        public const string ActionsId = "surface.data-actions";

        /// <summary>Id of the four validation hints (after the header, outside the tabs).</summary>
        public const string ChecksId = "surface.data-checks";

        /// <summary>Id of the "Pool mode needs the Footstep pool type" hint (card item).</summary>
        public const string PoolHintId = "surface.data-pool-hint";

        /// <summary>The tooltip of the Compile button.</summary>
        public const string CompileTooltip =
            "Generates the SurfaceGroups enum from the surface names and keeps every surface's id. Press it after any change, and after renaming or reordering surfaces.";

        private const string PoolHintText =
            "Pool mode needs the Footstep pool type: the PoolTypes member your Pool Data compiled the footstep prefab into. Without it sounds fall back to Instantiate.";

        private const string SubtitleClass = "vn-subtitle";

        private static readonly Func<SurfaceDataAnalysis, string> NeedsCompile = analysis => analysis.NeedsCompileMessage;
        private static readonly Func<SurfaceDataAnalysis, string> NameProblem = analysis => analysis.NameMessage;
        private static readonly Func<SurfaceDataAnalysis, string> NoTextures = analysis => analysis.NoTexturesMessage;
        private static readonly Func<SurfaceDataAnalysis, string> SharedTexture = analysis => analysis.SharedTextureMessage;

        /// <summary>Registers the custom items and the list widget of <see cref="SurfaceData"/>. Safe to call again (items are replaced).</summary>
        public static void Register()
        {
            Type type = typeof(SurfaceData);

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = StateId,
                Label = "State",
                Icon = "check-circle",
                Description = "A pill in the header: Empty, Errors, Needs compile, Compiled or Compiling. It also keeps the header's counts line (surfaces and textures) up to date.",
                Slot = VeneerCustomItemSlot.HeaderBadge,
                Build = BuildState,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = ActionsId,
                Label = "Compile button",
                Icon = "bolt",
                Description = "Compile and the button that selects the generated SurfaceGroups.cs. Drawn under the header, whatever tab is open.",
                Slot = VeneerCustomItemSlot.AfterHeader,
                Build = BuildActions,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = ChecksId,
                Label = "Surface checks",
                Icon = "warning",
                Description = "Hints under the header: Compile needed, names that make Compile abort, surfaces without textures and textures used by two surfaces.",
                Slot = VeneerCustomItemSlot.AfterHeader,
                Build = BuildChecks,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = PoolHintId,
                Label = "Pool type check",
                Icon = "warning",
                Description = "A warning while the Footstep pool type is empty (Pool mode needs it).",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildPoolHint,
            });

            VeneerCustomItems.RegisterWidget(type, "groups", BuildGroups);
        }

        private static VisualElement BuildState(VeneerCustomItemContext context)
        {
            SurfaceDataSession session = SurfaceDataSession.For(context.SerializedObject);
            SurfaceDataAnalysis analysis = session.Analysis;

            // The header's line under the title shows the counts and follows the data. It is found from the right end of the header
            // (the header card holds the top row, which holds the text column and this container); a banner header has none.
            VisualElement right = context.Builder.HeaderRight;
            Label subtitle = right != null && right.parent != null && right.parent.parent != null
                ? right.parent.parent.Q<Label>(className: SubtitleClass)
                : null;

            Label pill = VeneerStyle.Pill(string.Empty, "idle");
            Action refresh = () =>
            {
                analysis.Refresh();
                if (subtitle != null && subtitle.text != analysis.Summary)
                {
                    subtitle.text = analysis.Summary;
                }

                if (SurfaceDataAnalysis.IsCompiling)
                {
                    VeneerStyle.SetPill(pill, "special", "Compiling");
                }
                else
                {
                    VeneerStyle.SetPill(pill, analysis.PillKind, analysis.PillText);
                }
            };

            // The first run of Poll is on the first tick: show the right state at once.
            refresh();
            context.Poll(refresh);
            return pill;
        }

        private static VisualElement BuildActions(VeneerCustomItemContext context)
        {
            VeneerInspectorBuilder b = context.Builder;
            SurfaceDataSession session = SurfaceDataSession.For(context.SerializedObject);
            VisualElement actions = b.Row();
            using (b.Into(actions))
            {
                Button compile = b.Button("Compile", "bolt", session.Compile, CompileTooltip, true);
                compile.style.flexGrow = 1f;
                b.Add(VeneerStyle.IconButton("code", "Select the generated SurfaceGroups.cs.", SelectGenerated, SurfaceBrand.Value));
            }

            return null;
        }

        private static VisualElement BuildChecks(VeneerCustomItemContext context)
        {
            VeneerInspectorBuilder b = context.Builder;
            SurfaceDataSession session = SurfaceDataSession.For(context.SerializedObject);
            b.ConditionalHint(() => session.Message(NeedsCompile), "warn", true);
            b.ConditionalHint(() => session.Message(NameProblem), "error", true);
            b.ConditionalHint(() => session.Message(NoTextures), "warn", true);
            b.ConditionalHint(() => session.Message(SharedTexture), "warn", true);
            return null;
        }

        private static VisualElement BuildPoolHint(VeneerCustomItemContext context)
        {
            SurfaceDataSession session = SurfaceDataSession.For(context.SerializedObject);
            context.Builder.ConditionalHint(
                () =>
                {
                    SurfaceData data = session.Data;
                    return data != null && string.IsNullOrWhiteSpace(data.Settings.PoolFootstepTypeName) ? PoolHintText : null;
                },
                "warn");
            return null;
        }

        private static VisualElement BuildGroups(VeneerCustomItemContext context, SerializedProperty property)
        {
            return new SurfaceGroupsView(context).Build();
        }

        private static void SelectGenerated()
        {
            foreach (string guid in AssetDatabase.FindAssets("SurfaceGroups t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/Generated/SurfaceGroups.cs", StringComparison.Ordinal))
                {
                    Object script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    Selection.activeObject = script;
                    EditorGUIUtility.PingObject(script);
                    return;
                }
            }

            Debug.LogWarning("[SurfaceData] SurfaceGroups.cs has not been generated yet. Press Compile first.");
        }
    }
}
