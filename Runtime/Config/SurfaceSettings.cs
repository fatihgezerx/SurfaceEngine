using System;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// General raycast defaults, shown at the top of a <see cref="SurfaceData"/>. Used by
    /// <see cref="SurfaceEngine"/> whenever a caller doesn't pass its own distance or layer mask.
    /// </summary>
    [Serializable]
    public sealed class SurfaceSettings
    {
        [Tooltip("Layers considered ground when Footstep(Transform)/Impact raycast on their own.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Tooltip("How Footstep/Impact sounds are played - see AudioPlaybackMode.")]
        [SerializeField] private AudioPlaybackMode audioPlaybackMode = AudioPlaybackMode.Instantiate;

        [Tooltip("The exact PoolTypes member name your Pool Data asset Compiled the Footstep prefab into (e.g. \"FootstepSurface\"). Each Footstep takes one instance of it, plays its sound on it and puts its decal and VFX on it too. The prefab needs an AudioSource. Required by Pool mode.")]
        [UnityEngine.Serialization.FormerlySerializedAs("poolAudioTypeName")]
        [SerializeField] private string poolFootstepTypeName;

        [Tooltip("The same for Impacts (e.g. \"ImpactSurface\"): the pool whose prefab plays an impact's sound, decal and VFX. Empty = Impacts use the Footstep pool.")]
        [SerializeField] private string poolImpactTypeName;

        [Tooltip("How far down Footstep(Transform) raycasts when no distance is given.")]
        [Range(0.05f, 10f)] [SerializeField] private float defaultRayDistance = 1f;

        [Tooltip("Seconds a decal stays on the surface before it's released back to its pool (or destroyed). Sound and VFX without a decal are released as soon as they are done.")]
        [Range(0.1f, 60f)] [SerializeField] private float effectLifetime = 20f;

        [Tooltip("Size in meters of a decal when the hit does not say how big the mark should be (a footprint, a bullet hole).")]
        [SerializeField] private Vector2 decalSize = new(0.3f, 0.3f);

        [Tooltip("How deep (meters) the decal reaches into the surface. A deeper decal covers more of a curved or uneven surface; too deep and it shows on whatever lies behind.")]
        [Range(0.05f, 2f)] [SerializeField] private float decalDepth = 0.3f;

        [Tooltip("Seconds a decal takes to fade out at the end of its life (inside the Effect Lifetime).")]
        [Range(0f, 10f)] [SerializeField] private float decalFadeTime = 2f;

        public LayerMask GroundLayers => groundLayers;
        public AudioPlaybackMode AudioPlaybackMode => audioPlaybackMode;
        public string PoolFootstepTypeName => poolFootstepTypeName;

        /// <summary>The pool Impacts play from: <see cref="PoolImpactTypeName"/>, or the footstep one when that is empty.</summary>
        public string PoolImpactTypeName => string.IsNullOrEmpty(poolImpactTypeName) ? poolFootstepTypeName : poolImpactTypeName;
        public float DefaultRayDistance => defaultRayDistance;
        public float EffectLifetime => effectLifetime;
        public Vector2 DecalSize => decalSize;
        public float DecalDepth => decalDepth;
        public float DecalFadeTime => decalFadeTime;
    }
}
