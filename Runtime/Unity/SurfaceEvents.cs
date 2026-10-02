using UnityEngine;

namespace SurfaceSystem
{
    // Published through EventSystem's EventManager, e.g.:
    //     EventManager.Register<FootstepEvent>(OnFootstep);
    // All of them are readonly structs, so raising them never allocates.

    /// <summary>A Footstep of <see cref="Type"/> was identified as <see cref="Group"/> at <see cref="Point"/>.</summary>
    public readonly struct FootstepEvent
    {
        public readonly SurfaceGroup Group;
        public readonly FootstepType Type;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Collider Collider;

        public FootstepEvent(SurfaceGroup group, FootstepType type, Vector3 point, Vector3 normal, Collider collider)
        {
            Group = group;
            Type = type;
            Point = point;
            Normal = normal;
            Collider = collider;
        }
    }

    /// <summary>An Impact of <see cref="Type"/> was identified as <see cref="Group"/> at <see cref="Point"/>.</summary>
    public readonly struct ImpactEvent
    {
        public readonly SurfaceGroup Group;
        public readonly ImpactType Type;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly Collider Collider;

        public ImpactEvent(SurfaceGroup group, ImpactType type, Vector3 point, Vector3 normal, Collider collider)
        {
            Group = group;
            Type = type;
            Point = point;
            Normal = normal;
            Collider = collider;
        }
    }
}
