# Rider prototype

Open `Assets/MantaFlight/Scenes/MantaFlight.unity` and enter Play Mode. The rider starts mounted. The imported Starter Assets humanoid replaces the primitive rider in this scene; imported package files are unchanged.

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move / wingsuit bank and pitch | WASD | Left stick |
| Camera / limited glide look | Mouse | Right stick |
| Run | Left Shift | Left stick click |
| Crouch (hold) | Left Ctrl | LB |
| Ground jump | Space | A |
| Ground roll | Q | B |
| Step off / contextual mount | F | X |
| Jump off with wingsuit | J | Y while mounted |
| Deploy / retract wingsuit | G | Y while airborne |
| Drop without wingsuit | K | D-pad down |
| Call / cancel call | H | D-pad up |
| Invert wingsuit pitch | I or debug button | D-pad right |
| Pause / tuning | Escape | Start |
| Return to mounted spawn | R | Select |

Mounted manta controls remain as before. Hold its brake (C / LT) when low and slow: below `Hover Entry Speed` and within `Dismount Height`, it decelerates into a hover. Press F / X once the debug panel says `Dismount True`. Accelerating resumes flight if still mounted. A ground dismount checks a full standing capsule along the path; an obstructed side tries the other side.

For immediate ground testing, use **Manta > Rider > Test on foot here (Play mode)**. It places the rider on the ground below the current location. This is a debug shortcut, not a gameplay action.

## States and feel

`Grounded` uses acceleration/deceleration, a turn-rate limit, a short heavy jump and a crouching capsule. Headroom is checked before standing. A roll locks movement until its configured duration, respects collisions and then enters its cooldown. Walking off an edge enters `Falling`; impact vertical and horizontal speeds choose a short landing recovery, a forced roll, or a hard-landing recovery.

`Falling` can enter `Deploying` only above the configured height and after the airtime or downward-speed condition. Toggle again to retract, including during deployment. Physics continues during every toggle. `Second Jump Deploys` optionally makes a subsequent jump press use the same airborne toggle. It is disabled by default.

`JumpOff` inherits manta velocity and adds an upward/forward impulse, with the wingsuit already deployed. All other airborne exits use `Falling`.

Wingsuit flight integrates gravity, speed-squared lift and drag, angle of attack, bank-induced turning, and limited angular rates. Level flight has a configured sink/glide ratio; pulling up can temporarily redirect stored speed upwards but eventually stalls. Low airspeed or excessive angle of attack collapses lift and applies a tumble pose; pitching down restores speed. This is an arcade aerodynamic model, not a real-world flight simulator.

The glide camera follows velocity with a stable world-up reference, limited look offsets, speed FOV/pullback and partial banking. Near vertical flight and low speed blend towards a stable horizontal heading. The same Camera is shared with the existing manta camera; only one controller writes the shot at a time.

## Call and remount

H / D-pad up starts a predictive fly-by while falling or gliding. The manta approaches behind/below, matches velocity, and captures within the distance and relative-speed window after the detach grace period. The rider blends to the socket with input locked. A missed pass retries; total timeout, range limit or another call press cancels. The manta sweeps against world geometry during autonomous travel.

On the ground the call seeks a landing spot beside the rider. Walk into the mount window and press F / X. Ground capture is contextual rather than automatic.

## Tuning and preferences

Select **Manta > Rider > Select runtime feel** while playing. `RiderSettings` contains Ground, Glide, Camera and Mount groups plus input/deployment options. The controller uses a runtime copy. **Export runtime feel as new profile** saves that copy; assign the exported profile to Rider Character outside Play Mode to reuse it.

Wingsuit pitch inversion is stored separately in a runtime `RiderUserSettings` object and PlayerPrefs key `MantaFlight.Rider.InvertPitch.v1`. It is applied in `RiderInput` and never changes manta pitch or camera input. Off: stick/W forward dives. On: forward pulls up. The button in the upper-right debug panel and I / D-pad right change it immediately.

The Rider Input Action Map is in `Settings/RiderControls.inputactions`, separate from the manta map. Its bindings can be edited in Unity's Input Actions editor. Runtime manta rebinding does not change rider bindings.

## Architecture

- `RiderController`: explicit state machine with state-specific handlers and transition priorities.
- `RiderGroundMotor`: CharacterController collision capsule, grounded movement, gravity and headroom.
- `RiderGlideMotor`: aerodynamic integration, bank/pitch/yaw and stall recovery.
- `RiderInput` / `RiderUserSettings`: device-independent commands and persisted pitch preference.
- `MantaMountAdapter` / `IMount`: hovering, dismount queries, calls, predictive intercept and attachment.
- `RiderCamera`: rider camera and smooth handover to the existing mounted camera.
- `RiderVisuals`: imported animation parameters, simple glide/crouch/roll poses and placeholder wind audio.
- `RiderDebug`: state, speeds, height/airtime, deployment, dismount/scoop availability and pitch preference.

The CharacterController approach matches the imported Starter Assets. The wingsuit owns a velocity and integrates forces, then feeds displacement to the same capsule; there is no competing Rigidbody motor on the rider. Manta changes are limited to input ownership and a kinematic pose/velocity handover API. Looping remains removed.

## Verification

In Play Mode, use the three **Manta > Rider > Validate...** menus. They restore the rider and clean up their temporary obstacles. Reports are written under `Logs/MantaFlight/validation-rider*.txt`. Checks cover landing/roll locks, deployment gates and spam, dive/pull-out/stall, jump-off grace, ground call/mount, moving auto-scoop, cancellation/retry/timeout, inversion and vertical/stall camera stability.

Animation poses, wind and landing audio are placeholders. There is no damage/invulnerability system or command-wheel UI. Fully enclosed geometry can block the manta; timeout cancels the call safely. Human playtesting is still needed for final movement and camera tuning.
