using Veneer;
using UnityEditor;

namespace SurfaceSystem
{
    /// <summary>
    /// The studio-look Inspector of <see cref="SurfaceData"/>, drawn from a Veneer layout: the project's own copy made in the Inspector
    /// Designer when there is one, otherwise <see cref="SurfaceDataDefaultLayout"/> (tabs Surfaces and Settings). The state pill, the
    /// Compile row, the validation hints, the pool hint and the list of surfaces are custom items and a custom widget
    /// (<see cref="SurfaceDataItems"/>, <see cref="SurfaceGroupsView"/>). Only a selection of several assets is built here: a header and a hint.
    /// </summary>
    [CustomEditor(typeof(SurfaceData))]
    internal sealed class SurfaceDataStudioEditor : VeneerLayoutDrivenInspector<SurfaceData>
    {
        /// <inheritdoc />
        protected override VeneerBrand Brand => SurfaceBrand.Value;

        /// <inheritdoc />
        protected override bool BuildMultiSelection(VeneerInspectorBuilder b)
        {
            b.Header("Surface Data", "Several assets selected", null);
            b.Hint("Select one Surface Data asset at a time to edit its surfaces.", "info");
            return true;
        }
    }
}
