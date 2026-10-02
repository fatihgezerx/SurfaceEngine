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

        [Tooltip("The exact PoolTypes member name your Pool Data asset Compiled this sound's prefab into (e.g. \"Footstep\"). Required by Pool mode.")]
        [SerializeField] private string poolAudioTypeName;

        [Tooltip("How far down Footstep(Transform) raycasts when no distance is given.")]
        [Range(0.05f, 10f)] [SerializeField] private float defaultRayDistance = 1f;

        [Tooltip("Seconds a spawned VFX or decal instance stays before it's released back to its pool (or destroyed).")]
        [Range(0.1f, 60f)] [SerializeField] private float effectLifetime = 20f;

        public LayerMask GroundLayers => groundLayers;
        public AudioPlaybackMode AudioPlaybackMode => audioPlaybackMode;
        public string PoolAudioTypeName => poolAudioTypeName;
        public float DefaultRayDistance => defaultRayDistance;
        public float EffectLifetime => effectLifetime;
    }
}
