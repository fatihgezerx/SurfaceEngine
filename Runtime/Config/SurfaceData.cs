using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// Everything the surface system needs: general raycast settings on top, every surface below - organized in
    /// groups (e.g. "Grass", "Wood", "Snow"), each with its texture variants and what it sounds and looks like on
    /// Footstep and Impact (per weapon/damage type). Press "Compile" to generate the <see cref="SurfaceGroups"/> enum and the
    /// texture lookup <see cref="SurfaceEngine"/> uses to identify a hit's surface. At runtime, hand this asset
    /// to <see cref="SurfaceEngine.Initialize"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Surface Engine/Surface Data", fileName = "NewSurfaceData")]
    public sealed class SurfaceData : ScriptableObject
    {
        [SerializeField] private SurfaceSettings settings = new();
        [SerializeField] private List<SurfaceGroup> groups = new() { new SurfaceGroup() };

        // The next id to hand out. Never goes down, so a deleted group's id is never reused.
        [HideInInspector] [SerializeField] private int nextGroupId = 1;

        /// <summary>General raycast settings.</summary>
        public SurfaceSettings Settings => settings;

        /// <summary>Every surface group.</summary>
        public List<SurfaceGroup> Groups => groups;

        /// <summary>
        /// Gives every group without an id, or with an id another one already has (e.g. a duplicated list
        /// element), a fresh one. Returns true if anything changed.
        /// </summary>
        internal bool EnsureUniqueIds()
        {
            foreach (var group in groups)
            {
                if (group != null && group.id >= nextGroupId)
                {
                    nextGroupId = group.id + 1;
                }
            }

            var changed = false;
            var seen = new HashSet<int>();
            foreach (var group in groups)
            {
                if (group == null || (group.id > 0 && seen.Add(group.id)))
                {
                    continue;
                }

                group.id = nextGroupId++;
                seen.Add(group.id);
                changed = true;
            }

            return changed;
        }

#if UNITY_EDITOR
        private void OnValidate() => EnsureUniqueIds();
#endif
    }
}
