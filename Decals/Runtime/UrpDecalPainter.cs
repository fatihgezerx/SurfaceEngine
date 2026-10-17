using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SurfaceSystem
{
    /// <summary>
    /// Lays an impact's decal with URP's Decal Projector: the mark is projected onto whatever surface lies inside a thin box in
    /// front of the hit, so it follows a curved wall, a sphere, an edge or a corner instead of standing off the surface like a
    /// flat sticker would. The projector lives on the host object the impact plays from (the one with the sound and the
    /// particles), so a pooled host is simply reused. Registers itself with the engine; nothing to set up in the scene.
    /// </summary>
    /// <remarks>
    /// Needs the Universal Render Pipeline with the <b>Decal</b> renderer feature on the renderer asset (Tools &gt; Surface
    /// Engine &gt; Create Impact Effects adds it), and a material that uses a URP Decal shader. The projector is 0.05 m above the
    /// hit and as deep as the settings say, fades out where the surface turns away from it (so it does not smear across a
    /// perpendicular face) and has no per-frame work.
    /// </remarks>
    public sealed class UrpDecalPainter : IDecalPainter
    {
        // How far in front of the hit the projector starts, so a surface that dips towards the weapon is still inside it.
        private const float StandOff = 0.05f;

        // Each mark differs a little in size from the shape it is given.
        private const float SizeVariation = 0.15f;

        // Where the decal begins to fade and where it is gone, as the angle between the projector and the surface it lands on.
        private const float StartAngleFade = 60f;
        private const float EndAngleFade = 90f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => SurfaceEngine.DecalPainter = new UrpDecalPainter();

        /// <inheritdoc />
        public bool Paint(GameObject host, Material material, Vector3 point, Vector3 normal, in ImpactShape shape, SurfaceSettings settings)
        {
            if (!host.TryGetComponent<DecalProjector>(out var projector))
            {
                projector = host.AddComponent<DecalProjector>();
            }

            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;

            float length;
            float width;
            if (shape.HasSize)
            {
                var variation = 1f + Random.Range(-SizeVariation, SizeVariation);
                length = shape.Length * variation;
                width = shape.Width * variation;
            }
            else
            {
                width = settings.DecalSize.x;
                length = settings.DecalSize.y;
            }

            // The projector looks into the surface (forward = -normal), the mark's length along its up.
            host.transform.SetPositionAndRotation(point + normal * StandOff, Quaternion.LookRotation(-normal, shape.AlongSurface(normal)));

            var depth = Mathf.Max(settings.DecalDepth, StandOff * 2f);
            projector.material = material;
            projector.scaleMode = DecalScaleMode.ScaleInvariant;
            projector.size = new Vector3(width, length, depth);
            projector.pivot = new Vector3(0f, 0f, depth * 0.5f);
            projector.startAngleFade = StartAngleFade;
            projector.endAngleFade = EndAngleFade;
            projector.fadeFactor = 1f;
            projector.enabled = true;
            return true;
        }

        /// <inheritdoc />
        public void SetVisible(GameObject host, float visible)
        {
            if (host.TryGetComponent<DecalProjector>(out var projector))
            {
                projector.fadeFactor = Mathf.Clamp01(visible);
            }
        }

        /// <inheritdoc />
        public void Clear(GameObject host)
        {
            if (host.TryGetComponent<DecalProjector>(out var projector))
            {
                projector.enabled = false;
            }
        }
    }
}
