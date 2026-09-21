# Ambient hawks

The MantaFlight scene contains ten stylized brown hawks beneath **Ambient Hawks**: six near the lagoon start, four above the canyon. Press Play to see independent circling flight, banking, wingbeats and glides. They are decorative and have no colliders.

- Editable Blender source: `ArtSource/Hawks/CommonHawk.blend` (at the project root).
- Unity export: `Models/CommonHawk.fbx`, one skinned mesh, eight bones, seven materials.
- Animations: `Hawk_Flight` and `Hawk_Glide`, each 40 frames / 1.333 seconds at 30 fps, looping.
- Reusable prefab: `Prefabs/CommonHawk.prefab`.
- Select an individual bird and edit **Hawk Flight** to adjust its orbit center, radius, speed, height variation, direction, animation speed and starting phase. Selected birds show their flight path.

The source model has approximately a 2.2 m wingspan; scene instances are slightly enlarged for readability in this large playground. Appearance is stylized, with broad wings, separate primary feathers, brown upperparts, a pale streaked breast, amber eyes, a hooked beak and a russet tail.

## Regenerate

Run Blender in background mode with `Tools/Hawks/create_hawk.py` from the project root. This recreates the Blender source, studio preview and FBX. Then use **Manta → Hawks → Add flock to flight playground** in Unity. That command rebuilds the hawk prefab/controller and replaces only the contents of **Ambient Hawks**, so custom placement under that group is reset. It does not rebuild the playground.

`MantaFlight.Hawks.Editor.HawkSetup.Validate()` checks the saved population, skin bones, looping clips, actual wing deformation and flight movement. Reports and the in-engine inspection image are in `Logs/Hawks/` (not versioned).
