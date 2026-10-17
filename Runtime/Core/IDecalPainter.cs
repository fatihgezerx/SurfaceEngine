using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// What lays a decal on a surface. The Surface Engine does not know how: a painter is provided by the optional
    /// decals assembly (URP's Decal Projector) and registers itself in <see cref="SurfaceEngine.DecalPainter"/>, so a
    /// project without it simply gets no decals. The painter works on the host object an impact is played from - the
    /// same one that holds its sound and its particles.
    /// </summary>
    public interface IDecalPainter
    {
        /// <summary>
        /// Projects <paramref name="material"/> onto the surface at <paramref name="point"/> (facing out along
        /// <paramref name="normal"/>), sized and turned by <paramref name="shape"/> (or the settings' default size when it has
        /// none). Returns false when it could not.
        /// </summary>
        bool Paint(GameObject host, Material material, Vector3 point, Vector3 normal, in ImpactShape shape, SurfaceSettings settings);

        /// <summary>How visible the decal on <paramref name="host"/> is: 1 fully, 0 not at all.</summary>
        void SetVisible(GameObject host, float visible);

        /// <summary>Takes the decal off <paramref name="host"/>, so it can be reused for something else.</summary>
        void Clear(GameObject host);
    }
}
