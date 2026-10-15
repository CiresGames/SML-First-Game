# GPU weather research and proposed implementation

Research date: 2026-10-08. Target: 1920 x 1080 at 60 FPS. This document records the original design research. The implemented subset, validation and limitations are recorded in `GpuCloudImplementation.md`; the proposal below is not a claim that every production technique has been implemented.

## Recommendation

Build a shared GPU volumetric cloud field in the existing URP pipeline. Keep the seeded forecast state machine and gameplay queries on the CPU. Replace one rendered cube/GameObject per cloud with a bounded set of altitude layers, a regional weather map, and procedural 3D density. Nearby special formations can eventually use sparse local volumes integrated into that same ray march.

This changes the scaling model: visible screen samples, ray length, density complexity and lighting dominate cost, rather than separate overlapping cloud draw calls. It does not make arbitrarily dense clouds free. Simply translating the current pixel shader to a compute shader would not eliminate the repeated work.

## Verified project and hardware facts

- Unity 6000.1.1f1 and URP 17.1.0.
- Unity reports NVIDIA GeForce RTX 3080 Laptop GPU, Direct3D12, 8018 MB graphics memory, compute shaders, asynchronous compute and asynchronous GPU readback support. This is the development machine, not an agreed minimum hardware specification.
- `MantaCloudVolume.shader` already evaluates procedural density, shape evolution, ray marching and light probes on the GPU, separately for each mesh volume. It uses transparent compositing and scene-depth clipping.
- `MantaCloudscape` performs camera frustum tests, LOD selection and per-volume property uploads on the CPU.
- `MantaGodRayFeature` already uses URP Render Graph, a shared shadow cache and reduced-resolution scattering. Its cloud proxies are assembled from scene components, with a 192-proxy cap.
- `MantaLocalRain` queries generated bank footprints and checks individual drops against those footprints. Replacing cloud objects must replace these queries too.
- The installed ProceduralClouds package includes `WeatherMapGen.compute`, a weather-map generation example, and reusable noise resources. Its controller references the package's `CloudMaster` and allocates temporary buffers, so it is a starting reference, not a drop-in runtime system.
- The current checkout does not contain the `MantaWeatherMap.cs` from the previous chat pass. The implementation must use the current project state and not assume that earlier integration remains installed.

## Research findings

Guerrilla's original cloud work models evolving cloudscapes through a volumetric shader. Its later Nubis Evolved work specifically addresses fly-through rendering, fast cloud motion, temporal artifacts and internal lightning. These are directly relevant to Manta flight. The publications describe specialized production systems; their performance figures cannot be transferred to this project. [Guerrilla 2015](https://www.guerrilla-games.com/read/the-real-time-volumetric-cloudscapes-of-horizon-zero-dawn), [Nubis Evolved](https://www.guerrilla-games.com/read/nubis-evolved).

Nubis Cubed uses voxel clouds with acceleration techniques including compressed signed distance fields and accelerated light sampling for immersive flight. That supports a future sparse-volume direction for complex nearby formations, but it would be a substantially larger engineering commitment than a layered procedural field. [Nubis Cubed](https://www.guerrilla-games.com/read/nubis-cubed).

Unity's HDRP cloud controls illustrate a separation between regional coverage/rain/type maps and altitude-dependent density/erosion profiles. Its temporal accumulation documentation also explicitly describes ghosting tradeoffs. These are useful design references, not APIs available in this URP project. [HDRP cloud reference](https://docs.unity.cn/Packages/com.unity.render-pipelines.high-definition@17.6/manual/volumetric-clouds-volume-override-reference.html).

Unity 6.1 supports compute passes within URP Render Graph, so a custom cloud renderer can stay in the current pipeline. [Unity 6.1 compute passes](https://docs.unity3d.com/6000.1/Documentation/Manual/urp/render-graph-compute-shader-run.html).

## Options considered

| Option | Strength | Main limitation | Recommendation |
|---|---|---|---|
| GPU instances of current cloud cubes | Reduces CPU object and submission overhead | Still repeats expensive ray marches where clouds overlap; transparency ordering remains | Useful only if profiling finds CPU submission is dominant |
| Shared layered density field | Large coverage without one object per cloud; coherent regional weather | Needs careful in-cloud rendering, lighting and reconstruction | First implementation |
| Sparse voxel field/clipmaps | Supports more elaborate 3D fly-through formations | Memory, streaming and acceleration complexity | Later, if the layered field limits the art direction |
| Migrate to HDRP clouds | Existing cloud implementation | Broad pipeline/material/effect migration and different integration | Stay with URP for this task |

## Proposed data flow

1. **CPU forecast controller:** keep session seed, plausible state transitions, wind targets, day/night and event timing. Upload a small parameter block. This work is cheap and easier to debug and save on the CPU.
2. **GPU weather field:** use persistent textures, initially 256 x 256 per required layer, encoding coverage, type/convection, precipitation potential and thickness. Generate from the seed; blend state targets and advect slowly. Start with parameterized wind offsets and infrequent generation, not a full atmospheric fluid simulation. If advection simulation is needed, use ping-pong textures at a lower simulation frequency and interpolate rendering.
3. **Shared cloud density:** sample weather fields, altitude profiles and the package's baked 3D noise. Support the camera below, inside and above the layers. Blend density continuously; never respawn clouds at forecast boundaries. Preserve a thin-cloud treatment separately from tall cumulus.
4. **Compute ray march:** render initially at 960 x 540, one quarter of the pixels of 1080p. Intersect layer bounds and opaque scene depth, skip conservatively empty regions, reduce distant detail and stop once transmittance is negligible. Avoid testing every cloud against every pixel. If profiling justifies it, classify tiles and dispatch only occupied work; occupancy bounds must remain conservative through motion and transitions.
5. **Reconstruction:** retain cloud radiance, transmittance and representative cloud depth. Reproject with camera movement and wind; use depth rejection and neighborhood clamping. Reject history on camera cuts, disocclusion, major weather changes and lightning. A quality mode with little/no history is useful for close flight and comparison. Half-width/height reduces primary pixel work fourfold before reconstruction overhead; it is not a fourfold FPS promise.
6. **Shared light cache:** compute coarse cloud sunlight transmittance over a camera-centered area and update on a controlled budget. Feed consistent density into cloud shading, terrain shadows and god rays. Offscreen clouds that shadow visible scenery must still contribute. A 2D ground-shadow map alone is insufficient to light arbitrary altitudes; retain a layered or 3D cache for air/cloud receivers. Bound light samples and approximate multiple scattering.
7. **Rain and lightning:** replace object-footprint queries with the common regional weather model. Keep a lightweight deterministic CPU mirror for precipitation and shelter checks; avoid synchronous GPU readback. Small asynchronous queries are an alternative only if their latency is handled. Rain stays local to the player and below/in wet clouds; lightning positions follow wet storm regions and illuminate the same GPU density. GPU rain particles are a separate optimization if measured particle CPU cost warrants it.

## Initial budget and safeguards

60 FPS allows about 16.67 ms per frame. Propose an initial **3–4 ms GPU budget for clouds plus cloud-related lighting** on the development machine, leaving the rest for the game. This is an engineering target pending profiling, not a measured result or guarantee.

Start with half-width/height rendering, 48–64 primary steps as a tunable ceiling, cheap empty-space tests, existing noise textures and a bounded lighting cache. Tune from GPU captures rather than increasing cloud count or sample count blindly. The usable number of formations becomes an artistic field parameter; the ray-march budget remains bounded.

Do not assume asynchronous compute is faster: it can compete with other graphics work for the same GPU resources. Start with correct dependencies on the normal queue; evaluate overlap later. Persistent history must be per camera, resized/reset correctly, and disposed. Account for transparent rain and other effects when choosing composition order. Preserve the original renderer as a selectable fallback for A/B testing, with only one cloud renderer active at a time.

## Implementation and acceptance sequence

1. Profile the existing system in a standalone development build at 1080p: CPU, GPU, cloud draw cost, lighting cost and rain cost. Record the camera path, weather seed, resolution and quality settings. Editor synchronous screenshot/readback timings are unsuitable for this baseline.
2. Add a separate URP feature and shared field behind a mode switch. First validate sunny/scattered/overcast layers, inside-cloud flight, sky/terrain depth and day/night without temporal reuse.
3. Add temporal reconstruction and adaptive sampling. Test fast turns, maximum flight speed, teleports, entering/exiting clouds and thin silhouettes for ghosting, trails and edge halos.
4. Integrate the light cache, local rain queries, storm flashes and existing forecast blend. Verify no rain above/outside wet regions or beneath shelter, and no disappearance of shadows from offscreen casters.
5. Compare fixed-seed recordings for sparse sky, dense storm, stacked layers and multiple overlapping formations. Report median and 95th-percentile GPU timings, CPU submission cost, memory and whole-frame rate. Test increasing weather complexity at unchanged render resolution and sampling budgets. Only make the new renderer the default after the quality/performance comparison passes.

Implementation followed this research. See `GpuCloudImplementation.md` for the actual delivered scope and measurements, rather than interpreting this acceptance sequence as completed testing.
