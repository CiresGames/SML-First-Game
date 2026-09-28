# Local rain and forecasts

The cloudscape now has **Manta Rain Forecast** and **Manta Local Rain** components. Light, Moderate and Heavy rain assets live alongside the existing cloud weather presets. Each preset blends rain amount, cloud coverage, density, grey lighting and wind through the existing cloud-weather transition system.

Only altitude layers with **Rain Cloud** enabled produce precipitation. The mid-sky billows are enabled by default, leaving ground mist and high veils dry. Their generated cloud volumes supply conservative elliptical rain footprints. Rain tapers near footprint edges and stops above the cloud tops. The footprint is an approximation of the procedural cloud, not a test of every animated noise detail.

A single world-space particle system follows the flight camera. Player position determines exposure; drops are spawned only inside wet footprints, and drifting drops outside those footprints are removed. Leaving all rain clouds or entering solid overhead shelter clears the emitter. Rain collisions remove drops at scenery. Particles are capped at 2,200 by default; no scene-wide rain emitters or rain colliders are created. Particle arrays are reused and the cloud list is refreshed once per second.

## Forecast behavior

The ordered forecast states are fair (Scattered clouds), overcast (Heavy clouds), Light rain, Moderate rain and Heavy rain. A seeded persistent trend develops or clears a front, with occasional reversals. Only adjacent states are chosen: fair weather cannot instantly become a heavy storm. Each forecast holds for 120–240 seconds and blends over 50–90 seconds. These game-time durations are editable and intentionally compressed. Time scale and the cloud-weather speed/pause controls affect progression.

This is a plausible stochastic weather progression, not a numerical meteorological forecast or live weather service. It does not model humidity, temperature, terrain-driven rainfall or advected weather fronts. Rain shadows use cloud-volume footprints. Lightning, thunder audio, puddles and distant rain curtains are not part of this first rain implementation.

The new forecast component owns automatic transitions; the older ordered cloud cycle is disabled. **Manta → Environment → Preview rain** blends to a chosen preset in Play mode and switches off automatic forecasting. Re-enable **Automatic** on the forecast component to continue. Change **Starting Forecast** to choose the next Play session's initial conditions.

## Verification

- Rain footprint checks cover beneath, inside, outside and above a cloud.
- Heavy-rain Play test produced about 1,970 live drops, within the cap.
- A temporary solid roof stopped rain and cleared all drops.
- Moving outside all clouds produced zero local rain and zero particles.
- Forty accelerated transitions visited all five forecast states without skipping adjacent states.
- Rain shader compiled and the effect was visually inspected.
- Temporary review probes existed only in Play mode and were discarded on exit.
- No standalone build or broad hardware benchmark was run.

## Flight-relative rain streaks
MantaLocalRain now configures the stretched-particle renderer to include camera velocity. Movement Tilt (0–2, default 1) scales cameraVelocityScale relative to the renderer's velocityScale (both 0.045 at the default). This lets flight-camera translation influence the streak direction/length using Unity's native particle rendering. Actual particles remain in world simulation space with their existing downward/wind velocity; player motion is not subtracted from particle velocity a second time. Rain-cloud footprint, shelter checks, collisions and particle limits are unchanged. The renderer reference is cached; no additional particles, per-particle loop or render pass is added.

Compilation and the saved renderer configuration were verified in Unity. A moving-camera visual flight test remains unverified. Native setting reference: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/ParticleSystemRenderer-cameraVelocityScale.html

### Explicit motion correction (supersedes native camera stretch above)
The native camera stretch produced no visible tilt in the isolated manual-render check. Rain now measures and smooths the viewer's world velocity (14/s response), resets on camera replacement or a displacement over 100 m, and caps measured speed at 200 m/s. A reused property block sends this velocity to the rain vertex shader. The shader shears the existing stretched quad along its U/long axis, leaving world-space drop trajectories, collision and exposure unchanged. Native cameraVelocityScale is zero to avoid applying camera movement twice. Movement Tilt remains the artistic strength control. The alpha profile was corrected to use V across the narrow axis and U along the streak.

Controlled renders with zero and opposite 40 m/s lateral velocity inputs verified vertical and oppositely inclined streaks. This validates the render response; it is not a full interactive flight acceptance test. No extra particle loops, textures or render passes are added.

### Individual drop orientation
Replaced the quad shear with per-particle billboard reconstruction. Explicit vertex streams supply each drop's world centre and its own velocity. The shader projects (drop velocity minus smoothed viewer velocity) onto the viewing plane, then builds a thin perpendicular-width quad along that direction; length follows apparent speed. Head-on drops fall back to camera-down orientation. Actual simulated trajectories remain unchanged. Validated stream packing and a 45-drop render with differing velocities; the resulting individual inclinations vary correctly. Existing particle count and render pass are retained; additional vertex data and simple vertex math are required.
