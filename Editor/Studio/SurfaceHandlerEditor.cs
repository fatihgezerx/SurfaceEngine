using Veneer;
using UnityEditor;

namespace SurfaceSystem
{
    /// <summary>
    /// The studio-look Inspector of <see cref="SurfaceHandler"/>, drawn from a Veneer layout: the project's own copy made in the Inspector
    /// Designer when there is one, otherwise <see cref="SurfaceHandlerDefaultLayout"/> (one card for the footstep ray, one for the impact
    /// ray, a usage note, and a state pill that says in Play Mode whether <see cref="SurfaceEngine"/> has been initialized). Without
    /// Veneer the component keeps Unity's default Inspector.
    /// </summary>
    [CustomEditor(typeof(SurfaceHandler))]
    [CanEditMultipleObjects]
    internal sealed class SurfaceHandlerEditor : VeneerLayoutDrivenInspector<SurfaceHandler>
    {
        /// <inheritdoc />
        protected override VeneerBrand Brand => SurfaceBrand.Value;
    }
}
