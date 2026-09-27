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
