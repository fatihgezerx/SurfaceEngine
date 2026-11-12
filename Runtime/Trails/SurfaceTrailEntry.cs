using System;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// What a <see cref="SurfaceTrailSource"/> does on a <see cref="SurfaceGroup"/>: how deep its prints go (snow, sand, mud), whether
    /// the surface is water (ripples), and which particles it kicks up while moving fast enough (dust, snow spray). Grass bending needs
    /// nothing here - grass bends everywhere. All of it is optional: the defaults make a hard, dry surface with no kick-up.
    /// </summary>
    [Serializable]
    public sealed class SurfaceTrailEntry
    {
        [Tooltip("How deep prints go in this surface: 0 = hard (no print), 1 = the full print depth of the Trail Deform material.")]
        [Range(0f, 1f)] [SerializeField] private float depth;

        [Tooltip("Sources standing on this surface make ripples in it (the surface is water). A SurfaceWaterVolume does the same for water without a collider.")]
        [SerializeField] private bool water;

        [Tooltip("Particles kicked up while a source moves on this surface (dust on sand, spray in snow). Emission scales with the source's speed. Empty = none.")]
        [SerializeField] private ParticleSystem kickUp;

        [Tooltip("Speed (m/s) below which nothing is kicked up.")]
        [Min(0f)] [SerializeField] private float kickUpMinSpeed = 1f;

        [Tooltip("Speed (m/s) at which the kick-up reaches its full rate.")]
        [Min(0.1f)] [SerializeField] private float kickUpFullSpeed = 5f;

        [Tooltip("Particles per second at full speed.")]
        [Min(0f)] [SerializeField] private float kickUpRate = 40f;

        public float Depth => depth;
        public bool Water => water;
        public ParticleSystem KickUp => kickUp;
        public float KickUpMinSpeed => kickUpMinSpeed;
        public float KickUpFullSpeed => kickUpFullSpeed;
        public float KickUpRate => kickUpRate;

        /// <summary>The kick-up rate (particles per second) for a source moving at <paramref name="speed"/>: 0 below the minimum, full at or above the full speed.</summary>
        public float KickUpRateAt(float speed)
        {
            if (kickUp == null || kickUpRate <= 0f || speed < kickUpMinSpeed)
            {
                return 0f;
            }

            var span = Mathf.Max(kickUpFullSpeed - kickUpMinSpeed, 0.01f);
            return kickUpRate * Mathf.Clamp01((speed - kickUpMinSpeed) / span);
        }
    }
}
