using System;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// How the trail maps behave (<see cref="SurfaceSettings.Trails"/>): the area they cover around the focus, their resolution,
    /// how fast grass stands back up, how water ripples settle and whether prints in snow or sand fill back. Read by
    /// <see cref="SurfaceTrails"/> at <see cref="SurfaceTrails.Initialize"/>; a change at runtime needs a new Initialize.
    /// </summary>
    [Serializable]
    public sealed class SurfaceTrailSettings
    {
        [Tooltip("Whether SurfaceEngine.Initialize also starts the trail maps (grass bending, water ripples, snow and sand prints).")]
        [SerializeField] private bool enabled;

        [Tooltip("Meters covered by the trail maps around the focus (the first trail source, or SurfaceTrails.Focus). Trails exist only inside it.")]
        [Range(16f, 512f)] [SerializeField] private float areaSize = 64f;

        [Tooltip("Texels per side of each map. Medium with a 64 m area gives 12.5 cm texels.")]
        [SerializeField] private SurfaceTrailResolution resolution = SurfaceTrailResolution.Medium;

        [Tooltip("How fast bent grass stands back up, per second (2 = about half way back in a third of a second). 0 = it stays bent.")]
        [Range(0f, 10f)] [SerializeField] private float grassRecovery = 2f;

        [Tooltip("Energy a water ripple loses per simulation step. Small = ripples travel far; large = they die near the source.")]
        [Range(0.001f, 0.2f)] [SerializeField] private float rippleFade = 0.02f;

        [Tooltip("Simulation steps per second of the water ripples. Higher = faster, finer waves at more GPU cost.")]
        [Range(20, 120)] [SerializeField] private int rippleRate = 60;

        [Tooltip("How fast prints in snow or sand fill back, as a fraction of their depth per second. 0 = prints stay until the map scrolls away.")]
        [Range(0f, 1f)] [SerializeField] private float printRecovery;

        [Tooltip("Rain drops a second on the water at full rain (SurfaceTrails.Rain = 1), each a small ripple somewhere in the area.")]
        [Range(0, 2000)] [SerializeField] private int rainDropsPerSecond = 300;

        [Tooltip("Seconds between the raycasts a trail source uses to find out which surface it is on (depth, water, kick-up particles).")]
        [Range(0.02f, 1f)] [SerializeField] private float identifyInterval = 0.1f;

        public bool Enabled => enabled;
        public float AreaSize => areaSize;
        public SurfaceTrailResolution Resolution => resolution;
        public float GrassRecovery => grassRecovery;
        public float RippleFade => rippleFade;
        public int RippleRate => rippleRate;
        public float PrintRecovery => printRecovery;
        public float IdentifyInterval => identifyInterval;
        public int RainDropsPerSecond => rainDropsPerSecond;
    }
}
