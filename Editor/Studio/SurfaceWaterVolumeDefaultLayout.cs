using System;
using Veneer;

namespace SurfaceSystem
{
    /// <summary>
    /// The design of the <see cref="SurfaceWaterVolume"/> Inspector that Surface Engine ships: one Water volume card with an explanation
    /// and the system's own "collider is not a trigger" hint. The component has no fields. With Veneer installed a project can copy it in
    /// the Inspector Designer and change the copy; this default is never saved as an asset.
    /// </summary>
    internal sealed class SurfaceWaterVolumeDefaultLayout : IVeneerDefaultLayoutProvider
    {
        private const string VolumeNote =
            "Trail sources inside this trigger make ripples. The collider on this object must be a trigger that covers the water.";

        /// <inheritdoc />
        public Type TargetType => typeof(SurfaceWaterVolume);

        /// <inheritdoc />
        public VeneerLayout CreateLayout()
        {
            VeneerLayout layout = new VeneerLayoutBuilder(typeof(SurfaceWaterVolume), "Surface Water Volume", "Marks water for the trail ripples")
                .Card("Water volume", "globe")
                .Note(VolumeNote)
                .Custom(SurfaceWaterVolumeItems.TriggerHintId)
                .Build();

            layout.ThemeBrand = SurfaceBrand.Value.Name;
            return layout;
        }

        /// <inheritdoc />
        public void RegisterItems()
        {
            SurfaceWaterVolumeItems.Register();
        }
    }
}
