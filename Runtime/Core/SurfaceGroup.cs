using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// A named surface (e.g. "Grass", "Wood", "Snow") with every texture variant that counts as it, and what it
    /// sounds and looks like on Footstep and Impact. A group's name becomes its <see cref="SurfaceGroups"/>
    /// member on Compile. Its <see cref="Textures"/> are what <see cref="SurfaceEngine"/> matches a hit
    /// renderer's material (or a Terrain layer's diffuse texture) against to identify which group was hit -
    /// they aren't just previews, they're the runtime identification key.
    /// </summary>
    [Serializable]
    public sealed class SurfaceGroup
    {
        // Stable SurfaceGroups value, assigned once by SurfaceData and never reused, so renaming or reordering
        // groups never shifts a SurfaceGroups already saved in a scene or prefab.
        [HideInInspector] [SerializeField] internal int id;

        [SerializeField] private string header = "New Surface";
        [SerializeField] private List<Texture2D> textures = new();
        [SerializeField] private List<FootstepEntry> footsteps = new() { new FootstepEntry() };
        [SerializeField] private List<ImpactEntry> impacts = new() { new ImpactEntry() };

        /// <summary>The group's stable id - the same number as <see cref="Type"/>.</summary>
        public int Id => id;

        /// <summary>The group's <see cref="SurfaceGroups"/> member.</summary>
        public SurfaceGroups Type => (SurfaceGroups)id;

        public string Header => header;

        /// <summary>Every texture that identifies a hit as this surface (a terrain layer's diffuse, or a renderer's main texture).</summary>
        public List<Texture2D> Textures => textures;

        /// <summary>Every step kind (Walk, Run, JumpLand...) this surface sounds or looks different for. Only needs entries for the types that matter - see <see cref="ResolveFootstep"/>.</summary>
        public List<FootstepEntry> Footsteps => footsteps;

        /// <summary>
        /// The entry for <paramref name="type"/>, or this group's <see cref="FootstepType.Generic"/> entry if
        /// <paramref name="type"/> has none of its own, or null if neither exists.
        /// </summary>
        public FootstepEntry ResolveFootstep(FootstepType type)
        {
            FootstepEntry generic = null;
            foreach (var entry in footsteps)
            {
                if (entry == null)
                {
                    continue;
                }

                if (entry.Type == type)
                {
                    return entry;
                }

                if (entry.Type == FootstepType.Generic)
                {
                    generic = entry;
                }
            }

            return generic;
        }

        /// <summary>Every weapon/damage type this surface sounds or looks different for. Only needs entries for the types that matter - see <see cref="ResolveImpact"/>.</summary>
        public List<ImpactEntry> Impacts => impacts;

        /// <summary>
        /// The entry for <paramref name="type"/>, or this group's <see cref="ImpactType.Generic"/> entry if
        /// <paramref name="type"/> has none of its own, or null if neither exists.
        /// </summary>
        public ImpactEntry ResolveImpact(ImpactType type)
        {
            ImpactEntry generic = null;
            foreach (var entry in impacts)
            {
                if (entry == null)
                {
                    continue;
                }

                if (entry.Type == type)
                {
                    return entry;
                }

                if (entry.Type == ImpactType.Generic)
                {
                    generic = entry;
                }
            }

            return generic;
        }
    }
}
