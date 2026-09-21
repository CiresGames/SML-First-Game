# Flying great white sharks

Six prefab instances are saved under **Flying Great Whites** in `Assets/MantaFlight/Scenes/MantaFlight.unity`.

- Editable Blender source: `ArtSource/Sharks/GreatWhite.blend`.
- Regenerate with Blender: `blender -b --python Tools/Sharks/create_shark.py`.
- Model: `Models/GreatWhite.fbx`, eight-bone skin with `Shark_Swim` and `Shark_Bite` actions.
- Prefab: `Prefabs/GreatWhite.prefab`. Local forward is +Z; dimensions are approximately six metres long.
- `FlyingShark` patrols in three dimensions, detects the rider within 32 m, chases at 28 m/s, bites within 3.8 m after a .32 s windup, and recovers before returning to patrol. Bites deal 25 damage with a 2.5 s cooldown. Scenery blocks detection and bites; probes steer away from obstacles. Tune each instance in the Inspector; select it to see range gizmos.
- `SharkPlayerHealth` is attached to the rider and accepts bites while mounted or dismounted. Health starts at 100; hits grant .8 s protection. Defeat resets the existing manta/rider and grants five seconds of protection. `onBitten` and `onDefeated` events are available for additional feedback.
- Unity menu **Manta > Sharks > Populate flight course** rebuilds this group and prefab. **Validate sharks** verifies rig and animation data. This does not rebuild terrain or other wildlife.

Validation output: `Logs/Sharks/validation.txt`. Batch play-mode checks can be run with `-executeMethod MantaFlight.Sharks.Editor.SharkPlayValidation.Begin` (omit `-quit`; the validator exits on completion).
