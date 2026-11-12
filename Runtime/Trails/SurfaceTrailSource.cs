using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// Put it on anything that should leave trails - the player, an NPC, an animal, a rolling boulder. While enabled it is stamped into
    /// the trail maps every frame: it bends the grass it stands in and pushes it the way it moves, makes ripples when it is in water and
    /// leaves prints in surfaces that have a depth (<see cref="SurfaceTrailEntry"/>), kicking up that surface's particles while it moves.
    /// Which surface it is on comes from a short raycast down at <see cref="SurfaceTrailSettings.IdentifyInterval"/>; a
    /// <see cref="SurfaceWaterVolume"/> can also tell it that it is in water. Nothing to call: the maps are driven by <see cref="SurfaceTrails"/>.
    /// </summary>
    /// <remarks>
    /// Wiring values are hidden (switch the Inspector to Debug mode to change them): the ray starts <c>rayHeight</c> above the object's
    /// position and goes <c>rayDistance</c> down.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SurfaceTrailSource : MonoBehaviour
    {
        // Below this speed a source counts as standing: grass is pushed outward instead of along the movement, water is left alone.
        private const float StandingSpeed = 0.15f;

        // Speed at which ripples reach the full strength.
        private const float FullRippleSpeed = 2f;

        [Tooltip("Meters around the object's position that the trail covers (a person's feet: about 0.35; a horse: 0.8).")]
        [Min(0.05f)] [SerializeField] private float radius = 0.35f;

        [Tooltip("How much this source bends grass: 0 = not at all, 1 = flat.")]
        [Range(0f, 1f)] [SerializeField] private float bendStrength = 1f;

        [Tooltip("How deep this source sinks into soft surfaces, as a fraction of the surface's own depth (a child or a cat: low; a horse: 1).")]
        [Range(0f, 1f)] [SerializeField] private float pressure = 1f;

        [Tooltip("How big the ripples this source makes in water are, at full speed.")]
        [Range(0f, 2f)] [SerializeField] private float rippleStrength = 1f;

        // Where the identifying ray starts (above the feet, so a sunken foot still finds the ground) and how far it goes.
        [HideInInspector] [SerializeField] private float rayHeight = 0.5f;
        [HideInInspector] [SerializeField] private float rayDistance = 1.5f;

        private Vector3 lastPosition;
        private bool hasLastPosition;
        private Vector3 velocity;
        private float speed;
        private float lastDeltaTime;
        private float nextIdentify;
        private SurfaceGroup surface;
        private int waterVolumes;
        private ParticleSystem kickUpInstance;
        private ParticleSystem kickUpPrefab;

        /// <summary>Meters around the position that the trail covers.</summary>
        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0.05f, value);
        }

        /// <summary>World velocity measured between frames.</summary>
        public Vector3 Velocity => velocity;

        /// <summary>Horizontal speed (m/s) measured between frames.</summary>
        public float Speed => speed;

        /// <summary>The surface group the source last identified under itself, or null.</summary>
        public SurfaceGroup Surface => surface;

        /// <summary>Whether the source is in water: inside a <see cref="SurfaceWaterVolume"/>, or standing on a surface flagged as water.</summary>
        public bool InWater => waterVolumes > 0 || (surface != null && surface.Trail.Water);

        private void OnEnable()
        {
            hasLastPosition = false;
            velocity = Vector3.zero;
            speed = 0f;
            nextIdentify = 0f;
            SurfaceTrails.Register(this);
        }

        private void OnDisable()
        {
            SurfaceTrails.Unregister(this);
            ReleaseKickUp();
            surface = null;
            waterVolumes = 0;
        }

        /// <summary>A <see cref="SurfaceWaterVolume"/> the source entered.</summary>
        internal void EnterWater() => waterVolumes++;

        /// <summary>A <see cref="SurfaceWaterVolume"/> the source left.</summary>
        internal void ExitWater() => waterVolumes = Mathf.Max(0, waterVolumes - 1);

        /// <summary>Measures the movement since the last frame, re-identifies the surface when due and drives the kick-up particles.</summary>
        internal void Tick(float deltaTime, float time, LayerMask groundLayers, float identifyInterval)
        {
            var position = transform.position;
            if (hasLastPosition && deltaTime > 0f)
            {
                velocity = (position - lastPosition) / deltaTime;
                speed = new Vector2(velocity.x, velocity.z).magnitude;
            }
            else
            {
                velocity = Vector3.zero;
                speed = 0f;
            }

            lastPosition = position;
            hasLastPosition = true;
            lastDeltaTime = deltaTime;

            if (!SurfaceEngine.IsInitialized)
            {
                // No engine, no surfaces: a group kept from before a Shutdown must not keep kicking up particles.
                surface = null;
            }
            else if (time >= nextIdentify)
            {
                nextIdentify = time + identifyInterval;
                Identify(position, groundLayers);
            }

            UpdateKickUp();
        }

        /// <summary>
        /// The stamp for a map pass (0 bend, 1 ripple, 2 deform): where, how wide and the per-instance data the stamp shader reads
        /// (x, y: push direction; z: strength; w: how much the direction counts against a radial push). False = nothing to stamp.
        /// </summary>
        internal bool GetStamp(int pass, out Vector3 position, out float stampRadius, out Vector4 data)
        {
            position = transform.position;
            stampRadius = radius;
            data = Vector4.zero;

            switch (pass)
            {
                case 0:
                    if (bendStrength <= 0f)
                    {
                        return false;
                    }

                    var moving = speed > StandingSpeed;
                    var direction = moving ? new Vector2(velocity.x, velocity.z) / speed : Vector2.zero;
                    data = new Vector4(direction.x, direction.y, bendStrength, moving ? 1f : 0f);
                    return true;

                case 1:
                    if (rippleStrength <= 0f || speed <= StandingSpeed || !InWater)
                    {
                        return false;
                    }

                    // w scales the dent to the frame's share of simulation steps, so a fast frame rate does not dent more.
                    var rate = SurfaceTrails.Settings != null ? SurfaceTrails.Settings.RippleRate : 60;
                    data = new Vector4(0f, 0f, rippleStrength * Mathf.Clamp01(speed / FullRippleSpeed), lastDeltaTime * rate);
                    return true;

                case 2:
                    var depth = surface != null ? surface.Trail.Depth * pressure : 0f;
                    if (depth <= 0f)
                    {
                        return false;
                    }

                    data = new Vector4(0f, 0f, depth, 0f);
                    return true;

                default:
                    return false;
            }
        }

        // One short ray down through the existing engine identification. Without an initialized SurfaceEngine there is no surface
        // (the source still bends grass; water needs a volume then).
        private void Identify(Vector3 position, LayerMask groundLayers)
        {
            surface = null;
            if (Physics.Raycast(position + Vector3.up * rayHeight, Vector3.down, out var hit, rayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                surface = SurfaceEngine.Identify(hit.collider, hit.point, hit.triangleIndex);
            }
        }

        // The surface's kick-up particles follow the source and emit in proportion to its speed; they stop (but stay) when it slows
        // down, and are swapped when the source walks onto a surface with different particles.
        private void UpdateKickUp()
        {
            var entry = surface != null ? surface.Trail : null;
            var prefab = entry != null ? entry.KickUp : null;
            var rate = entry != null ? entry.KickUpRateAt(speed) : 0f;

            if (prefab == null || rate <= 0f)
            {
                if (kickUpInstance != null && kickUpInstance.isEmitting)
                {
                    kickUpInstance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                return;
            }

            if (kickUpInstance == null || kickUpPrefab != prefab)
            {
                ReleaseKickUp();
                var instance = SurfaceEngine.AcquireParticles(prefab.gameObject);
                instance.transform.SetParent(transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                kickUpInstance = instance.GetComponent<ParticleSystem>();
                kickUpPrefab = prefab;
                if (kickUpInstance == null)
                {
                    // Not a particle prefab after all: give it back and stop trying this surface.
                    SurfaceEngine.ReleaseParticles(instance);
                    kickUpPrefab = null;
                    return;
                }
            }

            var emission = kickUpInstance.emission;
            emission.rateOverTime = rate;
            if (!kickUpInstance.isEmitting)
            {
                kickUpInstance.Play(true);
            }
        }

        private void ReleaseKickUp()
        {
            if (kickUpInstance == null)
            {
                kickUpInstance = null;
                kickUpPrefab = null;
                return;
            }

            kickUpInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            kickUpInstance.transform.SetParent(null, true);
            SurfaceEngine.ReleaseParticles(kickUpInstance.gameObject);
            kickUpInstance = null;
            kickUpPrefab = null;
        }
    }
}
