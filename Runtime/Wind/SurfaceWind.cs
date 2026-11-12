using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// One global wind for every Surface Engine shader: direction (this object's forward, or the Wind Zone's), strength, travelling
    /// gusts and turbulence, published as shader globals (<c>_SurfaceWindParams</c>, <c>_SurfaceWindGust</c>) that the Trail Grass and
    /// Trail Water shaders - or any shader including <c>SurfaceWind.hlsl</c> - evaluate per vertex: the wind itself is computed on the GPU,
    /// nothing moves on the CPU. With a <see cref="WindZone"/> assigned, Unity's own wind stays the authoring tool: its main, turbulence,
    /// pulse magnitude and frequency and its direction are read at <c>updateInterval</c> and published, so the same zone also drives
    /// particles with the External Forces module. One per scene; the last enabled one wins.
    /// </summary>
    /// <remarks>
    /// Wiring values are hidden (switch the Inspector to Debug mode to change them): <c>updateInterval</c> is how often the values are
    /// re-published while enabled (a change in the Inspector or on the zone shows after at most that long).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SurfaceWind : MonoBehaviour
    {
        private static readonly int ParamsId = Shader.PropertyToID("_SurfaceWindParams");
        private static readonly int GustId = Shader.PropertyToID("_SurfaceWindGust");

        [Tooltip("Optional: take the wind from Unity's Wind Zone (its direction, Main, Turbulence, Pulse Magnitude and Pulse Frequency). Empty = the values below and this object's forward.")]
        [SerializeField] private WindZone windZone;

        [Tooltip("Steady wind: 0 = calm, 1 = strong (grass leans about its Wind influence in meters), 2 = storm.")]
        [Range(0f, 2f)] [SerializeField] private float strength = 0.5f;

        [Tooltip("How much stronger a gust is than the steady wind (0 = none, 1 = twice as strong at the peak).")]
        [Range(0f, 2f)] [SerializeField] private float gusts = 0.6f;

        [Tooltip("Meters from one gust front to the next.")]
        [Range(5f, 200f)] [SerializeField] private float gustSize = 30f;

        [Tooltip("Meters per second the gust fronts travel downwind.")]
        [Range(0f, 30f)] [SerializeField] private float gustSpeed = 6f;

        [Tooltip("Sideways flutter: 0 = every blade leans the same way, 1 = lively.")]
        [Range(0f, 2f)] [SerializeField] private float turbulence = 0.5f;

        [HideInInspector] [SerializeField] private float updateInterval = 0.1f;

        private CancellationTokenSource loopCts;
        private static Vector4 _params;
        private static Vector4 _gust;

        /// <summary>The wind last published: xy direction on XZ, z strength, w gusts. Zero while no SurfaceWind is enabled.</summary>
        public static Vector4 Current => _params;

        /// <summary>Whether a SurfaceWind is publishing (the shaders blow).</summary>
        public static bool IsActive => _gust.w > 0f;

        /// <summary>The wind direction on the XZ plane (unit), or forward when calm.</summary>
        public Vector3 Direction
        {
            get
            {
                var forward = windZone != null ? windZone.transform.forward : transform.forward;
                var flat = new Vector3(forward.x, 0f, forward.z);
                return flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
            }
        }

        /// <summary>Steady strength (0..2); set it from weather code. A Wind Zone overrides it at the next update.</summary>
        public float Strength
        {
            get => strength;
            set => strength = Mathf.Clamp(value, 0f, 2f);
        }

        /// <summary>Gust amplitude (0..2); set it from weather code. A Wind Zone overrides it at the next update.</summary>
        public float Gusts
        {
            get => gusts;
            set => gusts = Mathf.Clamp(value, 0f, 2f);
        }

        /// <summary>
        /// The wind at a world position, as the shaders compute it (meters of push on XZ before a material's wind influence): for
        /// drifting particles or leaves on the CPU. Zero while no wind is active.
        /// </summary>
        public static Vector3 VelocityAt(Vector3 position)
        {
            if (_gust.w <= 0f)
            {
                return Vector3.zero;
            }

            var direction = new Vector2(_params.x, _params.y);
            var across = new Vector2(-direction.y, direction.x);
            var flat = new Vector2(position.x, position.z);
            var along = Vector2.Dot(flat, direction) * _gust.x;
            var side = Vector2.Dot(flat, across) * _gust.x;
            var time = Time.time * _gust.y * _gust.x;
            var gust = Mathf.Sin((along - time) * 6.2832f) * 0.5f + 0.5f;
            gust *= Mathf.Sin((along * 0.37f + side * 0.61f - time * 0.7f) * 6.2832f) * 0.5f + 0.5f;
            var turbulence = Mathf.Sin(position.x * 3.1f + Time.time * 4.3f) * Mathf.Sin(position.z * 2.7f - Time.time * 3.7f) * _gust.z;
            var magnitude = _params.z * (1f + gust * _params.w);
            var push = direction * magnitude + across * (turbulence * magnitude * 0.3f);
            return new Vector3(push.x, 0f, push.y);
        }

        private void OnEnable()
        {
            loopCts = new CancellationTokenSource();
            Publish();
            LoopAsync(loopCts.Token).Forget();
        }

        private void OnDisable()
        {
            if (loopCts != null)
            {
                loopCts.Cancel();
                loopCts.Dispose();
                loopCts = null;
            }

            _params = Vector4.zero;
            _gust = Vector4.zero;
            Shader.SetGlobalVector(ParamsId, _params);
            Shader.SetGlobalVector(GustId, _gust);
        }

        // Re-publishes at the interval so Inspector tweaks and an animated Wind Zone reach the shaders; the globals are only touched
        // when a value changed.
        private async UniTaskVoid LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (await UniTask.Delay(System.TimeSpan.FromSeconds(updateInterval), ignoreTimeScale: true, cancellationToken: token).SuppressCancellationThrow())
                {
                    return;
                }

                Publish();
            }
        }

        private void Publish()
        {
            var direction = Direction;
            float mainStrength = strength;
            float gustAmount = gusts;
            float size = gustSize;
            float speed = gustSpeed;
            float flutter = turbulence;
            if (windZone != null)
            {
                mainStrength = Mathf.Clamp(windZone.windMain, 0f, 2f);
                flutter = Mathf.Clamp(windZone.windTurbulence, 0f, 2f);
                gustAmount = Mathf.Clamp(windZone.windPulseMagnitude, 0f, 2f);
                // A pulse frequency in Hz becomes a gust front travelling one gust size per period.
                speed = Mathf.Clamp(windZone.windPulseFrequency * size, 0f, 30f);
            }

            var parameters = new Vector4(direction.x, direction.z, mainStrength, gustAmount);
            var gust = new Vector4(1f / Mathf.Max(size, 1f), speed, flutter, 1f);
            if (parameters == _params && gust == _gust)
            {
                return;
            }

            _params = parameters;
            _gust = gust;
            Shader.SetGlobalVector(ParamsId, _params);
            Shader.SetGlobalVector(GustId, _gust);
        }

        // Keeps the static state clean when "Enter Play Mode Options" skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _params = Vector4.zero;
            _gust = Vector4.zero;
        }
    }
}
