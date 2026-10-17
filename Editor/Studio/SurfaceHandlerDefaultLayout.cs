using System;
using Veneer;

namespace SurfaceSystem
{
    /// <summary>
    /// The design of the <see cref="SurfaceHandler"/> Inspector that Surface Engine ships: a Footsteps card (ray origin and distance), an
    /// Impacts card (ray distance and hit layers) and a Usage note, plus the system's own items (state pill, "not initialized" hint).
    /// With Veneer installed a project can copy it in the Inspector Designer and change the copy; this default is never saved as an asset.
    /// </summary>
    internal sealed class SurfaceHandlerDefaultLayout : IVeneerDefaultLayoutProvider
    {
        private const string UsageNote =
            "Call Footstep() from your movement or an animation event, and Impact(origin) from a weapon's hit. " +
            "Each plays the sounds, VFX and decal of the surface it finds.";

        /// <inheritdoc />
        public Type TargetType => typeof(SurfaceHandler);

        /// <inheritdoc />
        public VeneerLayout CreateLayout()
        {
            VeneerLayout layout = new VeneerLayoutBuilder(typeof(SurfaceHandler), "Surface Handler", "Plays footsteps and impacts for this object")
                .Card("Footsteps", "step")
                .Custom(SurfaceHandlerItems.NotInitializedId)
                .Custom(SurfaceHandlerItems.StateId)
                .Field("footstepOrigin", "Ray origin")
                .Field("footstepDistance", "Ray distance", unit: "m", widget: VeneerWidget.Number)
                .Card("Impacts", "sword")
                .Field("impactDistance", "Ray distance", unit: "m", widget: VeneerWidget.Number)
                .Field("impactLayers", "Hit layers")
                .Card("Usage", "info")
                .Note(UsageNote)
                .Build();

            layout.ThemeBrand = SurfaceBrand.Value.Name;
            return layout;
        }

        /// <inheritdoc />
        public void RegisterItems()
        {
            SurfaceHandlerItems.Register();
        }
    }
}
