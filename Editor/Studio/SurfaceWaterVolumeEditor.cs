using Veneer;
using UnityEditor;

namespace SurfaceSystem
{
    /// <summary>
    /// The studio-look Inspector of <see cref="SurfaceWaterVolume"/>, drawn from a Veneer layout: the project's own copy made in the
    /// Inspector Designer when there is one, otherwise <see cref="SurfaceWaterVolumeDefaultLayout"/> (one card with an explanation and a
    /// warning while the collider is not a trigger). Without Veneer the component keeps Unity's default Inspector.
    /// </summary>
    [CustomEditor(typeof(SurfaceWaterVolume))]
    [CanEditMultipleObjects]
    internal sealed class SurfaceWaterVolumeEditor : VeneerLayoutDrivenInspector<SurfaceWaterVolume>
    {
        /// <inheritdoc />
        protected override VeneerBrand Brand => SurfaceBrand.Value;
    }
}
