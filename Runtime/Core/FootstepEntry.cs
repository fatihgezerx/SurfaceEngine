using System;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// Audio, VFX and decal a <see cref="SurfaceGroup"/> plays for one <see cref="FootstepType"/> (e.g. its own
    /// sounds and dust for Run, different ones for JumpLand). One entry of <see cref="Sounds"/> and one of
    /// <see cref="Vfx"/> are picked at random each time. A group only needs entries for the types it wants to
    /// sound different - see <see cref="SurfaceGroup.ResolveFootstep"/> for the fallback to <see cref="FootstepType.Generic"/>.
    /// </summary>
    [Serializable]
    public sealed class FootstepEntry
    {
        [SerializeField] private FootstepType type = FootstepType.Generic;
        [SerializeField] private AudioClip[] sounds = Array.Empty<AudioClip>();
        [SerializeField] private ParticleSystem[] vfx = Array.Empty<ParticleSystem>();

        [Tooltip("Optional decal prefab (e.g. a footprint) placed at the hit point. Empty = no decal.")]
        [SerializeField] private GameObject decalPrefab;

        public FootstepType Type => type;
        public AudioClip[] Sounds => sounds;
        public ParticleSystem[] Vfx => vfx;
        public GameObject DecalPrefab => decalPrefab;
    }
}
