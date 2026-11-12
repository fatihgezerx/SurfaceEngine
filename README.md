# SurfaceEngine

Footsteps and impacts that sound and look different on every surface.

## Overview

SurfaceEngine raycasts to what was hit (a renderer's material texture or a Terrain layer), identifies the surface from the
textures you listed for it, and plays that surface's sounds, VFX and decal for the kind of step (`FootstepType`) or hit
(`ImpactType`). Surfaces are described in one `SurfaceData` asset; **Compile** generates the `SurfaceGroups` enum with a stable
id per surface. Put a `SurfaceHandler` on anything that walks or strikes, call `SurfaceEngine.Initialize(data)` once, then call
`Footstep()` / `Impact(origin)`.

Requires Event System and UniTask; Pool System is optional (pooled sound / decal instances); URP decals need URP.

## Trails (grass, water, snow and sand)

Anything with a `SurfaceTrailSource` - the player, an NPC, an animal - bends the grass it walks through, sends ripples across the
water it wades in and sinks into snow, sand or mud, kicking up that surface's particles while it moves. Nothing is wired: the
sources are stamped every frame into three world-aligned **trail maps** (render textures around a focus) that `SurfaceTrails`
publishes as shader globals, and the shipped shaders read them by world position.

**Setting it up**

1. `Surface Data > Settings > Trails`: switch **Trails** on. `SurfaceEngine.Initialize` then also starts the maps (or call
   `SurfaceTrails.Initialize(settings, groundLayers)` yourself). Area size (meters around the focus), resolution, how fast grass
   stands back up, how water ripples fade, whether prints fill back.
2. Put a **Surface Trail Source** on every character (radius of its feet, grass bend, pressure, ripple strength). The maps follow
   the first source unless `SurfaceTrails.Focus` says otherwise.
3. Use the shaders on the ground materials (samples in `Effects/Materials`):
   - `SurfaceEngine/Trail Grass` on grass meshes (blades, tufts, patches, or a Terrain detail prototype in Instanced mode): leans
     away from sources and recovers, wind, tip colour, two-sided cutout, full lighting.
   - `SurfaceEngine/Trail Water` on a water plane (a few vertices per meter): the simulated height moves the surface, bends the
     normals and foams the crests; depth tint (needs the URP Depth Texture), scrolling normal maps, fresnel.
   - `SurfaceEngine/Trail Deform` on snow, sand or mud: hardware tessellation (shader model 4.6) sinks the prints with a soft rim,
     bends the normals and tints the trampled area; every pass (shadows, depth, normals) sinks the same way.
   A project shader can include `Shaders/Trails/SurfaceTrails.hlsl` instead (`SampleSurfaceBend`, `SampleSurfaceRippleHeight` /
   `...Normal`, `SampleSurfaceDeform` / `...Normal`).
4. Per surface, the **Trail** section of a surface in `Surface Data` says what sources do on it: **Print depth** (0 = hard ground,
   1 = the full print depth of the material), **Water** (sources on it make ripples), **Kick-up particles** with the speed at which
   they start, the speed at which they reach the full rate, and the rate. Grass bends everywhere - it needs no surface.
   A source finds its surface with a short raycast at the Surface check interval through the engine's usual identification, so the
   snow mesh must carry a texture listed in the Snow surface. Water without a collider gets a **Surface Water Volume** on a trigger.

**Wind, pushes, rain, monitor**

- **Wind**: one `SurfaceWind` component in the scene (direction = its forward, strength, gusts, gust size and speed, turbulence; or assign
  a Unity **Wind Zone** and its values and direction are used, so the same zone drives particles through their External Forces module).
  It publishes shader globals; Trail Grass and Trail Water read them (each material's **Wind influence** scales it) and any shader can
  include `Shaders/Trails/SurfaceWind.hlsl` (`SurfaceWindAt(positionWS)`). Gust fronts travel across the field; the wind itself is computed
  on the GPU. `SurfaceWind.VelocityAt(position)` mirrors it on the CPU for leaves or drifting particles. Without a SurfaceWind the grass
  falls back to its material's own simple wind.
- **Pushes**: `SurfaceTrails.Push(position, radius, bend, ripple, depth)` flattens grass outward, dents the water and prints the snow once
  (an explosion, a landing, a spell, something heavy dropped).
- **Rain**: `SurfaceTrails.Rain = 0..1` sprinkles small ripples over the whole area (`Rain drops` per second at full rain, in Settings > Trails).
  Rain and pushes keep the maps alive even without a source.
- `SurfaceTrails.Clear()` empties the maps.
- `Tools > Surface Engine > Trail Monitor` shows the three maps live in Play Mode (bend direction and strength, ripple height, print
  depth), the origin and sources, the wind, and has a Rain slider, a Push and a Clear button - the quickest way to see the system work.

**How it works**

| Map | Stores | Each frame | Read by |
|-----|--------|------------|---------|
| Bend | push direction and strength | decays (grass recovery) | Trail Grass |
| Ripple | signed water height, three buffers | wave-equation steps at a fixed rate (ripple rate, ripple fade) | Trail Water |
| Deform | print depth | kept, or fills back (print recovery) | Trail Deform |

One instanced draw per map stamps every source (one `CommandBuffer`, no helper camera, no extra layer). The maps re-centre when
the focus walks a quarter of the area away, snapping the origin to the texel grid and shifting their content, so prints behind the
player stay put. A map whose content has settled skips its pass. The internal shaders live in `Resources/SurfaceEngine` so a build
never strips them. Limits: trails exist only inside the area; the maps are planar (a bridge over water is not a water surface, so
nothing ripples under it unless a source is flagged in water); tessellation needs hull / domain stages (PC, consoles; not Metal).

## In the editor

`SurfaceData` (Create > Surface Engine > Surface Data) has a header with the surface and texture counts and a state pill
(Compiled, Needs compile, Errors, Empty), the **Compile** button, and hint rows for problems. Two tabs:

- **Surfaces**: one card per surface. Drag the handle to reorder, edit the name (it becomes the `SurfaceGroups` member), fold or
  remove it. Inside: a wrapping grid of texture cards (click the small button on a card to pick, or drop Texture2D assets, several
  at once), then Footsteps and Impacts, one card per kind. Open a card for its Sounds, VFX (drop audio clips or particle prefabs)
  and decal material. A kind a surface does not define falls back to its Generic card.
- **Settings**: ground layers and footstep ray distance; sound playback (Instantiate or Pool, with the pool type names); decal
  lifetime, fade, default size and projection depth; the Trails card (area, resolution, recovery and ripple settings).

Each surface card also has a **Trail** section (print depth, water, kick-up particles). `SurfaceTrailSource` shows its radius, grass
bend, pressure and ripple strength and, in Play Mode, its speed, surface and whether it is in water; `SurfaceWaterVolume` warns when
its collider is not a trigger.

`SurfaceHandler` shows the footstep ray (origin, distance) and the impact ray (distance, layers) and, in Play Mode, whether
`SurfaceEngine` is initialized.

The `SurfaceHandler` and `SurfaceData` Inspectors are drawn from default layouts that Surface Engine ships
(`SurfaceHandlerDefaultLayout`, `SurfaceDataDefaultLayout`). In `Tools > Veneer > Inspector Designer` you can **Customize** one (a
copy you may rearrange or extend) and **Reset** it to the system default. The system's own items (the state pills, the "not
initialized" hint, the Compile row, the validation hints, the pool type hint and the list of surfaces) can be moved or hidden
there, not changed.

The studio look needs the optional **Veneer** system. Without it, `SurfaceData` falls back to Unity's default Inspector with
the explanation and the Compile button, and `SurfaceHandler` to the default Inspector; everything still works. The dependency
guard offers to download the kit into `Assets/Scripts/Veneer` the first time.

