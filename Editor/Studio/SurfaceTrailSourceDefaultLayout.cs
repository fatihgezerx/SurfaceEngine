using System;
using Veneer;

namespace SurfaceSystem
{
    /// <summary>
    /// The design of the <see cref="SurfaceTrailSource"/> Inspector that Surface Engine ships: a Trail card (radius, grass bend, pressure,
    /// ripples) and a Usage note, plus the system's own items (state pill, Play Mode live line). With Veneer installed a project can
    /// copy it in the Inspector Designer and change the copy; this default is never saved as an asset.
    /// </summary>
    internal sealed class SurfaceTrailSourceDefaultLayout : IVeneerDefaultLayoutProvider
    {
        private const string UsageNote =
            "Stamped into the trail maps every frame while enabled. The surface under it decides prints and kick-up " +
            "(Surface Data > surface > Trail); a Surface Water Volume marks water without a collider.";

        /// <inheritdoc />
        public Type TargetType => typeof(SurfaceTrailSource);

        /// <inheritdoc />
        public VeneerLayout CreateLayout()
        {
            VeneerLayout layout = new VeneerLayoutBuilder(typeof(SurfaceTrailSource), "Surface Trail Source", "Leaves trails in grass, water, snow and sand")
                .Card("Trail", "move")
                .Custom(SurfaceTrailSourceItems.StateId)
                .Custom(SurfaceTrailSourceItems.LiveId)
                .Field("radius", "Radius", unit: "m", widget: VeneerWidget.Number)
                .Slider("bendStrength", "Grass bend", "None", "Flat")
                .Slider("pressure", "Pressure", "Light", "Heavy")
                .Slider("rippleStrength", "Ripples", "None", "Big")
                .Card("Usage", "info")
                .Note(UsageNote)
                .Build();

            layout.ThemeBrand = SurfaceBrand.Value.Name;
            return layout;
        }

        /// <inheritdoc />
        public void RegisterItems()
        {
            SurfaceTrailSourceItems.Register();
        }
    }
}
