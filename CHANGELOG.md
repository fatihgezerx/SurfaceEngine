# Changelog

## [Unreleased]

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
