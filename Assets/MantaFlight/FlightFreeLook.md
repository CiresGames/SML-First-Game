# Flight free look

- Hold the right-stick click (R3) to instantly move the camera ahead of the manta
  or Rider and look backward. Release to immediately restore the previous view.
  This overrides free look while held and keeps collision protection. R3 no longer
  activates the manta dive action; keyboard V and left-stick pitch remain available.

- Mouse movement or the gamepad right stick looks around while riding the manta
  or gliding. Both support full horizontal revolutions and 65 degrees up/down.
- Both flight cameras smooth motion along a sphere around their focus, keeping the
  radius during turns and the subject centered. The configured speed/dive pullback
  still applies. Obstacle casts ignore the manta and its attached Rider; actual
  environment obstacles can still bring the camera closer to avoid clipping.
  The glide camera likewise ignores the Rider's own colliders, while treating the
  nearby unmounted manta as an obstacle.
- The view holds for 1.8 seconds after input stops, then recenters smoothly.
- The left stick still pilots the manta/glider. The manta's former additional
  right-stick-Y climb binding was removed so looking up does not change altitude.
  Keyboard Space/Ctrl climb controls are retained.
- Holding the right mouse button keeps the existing manta mouse-steering mode;
  mouse look is suppressed during that mode. Release it to look around again.
- Menus release the cursor and pause camera input; gameplay captures the cursor.

Camera orbit limits and recenter timing are exposed through `freeLook` on
`MantaCameraController` and `RiderCamera`. Mouse and stick sensitivity are in
`MantaInput` for mounted flight and `RiderSettings` for the Rider. Mouse deltas
are pixel-scaled; stick input is time-scaled. The old Rider camera `lookLimit`
and `lookRecenter` fields no longer control glide free look.

`FlightLookSetup.Install` upgrades the existing input asset without recreating
other bindings. The prototype builder also creates the new layout. Validation
reports are in `Logs/MantaFlight/validation-free-look*.txt`; the Play-mode check
uses temporary input devices to verify camera/piloting separation, mouse-steering
priority, pause, and the Deploying-to-Glide transition.
