using System;
using System.Runtime.CompilerServices;
using UnityEditor;

namespace SurfaceSystem
{
    /// <summary>
    /// What the pieces of one <see cref="SurfaceData"/> Inspector share: the cached <see cref="SurfaceDataAnalysis"/> and a counter that
    /// says Compile just ran. The pieces (state pill, Compile button, checks, surface list) are separate Veneer items, so they find the
    /// session through the Inspector's <see cref="SerializedObject"/>. The table holds its entries weakly: a session lives exactly as
    /// long as the Inspector's serialized object.
    /// </summary>
    internal sealed class SurfaceDataSession
    {
        private static readonly ConditionalWeakTable<SerializedObject, SurfaceDataSession> Sessions =
            new ConditionalWeakTable<SerializedObject, SurfaceDataSession>();

        private SurfaceDataSession(SerializedObject serializedObject)
        {
            SerializedObject = serializedObject;
            Analysis = new SurfaceDataAnalysis(Data);
            Analysis.Refresh(true);
        }

        /// <summary>The serialized object of the Inspector.</summary>
        public SerializedObject SerializedObject { get; }

        /// <summary>The edited asset (null when it was destroyed).</summary>
        public SurfaceData Data => SerializedObject.targetObject as SurfaceData;

        /// <summary>The validation of the asset, shared by every piece of the Inspector.</summary>
        public SurfaceDataAnalysis Analysis { get; }

        /// <summary>Increases every time Compile ran from the Inspector; the surface list rebuilds when it sees a new value.</summary>
        public int CompileCount { get; private set; }

        /// <summary>Gets the session of <paramref name="serializedObject"/>, making it on first use.</summary>
        public static SurfaceDataSession For(SerializedObject serializedObject)
        {
            return Sessions.GetValue(serializedObject, Create);
        }

        /// <summary>Refreshes the analysis and returns the message <paramref name="pick"/> reads from it, or null while scripts compile (the enum is about to change).</summary>
        public string Message(Func<SurfaceDataAnalysis, string> pick)
        {
            Analysis.Refresh();
            return SurfaceDataAnalysis.IsCompiling ? null : pick(Analysis);
        }

        /// <summary>Applies pending edits, runs <see cref="SurfaceCompiler.Compile"/> and refreshes the analysis; the surface list rebuilds on its next poll.</summary>
        public void Compile()
        {
            SurfaceData data = Data;
            if (data == null)
            {
                return;
            }

            SerializedObject.ApplyModifiedProperties();
            SurfaceCompiler.Compile(data);
            SerializedObject.Update();
            Analysis.Refresh(true);
            CompileCount++;
        }

        private static SurfaceDataSession Create(SerializedObject serializedObject)
        {
            return new SurfaceDataSession(serializedObject);
        }
    }
}
