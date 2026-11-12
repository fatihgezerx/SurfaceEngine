using System;
using Veneer;
using UnityEngine;
using UnityEngine.UIElements;

namespace SurfaceSystem
{
    /// <summary>
    /// The pieces of the <see cref="SurfaceWind"/> Inspector that are not fields, registered with Veneer so the default layout (and a
    /// user's copy of it) can place them: the header state pill (Edit mode, or in Play Mode Blowing / Calm). It can be moved or hidden in
    /// the Inspector Designer, not changed.
    /// </summary>
    internal static class SurfaceWindItems
    {
        /// <summary>Id of the header state pill (header badge).</summary>
        public const string StateId = "surface.wind-state";

        /// <summary>Registers the custom items of <see cref="SurfaceWind"/>. Safe to call again (items are replaced).</summary>
        public static void Register()
        {
            VeneerCustomItems.Register(typeof(SurfaceWind), new VeneerCustomItem
            {
                Id = StateId,
                Label = "State",
                Icon = "check-circle",
                Description = "A pill in the header: Edit mode, or in Play Mode Blowing / Calm.",
                Slot = VeneerCustomItemSlot.HeaderBadge,
                Build = BuildState,
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
                else if (SurfaceWind.IsActive)
                {
                    VeneerStyle.SetPill(pill, "ok", "Blowing");
                }
                else
                {
                    VeneerStyle.SetPill(pill, "idle", "Calm");
                }
            };

            // The first run of Poll is on the first tick: show the right state at once.
            refresh();
            context.Poll(refresh);
            return pill;
        }
    }
}
