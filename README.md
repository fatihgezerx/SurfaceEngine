# SurfaceEngine

Surface-aware footsteps and impacts for Unity: identify what you hit, play its sound, VFX and decal.

## Overview

SurfaceEngine answers one question - *"what surface did I just hit?"* - and then plays the right
sound, particle effect and decal for it. You describe every surface once in a `SurfaceData` asset:
named groups (e.g. `Grass`, `Wood`, `Snow`), each with the textures that identify it and what it
sounds and looks like on **Footstep** (walk, run, jump land...) and on **Impact** (sword, gun, axe...).
A one-click **Compile** step generates a strongly-typed `SurfaceGroups` enum.

Identification is texture based, with no tags or physics materials to maintain: a hit is matched
against the main texture of the hit renderer's material (or, on a `Terrain`, the dominant layer's
diffuse texture at the hit point) and looked up in a dictionary built once at `Initialize`.

It owns no `Update` and no coroutines. Every call is triggered by the caller - a movement loop, an
animation event, a weapon's own hit raycast - and the effects' lifetimes run on cancellable
`UniTask.Delay`s.

> The repository is named **SurfaceEngine**, the C# namespace and the folder inside your project are
> **SurfaceSystem** (`using SurfaceSystem;`), and the static entry point is `SurfaceEngine`.

## Features

- `SurfaceData` ScriptableObject with a custom Inspector: general raycast settings on top, then named
  surface groups you can reorder by dragging, each split into **Textures** (a wrapping 50x50 card
  grid), **Footsteps** and **Impacts**
- Footstep entries per `FootstepType` (`Generic`, `Crouch`, `Walk`, `Run`, `JumpStart`, `JumpLand`) and
  impact entries per `ImpactType` (`Generic`, `Sword`, `Axe`, `Blunt`, `Bow`, `Gun`, `Explosive`). A
  surface only defines the types that should sound different - everything else falls back to its
  `Generic` entry
- Each entry plays a random sound and a random VFX out of its lists, plus an optional decal prefab
  (footprint, bullet hole, sword gash...) placed on the hit point, oriented to the hit normal
- Terrain support (dominant layer at the hit point) and multi-material renderers (submesh resolved
  from the hit triangle)
- One-click **Compile**: generates a `SurfaceGroups` enum whose values are stable ids, so renaming or
  reordering groups never shifts a value already saved in a scene or prefab. Nothing is rewritten (and
  no script reload happens) if the result did not change
- `SurfaceHandler` component: attach it to a player, an NPC or an animal and call `Footstep()` /
  `Impact()` with that instance's own ray settings
- `FootstepEvent` / `ImpactEvent` published through [EventSystem](https://github.com/fatihgezerx/EventSystem)
  (readonly structs, no allocation) so UI, AI noise or analytics can react without coupling
- Audio playback modes: `Instantiate` (`AudioSource.PlayClipAtPoint`, no setup) or `Pool` (an
  `AudioSource` prefab taken from PoolSystem and released when the clip ends)
- Optional [PoolSystem](https://github.com/fatihgezerx/PoolSystem) integration for VFX and decals;
  without it they are simply instantiated and destroyed
- A dependency guard that installs what is missing and keeps the project compiling without it

## Setup

### Requirements

- Unity 6000.3 LTS or newer
- [UniTask](https://github.com/Cysharp/UniTask) (required)
- [EventSystem](https://github.com/fatihgezerx/EventSystem) (required)
- [PoolSystem](https://github.com/fatihgezerx/PoolSystem) (optional, for pooled VFX / decals / audio)

### Installation

Clone or download this repository, then copy its contents into `Assets/Scripts/SurfaceEngine/`.
Systems that use SurfaceEngine (e.g. [FirstPersonSystem](https://github.com/fatihgezerx/FirstPersonSystem))
can also download it there for you, from their setup dialog. Either way you get the same files, visible
and editable in `Assets`.

On the first import SurfaceEngine's setup dialog offers to install whatever is missing: UniTask
through the Package Manager, EventSystem and PoolSystem by downloading their repositories into
`Assets/Scripts/...`. Until its required dependencies are present the runtime and editor assemblies
are simply left out of compilation, so adding SurfaceEngine to a project never produces compile
errors.

## Quick Start

**1. Create a Surface Data asset** via `Create > Surface Engine > Surface Data`.

- General settings sit on top: **Ground Layers** and **Default Ray Distance** (used when `Footstep`
  raycasts on its own), **Effect Lifetime** (seconds a VFX / decal stays before it is released) and
  **Audio Playback Mode**.
- Add a group per surface and give it a **unique name**. The name becomes its `SurfaceGroups` member.
- Drop the surface's **textures** into the group. They must be the **exact same `Texture2D` assets**
  the surface's material (or Terrain layer) uses - they are the runtime identification key, not just
  previews.
- Add **Footstep** and **Impact** cards for the types that should sound or look different.
  A `Generic` card is the fallback for every type the surface has no card for.

**2. Click Compile.** Press it again after renaming or reordering groups.

**3. Initialize once** (e.g. from a GameManager), and shut down when you are done:

```csharp
using SurfaceSystem;
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private SurfaceData surfaceData;

    private void Awake() => SurfaceEngine.Initialize(surfaceData);
    private void OnDestroy() => SurfaceEngine.Shutdown();
}
```

**4. Add a `SurfaceHandler` to any object that should step or hit** and call it from wherever fits -
a movement loop, an animation event, a weapon:

```csharp
[SerializeField] private SurfaceHandler surfaceHandler;

surfaceHandler.Footstep(FootstepType.Run);             // ray straight down from the handler
surfaceHandler.Impact(weaponMuzzle, ImpactType.Gun);   // ray forward from the given transform
```

Both return the identified `SurfaceGroup` (or `null` if nothing was hit or the surface is not
recognised), and do nothing if `SurfaceEngine` has not been initialized yet.

If you already run your own raycast, skip the second one and hand the hit over:

```csharp
if (Physics.Raycast(origin, Vector3.down, out var hit, 2f))
{
    SurfaceEngine.Footstep(hit, FootstepType.JumpLand);
}

SurfaceEngine.Impact(hit, ImpactType.Sword);

// Hits that do not come from a raycast, e.g. a collision callback:
SurfaceEngine.Impact(contact.point, contact.normal, contact.otherCollider, ImpactType.Blunt);
```

**5. React to what happened** (optional):

```csharp
private void OnEnable() => EventManager.Register<FootstepEvent>(OnFootstep);
private void OnDisable() => EventManager.Unregister<FootstepEvent>(OnFootstep);

private void OnFootstep(FootstepEvent e)
{
    // e.Group, e.Type, e.Point, e.Normal, e.Collider
}
```

`ImpactEvent` carries the same data.

## Settings

| Setting | What it does |
|---------|--------------|
| Ground Layers | Layers `Footstep(Transform)` raycasts against when none is given |
| Default Ray Distance | How far down `Footstep(Transform)` raycasts when no distance is given |
| Effect Lifetime | Seconds a spawned VFX / decal stays before it is released (pool) or destroyed |
| Audio Playback Mode | `Instantiate` - `AudioSource.PlayClipAtPoint`, no configuration. `Pool` - takes an `AudioSource` instance from PoolSystem and releases it when the clip ends |
| Pool Audio Type Name | `Pool` mode only: the exact `PoolTypes` member name your Pool Data compiled the audio prefab into. If it is not a registered pool, that call logs a warning and falls back to `Instantiate` |

`SurfaceHandler` has its own **Footstep Origin**, **Footstep Distance**, **Impact Distance** and
**Impact Layers**, so a player, a wolf and a rifle can each use different rays.

`FootstepType` and `ImpactType` are plain hand-written enums - add your own members to them directly.

## How a hit is identified

1. The hit collider is a `TerrainCollider`: the terrain layer with the highest weight at the hit
   point is used, via its diffuse texture.
2. Otherwise the hit renderer's material `mainTexture` is used. On a multi-material renderer, the
   submesh is resolved from the hit triangle when possible, otherwise the first material is used.
3. That texture is looked up in the group textures. No match means `null` - nothing plays and no
   event is raised.

## License

[MIT License](LICENSE)
