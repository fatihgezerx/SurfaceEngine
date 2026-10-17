using System;
using Veneer;
using UnityEngine;
using UnityEngine.UIElements;

namespace SurfaceSystem
{
    /// <summary>
    /// The pieces of the <see cref="SurfaceHandler"/> Inspector that are not fields, registered with Veneer so the default layout (and a
    /// user's copy of it) can place them: the state pill and the "SurfaceEngine is not initialized" hint. They can be moved or hidden in
    /// the Inspector Designer, not changed.
    /// </summary>
    internal static class SurfaceHandlerItems
    {
        /// <summary>Id of the header state pill (header badge).</summary>
        public const string StateId = "surface.handler-state";

        /// <summary>Id of the Play Mode hint shown while the engine is not initialized (card item).</summary>
        public const string NotInitializedId = "surface.handler-not-initialized";

        private const string NotInitializedText =
            "SurfaceEngine.Initialize has not been called yet: Footstep and Impact do nothing until it is.";

        /// <summary>Registers the custom items of <see cref="SurfaceHandler"/>. Safe to call again (items are replaced).</summary>
        public static void Register()
        {
            Type type = typeof(SurfaceHandler);

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = StateId,
                Label = "State",
                Icon = "check-circle",
                Description = "A pill in the header: Edit mode, or in Play Mode Ready / Engine not initialized.",
                Slot = VeneerCustomItemSlot.HeaderBadge,
                Build = BuildState,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = NotInitializedId,
                Label = "Not initialized hint",
                Icon = "warning",
                Description = "A warning in Play Mode while SurfaceEngine.Initialize has not been called.",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildNotInitialized,
            });
        }

        private static VisualElement BuildState(VeneerCustomItemContext context)
        {
            Label pill = VeneerStyle.Pill(string.Empty, "idle");
            Action refresh = () =>
            {
                if (!Application.isPlaying)
                {
                    VeneerStyle.SetPill(pill, "idle", "Edit mode");
                }
                else if (SurfaceEngine.IsInitialized)
                {
                    VeneerStyle.SetPill(pill, "ok", "Ready");
                }
                else
                {
                    VeneerStyle.SetPill(pill, "warn", "Engine not initialized");
                }
            };

            // The first run of Poll is on the first tick: show the right state at once.
            refresh();
            context.Poll(refresh);
            return pill;
        }

        private static VisualElement BuildNotInitialized(VeneerCustomItemContext context)
        {
            context.Builder.ConditionalHint(
                () => Application.isPlaying && !SurfaceEngine.IsInitialized ? NotInitializedText : null,
                "warn", true);
            return null;
        }
    }
}
