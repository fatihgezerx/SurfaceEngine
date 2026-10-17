# SurfaceEngine

Footsteps and impacts that sound and look different on every surface.

## Overview

SurfaceEngine raycasts to what was hit (a renderer's material texture or a Terrain layer), identifies the surface from the
textures you listed for it, and plays that surface's sounds, VFX and decal for the kind of step (`FootstepType`) or hit
(`ImpactType`). Surfaces are described in one `SurfaceData` asset; **Compile** generates the `SurfaceGroups` enum with a stable
id per surface. Put a `SurfaceHandler` on anything that walks or strikes, call `SurfaceEngine.Initialize(data)` once, then call
`Footstep()` / `Impact(origin)`.

Requires Event System and UniTask; Pool System is optional (pooled sound / decal instances); URP decals need URP.

## In the editor

`SurfaceData` (Create > Surface Engine > Surface Data) has a header with the surface and texture counts and a state pill
(Compiled, Needs compile, Errors, Empty), the **Compile** button, and hint rows for problems. Two tabs:

- **Surfaces**: one card per surface. Drag the handle to reorder, edit the name (it becomes the `SurfaceGroups` member), fold or
  remove it. Inside: a wrapping grid of texture cards (click the small button on a card to pick, or drop Texture2D assets, several
  at once), then Footsteps and Impacts, one card per kind. Open a card for its Sounds, VFX (drop audio clips or particle prefabs)
  and decal material. A kind a surface does not define falls back to its Generic card.
- **Settings**: ground layers and footstep ray distance; sound playback (Instantiate or Pool, with the pool type names); decal
  lifetime, fade, default size and projection depth.

`SurfaceHandler` shows the footstep ray (origin, distance) and the impact ray (distance, layers) and, in Play Mode, whether
`SurfaceEngine` is initialized.

The studio look needs the optional **Veneer** system. Without it, `SurfaceData` falls back to Unity's default Inspector with
the explanation and the Compile button, and `SurfaceHandler` to the default Inspector; everything still works. The dependency
guard offers to download the kit into `Assets/Scripts/Veneer` the first time.

