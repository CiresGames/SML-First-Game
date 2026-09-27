# Manta flight atmosphere

Select **Sky • day night and atmosphere** to adjust the clock, lighting, fog, stars and god rays. The scene starts at golden hour. **Animate** advances a full day in **18 minutes** of game time; pausing game time also pauses the sky. **Manta → Environment → Time of day** previews dawn, noon, sunset and night.

The cycle coordinates warm sunrise/sunset, daylight, blue moonlight, soft sky fill, ambient lighting, stars, cloud shading and exposure. Cloud weather retains its authored colors and receives a separate environment tint.

## Atmospheric god rays

The old screen-space radial blur has been replaced with world-space single scattering. Each pixel traces a ray through an exponential-height atmosphere, stopping at opaque scene depth. Beer–Lambert extinction attenuates the background and sunlight; a normalized Henyey–Greenstein phase function scatters more light when looking toward the sun. The source follows the sun's actual color and intensity. Sun visibility on the screen is not required.

Scenery shadows come from the main directional-light shadow map. Cloud banks contribute approximate optical-depth shadows from ellipsoidal proxies that follow their generated position, size, density, coverage and thickness. The view is integrated at half resolution and upsampled using scene depth to reduce leaking across silhouettes. Atmospheric scattering is composited before transparent cloud volumes. Daytime legacy fog is disabled while this effect is on, avoiding duplicated haze; nighttime retains the softer fog treatment.

Controls:
- **Air Extinction**: extinction per metre at ground level; scene value 0.00055.
- **Air Height**: density scale height; scene value 420 m.
- **Ray Distance**: maximum integration distance; scene value 1800 m.
- **Ray Spread**: forward-scattering anisotropy, not a screen-space blur radius; scene value 0.72.
- **Ray Samples**: integration quality, 16–64; scene value 64.
- **Ray Intensity**: artistic radiance multiplier; scene value 1.8.

This is a real-time approximation, not a complete physical atmosphere. It uses single scattering, simplified homogeneous cloud shapes (up to 64 banks), and the existing 700 m scenery shadow range. Cloud shadow edges do not match every animated noise detail. Transparent surfaces do not supply opaque depth. Multiple scattering, spectral Rayleigh scattering, terrain beyond the shadow range, XR and orthographic cameras are not modeled/validated. The existing sky and cloud shading retain artistic choices.

The global grading profile adds ACES, subtle bloom, contrast and vignette. The shared desktop URP renderer contains the atmospheric feature; its pipeline shadow distance is 700 m. Other scenes using that pipeline inherit that distance, but atmospheric scattering requires an active Manta day/night controller. Unity 6 URP Render Graph is required.

Use **Manta → Environment → Validate lighting** for clock, sun/moon, cloud tint, toggle, camera, profile and shader checks. Editor captures and Play mode were checked with all 36 cloud banks. No new rendering errors occurred after compilation completed; a temporary old-pass-index error occurred while Unity switched from the old three-pass shader to the new two-pass shader. No standalone build or broad GPU benchmark was run.

## Render Graph validation

### 1. Validation Summary
Two raster passes with explicit depth/color/shadow dependencies and graph-owned intermediate textures. Reviewed against local URP 17.1 APIs and rendered in Unity 6000.1.1f1.

### 2. Confirmed Issues
The previous effect depended on screen-space brightness and sun position rather than world-space optical depth. Replaced. The previous daytime fog would duplicate extinction; it is now disabled during daytime volumetric rendering.

### 3. Likely Issues / Risky Assumptions
Shadow reach is bounded by the pipeline shadow distance. Cloud proxy shading is approximate. XR, dynamic-resolution scaling and orthographic cameras remain unverified.

### 4. Recommended Fixes
Implemented explicit shadow-map reads, pre-transparent compositing, normalized phase scattering, exponential extinction, depth-aware upsampling and per-record property blocks.

### 5. Corrected Snippets
Implementation is in Runtime/MantaGodRayFeature.cs and Shaders/MantaGodRays.shader. No shared material mutation, manual global texture exposure or camera-color feedback loop is used.

### 6. Missing Information
Target hardware frame-time budget and standalone-player validation.


## Shaft definition refinement

The atmosphere receives sun color multiplied by the daylight source intensity, before the surface-light horizon dimming, so atmospheric slant-path attenuation is not applied twice. Three overlapping lobe proxies per cloud replace the single oval proxy, improving irregular gaps. Denser low-altitude aerosol, stronger forward scattering and 64 quadratically spaced integration steps give nearby occluders more definition. The shared desktop shadow range is now 700 m. Clouds remain approximate occluders; proxy intersections can overestimate extinction in overlapping lobes. Half-resolution integration and early exit from optically thick cloud shadows bound the cost, but the higher sample/proxy counts are more expensive than before. Editor visual and runtime checks passed; no standalone benchmark was run.

## Performance optimization (current defaults)

Cloud optical-depth queries are now precomputed once per camera into a 128 × 128 × 8 sun-aligned lookup, stored as a 1024 × 128 R16 atlas. The 48-step atmospheric integration reuses this field instead of intersecting every cloud lobe at every step. Scenery shadow sampling remains direct. The atlas is rebuilt each rendered frame, so moving clouds and changing weather do not leave a stale temporal trail. Explicit Render Graph dependencies connect cache generation, scattering and depth-aware compositing. Atlas filtering is clamped within slices; depth interpolation can soften fine cloud-shadow changes, especially inside cloud formations.

The balanced configuration renders air at one-third resolution (Resolution Divisor 3) with 48 samples; depth-aware upsampling preserves scene silhouettes. Divisor 2 restores higher atmospheric resolution. Cache Cloud Shadows can be disabled for diagnostic comparison. Cloud volumes skip detail-noise reads where the density is provably empty and gradually reduce ray steps at distance; nearby volume quality remains unchanged. Runtime cloud appearance uploads occur once in the cloud update rather than again for every weather and sunlight change. Working shadow arrays and component lists are reused.

Controlled 1280 × 720 frozen-camera comparison, six samples after two warmups: previous-style direct cloud shadow evaluation / divisor 2 / 64 steps measured median 25.10 ms; cached evaluation / divisor 3 / 48 steps measured 9.03 ms. Both used the same current cloud shader and include a one-pixel synchronous readback. This is about 64% lower measured render-and-sync time, not a standalone FPS promise. Earlier full-image readback timings were dominated by transfer overhead and were not used for this comparison. Scene appearance was visually reviewed and Play mode produced no new rendering errors.

### Definition adjustment
The current scene now uses Resolution Divisor 2 (half resolution) with 48 ray samples and cached cloud shadows enabled. This restores more atmospheric edge detail than divisor 3 while retaining the primary repeated-shadow-work optimization. It costs more than the earlier one-third-resolution balanced preset. New Editor timing samples varied substantially, so the earlier 9.03 ms figure should not be read as a measurement of this current configuration.

## Earth-inspired night sky

The skybox now samples one baked 2048 × 1024 star atlas with mipmaps. It contains 11,000 synthetic stars, with apparent brightness derived from a distribution of distances and luminosities, warm/cool colors, and a denser galactic population. A broad Milky Way band includes a brighter core and mottled dust lanes. This is an Earth-inspired fictional star map, not a catalogue-accurate constellation map. Distance affects brightness; stars remain effectively at infinity, without visible flight-scale parallax.

Star Brightness, Milky Way Brightness, Star Twinkle, Celestial Latitude and Star Map Rotation are exposed on the day/night controller. The map rotates with the 24-hour game clock. Twinkle is subtle and stronger near the horizon. Daylight fades it out, moon glare reduces visibility nearby, the moon disc blocks stars, and cloud transparency naturally obscures the background. Runtime work is a single atlas sample plus simple tint/twinkle math in the existing skybox draw. There are no star GameObjects or runtime atlas generation. The procedural generator runs only from Manta → Environment → Build Earthlike night sky and preserves the atlas asset GUID.

Verified: shader compilation, midnight visual review, changing celestial rotation and daytime fade. The prior scene time and flight-camera orientation were restored after review. Exact astronomical accuracy and standalone frame-time benchmarking are outside this implementation.

## Occasional northern lights

Aurora Enabled on the day/night controller enables seeded nighttime events. Quiet intervals are 180–360 seconds of dark game time; events last 70–130 seconds, including approximately 20-second fades. Daylight suppresses visibility and pauses the event timer. The motion and timer use scaled game time, so pausing time freezes the effect. Preview Aurora forces an event for editing; it is disabled in the saved scene. Brightness, heading, interval, duration and seed are adjustable.

Three analytic skybox curtains combine slow folds, fine vertical filaments, green lower emissions and faint violet upper edges. The moon disc and foreground cloud rendering obscure them. This is an artistic approximation of auroral morphology, not a simulation of magnetic fields or solar activity. No particles, extra objects, new textures or extra render passes are added. Shader work exits during quiet periods, daylight and outside the auroral region.

Visual review and scheduler tests passed for active/quiet phases, fade endpoints, pause and daylight suppression. A 1280 × 720 Editor render-and-sync test measured 20.09 ms with quiet sky versus 21.85 ms during an event, before the final spatial early-outs. These are whole-view timings with readback, not guaranteed player GPU times or FPS.

### Aurora shape variation
Each curtain now has independent seeded smooth-noise width, height, lateral drift and vertical lift. Aurora Shape Variation controls the amount; Aurora Drift Speed controls the pace of those broad changes. Local folds and fine filaments continue within the changing curtain coordinates. Noise is evaluated only four times per curtain per sky update on the CPU, with a reused three-vector array; no new textures or draw calls are added. Shader bounds expand safely to accommodate the movement. Ten simulated minutes of scale/continuity checks passed and previews at two times were visually reviewed.

### Aurora spatial depth
Auroras now intersect three curved world-space emitting sheets at different distances, replacing the angular curtain projection. The sheets are anchored to the environment controller, so camera translation produces parallax and perspective rather than carrying the aurora along with the viewer. Fold orientation increases emission toward grazing views; distant sheets dim and shift toward a softer blue-green. Existing independent shape evolution and drift remain active. Aurora Distance Scale adjusts the whole formation's world scale: smaller values strengthen flight parallax. Altitude and distance are deliberately compressed for this scene, not true Earth auroral altitudes.

The existing skybox draw evaluates three bounded intersection iterations per sheet, with conservative bounds checks before solving. No textures, particles or render passes are added. This is a thin-sheet emission approximation, not full volumetric simulation. Shader compilation and visual reviews at two camera positions passed. Repeated 1280 × 720 Editor render/readback timings varied too widely (active medians 12.41–27.55 ms versus quiet 11.46–13.28 ms) to establish a reliable incremental cost; standalone GPU profiling remains unverified. Camera placement, scene time 17.7 and occasional-event mode were restored after review.

### Traveling undulation
Two smooth waves with different wavelengths and opposing travel directions now roll along each curtain's lower edge. Rays bend progressively with height, giving their upper ends a trailing motion. Each layer has a different phase and pace. Aurora Wave Amount and Aurora Wave Speed control this separately from broad drift; amount zero removes the added deformation. The wave clock follows scaled game time. Bounds include the maximum vertical displacement, preventing ripple crests from being clipped. Three additional sine evaluations per visible sheet reuse the existing skybox pass, without new textures or particles. Compilation and visual comparison eight seconds apart passed; normal scene settings were restored and saved.

### Variable band count
Each nighttime event now chooses a seeded random count from one to four bands, held until the event fades out. The group stays centered around Aurora Heading; unused bands skip shader work and CPU shape updates. Preview Aurora Bands selects a fixed count for editing. All counts occurred in a 100-event scheduler check (25/27/24/24), with no mid-event changes. One-band and four-band renders were reviewed; compilation passed and the normal scene settings were restored and saved.
