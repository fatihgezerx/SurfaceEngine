using System;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// Audio, VFX and decal a <see cref="SurfaceGroup"/> plays for one <see cref="ImpactType"/> (e.g. its own
    /// sound and decal for a Sword hit, different ones for a Gun hit). One entry of <see cref="Sounds"/> and
    /// one of <see cref="Vfx"/> are picked at random each time. A group only needs entries for the types it
    /// wants to sound different - see <see cref="SurfaceGroup.ResolveImpact"/> for the fallback to <see cref="ImpactType.Generic"/>.
    /// </summary>
    [Serializable]
    public sealed class ImpactEntry
    {
        [SerializeField] private ImpactType type = ImpactType.Generic;
        [SerializeField] private AudioClip[] sounds = Array.Empty<AudioClip>();
        [SerializeField] private ParticleSystem[] vfx = Array.Empty<ParticleSystem>();

        [Tooltip("Optional decal material (a URP Decal shader material, e.g. a sword gash) projected onto the surface at the hit point. Empty = no decal.")]
        [SerializeField] private Material decalMaterial;

        public ImpactType Type => type;
        public AudioClip[] Sounds => sounds;
        public ParticleSystem[] Vfx => vfx;
        public Material DecalMaterial => decalMaterial;
    }
}
