# Shared GPU cloud field

The Manta flight scene now renders clouds through `MantaGpuCloudFeature` in the existing URP Render Graph. `MantaGpuWeather` is attached to **Cloudscape • procedural weather**. The previous mesh-volume renderer remains available through **Use GPU Clouds**. This is a first continuous volumetric implementation inspired by the reference images, not a reproduction of Flight Simulator's renderer.

## What runs where

- The existing CPU forecast controller still owns session seeds, plausible forecast transitions, wind targets, day/night and event timing. These are inexpensive control operations, not a fluid simulation.
- A compute shader generates a persistent 256 × 256 periodic weather map from the session seed. Coverage and regional convection control cloud distribution and top height.
- The GPU evaluates two altitude layers with the imported ProceduralClouds package's 3D shape and erosion textures. Wind continuously advects the field; shape evolution changes its volume over time. Forecast values blend without regenerating cloud objects.
- A single screen-space compute ray march replaces overlapping per-cloud volume draws. It clips against layer bounds and opaque depth, rejects empty regions, spends progressively coarser samples with distance and stops at negligible transmittance.
- A 64 × 32 × 64 sunlight cache amortizes eight sunlight density samples across visible cloud pixels. Expanded camera planes reject offscreen lighting receivers. Shadow rays still sample offscreen casters, so rotating away from a cloud does not remove its shadow on visible receivers.
- Cloud lighting includes Beer–Lambert extinction, a forward-scattering approximation, height-dependent ambient light and a cheap multiple-scattering approximation. The key light follows the sun or moon; storm flashes illuminate nearby density.
- At the default divisor of 2, primary clouds render at 960 × 540 for a 1920 × 1080 camera. Per-camera history reprojects camera motion and wind, rejects cuts/flashes/disocclusions and clamps history to the current neighbourhood. Depth-aware upsampling protects opaque silhouettes.
- God rays and scenery shadows sample the same density definition through their existing sun-aligned cache. They do not run a nested density march at each atmospheric sample.
- Rain keeps its local particle emitter, individual streak tilt and shelter checks. A deterministic CPU weather-map mirror replaces bank-footprint queries without GPU readback. Lightning strikes select wet regional areas; pooled ribbons and one light remain inexpensive.

## Inspector controls

| Control | Default | Meaning |
|---|---:|---|
| Use GPU Clouds | On | Toggle continuous GPU clouds / existing mesh volumes |
| World Size | 24,000 m | Period of the seeded regional field |
| Main Layer | 0 / 2,200 m | Base altitude / maximum height; forecast thickness and convection reduce the top |
| High Layer | 3,200 / 260 m | Base altitude / thickness of the thin upper deck |
| View Distance | 18,000 m | Maximum primary cloud-ray reach |
| Billow Size | 2,200 m | Shape scale, modulated by the existing forecast noise scale |
| Extinction | 0.012 / m | Optical depth scale before density/profile multipliers |
| Samples | 80 | Maximum samples per primary ray |
| Resolution Divisor | 2 | Half width/height; 3 or 4 reduce cost further |
| History Weight | 0.85 | Temporal reuse; lower values reduce trails but reveal more noise |
| Detail Fade Start / End | 3,000 / 6,000 m | Smoothly remove fine erosion at distance |

The existing **Manta Rain Forecast** component remains the place to force a forecast, replay a fixed seed, or configure durations and transition weights. GPU cloud LOD is sample-based; the old per-mesh ray-step controls apply only to the fallback renderer. The existing culling bias also expands the new light-cache receiver bounds.

## Validation and reproducibility

`MantaGpuCloudValidation.ValidateField()` compares all 65,536 GPU weather texels with the CPU generator. It also checks wet/dry variation, rejection above the main layer and renderer-feature toggle fallback. Observed maximum map error: **0.000488162**, within half-float quantization. The sampled grid contained **522 wet and 502 dry columns**.

`MantaGpuCloudValidation.BuildBenchmark()` builds a Windows development player into `Builds/MantaGpuBenchmark`. Start it with `--manta-cloud-benchmark -force-d3d12 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`. Only that explicit switch creates the benchmark controller; normal sessions are unaffected. It runs 90 warm-up frames and 240 measured frames per scenario, writes JSON and screenshots into the adjacent `BenchmarkResults` folder, and exits. FrameTimingManager data covers the whole GPU frame, not isolated cloud cost. Other open applications and development-build overhead can influence the result.

For a hidden window, also pass `--manta-cloud-offscreen`. This explicitly renders the camera to a 1920 × 1080 HDR target every frame and waits for GPU completion with a one-pixel readback. Such timings include simulation, submission, GPU completion and readback overhead; they are **not presented gameplay FPS** or isolated GPU cloud timings. The initial hidden-window test produced black captures and no GPU samples and was discarded. Inspect the screenshots as well as the JSON before accepting a run.

The legacy comparison has different spatial coverage, so it is not an equal-image speedup measurement. Editor screenshot timings include synchronous GPU readback and must not be quoted as gameplay FPS.

### Recorded run — 2026-10-09

Windows development build, Direct3D12, RTX 3080 Laptop GPU, 1920 × 1080, divisor 2, 80 maximum ray samples, seed 90210. **Offscreen render + simulation + synchronous readback**, 240 frames after 90 warm-up frames per case. Raw results: `GpuCloudBenchmark.json`. FrameTimingManager returned no GPU timestamps in this offscreen mode, so no isolated GPU-time figure is claimed.

| Case | Median (ms) | 95th percentile (ms) |
|---|---:|---:|
| GPU fair | 4.98 | 5.48 |
| GPU overcast | 5.36 | 5.78 |
| GPU inside storm | 9.67 | 10.43 |
| GPU fast flight | 5.25 | 5.73 |
| GPU sunset | 5.22 | 5.65 |
| GPU night | 4.72 | 5.06 |

These samples fit within a 16.67 ms frame budget, but **do not establish sustained 60 FPS in normal presented gameplay**. The cameras emphasize the sky and do not exercise every ground-level gameplay workload. The legacy fair case measured 23.82 ms median in the same harness; its different cloud distribution means this must not be marketed as an equal-quality speedup.

The final incremental build reported success with zero errors, and the final player's log had no shader errors or exceptions. Earlier builds exposed the separate leaf motion-vector shader issue listed below; an incremental zero-error result does not prove that unrelated shader has been repaired. Captures were inspected for day, storm, sunset, night and the normal Manta camera. The scene and renderer-feature wiring are saved.

Optional restoration of Unity-generated build metadata was blocked by automatic approval review because clearing runtime settings could affect unrelated configuration. Those generated settings were retained; no user scene content was removed for cleanup.

## Limits

- Windows/D3D12 on the development RTX 3080 Laptop GPU is the validation target. Mobile, VR, orthographic cameras and multi-renderer camera stacks are not acceptance-tested. Orthographic/stereo views retain mesh clouds. Unsupported compute kernels retain the old renderer.
- Density is a layered procedural field, not a full atmospheric/voxel simulation. Specialized lenticular profiles and sparse local hero formations are not reproduced independently by this first GPU path.
- Rain follows conservative wet regional columns, not an exact CPU copy of every 3D erosion hole. Its tests establish map consistency and the altitude boundary, not perfect drop-to-cloud-surface correspondence.
- Lighting and multiple scattering are bounded approximations. The cloud cache is coarse; dense silhouettes and fast movement can retain noise or temporal artifacts. No asynchronous-compute overlap or indirect occupied-tile dispatch is claimed.
- Transparent effects render after clouds. Arbitrary distant transparent geometry needs additional integration for correct cloud occlusion.
- The two-layer maximum view range can remain visible at the far horizon. Reference-level detail, streaming and atmospheric horizon continuity remain future rendering work.
- A pre-existing `MantaFlight/ReiLeaves` motion-vector shader error appears in standalone builds. It is separate from the cloud shaders and is recorded rather than hidden.

## Render Graph validation

### 1. Validation Summary

Reviewed against Unity 6000.1.1f1 / URP 17.1. Resources are explicitly wired through compute and composite passes. Runtime images and a standalone build validate the intended desktop path; this is not a platform-wide correctness guarantee.

### 2. Confirmed Issues

Fixed: initial noise-hash naming collision; build-only nested god-ray density-loop failure; invalid-history reads on first frame; stale feature availability after disabling; missing continuous detail fade. GPU god rays now always use the density cache.

Also fixed: silhouette opacity rejection prevented stochastic edges from accumulating; distant cloud lighting incorrectly inherited the opaque camera's 5 km far plane even though sky clouds render to 18 km. Lighting receiver culling now uses the cloud reach.

### 3. Likely Issues / Risky Assumptions

The singleton active field/feature assumes the current single-world, primary-renderer setup. Multiple independently configured renderer assets and XR require further work. Transparent ordering and coarse light-cache precision remain limitations.

### 4. Recommended Fixes

The delivered desktop path uses an explicit feature switch, kernel support checks, independent camera histories, resize invalidation and release on disposal. Before extending to XR/multiple renderers, replace global availability with camera/renderer-specific ownership and add stereo history resources.

### 5. Corrected Snippets

The composite explicitly binds `_BlitTexture`, `_Clouds`, `_CloudDepth` and `_SceneDepth`. Compute passes declare every sampled texture and write target, snapshot parameters into pass data, and use static render callbacks. Textures derive from the active graph descriptor; the 3D light cache intentionally changes dimensions/format. There are no unnecessary global texture publications. The custom composite pass is warranted by its multiple depth and cloud inputs.

### 6. Missing Information

Minimum supported GPU beyond the development machine, target VR/mobile requirements and representative long-flight captures are not specified. Cloud-only GPU timestamp profiling and long-duration memory soak tests remain outstanding.
