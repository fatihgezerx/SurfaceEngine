using Veneer;
using UnityEditor;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// The studio-look Inspector of <see cref="SurfaceHandler"/>: one card for the footstep ray and one for the impact ray, plus a state pill
    /// that says in Play Mode whether <see cref="SurfaceEngine"/> has been initialized. Without Veneer the component keeps Unity's default Inspector.
    /// </summary>
    [CustomEditor(typeof(SurfaceHandler))]
    [CanEditMultipleObjects]
    internal sealed class SurfaceHandlerEditor : VeneerInspector<SurfaceHandler>
    {
        protected override VeneerBrand Brand => SurfaceBrand.Value;

        protected override void Build(VeneerInspectorBuilder b)
        {
            b.Header("Surface Handler", "Plays footsteps and impacts for this object", null);
            b.StatePill(() =>
            {
                if (!Application.isPlaying)
                {
                    return ("Edit mode", "idle");
                }

                return SurfaceEngine.IsInitialized ? ("Ready", "ok") : ("Engine not initialized", "warn");
            });

            b.ConditionalHint(
                () => Application.isPlaying && !SurfaceEngine.IsInitialized
                    ? "SurfaceEngine.Initialize has not been called yet: Footstep and Impact do nothing until it is."
                    : null,
                "warn", true);

            b.Card("Footsteps", "step", () =>
            {
                b.Field("footstepOrigin", "Ray origin");
                b.Number("footstepDistance", "Ray distance", "m");
            });

            b.Card("Impacts", "sword", () =>
            {
                b.Number("impactDistance", "Ray distance", "m");
                b.Field("impactLayers", "Hit layers");
            });

            b.Hint("Call Footstep() from your movement or an animation event, and Impact(origin) from a weapon's hit. Each plays the sounds, VFX and decal of the surface it finds.", "info");
        }
    }
}
