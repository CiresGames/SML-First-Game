# Cinematic cloudscape

The MantaFlight scene procedurally generates **36 decorative, collider-free cloud formations** across four altitude layers under **Cloudscape • procedural weather**. The scene stores a distribution recipe rather than individual cloud transforms. Generated geometry is transient and rebuilt in Edit mode and Play mode. The former `Distant cloud` primitives remain disabled; the seven manually arranged volumetric banks have been replaced.

## Formation shapes and motion

Each volume combines five seeded, connected density lobes into an asymmetric formation, with a flatter condensation base, uneven tops and noise-distorted edges. Low/thin layers compress this structure into banks and veils; deep layers produce broader billows. Lenticular weather deliberately retains its smoother lens shape. All lobes render inside the existing single volume; no extra meshes, draw calls or noise-texture fetches are added. The density function does perform more arithmetic per sample, so unchanged volume count is not a guarantee of unchanged GPU cost.

Wind now transports whole cloud volumes horizontally, scaled by each altitude layer's Wind Speed. Rain footprints and atmospheric cloud-shadow proxies follow their transforms. Integrated noise motion carries texture detail with the bank, while Evolution drives slow internal changes and lobe growth/decay. Growth is calculated on the CPU once per cloud update instead of inside each ray-march step. Time scale zero pauses drift and evolution.

Clouds fade out and recycle beyond the distribution region, using the same bounded pool. Rain and shadow strength fade with them. Corridor/spacing rules describe initial generation; drifting clouds can subsequently cross those boundaries. This is an artistic approximation of advection and condensation, not a fluid simulation. Rain and god-ray occlusion still use simplified cloud envelopes rather than the full new lobe/noise density.

Verified in Play mode: wind displacement, zero-time pause, moving rain footprint, faded boundary recycling, 36-volume limit and shader compilation. No Unity errors were reported. The new silhouettes were visually reviewed; standalone performance remains unbenchmarked.

## Procedural placement

Select the cloudscape's **Manta Cloud Distribution** component:

- **Seed** reproduces a layout exactly. Change it for another distribution, or use **Manta → Environment → Cloud layout → Try another seed**.
- **Cloud Count** sets the requested number (0–64; default 24).
- **Region Center / Size** define the area around the scene (default 5400 × 5000 metres). Coordinates are local to the cloudscape root.
- **Altitude Range**, **Minimum / Maximum Size**, and **Minimum Cloud Base** control elevation and dimensions. Altitudes are relative to Region Center's Y.
- **Minimum Spacing** separates visible cloud banks. **Patchiness / Patch Size** vary broad weather clusters across the area rather than producing an even grid.
- **Clear Corridor / Corridor Clearance** keep low clouds out of a rectangular flight corridor centered on the region. Clouds above the clearance can span it. This is a configurable spatial constraint, not terrain collision detection.
- **Show Region** displays the cloud region and corridor when selected.

Inspector edits regenerate automatically; **Regenerate from seed** is also available from the component context menu. The same settings and seed produce the same positions, sizes, and noise variation without altering gameplay's random state. Weather transitions keep the distribution stable and smoothly change the generated clouds' shapes and density.

The generator uses bounded, spaced rejection sampling and broad procedural noise patches. If the requested cloud count cannot fit, it generates fewer clouds instead of violating spacing or looping indefinitely. `GenerationStatus` reports the result; enlarge the region or reduce sizes/spacing if necessary. Generated children are rendering containers only: edits to them are discarded on regeneration. Edit the distribution component to make persistent changes.

Disable the generator to remove its generated clouds; re-enable it to rebuild them. Geometry is rebuilt only when needed, not every frame. The region is finite and does not follow the camera. Increasing cloud count can increase rendering cost; the default is tuned for this playground rather than an infinite world.

The `MantaCloudscape` component exposes coverage, footprint, thickness, noise scale, edge erosion, lenticular shape, layer strength, density, sun/shadow colors, wind, detail evolution, and ray steps (32–96; default 64). Coverage is an artistic density-field control, not a measured percentage of the sky. Wind displacement and evolution are integrated over scaled game time, then sent to the shader using cached renderers/property blocks without per-frame C# allocations. Changing wind during a transition does not teleport the noise.

## Weather presets and automatic cycle

Five editable `MantaCloudPreset` assets live in `Assets/MantaFlight/Settings/Cloud Weather/`:

| Preset | Appearance | Hold | Blend into preset |
| --- | --- | --- | --- |
| Big clouds | Warm towering cumulus | 100 s | 55 s |
| Scattered clouds | Smaller broken cotton patches | 80 s | 45 s |
| Thin clouds | Translucent flattened veils | 80 s | 50 s |
| Lenticular clouds | Smooth, stacked lens shapes | 100 s | 60 s |
| Heavy clouds | Broad dense banks, cool dark interiors | 90 s | 65 s |

The scene's **Manta Cloud Weather** component starts with Big clouds and loops through this order in **12 minutes 5 seconds**. Every shape, density, lighting, and wind parameter smoothly interpolates. Each stage's transition duration means the time to blend **into that stage**; the starting preset is applied immediately at startup. The final stage blends back into the first.

- Edit preset assets to tune each look; duplicate an asset to create another preset.
- Edit **Sequence** to reorder presets and change each hold/transition duration. Empty entries are skipped. A single valid preset holds steadily.
- **Cycle Speed** accelerates both holds and transitions (try 10 for a quick review). **Paused** freezes weather progression while wind continues; game pause (`Time.timeScale = 0`) freezes both.
- **Manta → Environment → Preview clouds** selects any look instantly in Edit mode. In Play mode, it blends to that look over 8 seconds and disables the automatic cycle.
- Use the component context menu **Resume and restart weather cycle** to resume automatic cycling, or **Preview starting preset** to restore the startup appearance in Edit mode. Previewing a look does not change the configured starting preset.
- From code, call `MantaCloudWeather.TransitionTo(preset, seconds)` for a manual transition. It blends from the current appearance even halfway through another transition. Set `automaticCycle` back to true to continue, or call `ResumeCycle()` to restart.
- Disable **Manta Cloud Weather** to tune the cloudscape's appearance fields directly without the cycle overriding them.

**Manta → Environment → Set up cloud weather presets** creates missing presets and attaches the controller. Re-running it preserves existing preset tuning and sequence settings.

This uses the imported **ProceduralClouds** package's `Flight Demo_ShapeNoise` and `Flight Demo_DetailNoise` 3D textures, adapting its density/erosion, light-marching, and extinction approach to a URP mesh-volume shader. Its legacy `CloudMaster.OnRenderImage` is not compatible with URP and is not attached. The package source and demo assets are unchanged.

Each cube is only a ray-marching boundary: the shader renders a rounded, eroded density field, clips integration against camera depth, and blends premultiplied light. The flight camera requests depth and has a 5000 m far plane. Clouds alone do not require renderer changes; the day/night lighting and god-ray additions are documented in [Lighting.md](Lighting.md).

Use **Manta → Environment → Add cinematic procedural clouds** to add the setup if it is absent. If it already exists, the menu selects it without overwriting tuning. **Use procedural cloud distribution** migrates the older seven-bank setup and preserves existing weather presets. Keep sensible spacing to avoid transparent-volume sorting artifacts; interpenetrating banks do not share a unified lighting simulation.

Verified in Unity 6000.1.1f1: script and shader compilation, live Play mode, terrain occlusion, generated clouds with no colliders, evolving clouds, and five preset renders. **Validate procedural cloud distribution** checks deterministic seeds, different-seed variation, gameplay random-state preservation, count, bounds, spacing, altitude/flight clearance, duplicate prevention, transient geometry, empty/impossible configurations, and cleanup. **Validate cloud weather** checks midpoint interpolation, interrupted/manual blends, pause, a complete loop, variable frame sizes, empty/null sequences, and preset availability. Both are under **Manta → Environment**. No standalone build or broad hardware performance benchmark was run. Lower Ray Steps or Cloud Count for slower GPUs.

## Altitude layers

The scene now uses four seeded layers: ground mist (35–85 m centers), low banks (200–380 m), mid-sky billows (720–1100 m), and high veils (1550–1950 m). Edit **Altitude Layers** on the cloud distribution component for each layer's count, height, size, density multiplier and wind speed. There are 36 volumes in total. Heights are relative to the cloudscape transform; the shared region controls horizontal coverage. Emptying the layer list restores the original single-layer recipe. Layers share weather and day/night lighting, but use independent placement seeds and drift speeds. Spacing is evaluated within each layer; avoid overlapping altitude bands for best transparent-volume rendering. **Add altitude cloud layers** initializes only an empty layer list and preserves existing layer edits.

## Camera LOD and visibility optimization
MantaCloudscape exposes Distance Lod, Frustum Culling, Medium Lod Distance (450 m), Far Lod Distance (1600 m), Lod Blend Distance (500 m), Medium Ray Steps (32), Far Ray Steps (24), and Culling Bias (100 m), with Inspector tooltips. Near sampling uses the existing Ray Steps (64). Distance is measured to the volume AABB surface, so entering a large cloud retains near quality. Transitions use SmoothStep; the far transition is constrained to start after the medium transition. Sample-count rounding is spatially distributed to avoid a simultaneous whole-volume step. Lower sample counts increase march stride while Beer–Lambert integration preserves optical-depth scaling.

Medium quality blends three light-density probes down to two; far quality reduces to one and replaces fine 3D erosion noise with its mean. Five-lobe silhouettes and the shape-noise texture remain at every LOD. This is simplified volumetric rendering, not a billboard impostor. Far jitter is reduced to avoid stippling. No duplicate geometry or crossfade draw is needed.

Before each camera renders, GeometryUtility.TestPlanesAABB checks expanded volume bounds. Invisible volumes skip shape animation/property uploads and are force-hidden for that camera. Camera callbacks restore visibility afterward, so Game and Scene views independently choose visibility and LOD. Layer references are cached; arrays and property blocks are reused. Cheap world drift, forecast and rain logic continue offscreen so clouds re-enter at their correct positions and still affect weather.

God-ray lighting already integrates screen pixels, without compute dispatch. Its sun-aligned shadow-cache shader now skips cloud intersection loops for cells outside the camera frustum/ray distance, retaining a full interpolation-cell halo. CPU shadow-proxy collection tests each bank's downstream shadow corridor against the atmospheric receiver frustum, including the cache halo. This intentionally preserves offscreen clouds that can shadow visible air. Cache generation is omitted in the direct-shadow diagnostic mode. The atlas allocation and raster extent remain fixed; this is work rejection, not a sparse compute allocation.

Validation: shader compilation passed, four camera directions culled 14–27 of 36 volumes, and Play mode showed 25 culled / 11 retained in its observed view. Near/medium/far selection and saved camera restoration were checked. Visual comparison led to raising far sampling from 16 to 24 for quality. At 1280x720, full-quality reference median was 25.46 ms versus final adaptive render 21.27 ms, with highly variable render/readback samples; these are illustrative Editor measurements, not guaranteed player FPS or an isolated GPU benchmark. Existing URP Volume Inspector SerializedObjectNotCreatableException messages recur around reload/play transitions; no cloud shader compilation failure was reported. Standalone target-hardware and XR validation remain outstanding.
