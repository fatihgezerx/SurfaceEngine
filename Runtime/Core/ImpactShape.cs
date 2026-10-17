using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// How big and which way the mark of an Impact is: a gash as long as the blade that made it, along the way the blade
    /// travelled, or a round dent as wide as the hammer. Passed to <see cref="SurfaceEngine.Impact(Vector3, Vector3, Collider, ImpactType, int, ImpactShape)"/>;
    /// the decal is sized and turned to fit it. Without a shape it gets the
    /// default size from the settings.
    /// </summary>
    public readonly struct ImpactShape
    {
        /// <summary>The way the mark runs (any direction; it is projected onto the surface). Zero = any direction.</summary>
        public readonly Vector3 Direction;

        /// <summary>Meters along <see cref="Direction"/>. 0 = no shape: the decal keeps its own size.</summary>
        public readonly float Length;

        /// <summary>Meters across it.</summary>
        public readonly float Width;

        /// <summary>Plays the sound and effect but leaves no decal - for a hit on something that moves (a character), where a decal would hang in the air.</summary>
        public readonly bool SkipDecal;

        public ImpactShape(Vector3 direction, float length, float width, bool skipDecal = false)
        {
            Direction = direction;
            Length = length;
            Width = width;
            SkipDecal = skipDecal;
        }

        /// <summary>
        /// The way the mark runs on a surface facing <paramref name="normal"/>: <see cref="Direction"/> projected onto it, or - when
        /// there is none, or it points straight into the surface - any direction on it.
        /// </summary>
        public Vector3 AlongSurface(Vector3 normal)
        {
            var along = Vector3.ProjectOnPlane(Direction, normal);
            if (along.sqrMagnitude < 1e-4f)
            {
                along = Vector3.Cross(normal, Random.onUnitSphere);
                if (along.sqrMagnitude < 1e-4f)
                {
                    along = Vector3.Cross(normal, Vector3.right);
                }
            }

            return along.normalized;
        }

        /// <summary>Whether the shape asks for a particular size.</summary>
        public bool HasSize => Length > 0f && Width > 0f;

        /// <summary>No shape, with the decal as usual.</summary>
        public static ImpactShape None => default;

        /// <summary>No shape and no decal.</summary>
        public static ImpactShape NoDecal => new ImpactShape(Vector3.zero, 0f, 0f, true);
    }
}
