using System;
using Veneer;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SurfaceSystem
{
    /// <summary>
    /// The piece of the <see cref="SurfaceWaterVolume"/> Inspector that is not a field, registered with Veneer so the default layout (and
    /// a user's copy of it) can place it: the "collider is not a trigger" hint. It can be moved or hidden in the Inspector Designer, not changed.
    /// </summary>
    internal static class SurfaceWaterVolumeItems
    {
        /// <summary>Id of the hint shown while a selected volume's collider is not a trigger (card item).</summary>
        public const string TriggerHintId = "surface.water-volume-trigger-hint";

        private const string TriggerHintText = "The collider is not a trigger: sources will not be detected.";

        /// <summary>Registers the custom items of <see cref="SurfaceWaterVolume"/>. Safe to call again (items are replaced).</summary>
        public static void Register()
        {
            VeneerCustomItems.Register(typeof(SurfaceWaterVolume), new VeneerCustomItem
            {
                Id = TriggerHintId,
                Label = "Trigger check",
                Icon = "warning",
                Description = "A warning while the collider on this object is not a trigger (sources would not be detected).",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildTriggerHint,
            });
        }

        private static VisualElement BuildTriggerHint(VeneerCustomItemContext context)
        {
            Object[] targets = context.Targets;
            context.Builder.ConditionalHint(() => AnyNotTrigger(targets) ? TriggerHintText : null, "warn", true);
            return null;
        }

        // Several selected volumes: warn if any of them is wrong. The collider is looked up on the 200 ms poll only, never per frame.
        private static bool AnyNotTrigger(Object[] targets)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is SurfaceWaterVolume volume && volume.TryGetComponent<Collider>(out Collider collider) && !collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
