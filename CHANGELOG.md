# Changelog

## [1.0.1] - 2026-10-03

### Fixed
- Deleting Surface Engine no longer clears the define symbols it shares with other systems
  (`HAS_EVENT_SYSTEM`, `HAS_UNITASK`, `HAS_POOL_SYSTEM`): the guard now sets them to whatever is still
  installed. Before, removing it left Event System / UniTask users (Interaction System, Inventory System)
  out of compilation until the symbols were re-added by hand.
- The setup dialog now says "Surface Engine" instead of "Surface System".

### Changed
- `SurfaceData`'s Create menu entry is now **Create > Surface Engine > Surface Data** (was Surface System). Existing
  assets are unaffected.

## [1.0.0] - 2026-10-03

### Added
- `SurfaceEngine` static entry point (`Initialize`, `Shutdown`, `Footstep`, `Impact`, `Identify`) that
  identifies the hit surface by texture and plays its sound, VFX and decal, with no `Update` or
  coroutine - effect lifetimes run on cancellable `UniTask.Delay`s.
- Surface identification from a renderer's material `mainTexture` (submesh resolved from the hit
  triangle on multi-material renderers) and from the dominant layer on a `Terrain`.
- `SurfaceData` ScriptableObject (**Create > Surface System > Surface Data**): general raycast settings
  plus named `SurfaceGroup`s, each with identifying textures, `FootstepEntry` per `FootstepType` and
  `ImpactEntry` per `ImpactType`, with a `Generic` fallback entry.
- Custom Inspector (`SurfaceDataEditor`) with drag-to-reorder groups, a wrapping texture card grid and
  compact Footstep / Impact cards, plus one-click **Compile** (`SurfaceCompiler`) generating the
  `SurfaceGroups` enum from stable group ids.
- `SurfaceHandler` component with its own footstep / impact ray settings.
- `FootstepEvent` and `ImpactEvent`, published through EventSystem's `EventManager`.
- `AudioPlaybackMode`: `Instantiate` (`AudioSource.PlayClipAtPoint`) or `Pool` (PoolSystem).
- Optional PoolSystem integration for VFX, decals and audio sources.
- `DependencyGuard`: installs UniTask, EventSystem and PoolSystem when missing and keeps the
  `HAS_*` define symbols in sync, so the project compiles without them.
