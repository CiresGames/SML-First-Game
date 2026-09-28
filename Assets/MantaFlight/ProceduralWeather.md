# Procedural session weather

The existing MantaRainForecast now owns session initialization and weighted state selection. MantaCloudWeather still blends appearances. MantaCloudDistribution still generates the same bounded volume population, and MantaCloudscape retains camera LOD/culling. No duplicate cloud renderer, per-state cloud objects, or additional rendering passes were introduced.

## New sessions and reproducible seeds

At Play/session startup, Randomize Session Seed uses a new GUID-derived integer without touching UnityEngine.Random. Session Seed displays the actual value at runtime. Disable randomization and set Seed to replay a session's initial layout, forecast choices and wind targets. A world/new-game system can call `forecast.BeginSession(worldSeed)` explicitly; a call before Start is respected. Only call this at a new-session/loading boundary: it intentionally rebuilds the cloud layout. It is not a save/resume API for mid-session elapsed state.

The distribution derives its own integer seed from the session seed. Existing Perlin patch sampling varies bank locations; seeded sizes/lobes vary shapes. Patch Density Variation adds correlated density variation across these patches. Its value is independent of the state preset's overall density. Changing weather never regenerates or teleports the banks. Wind advection, edge fading/wrapping and shape evolution continue through transitions.

## States and plausible progression

The saved scene uses the five existing presets in order:

1. Scattered clouds (fair/partly cloudy)
2. Heavy clouds (overcast)
3. Light rain
4. Moderate rain
5. Heavy rain

These are the effects already implemented; lightning and a separate fog forecast are not introduced. The light-rain start is no longer forced. Initial weights default to 3, 3, 2, 1 and 0.4, favoring fair/overcast weather while allowing a rainy start.

State Rules exposes a name, hold and incoming blend min/max durations, initial weight, transition weights indexed by the Forecasts list, wind-speed multiplier range, and maximum wind turn. Probabilities are relative weights, not percentages. Only self/adjacent targets are eligible even if other entries are nonzero. Missing/null targets are skipped. All zero eligible weights retain the state. Persistence Bias favors continued development or clearing of a front; it does not bypass adjacency. Bounds reverse naturally because only valid neighbors are eligible.

Default holds are 120–240 seconds and blends 50–90 seconds. Wind, density, coverage, colours and rain all use the existing SmoothStep appearance blend. Wind heading evolves by bounded front-to-front turns. Gust Strength (0.18) and Gust Period (45 s) add smooth seeded Perlin variation during holds, shared by cloud advection/noise and new raindrops. Gusts remain separate from the base appearance, avoiding feedback into transitions. Weather Pause and Cycle Speed are retained; game time pauses all runtime movement.

## Debugging

- Disable Randomize Session Seed and enter a Seed for repeatable tests.
- Disable Randomize Starting Forecast to use Starting Forecast.
- During Play, use Restart with fixed seed to reconstruct the start of that session.
- Set Debug Forecast and use Force debug forecast (smooth transition) to enter an arbitrary state over 15 seconds. This intentionally bypasses adjacency and disables Automatic until re-enabled.
- Existing rain-preview menu actions remain available.
- State density, cloud shape, rain and base wind are authored in each referenced MantaCloudPreset.

## Validation and scope

240 simulated forecast steps checked adjacency, identical fixed-seed sequences/wind targets, variation across seeds and pause behavior. Layout checks confirmed repeatability of positions/sizes/densities and different distributions across seeds, with 36 volumes in both tested layouts. Two actual Play sessions generated different seeds and started in different states (overcast and moderate rain); camera LOD/culling remained active. No new volume count or render pass was added. This is a plausible stochastic weather model, not a meteorological fluid simulation or a biome model. Exact cross-platform replay of floating-point cloud motion and mid-session persistence are not guaranteed.
