using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// Attach to any controller that should play Footsteps/Impacts - player, NPC, or an animal - and call
    /// <see cref="Footstep"/>/<see cref="Impact"/> from wherever fits it (a movement loop, an animation event,
    /// a weapon's own hit raycast). Wraps <see cref="SurfaceEngine"/> with this instance's own ray settings so
    /// callers never touch SurfaceEngine directly; does nothing if SurfaceEngine hasn't been initialized yet
    /// (e.g. by GameManager).
    /// </summary>
    public sealed class SurfaceHandler : MonoBehaviour
    {
        [Tooltip("Where Footstep rays start from, pointing down. Empty = this transform.")]
        [SerializeField] private Transform footstepOrigin;

        [Tooltip("How far down the Footstep ray goes.")]
        [Min(0.1f)] [SerializeField] private float footstepDistance = 3f;

        [Tooltip("How far forward an Impact ray goes from the origin passed to Impact().")]
        [Min(0.1f)] [SerializeField] private float impactDistance = 50f;

        [SerializeField] private LayerMask impactLayers = ~0;

        /// <summary>
        /// Raycasts straight down from Footstep Origin (or this transform) and plays the identified surface's
        /// Footstep. <paramref name="type"/> picks which of the surface's entries to use (Walk, Run, JumpLand...);
        /// defaults to Generic, which is the only one with sounds assigned right now.
        /// </summary>
        public SurfaceGroup Footstep(FootstepType type = FootstepType.Generic)
        {
            if (!SurfaceEngine.IsInitialized)
            {
                return null;
            }

            var origin = footstepOrigin != null ? footstepOrigin : transform;
            return SurfaceEngine.Footstep(origin, type, footstepDistance);
        }

        /// <summary>
        /// Raycasts forward from <paramref name="origin"/> (e.g. the player's camera, an animal's claw) and
        /// plays the identified surface's Impact. <paramref name="type"/> picks which entry to use (Sword,
        /// Gunshot...); defaults to Generic, which is the only one with sounds assigned right now.
        /// </summary>
        public SurfaceGroup Impact(Transform origin, ImpactType type = ImpactType.Generic)
        {
            if (!SurfaceEngine.IsInitialized || origin == null)
            {
                return null;
            }

            return Physics.Raycast(origin.position, origin.forward, out var hit, impactDistance, impactLayers, QueryTriggerInteraction.Ignore)
                ? SurfaceEngine.Impact(hit, type)
                : null;
        }
    }
}
