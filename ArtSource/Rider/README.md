# Rider wingsuit

`RiderWingsuit.blend` contains the editable fabric, a reference Rider and a studio preview.
`Tools/Rider/create_wingsuit.py` regenerates it with Blender 2.93+ and exports
`Assets/MantaFlight/Rider/Models/RiderWingsuit.fbx`.

The three triangulated panels carry barycentric UV coordinates. Unity uses these
to pin the arm panels to shoulder, wrist and hip, and the leg panel to hips and feet.
Keep the UV coordinates intact when editing the mesh. The runtime inflation and
small travelling ripples vanish at the sewn edges. Petrol fabric, amber seams and
dark edging use two-sided URP materials.

The fabric opens over the configured deployment duration on becoming airborne and
retracts on landing or remount. This is visual: the existing glide control still
activates aerodynamic lift. Glide adds speed-dependent body pitch/roll, chest
corrections when banking, and fabric pressure/ripples. Pausing freezes these effects.

`RiderVisuals` exposes Fall Animation Delay (0.45 s) and Fall Animation Down Speed
(5 m/s). Both must be reached before the Jump animation switches to Fall; Fall is
latched until a new ascent or another animation state. The controller's existing
Falling physics state still covers the ballistic jump, including ascent.

Unity menu: **Manta / Rider / Install Blender wingsuit** updates the prefab and
Riders in the open scene. **Validate wingsuit and fall timing** checks the assets
and transition policy. `RiderWingsuitPlayValidation.RunBatch` is a disposable batch
Play-mode check that exits Unity after validating opening, sustained glide and
remount retraction. Run it only in a separate batch Editor, without `-quit`.
Reports are in `Logs/MantaFlight/validation-wingsuit*.txt`.
