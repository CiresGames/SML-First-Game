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
