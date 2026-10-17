using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using EventSystem;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// Static entry point of the surface system. Call <see cref="Initialize"/> once (e.g. from a GameManager)
    /// with a compiled <see cref="SurfaceData"/>; from then on <see cref="Footstep(RaycastHit, FootstepType)"/> and
    /// <see cref="Impact(RaycastHit)"/> identify which <see cref="SurfaceGroup"/> was hit - by matching the hit
    /// renderer's material (or, on a Terrain, the dominant layer at that point) against every group's
    /// <see cref="SurfaceGroup.Textures"/> - play a random sound, VFX and optional decal from that group, and
    /// publish <see cref="FootstepEvent"/>/<see cref="ImpactEvent"/> through <see cref="EventManager"/>.
    /// </summary>
    /// <remarks>
    /// Never raycasts on its own timer and owns no Update/Coroutine: every call is triggered by the caller - an
    /// animation event, a headbob script, a weapon's own hit raycast, a hand-rolled pacing loop - whatever fits
    /// the project. A group's Textures must be the exact same Texture2D asset used by the surface's material
    /// (or Terrain layer); Identify matches by reference. Decal/VFX lifetime is a cancellable UniTask.Delay,
    /// released through PoolSystem when it's installed (falls back to Instantiate/Destroy otherwise); how audio
    /// is played is controlled separately by <see cref="SurfaceSettings.AudioPlaybackMode"/>.
    /// </remarks>
    public static class SurfaceEngine
    {
        private static SurfaceData _data;
        private static Dictionary<Texture2D, SurfaceGroup> _textureLookup;
        private static CancellationTokenSource _lifetimeCts;

        /// <summary>Whether <see cref="Initialize"/> has been called.</summary>
        public static bool IsInitialized => _data != null;

        /// <summary>The data the engine is currently identifying surfaces with, or null.</summary>
        public static SurfaceData Data => _data;

        /// <summary>
        /// Builds the texture -> group lookup from every group's Textures. Calling it again (e.g. with a
        /// different data asset) replaces the previous one and cancels any decal/VFX still waiting to be released.
        /// </summary>
        public static void Initialize(SurfaceData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Shutdown();

            _data = data;
            _textureLookup = new Dictionary<Texture2D, SurfaceGroup>();
            foreach (var group in data.Groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (var texture in group.Textures)
                {
                    if (texture != null)
                    {
                        _textureLookup.TryAdd(texture, group);
                    }
                }
            }

            _lifetimeCts = new CancellationTokenSource();
        }

        /// <summary>Stops the engine: cancels every decal/VFX release still pending and drops the current data.</summary>
        public static void Shutdown()
        {
            if (_lifetimeCts != null)
            {
                _lifetimeCts.Cancel();
                _lifetimeCts.Dispose();
                _lifetimeCts = null;
            }

            _data = null;
            _textureLookup = null;
            ParticleTimes.Clear();
        }

        // ---- Footstep ----

        /// <summary>
        /// Raycasts straight down from <paramref name="origin"/> and, if it hits something, identifies and
        /// plays its Footstep. <paramref name="type"/> picks which of the surface's <see cref="FootstepEntry"/>s
        /// to use (Walk, Run, JumpLand...), falling back to its <see cref="FootstepType.Generic"/> entry.
        /// <paramref name="rayDistance"/>/<paramref name="layerMask"/> default to the data's Settings when left
        /// unset. For a caller that already runs its own ground-check raycast, prefer
        /// <see cref="Footstep(RaycastHit, FootstepType)"/> to avoid a second one. Returns the identified group,
        /// or null if nothing was hit or the hit wasn't a recognized surface.
        /// </summary>
        public static SurfaceGroup Footstep(Transform origin, FootstepType type = FootstepType.Generic, float rayDistance = -1f, LayerMask? layerMask = null)
        {
            if (origin == null) throw new ArgumentNullException(nameof(origin));
            RequireInitialized();

            var distance = rayDistance >= 0f ? rayDistance : _data.Settings.DefaultRayDistance;
            var layers = layerMask ?? _data.Settings.GroundLayers;

            return Physics.Raycast(origin.position, Vector3.down, out var hit, distance, layers, QueryTriggerInteraction.Ignore)
                ? Footstep(hit, type)
                : null;
        }

        /// <summary>
        /// Identifies and plays the Footstep of whatever <paramref name="hit"/> struck - e.g. from a
        /// ground-check raycast the caller already ran, with no extra raycast. Returns the identified group, or
        /// null if it wasn't a recognized surface.
        /// </summary>
        public static SurfaceGroup Footstep(RaycastHit hit, FootstepType type = FootstepType.Generic)
        {
            RequireInitialized();

            var group = Identify(hit.collider, hit.point, hit.triangleIndex);
            if (group == null)
            {
                return null;
            }

            var entry = group.ResolveFootstep(type);
            if (entry != null)
            {
                Play(entry.Sounds, entry.Vfx, entry.DecalMaterial, hit.point, hit.normal, ImpactShape.None, _data.Settings.PoolFootstepTypeName);
            }

            EventManager.Invoke(new FootstepEvent(group, type, hit.point, hit.normal, hit.collider));
            return group;
        }

        // ---- Impact ----

        /// <summary>
        /// Identifies and plays the Impact of whatever <paramref name="hit"/> struck (e.g. a weapon's own hit
        /// raycast). <paramref name="type"/> picks which of the surface's <see cref="ImpactEntry"/>s to use -
        /// e.g. a sword and a gunshot can sound and look different on the same surface - falling back to its
        /// <see cref="ImpactType.Generic"/> entry if it has none of its own for <paramref name="type"/>.
        /// </summary>
        public static SurfaceGroup Impact(RaycastHit hit, ImpactType type = ImpactType.Generic) =>
            Impact(hit.point, hit.normal, hit.collider, type, hit.triangleIndex);

        /// <summary>Identifies and plays an Impact at an explicit point/normal/collider - for hits that don't come from a raycast (e.g. a collision event).</summary>
        public static SurfaceGroup Impact(Vector3 point, Vector3 normal, Collider collider, ImpactType type = ImpactType.Generic, int triangleIndex = -1) =>
            Impact(point, normal, collider, type, triangleIndex, ImpactShape.None);

        /// <summary>
        /// The same, with the mark's <paramref name="shape"/>: the decal is turned along the shape's direction and
        /// sized to its length and width (a long blade leaves a long gash), or left out when the shape says so.
        /// </summary>
        public static SurfaceGroup Impact(Vector3 point, Vector3 normal, Collider collider, ImpactType type, int triangleIndex, ImpactShape shape)
        {
            RequireInitialized();

            var group = Identify(collider, point, triangleIndex);
            if (group == null)
            {
                return null;
            }

            var entry = group.ResolveImpact(type);
            if (entry != null)
            {
                Play(entry.Sounds, entry.Vfx, entry.DecalMaterial, point, normal, shape, _data.Settings.PoolImpactTypeName);
            }

            EventManager.Invoke(new ImpactEvent(group, type, point, normal, collider));
            return group;
        }

        // ---- Identification ----

        /// <summary>
        /// The group <paramref name="collider"/> belongs to at <paramref name="point"/>: on a Terrain, the
        /// dominant layer's texture at that point; otherwise the hit renderer's material's main texture -
        /// either way matched against every group's Textures. Null if nothing matches (e.g. an untagged surface).
        /// </summary>
        public static SurfaceGroup Identify(Collider collider, Vector3 point, int triangleIndex = -1)
        {
            RequireInitialized();

            if (collider == null)
            {
                return null;
            }

            if (collider is TerrainCollider && collider.TryGetComponent<Terrain>(out var terrain))
            {
                var terrainTexture = DominantTerrainTexture(terrain, point);
                return terrainTexture != null && _textureLookup.TryGetValue(terrainTexture, out var terrainGroup) ? terrainGroup : null;
            }

            var renderer = collider.GetComponentInParent<Renderer>();
            var material = ResolveMaterial(renderer, triangleIndex);
            var mainTexture = material != null ? material.mainTexture as Texture2D : null;
            return mainTexture != null && _textureLookup.TryGetValue(mainTexture, out var group) ? group : null;
        }

        // A multi-material renderer's submesh is resolved from the hit triangle when possible (note: RaycastHit's
        // triangleIndex is into the physics mesh, which usually - but isn't guaranteed to - match the render
        // mesh); otherwise the renderer's first material is used.
        private static Material ResolveMaterial(Renderer renderer, int triangleIndex)
        {
            if (renderer == null || renderer.sharedMaterials.Length == 0)
            {
                return null;
            }

            if (renderer.sharedMaterials.Length == 1 || triangleIndex < 0 ||
                !renderer.TryGetComponent<MeshFilter>(out var meshFilter) || meshFilter.sharedMesh == null)
            {
                return renderer.sharedMaterial;
            }

            var mesh = meshFilter.sharedMesh;
            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var range = mesh.GetSubMesh(sub);
                var triangleStart = range.indexStart / 3;
                var triangleCount = range.indexCount / 3;
                if (triangleIndex >= triangleStart && triangleIndex < triangleStart + triangleCount)
                {
                    return sub < renderer.sharedMaterials.Length ? renderer.sharedMaterials[sub] : renderer.sharedMaterial;
                }
            }

            return renderer.sharedMaterial;
        }

        // The TerrainLayer with the highest alpha weight at the hit's terrain-local position.
        private static Texture2D DominantTerrainTexture(Terrain terrain, Vector3 worldPoint)
        {
            var data = terrain.terrainData;
            var local = worldPoint - terrain.transform.position;
            var mapX = Mathf.Clamp(Mathf.RoundToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            var mapZ = Mathf.Clamp(Mathf.RoundToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);

            var alphamap = data.GetAlphamaps(mapX, mapZ, 1, 1);
            var bestLayer = 0;
            var bestWeight = 0f;
            for (var layer = 0; layer < data.alphamapLayers; layer++)
            {
                var weight = alphamap[0, 0, layer];
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    bestLayer = layer;
                }
            }

            return bestLayer < data.terrainLayers.Length ? data.terrainLayers[bestLayer].diffuseTexture : null;
        }

        // ---- Playback ----

        /// <summary>
        /// What lays decals on surfaces. Provided by the optional decals assembly (URP's Decal Projector), which registers it
        /// by itself; null means no decals can be drawn, and the sound and VFX of an impact play without one.
        /// </summary>
        public static IDecalPainter DecalPainter { get; set; }

        // Seconds a particle prefab plays for (its duration plus its longest particle), looked up once per prefab.
        private static readonly Dictionary<ParticleSystem, float> ParticleTimes = new();
        private static bool _warnedNoPainter;

        // One host object plays a whole impact: it holds the sound (an AudioSource), the decal (projected from it) and the
        // particles (a child), and goes back to its pool when they are done. Nothing is made for what an entry does not have.
        private static void Play(AudioClip[] sounds, ParticleSystem[] vfx, Material decalMaterial, Vector3 point, Vector3 normal, in ImpactShape shape, string poolTypeName)
        {
            var clip = PickOne(sounds);
            var effect = PickOne(vfx);
            var decal = shape.SkipDecal ? null : decalMaterial;
            if (clip == null && effect == null && decal == null)
            {
                return;
            }

            PlayAsync(clip, effect, decal, point, normal, shape, poolTypeName).Forget();
        }

        private static T PickOne<T>(T[] items) where T : class =>
            items == null || items.Length == 0 ? null : items[UnityEngine.Random.Range(0, items.Length)];

        private static async UniTaskVoid PlayAsync(AudioClip clip, ParticleSystem effect, Material decal, Vector3 point, Vector3 normal, ImpactShape shape, string poolTypeName)
        {
            var settings = _data.Settings;
            var token = _lifetimeCts.Token;
            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;

            var host = AcquireHost(point, normal, settings, poolTypeName);
            GameObject particles = null;
            var painted = false;

            if (clip != null)
            {
                PlaySound(host, clip);
            }

            if (decal != null)
            {
                painted = Paint(host, decal, point, normal, shape, settings);
            }

            if (effect != null)
            {
                particles = SpawnParticles(effect.gameObject, point, normal, host.transform);
            }

            // A decal stays for its whole lifetime (and fades at the end); without one the host is let go as soon as the sound and particles are done.
            var hold = settings.EffectLifetime;
            if (!painted)
            {
                hold = Mathf.Min(hold, Mathf.Max(clip != null ? clip.length : 0f, effect != null ? ParticleTime(effect) : 0f));
            }

            var fade = painted ? Mathf.Min(settings.DecalFadeTime, hold) : 0f;
            var canceled = await UniTask.Delay(TimeSpan.FromSeconds(hold - fade), cancellationToken: token).SuppressCancellationThrow();
            if (!canceled && fade > 0f)
            {
                await FadeDecal(host, fade, token);
            }

            EndHost(host, particles, painted);
        }

        // An instance of the pool named by the footstep or impact setting (Pool mode), else - or when that pool does not exist - a new object.
        private static GameObject AcquireHost(Vector3 point, Vector3 normal, SurfaceSettings settings, string poolTypeName)
        {
            GameObject host = null;
            if (settings.AudioPlaybackMode == AudioPlaybackMode.Pool && !TryPoolGetByName(poolTypeName, out host))
            {
                Debug.LogWarning($"[SurfaceEngine] Audio Playback Mode is Pool but '{poolTypeName}' isn't a registered PoolSystem type - using a plain object for this call.");
            }

            if (host == null)
            {
                host = new GameObject("Surface Impact");
            }

            host.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
            return host;
        }

        private static void PlaySound(GameObject host, AudioClip clip)
        {
            if (!host.TryGetComponent<AudioSource>(out var source))
            {
                source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
            }

            source.clip = clip;
            source.Play();
        }

        private static bool Paint(GameObject host, Material material, Vector3 point, Vector3 normal, in ImpactShape shape, SurfaceSettings settings)
        {
            var painter = DecalPainter;
            if (painter == null)
            {
                if (!_warnedNoPainter)
                {
                    _warnedNoPainter = true;
                    Debug.LogWarning("[SurfaceEngine] A decal material is set, but nothing can draw decals: they need the Universal Render Pipeline (the SurfaceSystem.Decals.Runtime assembly). Sounds and VFX still play.");
                }

                return false;
            }

            return painter.Paint(host, material, point, normal, in shape, settings);
        }

        // The particles of an impact (pooled if PoolSystem knows the prefab, Instantiate otherwise) as a child of the host.
        private static GameObject SpawnParticles(GameObject prefab, Vector3 point, Vector3 normal, Transform host)
        {
            var instance = TryPoolGet(prefab, out var pooled) ? pooled : UnityEngine.Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
            instance.transform.SetParent(host, true);

            // Play on Awake alone isn't enough for a pooled/reused instance, so play explicitly.
            if (instance.TryGetComponent<ParticleSystem>(out var system))
            {
                system.Play(true);
            }

            return instance;
        }

        private static float ParticleTime(ParticleSystem prefab)
        {
            if (ParticleTimes.TryGetValue(prefab, out var seconds))
            {
                return seconds;
            }

            foreach (var system in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                seconds = Mathf.Max(seconds, main.duration + main.startLifetime.constantMax);
            }

            seconds += 0.5f;
            ParticleTimes[prefab] = seconds;
            return seconds;
        }

        // The decal fades from fully visible to gone over `seconds`. Only runs while it fades.
        private static async UniTask FadeDecal(GameObject host, float seconds, CancellationToken token)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow() || host == null)
                {
                    return;
                }

                elapsed += Time.deltaTime;
                DecalPainter?.SetVisible(host, 1f - Mathf.Clamp01(elapsed / seconds));
            }
        }

        // Everything the impact put on the host comes off, and the host goes back to its pool (or is destroyed).
        private static void EndHost(GameObject host, GameObject particles, bool painted)
        {
            if (particles != null)
            {
                particles.transform.SetParent(null, true);
                Release(particles);
            }

            if (host == null)
            {
                return;
            }

            if (painted)
            {
                DecalPainter?.Clear(host);
            }

            if (host.TryGetComponent<AudioSource>(out var source))
            {
                source.Stop();
                source.clip = null;
            }

            Release(host);
        }

        private static void Release(GameObject instance)
        {
            if (!TryPoolRelease(instance))
            {
                UnityEngine.Object.Destroy(instance);
            }
        }

#if HAS_POOL_SYSTEM
        private static bool TryPoolGet(GameObject prefab, out GameObject instance) => PoolSystem.PoolManager.TryGet(prefab, out instance);
        private static bool TryPoolRelease(GameObject instance) => PoolSystem.PoolManager.TryRelease(instance);

        private static bool TryPoolGetByName(string poolTypeName, out GameObject instance)
        {
            if (!string.IsNullOrEmpty(poolTypeName) && Enum.TryParse<PoolSystem.PoolTypes>(poolTypeName, out var type))
            {
                return PoolSystem.PoolManager.TryGet(type, out instance);
            }

            instance = null;
            return false;
        }
#else
        private static bool TryPoolGet(GameObject prefab, out GameObject instance)
        {
            instance = null;
            return false;
        }

        private static bool TryPoolRelease(GameObject instance) => false;
        private static bool TryPoolGetByName(string poolTypeName, out GameObject instance)
        {
            instance = null;
            return false;
        }
#endif

        private static void RequireInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("[SurfaceEngine] Not initialized. Call SurfaceEngine.Initialize(surfaceData) first.");
            }
        }

        // Keeps the static state clean when "Enter Play Mode Options" skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _lifetimeCts = null;
            _data = null;
            _textureLookup = null;
        }
    }
}
