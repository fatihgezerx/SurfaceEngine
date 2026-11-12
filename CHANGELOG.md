# Changelog

## [Unreleased]

### Trails
- **Surface Trails**: grass bends under anything that walks through it, water ripples where things wade, snow / sand / mud keep prints
  (Red Dead Redemption 2 style) and kick up particles. Three world-aligned trail maps (`SurfaceTrails`: Bend, Ripple - a fixed-rate
  wave-equation height field - and Deform) around a focus, stamped every frame with one instanced draw per map, re-centred and shifted
  as the focus moves, published as shader globals (`_SurfaceTrailBend` / `_SurfaceTrailRipple` / `_SurfaceTrailDeform`,
  `_SurfaceTrailParams`, `_SurfaceTrailTexel`). One UniTask loop at `PostLateUpdate`, alive only while sources exist; settled maps skip their pass.
- `SurfaceTrailSource` (radius, grass bend, pressure, ripple strength): registers itself, measures its speed, identifies its surface with a
  short raycast at an interval and runs the surface's kick-up particles (pooled through PoolSystem when known). `SurfaceWaterVolume`: a
  trigger that marks sources as in water.
- `SurfaceSettings.Trails` (`SurfaceTrailSettings`: enabled, area size, resolution, grass recovery, ripple fade and rate, print recovery,
  surface check interval); `SurfaceEngine.Initialize` starts the maps when enabled and `Shutdown` stops them.
- `SurfaceGroup.Trail` (`SurfaceTrailEntry`: print depth, water, kick-up particles with min / full speed and rate).
- Shaders: `SurfaceEngine/Trail Grass` (bend + wind, cutout, two-sided, lit, shadow / depth / normals passes), `SurfaceEngine/Trail Water`
  (ripple height and normals, foam, depth tint, scrolling normal maps, fresnel), `SurfaceEngine/Trail Deform` (distance-based hardware
  tessellation, prints with a rim, bent normals, trampled tint, every pass sinks the same way) and the include `Shaders/Trails/SurfaceTrails.hlsl`.
  Internal passes in `Resources/SurfaceEngine` (stamp, decay, ripple step, shift). Sample materials `Effects/Materials/Trail*.mat`.
- Editor: Trails card on the Settings tab, a Trail section per surface, studio Inspectors for `SurfaceTrailSource` and `SurfaceWaterVolume`.
- **Wind**: `SurfaceWind` component (direction, strength, gusts, gust size / speed, turbulence; optional Wind Zone bridge) publishes
  `_SurfaceWindParams` / `_SurfaceWindGust`; `Shaders/Trails/SurfaceWind.hlsl` (`SurfaceWindAt`, `SurfaceWindFlutter`); Trail Grass leans
  with it (`Wind influence`, material sine as the fallback) and Trail Water's normal layers drift downwind; `SurfaceWind.VelocityAt` CPU mirror.
- `SurfaceTrails.Push(position, radius, bend, ripple, depth)` one-off impulses, `SurfaceTrails.Rain` (0..1, `Rain drops` per second in the
  settings) and `SurfaceTrails.Clear()`; the loop also runs for rain and pushes without a source.
- `Tools > Surface Engine > Trail Monitor` (Veneer): the three maps live, KPIs, wind, Rain slider, Push and Clear; `Editor/Studio/Shaders/TrailView.shader`.
  Studio Inspector for `SurfaceWind`.

### Editor
- The `SurfaceData` Inspector is drawn from a default layout shipped by the system (`SurfaceDataDefaultLayout`), customizable in the
  Veneer Inspector Designer (Customize / Reset). Tabs Surfaces and Settings are layout entries (explanation note, Ground detection,
  Sound playback and Decals cards, pool fields shown only in Pool mode). The state pill, the Compile row, the validation hints and the
  pool type hint are registered items, and the list of surfaces (cards, textures, footstep / impact entries, drag reorder) is the
  custom widget of `groups` (`SurfaceGroupsView`). Behaviour and saved data are unchanged; the playback mode tooltip is now one
  tooltip on the row.
- The `SurfaceHandler` Inspector is drawn from a default layout shipped by the system (`SurfaceHandlerDefaultLayout`), customizable in
  the Veneer Inspector Designer (Customize / Reset). The state pill and the "not initialized" hint are registered items; the hint
  now sits at the top of the Footsteps card and the usage note in a Usage card.
- **Removed `Tools > Surface Engine > Create Impact Effects`** (and its generator `ImpactEffects` and the drawn textures in `ImpactTextures`). The default effects are no longer made by the system, and nothing runs by itself when the scripts load any more; effects (decal materials, bursts) are made and assigned in the project.
- New studio-look `SurfaceData` Inspector (Veneer): header card with the surface and texture counts and a compiled /
  needs compile / empty / errors state pill, a prominent Compile button, validation hint rows (Compile-aborting names, a surface
  without textures, a texture shared by two surfaces, the enum being out of date). Two tabs: **Surfaces** (one card per surface with
  drag-to-reorder, editable name, texture count, fold and remove; a wrapping texture grid with drop-several-at-once; Footsteps and
  Impacts cards per kind that open to Sounds, VFX and decal material, with the same drop rules as before) and **Settings** (ground
  layers and ray distance, sound playback mode with the Pool type names shown only in Pool mode, decal lifetime / fade / size / depth).
  Undoable add / remove / reorder. Surface folds keep their old EditorPrefs key; the open state of footstep / impact cards is a new key.
- New studio-look `SurfaceHandler` Inspector (footstep ray, impact ray, Play Mode ready / not initialized pill).
- The studio Inspectors live in their own gated assembly `SurfaceSystem.Editor.Studio` (needs `HAS_VENEER`);
  `SurfaceSystem.Editor` keeps the compile pipeline and the impact effect maker and, without the kit, a small fallback Inspector
  (default drawing plus the explanation and a Compile button).
- `Editor/Setup/DependencyGuard`: the optional Veneer is detected (`HAS_VENEER`) and offered for download.
- Brand: orange-brown accent, `SurfaceBrand` and `Editor/Brand/SurfaceBrand.uss`.
- Removed the IMGUI card editor and `GroupDragReorder`. Dropped: the inner scroll and bottom fade of a long texture grid (the grid wraps
  fully now). `SurfaceCompiler.ToIdentifier` became internal so the Inspector validates names with Compile's own rule.
