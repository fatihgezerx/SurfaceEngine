using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurfaceSystem
{
    /// <summary>
    /// The trail maps: three world-aligned render textures around a focus that record where <see cref="SurfaceTrailSource"/>s moved -
    /// <b>Bend</b> (grass leans away and stands back up), <b>Ripple</b> (a height-field wave simulation for water) and <b>Deform</b>
    /// (prints that stay in snow, sand or mud). Every frame each source is stamped into the maps with one instanced draw per map, the
    /// maps run their pass (decay, wave step, fill-back) and are published as shader globals (<c>_SurfaceTrailBend</c>,
    /// <c>_SurfaceTrailRipple</c>, <c>_SurfaceTrailDeform</c>, <c>_SurfaceTrailParams</c>, <c>_SurfaceTrailTexel</c>), so any material
    /// using the Trail Grass / Trail Water / Trail Deform shaders - or a project shader that includes <c>SurfaceTrails.hlsl</c> - reacts with
    /// nothing assigned. When the focus walks a quarter of the area away from the origin, the maps re-centre on a texel-snapped origin and
    /// shift their content, so prints behind the player stay where they were made.
    /// </summary>
    /// <remarks>
    /// Started by <see cref="SurfaceEngine.Initialize"/> when <see cref="SurfaceSettings.Trails"/> is enabled (or by hand with
    /// <see cref="Initialize"/>), stopped by <see cref="Shutdown"/>. One UniTask loop at <c>PostLateUpdate</c> - after everything moved,
    /// before rendering - alive only while sources are registered. The internal shaders are loaded from
    /// <c>Resources/SurfaceEngine</c>. A map whose content has settled (nothing stamped for a while) skips its pass.
    /// </remarks>
    public static class SurfaceTrails
    {
        // Instances per DrawMeshInstanced call; stays well under the instancing buffer limit of the stamp shader.
        private const int StampBatch = 256;

        // The focus re-centres the maps once it is this fraction of the area away from the origin.
        private const float RecentreFraction = 0.25f;

        // Longest frame the passes are asked to cover (a hitch must not make grass vanish or ripples explode).
        private const float MaxDeltaTime = 0.1f;

        private const int MaxRippleStepsPerFrame = 4;

        private static readonly int BendId = Shader.PropertyToID("_SurfaceTrailBend");
        private static readonly int RippleId = Shader.PropertyToID("_SurfaceTrailRipple");
        private static readonly int DeformId = Shader.PropertyToID("_SurfaceTrailDeform");
        private static readonly int ParamsId = Shader.PropertyToID("_SurfaceTrailParams");
        private static readonly int TexelId = Shader.PropertyToID("_SurfaceTrailTexel");
        private static readonly int StampDataId = Shader.PropertyToID("_StampData");
        private static readonly int FactorId = Shader.PropertyToID("_Factor");
        private static readonly int AmountId = Shader.PropertyToID("_Amount");
        private static readonly int DampingId = Shader.PropertyToID("_Damping");
        private static readonly int PrevTexId = Shader.PropertyToID("_PrevTex");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int ClearId = Shader.PropertyToID("_ClearValue");

        private static readonly List<SurfaceTrailSource> Sources = new();
        private static readonly List<Impulse> Impulses = new(64);
        private static readonly Matrix4x4[] Matrices = new Matrix4x4[StampBatch];
        private static readonly Vector4[] StampData = new Vector4[StampBatch];

        private static SurfaceTrailSettings _settings;
        private static LayerMask _groundLayers;
        private static CancellationTokenSource _loopCts;

        private static SurfaceTrailMap _bend;
        private static SurfaceTrailMap _ripple;
        private static SurfaceTrailMap _deform;

        private static Material _stampMaterial;
        private static Material _decayMaterial;
        private static Material _rippleMaterial;
        private static Material _shiftMaterial;
        private static MaterialPropertyBlock _stampProperties;
        private static Mesh _quad;
        private static CommandBuffer _commands;

        private static Vector2 _origin;
        private static bool _originSet;
        private static float _rippleAccumulator;
        private static float _rain;
        private static float _rainAccumulator;
        private static int _stampCount;
        private static int _stampPass;
        private static bool _stampDrawn;

        // Radius and dent of one rain drop, and the most drops one frame may stamp.
        private const float RainDropRadius = 0.12f;
        private const float RainDropStrength = 0.3f;
        private const int MaxRainDropsPerFrame = 256;

        // A one-off stamp queued by Push, stamped on the next tick into every map it has a value for.
        private struct Impulse
        {
            public Vector3 Position;
            public float Radius;
            public float Bend;
            public float Ripple;
            public float Depth;
        }

        /// <summary>Whether <see cref="Initialize"/> has been called.</summary>
        public static bool IsInitialized => _settings != null;

        /// <summary>Whether the per-frame loop is alive (initialized and at least one source registered).</summary>
        public static bool IsRunning => _loopCts != null;

        /// <summary>The settings the maps were made with, or null.</summary>
        public static SurfaceTrailSettings Settings => _settings;

        /// <summary>
        /// What the maps follow. Null (the default) = the first registered source, which is usually the player. Set it to the camera
        /// or anything else when the trails should be kept around something other than the first source.
        /// </summary>
        public static Transform Focus { get; set; }

        /// <summary>World XZ of the centre of the maps (snapped to the texel grid).</summary>
        public static Vector2 Origin => _origin;

        /// <summary>Meters covered by the maps.</summary>
        public static float AreaSize => _settings != null ? _settings.AreaSize : 0f;

        /// <summary>Texels per side of each map.</summary>
        public static int Resolution => _settings != null ? (int)_settings.Resolution : 0;

        /// <summary>Meters per texel.</summary>
        public static float TexelSize => _settings != null ? _settings.AreaSize / (int)_settings.Resolution : 0f;

        /// <summary>How many sources are registered (enabled <see cref="SurfaceTrailSource"/>s).</summary>
        public static int SourceCount => Sources.Count;

        /// <summary>Impulses queued by <see cref="Push"/> that the next tick stamps.</summary>
        public static int PendingPushes => Impulses.Count;

        /// <summary>
        /// Rain on the water, 0 (none) to 1 (<see cref="SurfaceTrailSettings.RainDropsPerSecond"/> drops a second, each a small ripple
        /// somewhere in the area). Keeps the loop alive even without sources.
        /// </summary>
        public static float Rain
        {
            get => _rain;
            set
            {
                _rain = Mathf.Clamp01(value);
                if (_rain > 0f && IsInitialized && !IsRunning)
                {
                    StartLoop();
                }
            }
        }

        /// <summary>
        /// A one-off push at <paramref name="position"/>: grass inside <paramref name="radius"/> is flattened outward by <paramref name="bend"/>
        /// (0..1), water gets a dent of <paramref name="ripple"/> (1 = the splash of a body, 5 = a big one) and snow or sand a print of
        /// <paramref name="depth"/> (0..1) - an explosion, a landing, a spell, something heavy dropped. Stamped on the next tick; nothing
        /// happens while the maps are not initialized.
        /// </summary>
        public static void Push(Vector3 position, float radius, float bend = 1f, float ripple = 1f, float depth = 0f)
        {
            if (!IsInitialized || radius <= 0f)
            {
                return;
            }

            Impulses.Add(new Impulse { Position = position, Radius = radius, Bend = Mathf.Clamp01(bend), Ripple = Mathf.Max(0f, ripple), Depth = Mathf.Clamp01(depth) });
            if (!IsRunning)
            {
                StartLoop();
            }
        }

        /// <summary>Empties every map (grass stands, water calms, prints vanish) and forgets queued pushes. Safe when not initialized.</summary>
        public static void Clear()
        {
            Impulses.Clear();
            if (!IsInitialized)
            {
                return;
            }

            _bend.Clear();
            _ripple.Clear();
            _deform.Clear();
            _bend.ActiveFor = 0f;
            _ripple.ActiveFor = 0f;
            _deform.ActiveFor = 0f;
            _rippleAccumulator = 0f;
        }

        /// <summary>The bend map (RG = push direction, A = strength), for debugging; null when not initialized.</summary>
        public static RenderTexture BendMap => _bend != null ? _bend.Current : null;

        /// <summary>The ripple height map (R, signed), for debugging; null when not initialized.</summary>
        public static RenderTexture RippleMap => _ripple != null ? _ripple.Current : null;

        /// <summary>The print depth map (R, 0..1), for debugging; null when not initialized.</summary>
        public static RenderTexture DeformMap => _deform != null ? _deform.Current : null;

        /// <summary>
        /// Makes the maps and their materials and starts the loop if sources are already registered. Calling it again replaces
        /// everything (the old maps are released). <paramref name="groundLayers"/> is what the sources raycast against to identify
        /// their surface.
        /// </summary>
        public static void Initialize(SurfaceTrailSettings settings, LayerMask groundLayers)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Shutdown();

            _settings = settings;
            _groundLayers = groundLayers;

            var resolution = (int)settings.Resolution;
            _bend = new SurfaceTrailMap("Surface Trail Bend", resolution, Pick(RenderTextureFormat.ARGB32), 2, new Color(0.5f, 0.5f, 0f, 0f));
            _ripple = new SurfaceTrailMap("Surface Trail Ripple", resolution, Pick(RenderTextureFormat.RHalf, RenderTextureFormat.ARGBHalf), 3, Color.clear);
            _deform = new SurfaceTrailMap("Surface Trail Deform", resolution, Pick(RenderTextureFormat.RHalf, RenderTextureFormat.ARGBHalf), 2, Color.clear);

            _stampMaterial = LoadMaterial("SurfaceEngine/TrailStamp");
            // The stamps are drawn with DrawMeshInstanced: the material must say so.
            _stampMaterial.enableInstancing = true;
            _decayMaterial = LoadMaterial("SurfaceEngine/TrailDecay");
            _rippleMaterial = LoadMaterial("SurfaceEngine/TrailRipple");
            _shiftMaterial = LoadMaterial("SurfaceEngine/TrailShift");
            _stampProperties = new MaterialPropertyBlock();
            _quad = MakeQuad();
            _commands = new CommandBuffer { name = "Surface Trails" };

            _origin = Vector2.zero;
            _originSet = false;
            _rippleAccumulator = 0f;
            PublishGlobals();

            if (Sources.Count > 0)
            {
                StartLoop();
            }
        }

        /// <summary>Stops the loop, releases the maps and clears the shader globals. Safe when not initialized; sources stay registered.</summary>
        public static void Shutdown()
        {
            StopLoop();

            _bend?.Release();
            _ripple?.Release();
            _deform?.Release();
            _bend = null;
            _ripple = null;
            _deform = null;

            DestroyObject(_stampMaterial);
            DestroyObject(_decayMaterial);
            DestroyObject(_rippleMaterial);
            DestroyObject(_shiftMaterial);
            DestroyObject(_quad);
            _stampMaterial = null;
            _decayMaterial = null;
            _rippleMaterial = null;
            _shiftMaterial = null;
            _quad = null;
            _stampProperties = null;

            if (_commands != null)
            {
                _commands.Release();
                _commands = null;
            }

            _settings = null;
            _originSet = false;
            _rain = 0f;
            Impulses.Clear();
            PublishGlobals();
        }

        /// <summary>Adds <paramref name="source"/> to the stamped sources (called by the source itself in OnEnable). Starts the loop if the maps exist.</summary>
        internal static void Register(SurfaceTrailSource source)
        {
            if (source == null || Sources.Contains(source))
            {
                return;
            }

            Sources.Add(source);
            if (IsInitialized && !IsRunning)
            {
                StartLoop();
            }
        }

        /// <summary>Takes <paramref name="source"/> out of the stamped sources (OnDisable). The loop ends with the last one.</summary>
        internal static void Unregister(SurfaceTrailSource source)
        {
            if (Sources.Remove(source) && Sources.Count == 0)
            {
                StopLoop();
            }
        }

        // ---- Loop ----

        private static void StartLoop()
        {
            if (_loopCts != null)
            {
                return;
            }

            _loopCts = new CancellationTokenSource();
            LoopAsync(_loopCts.Token).Forget();
        }

        private static void StopLoop()
        {
            if (_loopCts == null)
            {
                return;
            }

            _loopCts.Cancel();
            _loopCts.Dispose();
            _loopCts = null;
        }

        private static async UniTaskVoid LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // After every movement and animation of the frame, before the cameras render.
                if (await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, token).SuppressCancellationThrow())
                {
                    return;
                }

                Tick(Mathf.Min(Time.deltaTime, MaxDeltaTime), Time.time);
            }
        }

        // One frame: follow the focus, let the sources measure themselves, stamp them into the maps, run each map's pass, publish.
        private static void Tick(float deltaTime, float time)
        {
            var settings = _settings;
            for (var i = Sources.Count - 1; i >= 0; i--)
            {
                var source = Sources[i];
                if (source == null)
                {
                    // Destroyed without OnDisable (a scene unload while disabled): drop it.
                    Sources.RemoveAt(i);
                    continue;
                }

                source.Tick(deltaTime, time, _groundLayers, settings.IdentifyInterval);
            }

            if (Sources.Count == 0 && Impulses.Count == 0 && _rain <= 0f)
            {
                StopLoop();
                return;
            }

            // Without a source the maps stay where they are (rain and pushes still land in them).
            var focus = Focus != null ? Focus : Sources.Count > 0 ? Sources[0].transform : null;
            if (focus != null)
            {
                Recentre(focus.position);
            }

            // Bend: stands back up, then this frame's pushes. Only while something is bent.
            if (_bend.ActiveFor > 0f && settings.GrassRecovery > 0f)
            {
                _decayMaterial.SetFloat(FactorId, Mathf.Exp(-settings.GrassRecovery * deltaTime));
                Graphics.Blit(_bend.Current, _bend.Next, _decayMaterial, 0);
                _bend.Advance();
                _bend.ActiveFor -= deltaTime;
            }

            if (Stamp(_bend, 0, deltaTime))
            {
                // Bent grass below 1/255 is invisible: that long after the last stamp the decay can stop.
                _bend.ActiveFor = settings.GrassRecovery > 0f ? 5.6f / settings.GrassRecovery : float.PositiveInfinity;
            }

            // Deform: prints fill back (if asked), then this frame's prints as the deepest value.
            if (_deform.ActiveFor > 0f && settings.PrintRecovery > 0f)
            {
                _decayMaterial.SetFloat(AmountId, settings.PrintRecovery * deltaTime);
                Graphics.Blit(_deform.Current, _deform.Next, _decayMaterial, 1);
                _deform.Advance();
                _deform.ActiveFor -= deltaTime;
            }

            if (Stamp(_deform, 2, deltaTime))
            {
                _deform.ActiveFor = settings.PrintRecovery > 0f ? 1f / settings.PrintRecovery + 1f : float.PositiveInfinity;
            }

            // Ripple: fixed-rate wave steps (the stencil is only stable at a fixed step), then this frame's splashes.
            if (_ripple.ActiveFor > 0f)
            {
                var stepTime = 1f / settings.RippleRate;
                _rippleAccumulator += deltaTime;
                var steps = Mathf.Min((int)(_rippleAccumulator / stepTime), MaxRippleStepsPerFrame);
                _rippleAccumulator -= steps * stepTime;
                if (steps > 0)
                {
                    _rippleMaterial.SetFloat(DampingId, 1f - settings.RippleFade);
                    for (var step = 0; step < steps; step++)
                    {
                        _rippleMaterial.SetTexture(PrevTexId, _ripple.Previous);
                        Graphics.Blit(_ripple.Current, _ripple.Next, _rippleMaterial, 0);
                        _ripple.Advance();
                    }

                    _ripple.ActiveFor -= steps * stepTime;
                }
            }
            else
            {
                _rippleAccumulator = 0f;
            }

            if (Stamp(_ripple, 1, deltaTime))
            {
                // Steps until a full-height ripple has faded below 1/1024, as seconds.
                var stepsToSettle = Mathf.Log(1f / 1024f) / Mathf.Log(1f - settings.RippleFade);
                _ripple.ActiveFor = stepsToSettle / settings.RippleRate + 0.5f;
            }

            // Pushes are stamped once, by every map they had a value for.
            Impulses.Clear();
            PublishGlobals();
        }

        // Moves the origin (and the maps' content) when the focus has walked far enough from it. The origin sits on the texel grid, so
        // the content moves by whole texels and nothing blurs.
        private static void Recentre(Vector3 focusPosition)
        {
            var focus = new Vector2(focusPosition.x, focusPosition.z);
            var texel = TexelSize;
            var limit = _settings.AreaSize * RecentreFraction;

            if (_originSet && Mathf.Abs(focus.x - _origin.x) <= limit && Mathf.Abs(focus.y - _origin.y) <= limit)
            {
                return;
            }

            var snapped = new Vector2(Mathf.Round(focus.x / texel) * texel, Mathf.Round(focus.y / texel) * texel);
            if (!_originSet)
            {
                _origin = snapped;
                _originSet = true;
                return;
            }

            var delta = snapped - _origin;
            var texels = new Vector2Int(Mathf.RoundToInt(delta.x / texel), Mathf.RoundToInt(delta.y / texel));
            if (texels == Vector2Int.zero)
            {
                return;
            }

            _origin = new Vector2(_origin.x + texels.x * texel, _origin.y + texels.y * texel);
            _bend.Shift(texels, _shiftMaterial, OffsetId, ClearId);
            _ripple.Shift(texels, _shiftMaterial, OffsetId, ClearId);
            _deform.Shift(texels, _shiftMaterial, OffsetId, ClearId);
        }

        // Draws every source, queued push and rain drop that has a stamp for this map (pass) as instanced quads into the map's current
        // buffer. Returns whether anything was drawn.
        private static bool Stamp(SurfaceTrailMap map, int pass, float deltaTime)
        {
            _stampCount = 0;
            _stampPass = pass;
            _stampDrawn = false;
            _commands.Clear();
            _commands.SetRenderTarget(map.Current);
            _commands.SetViewProjectionMatrices(ViewMatrix(), ProjectionMatrix());

            foreach (var source in Sources)
            {
                if (source.GetStamp(pass, out var position, out var radius, out var data))
                {
                    AddStamp(position, radius, data);
                }
            }

            foreach (var impulse in Impulses)
            {
                switch (pass)
                {
                    case 0 when impulse.Bend > 0f:
                        AddStamp(impulse.Position, impulse.Radius, new Vector4(0f, 0f, impulse.Bend, 0f));
                        break;
                    case 1 when impulse.Ripple > 0f:
                        AddStamp(impulse.Position, impulse.Radius, new Vector4(0f, 0f, impulse.Ripple, 1f));
                        break;
                    case 2 when impulse.Depth > 0f:
                        AddStamp(impulse.Position, impulse.Radius, new Vector4(0f, 0f, impulse.Depth, 0f));
                        break;
                }
            }

            if (pass == 1 && _rain > 0f)
            {
                StampRain(deltaTime);
            }

            if (_stampCount > 0)
            {
                Flush(pass, _stampCount);
                _stampDrawn = true;
                _stampCount = 0;
            }

            if (_stampDrawn)
            {
                Graphics.ExecuteCommandBuffer(_commands);
            }

            return _stampDrawn;
        }

        private static void AddStamp(Vector3 position, float radius, Vector4 data)
        {
            Matrices[_stampCount] = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(radius * 2f, 1f, radius * 2f));
            StampData[_stampCount] = data;
            _stampCount++;
            if (_stampCount == StampBatch)
            {
                Flush(_stampPass, _stampCount);
                _stampDrawn = true;
                _stampCount = 0;
            }
        }

        private static void Flush(int pass, int count)
        {
            _stampProperties.SetVectorArray(StampDataId, StampData);
            _commands.DrawMeshInstanced(_quad, 0, _stampMaterial, pass, Matrices, count, _stampProperties);
        }

        // Rain drops: small dents at random points of the area, as many as the rain and the frame time say (fractions carry over).
        private static void StampRain(float deltaTime)
        {
            _rainAccumulator += _rain * _settings.RainDropsPerSecond * deltaTime;
            var drops = Mathf.Min((int)_rainAccumulator, MaxRainDropsPerFrame);
            _rainAccumulator -= drops;
            var half = _settings.AreaSize * 0.5f;
            for (var i = 0; i < drops; i++)
            {
                var position = new Vector3(_origin.x + UnityEngine.Random.Range(-half, half), 0f, _origin.y + UnityEngine.Random.Range(-half, half));
                AddStamp(position, RainDropRadius * UnityEngine.Random.Range(0.7f, 1.3f), new Vector4(0f, 0f, RainDropStrength, 1f));
            }
        }

        // A camera above the origin looking straight down, world +X to the right and world +Z up, covering the area.
        private static Matrix4x4 ViewMatrix()
        {
            var view = Matrix4x4.TRS(new Vector3(_origin.x, 100f, _origin.y), Quaternion.LookRotation(Vector3.down, Vector3.forward), Vector3.one).inverse;
            return Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * view;
        }

        private static Matrix4x4 ProjectionMatrix()
        {
            var half = _settings.AreaSize * 0.5f;
            return GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-half, half, -half, half, 0.1f, 200f), true);
        }

        // The shaders read these. Zero / black when the system is off, so the Trail shaders draw plain surfaces.
        private static void PublishGlobals()
        {
            if (!IsInitialized)
            {
                Shader.SetGlobalTexture(BendId, Texture2D.blackTexture);
                Shader.SetGlobalTexture(RippleId, Texture2D.blackTexture);
                Shader.SetGlobalTexture(DeformId, Texture2D.blackTexture);
                Shader.SetGlobalVector(ParamsId, Vector4.zero);
                Shader.SetGlobalVector(TexelId, Vector4.zero);
                return;
            }

            var resolution = (int)_settings.Resolution;
            Shader.SetGlobalTexture(BendId, _bend.Current);
            Shader.SetGlobalTexture(RippleId, _ripple.Current);
            Shader.SetGlobalTexture(DeformId, _deform.Current);
            Shader.SetGlobalVector(ParamsId, new Vector4(_origin.x, _origin.y, 1f / _settings.AreaSize, _originSet ? 1f : 0f));
            Shader.SetGlobalVector(TexelId, new Vector4(1f / resolution, _settings.AreaSize / resolution, resolution, _settings.AreaSize));
        }

        // ---- Resources ----

        private static Material LoadMaterial(string resourcePath)
        {
            var shader = Resources.Load<Shader>(resourcePath);
            if (shader == null)
            {
                throw new InvalidOperationException("[SurfaceTrails] Shader 'Resources/" + resourcePath + "' is missing: Surface Engine's Resources folder must be in the project.");
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private static RenderTextureFormat Pick(RenderTextureFormat wanted, RenderTextureFormat fallback = RenderTextureFormat.ARGB32) =>
            SystemInfo.SupportsRenderTextureFormat(wanted) ? wanted : fallback;

        // A unit quad on the XZ plane (uv x along +X, uv y along +Z), scaled per stamp to the source's diameter.
        private static Mesh MakeQuad()
        {
            var mesh = new Mesh { name = "Surface Trail Stamp", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.01f, 1f));
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        // Keeps the static state clean when "Enter Play Mode Options" skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loopCts = null;
            _settings = null;
            _bend = null;
            _ripple = null;
            _deform = null;
            _stampMaterial = null;
            _decayMaterial = null;
            _rippleMaterial = null;
            _shiftMaterial = null;
            _stampProperties = null;
            _quad = null;
            _commands = null;
            _originSet = false;
            Focus = null;
            _rain = 0f;
            Sources.Clear();
            Impulses.Clear();
        }
    }
}
