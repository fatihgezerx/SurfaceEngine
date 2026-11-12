using System;
using System.Globalization;
using Veneer;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SurfaceSystem
{
    /// <summary>
    /// The pieces of the <see cref="SurfaceTrailSource"/> Inspector that are not fields, registered with Veneer so the default layout (and
    /// a user's copy of it) can place them: the state pill and the Play Mode live line (speed, surface, water). They can be moved or
    /// hidden in the Inspector Designer, not changed.
    /// </summary>
    internal static class SurfaceTrailSourceItems
    {
        /// <summary>Id of the header state pill (header badge).</summary>
        public const string StateId = "surface.trail-source-state";

        /// <summary>Id of the Play Mode live line: speed, surface under the source and whether it is in water (card item).</summary>
        public const string LiveId = "surface.trail-source-live";

        private const string Separator = "  |  ";

        /// <summary>Registers the custom items of <see cref="SurfaceTrailSource"/>. Safe to call again (items are replaced).</summary>
        public static void Register()
        {
            Type type = typeof(SurfaceTrailSource);

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = StateId,
                Label = "State",
                Icon = "check-circle",
                Description = "A pill in the header: Edit mode, or in Play Mode Trails off / Waiting / Running.",
                Slot = VeneerCustomItemSlot.HeaderBadge,
                Build = BuildState,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = LiveId,
                Label = "Live line",
                Icon = "speed",
                Description = "In Play Mode: the source's speed, the surface under it and whether it is in water.",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildLive,
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
                else if (!SurfaceTrails.IsInitialized)
                {
                    VeneerStyle.SetPill(pill, "warn", "Trails off");
                }
                else if (SurfaceTrails.IsRunning)
                {
                    VeneerStyle.SetPill(pill, "ok", "Running");
                }
                else
                {
                    VeneerStyle.SetPill(pill, "info", "Waiting");
                }
            };

            // The first run of Poll is on the first tick: show the right state at once.
            refresh();
            context.Poll(refresh);
            return pill;
        }

        private static VisualElement BuildLive(VeneerCustomItemContext context)
        {
            var line = new LiveLine(context.Targets);
            context.Poll(line.Refresh);
            line.Refresh();
            return line.Strip;
        }

        /// <summary>
        /// The live strip. Polled five times a second, so it keeps what it last showed and builds a new text only when the speed (to a
        /// tenth), the surface or the water flag changed; a source standing still costs nothing.
        /// </summary>
        private sealed class LiveLine
        {
            private readonly Label label;
            private readonly SurfaceTrailSource source;
            private int shownTenths = -1;
            private string shownSurface;
            private bool shownWater;
            private bool shownVisible = true;

            public LiveLine(Object[] targets)
            {
                // One selected source only: with several there is no single speed to show.
                source = targets.Length == 1 ? targets[0] as SurfaceTrailSource : null;
                Strip = new VisualElement();
                Strip.AddToClassList("vn-live");
                label = new Label(string.Empty);
                label.AddToClassList("vn-live__text");
                Strip.Add(label);
                VeneerStyle.SetDisplay(Strip, false);
                shownVisible = false;
            }

            public VisualElement Strip { get; }

            public void Refresh()
            {
                bool visible = Application.isPlaying && source != null;
                if (visible != shownVisible)
                {
                    shownVisible = visible;
                    VeneerStyle.SetDisplay(Strip, visible);
                }

                if (!visible)
                {
                    shownTenths = -1;
                    return;
                }

                int tenths = Mathf.RoundToInt(source.Speed * 10f);
                SurfaceGroup surface = source.Surface;
                string surfaceName = surface != null ? surface.Header : null;
                bool water = source.InWater;
                if (tenths == shownTenths && water == shownWater && string.Equals(surfaceName, shownSurface, StringComparison.Ordinal))
                {
                    return;
                }

                shownTenths = tenths;
                shownSurface = surfaceName;
                shownWater = water;
                string speed = (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture) + " m/s";
                string where = string.IsNullOrEmpty(surfaceName) ? "none" : surfaceName;
                label.text = water ? speed + Separator + where + Separator + "in water" : speed + Separator + where;
            }
        }
    }
}
