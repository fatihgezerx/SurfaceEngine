using Veneer;
using UnityEditor;

namespace SurfaceSystem
{
    /// <summary>
    /// The studio-look Inspector of <see cref="SurfaceWind"/>, drawn from a Veneer layout: the project's own copy made in the Inspector
    /// Designer when there is one, otherwise <see cref="SurfaceWindDefaultLayout"/> (a Wind card, a usage note and a state pill). Without
    /// Veneer the component keeps Unity's default Inspector.
    /// </summary>
    [CustomEditor(typeof(SurfaceWind))]
    [CanEditMultipleObjects]
    internal sealed class SurfaceWindEditor : VeneerLayoutDrivenInspector<SurfaceWind>
    {
        /// <inheritdoc />
        protected override VeneerBrand Brand => SurfaceBrand.Value;
    }
}
