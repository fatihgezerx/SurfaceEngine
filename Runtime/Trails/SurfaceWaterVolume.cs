using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// Marks a volume of water for the trail maps: a <see cref="SurfaceTrailSource"/> inside it makes ripples. Put it on a trigger
    /// collider that covers the water (a lake, a river stretch, a pool). Only needed for water that has no collider of its own in the
    /// ground layers - a water surface the identifying ray can hit is flagged with the surface's <see cref="SurfaceTrailEntry.Water"/> instead.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SurfaceWaterVolume : MonoBehaviour
    {
        private void Reset()
        {
            if (TryGetComponent<Collider>(out var collider))
            {
                collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // A lookup on enter and exit only - never per frame.
            var source = other.GetComponentInParent<SurfaceTrailSource>();
            if (source != null)
            {
                source.EnterWater();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var source = other.GetComponentInParent<SurfaceTrailSource>();
            if (source != null)
            {
                source.ExitWater();
            }
        }
    }
}
