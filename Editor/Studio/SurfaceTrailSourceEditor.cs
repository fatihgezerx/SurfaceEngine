using Veneer;
using UnityEditor;

namespace SurfaceSystem
{
    /// <summary>
    /// The studio-look Inspector of <see cref="SurfaceTrailSource"/>, drawn from a Veneer layout: the project's own copy made in the
    /// Inspector Designer when there is one, otherwise <see cref="SurfaceTrailSourceDefaultLayout"/> (a Trail card, a usage note, a state
    /// pill and, in Play Mode, a live line with the speed and the surface). Without Veneer the component keeps Unity's default Inspector.
    /// </summary>
    [CustomEditor(typeof(SurfaceTrailSource))]
    [CanEditMultipleObjects]
    internal sealed class SurfaceTrailSourceEditor : VeneerLayoutDrivenInspector<SurfaceTrailSource>
    {
        /// <inheritdoc />
        protected override VeneerBrand Brand => SurfaceBrand.Value;
    }
}
