using System;
using Veneer;

namespace SurfaceSystem
{
    /// <summary>
    /// The design of the <see cref="SurfaceWind"/> Inspector that Surface Engine ships: a Wind card (optional Wind Zone, strength and gusts
    /// that only show without a zone, gust size and speed, turbulence), a Usage note and the system's own state pill. With Veneer
    /// installed a project can copy it in the Inspector Designer and change the copy; this default is never saved as an asset.
    /// </summary>
    internal sealed class SurfaceWindDefaultLayout : IVeneerDefaultLayoutProvider
    {
        private const string UsageNote =
            "The wind blows along this object's forward (or the Wind Zone's). Trail Grass and Trail Water read it through shader globals; " +
            "particles follow a Wind Zone through their External Forces module.";

        /// <inheritdoc />
        public Type TargetType => typeof(SurfaceWind);

        /// <inheritdoc />
        public VeneerLayout CreateLayout()
        {
            VeneerLayout layout = new VeneerLayoutBuilder(typeof(SurfaceWind), "Surface Wind", "One global wind for grass and water")
                .Card("Wind", "water")
                .Custom(SurfaceWindItems.StateId)
                .Field("windZone", "Wind Zone (optional)")
                .Slider("strength", "Strength", "Calm", "Storm").ShowIf("windZone", VeneerCondition.IsNull)
                .Slider("gusts", "Gusts", "Steady", "Gusty").ShowIf("windZone", VeneerCondition.IsNull)
                .Slider("gustSize", "Gust size", "Small", "Wide", unit: "m")
                .Slider("gustSpeed", "Gust speed", "Slow", "Fast", unit: "m/s")
                .Slider("turbulence", "Turbulence", "Even", "Lively")
                .Card("Usage", "info")
                .Note(UsageNote)
                .Build();

            layout.ThemeBrand = SurfaceBrand.Value.Name;
            return layout;
        }

        /// <inheritdoc />
        public void RegisterItems()
        {
            SurfaceWindItems.Register();
        }
    }
}
