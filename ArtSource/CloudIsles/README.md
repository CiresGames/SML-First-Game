# Cloud Isles

Open `Assets/MantaFlight/Scenes/MantaFlight.unity` in Unity 6000.1.1f1.
The environment is under **Cloud Isles • floating archipelago** in the Hierarchy.

The scene contains 18 main islands, nine detached rock shards, 866 trees,
16 animated waterfalls with feeding streams and spray, and three editable
ProBuilder lookout ruins. High islands rise above the cloud sea; low gardens
sit within it. The departure island has an open flight corridor and meadow
colliders for landing. The existing flight, weather, and day/night systems
remain in use.

## Source assets

- `CloudIsles_IslandKit.blend`: six Blender island variants and a crown outcrop.
- `Assets/MantaFlight/Environment/CloudIsles/Models`: exported FBX models and
  the shared grove tree, derived from the project's Rei tree.
- `Assets/MantaFlight/Environment/CloudIsles/Materials`: stone, meadow, water,
  foliage and ruin materials.
- `Assets/MantaFlight/Environment/CloudIsles/Weather`: local cloud preset variants.

Foliage remains alpha-clipped intersecting leaf cards. Each island's
`MantaIslandGrove` component draws shared trunk and leaf meshes using GPU
instancing, with per-island camera culling. This avoids duplicated forest mesh
assets. The scenery targets the project's desktop URP renderer; mobile and
standalone player performance have not been profiled.

## Editing and regeneration

Move or scale island groups directly in Unity. Edit the lookout geometry with
ProBuilder. The hierarchy contains three named view markers for inspecting the
departure, the archipelago, and the cloud level.

The menu **Manta > Environment > Build Cloud Isles** regenerates the environment
from `CloudIslesEnvironmentBuilder.cs`. It replaces the generated root and its
layout edits, updates its materials and weather variants, and resets departure
placement. It does not save the scene automatically.

The previous prototype landscape remains disabled in the scene.
`MantaFlight_before_environment.unity` is a snapshot from before this rework;
copy it into the project's Assets folder under a different name to inspect it.

The two scripts in `Tools/CloudIsles` regenerate the Blender exports. The
`.blend` file can be opened and edited independently of those scripts.

## Validation

The saved scene was checked in the Editor and in Play mode. The flight test
covered approximately 567 metres over 20 seconds with no new runtime errors.
The departure has 300 metres of forward clearance and a meadow landing surface
about 30 metres below the spawn. All three new environment shaders compiled.
The player's original progression save was restored after testing.

Testing also exposed an uninitialized resting rotation when recovering from
exhaustion. `MantaMountAdapter.RestAfterExhaustion` now retains the manta's
current heading; the resting transition was checked for a valid unit rotation.
The machine-readable result is in `validation.json`. Final previews are
`Archipelago_overview.png`, `Cloud_level.png`, and `Flight_playtest.png`.
