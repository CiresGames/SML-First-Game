using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MantaFlight.Editor
{
    // Run inside Play mode via Manta/Validate. Uses real physics steps, without a second scene or test package.
    public static class MantaFlightValidation
    {
        static readonly List<string> results = new List<string>();
        static MantaController manta;
        static void Check(bool pass, string message)
        {
            results.Add((pass ? "PASS " : "FAIL ") + message);
            if (!pass) throw new Exception("MANTA VALIDATION: " + message);
        }
        static void Advance(FlightInput input, float seconds, float dt = .02f)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds / dt); i++)
            { manta.Simulate(input, dt); Physics.Simulate(dt); }
        }
        static void AtAltitude()
        {
            manta.ResetFlight(); var body = manta.GetComponent<Rigidbody>();
            body.position = new Vector3(-650, 550, -500); manta.transform.position = body.position;
            Physics.SyncTransforms();
        }
        [MenuItem("Manta/Validate current flight phase (Play mode)")]
        public static void ValidateFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Validation requires Play mode.");
            manta = UnityEngine.Object.FindFirstObjectByType<MantaController>();
            var mode = Physics.simulationMode; var mask = manta.settings.environmentMask;
            var phase = manta.settings.phase; bool paused = manta.Paused;
            var input = manta.GetComponent<MantaInput>(); bool menu = input.MenuOpen;
            results.Clear();
            try
            {
                input.SetMenu(false); manta.Paused = true; Physics.simulationMode = SimulationMode.Script;
                manta.settings.environmentMask = 0;
                AtAltitude(); Advance(new FlightInput { throttle = 1 }, 8);
                Check(manta.Speed > manta.settings.cruiseSpeed + 20 && manta.Speed <= manta.settings.maximumSpeed + .1f, "Acceleration reaches high speed within bounds");
                Advance(new FlightInput { brake = 1 }, 8);
                Check(Mathf.Abs(manta.Speed - manta.settings.minimumSpeed) < .05f, "Brake respects minimum speed");
                AtAltitude(); Advance(new FlightInput { steering = Vector2.right }, .3f);
                float bank = manta.Bank;
                Check(bank < -2 && bank > -manta.settings.maximumBanking, "Right turn progressively banks right");
                Advance(default, 2);
                Check(Mathf.Abs(manta.Bank) < .1f, "Bank returns smoothly to neutral");
                AtAltitude(); Advance(new FlightInput { steering = Vector2.up }, 4);
                Check(Mathf.Abs(manta.Pitch + manta.settings.pitchLimit) < .1f, "Normal pitch is clamped without flipping");
                AtAltitude(); Advance(new FlightInput { throttle = .7f, steering = new Vector2(.25f, .1f) }, 3, .02f);
                Vector3 p50 = manta.GetComponent<Rigidbody>().position;
                AtAltitude(); Advance(new FlightInput { throttle = .7f, steering = new Vector2(.25f, .1f) }, 3, .01f);
                Check(Vector3.Distance(p50, manta.GetComponent<Rigidbody>().position) < 2, "50 / 100 Hz trajectories stay within 2 metres after 3 seconds");
                manta.settings.environmentMask = 1;
                AtAltitude();
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Temporary validation wall"; wall.transform.position = manta.transform.position + Vector3.forward * 12;
                wall.transform.localScale = new Vector3(30, 30, .15f); Physics.SyncTransforms();
                float wallZ = wall.transform.position.z;
                try
                {
                    manta.ScaleSpeed(3); Advance(default, .8f);
                    Check(manta.GetComponent<Rigidbody>().position.z < wallZ - .5f, "Fast flight does not tunnel through a 15 cm wall");
                }
                finally { UnityEngine.Object.DestroyImmediate(wall); }
                manta.settings.environmentMask = 0;
                if (phase >= FlightPhase.SpeedAndCamera)
                {
                    AtAltitude(); manta.SetHeading(Quaternion.Euler(65, 0, 0)); Advance(default, 2);
                    float diveSpeed = manta.Speed;
                    Check(diveSpeed > manta.settings.cruiseSpeed + 20, "Dive gains arcade gravitational energy");
                    manta.SetHeading(Quaternion.Euler(-65, 0, 0)); Advance(default, 1);
                    Check(manta.Speed < diveSpeed && manta.Speed > manta.settings.minimumSpeed, "Climb trades stored speed for altitude");
                }
                if (phase >= FlightPhase.AdvancedManeuvers)
                {
                    var tricks = manta.GetComponent<MantaManeuvers>();
                    foreach (MantaTrick trick in new[] { MantaTrick.RollLeft, MantaTrick.RollRight, MantaTrick.LoopForward, MantaTrick.LoopBackward, MantaTrick.Turnaround })
                    {
                        AtAltitude(); Vector3 start = manta.GetComponent<Rigidbody>().position; float initialSpeed = manta.Speed;
                        Check(tricks.TryStart(trick, manta), trick + " starts");
                        Check(!tricks.TryStart(MantaTrick.RollLeft, manta), "Concurrent maneuver is rejected");
                        float duration = trick == MantaTrick.Turnaround ? manta.settings.turnaroundDuration : trick == MantaTrick.RollLeft || trick == MantaTrick.RollRight ? manta.settings.barrelDuration : manta.settings.loopDuration;
                        Advance(default, duration * .5f);
                        Check(Vector3.Distance(start, manta.GetComponent<Rigidbody>().position) > 6, trick + " travels through space");
                        Advance(default, duration * .5f + .04f);
                        Check(tricks.Current == MantaTrick.None, trick + " finishes");
                        Check(Vector3.Dot(manta.Heading * Vector3.forward, trick == MantaTrick.Turnaround ? Vector3.back : Vector3.forward) > .99f, trick + " exits in the expected direction");
                        Check(manta.Speed > initialSpeed * .8f, trick + " preserves momentum");
                    }
                    AtAltitude(); Advance(new FlightInput { steering = Vector2.right }, .5f);
                    float normalAngle = Vector3.Angle(Vector3.forward, manta.Heading * Vector3.forward);
                    AtAltitude(); Advance(new FlightInput { steering = Vector2.right, tightTurn = true }, .5f);
                    Check(Vector3.Angle(Vector3.forward, manta.Heading * Vector3.forward) > normalAngle * 1.7f, "Tight turn is distinctly stronger");
                }
                // Exercise a virtual controller through the actual Input System and action bindings.
                var gamepad = InputSystem.AddDevice<Gamepad>();
                string originalBindings = input.actions.SaveBindingOverridesAsJson();
                try
                {
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.6f, .25f), rightTrigger = .7f });
                    InputSystem.Update();
                    var map = input.actions.FindActionMap("Flight");
                    Check(map["Steering"].ReadValue<Vector2>().x > .3f && map["Accelerate"].ReadValue<float>() > .6f, "Analog stick and trigger reach Flight map");
                    float originalDeadZone = input.stickDeadZone;
                    try
                    {
                        input.stickDeadZone = 0;
                        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.08f, 0) }); InputSystem.Update(); input.SendMessage("Update");
                        Check(input.State.steering.x > .01f, "Custom dead zone can expose small analog inputs");
                        input.stickDeadZone = .2f; input.SendMessage("Update");
                        Check(input.State.steering.sqrMagnitude < .0001f, "Custom dead zone suppresses stick drift");
                    }
                    finally { input.stickDeadZone = originalDeadZone; }
                    int binding = -1;
                    for (int i = 0; i < map["RollLeft"].bindings.Count; i++) if (map["RollLeft"].bindings[i].groups.Contains("Gamepad")) binding = i;
                    map["RollLeft"].ApplyBindingOverride(binding, "<Gamepad>/dpad/left");
                    string json = input.actions.SaveBindingOverridesAsJson(); input.actions.RemoveAllBindingOverrides(); input.actions.LoadBindingOverridesFromJson(json);
                    Check(map["RollLeft"].bindings[binding].effectivePath == "<Gamepad>/dpad/left", "Rebinding overrides round-trip through JSON");
                    map["RollLeft"].RemoveBindingOverride(binding);
                }
                finally { input.actions.LoadBindingOverridesFromJson(originalBindings); InputSystem.RemoveDevice(gamepad); }
                manta.ResetFlight();
                Check(manta.GetComponent<MantaManeuvers>().Current == MantaTrick.None && Mathf.Abs(manta.Speed - manta.settings.cruiseSpeed) < .01f, "Reset clears maneuver and restores cruise");
                return string.Join("\n", results);
            }
            finally
            {
                manta.settings.environmentMask = mask; Physics.simulationMode = mode;
                manta.ResetFlight(); manta.Paused = paused; input.SetMenu(menu);
                Directory.CreateDirectory("Logs/MantaFlight");
                File.WriteAllLines("Logs/MantaFlight/validation-" + phase + ".txt", results);
            }
        }

        public static string ValidateCameraAndRebind()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Requires Play mode.");
            manta = UnityEngine.Object.FindFirstObjectByType<MantaController>();
            var input = manta.GetComponent<MantaInput>();
            var camera = UnityEngine.Object.FindFirstObjectByType<MantaCameraController>();
            var lens = camera.GetComponent<Camera>();
            var mode = Physics.simulationMode;
            bool wasPaused = manta.Paused;
            bool menu = input.MenuOpen;
            LayerMask mask = manta.settings.environmentMask;
            string overrides = input.actions.SaveBindingOverridesAsJson();
            string stored = PlayerPrefs.GetString("MantaFlight.Bindings.v1", "");
            var virtualKeyboard = InputSystem.AddDevice<Keyboard>();
            results.Clear();
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                input.SetMenu(false); manta.Paused = false; manta.settings.environmentMask = 0;
                AtAltitude(); camera.Snap(); camera.SendMessage("LateUpdate");
                float cruiseFOV = lens.fieldOfView;
                manta.ScaleSpeed(2.5f); camera.Snap(); camera.SendMessage("LateUpdate");
                Check(lens.fieldOfView > cruiseFOV + 5 && lens.fieldOfView <= manta.settings.maximumFOV + 4.1f, "FOV increases with speed and stays bounded");
                var tricks = manta.GetComponent<MantaManeuvers>();
                foreach (var trick in new[] { MantaTrick.RollRight, MantaTrick.LoopBackward, MantaTrick.Turnaround })
                {
                    AtAltitude(); tricks.TryStart(trick, manta); camera.Snap();
                    float leastUp = 1;
                    for (int i = 0; i < 190; i++)
                    {
                        manta.Simulate(default, .02f); Physics.Simulate(.02f); camera.SendMessage("LateUpdate");
                        leastUp = Mathf.Min(leastUp, Vector3.Dot(camera.transform.up, Vector3.up));
                    }
                    Check(leastUp > .25f && !float.IsNaN(camera.transform.position.x), trick + " camera keeps a readable horizon");
                }
                AtAltitude(); camera.Snap(); camera.SendMessage("LateUpdate");
                Vector3 focus = manta.transform.position + Vector3.up * 1.1f;
                Vector3 offset = camera.transform.position - focus;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Temporary camera wall"; wall.transform.position = focus + offset * .55f;
                wall.transform.localScale = new Vector3(50, 50, .5f); Physics.SyncTransforms();
                try
                {
                    manta.settings.environmentMask = 1; camera.SendMessage("LateUpdate");
                    Check(Vector3.Distance(focus, camera.transform.position) < offset.magnitude * .55f, "Camera retracts before a wall after smoothing");
                }
                finally { UnityEngine.Object.DestroyImmediate(wall); }
                input.SetMenu(true);
                var action = input.actions.FindActionMap("Flight")["RollLeft"];
                input.Rebind("RollLeft", 0);
                InputSystem.QueueStateEvent(virtualKeyboard, new KeyboardState(Key.J)); InputSystem.Update();
                // Input System waits 50 ms for a better candidate before completing a rebind.
                System.Threading.Thread.Sleep(80); InputSystem.Update();
                Check(input.RebindingLabel == null && action.bindings[0].effectivePath == "<Keyboard>/j", "Interactive rebinding captures a keyboard button");
                input.Rebind("RollLeft", 0);
                InputSystem.QueueStateEvent(virtualKeyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
                Check(input.RebindingLabel == null && input.MenuOpen, "Escape cancels rebinding without resuming flight");
                return string.Join("\n", results);
            }
            finally
            {
                input.CancelRebind();
                input.actions.LoadBindingOverridesFromJson(overrides);
                if (string.IsNullOrEmpty(stored)) PlayerPrefs.DeleteKey("MantaFlight.Bindings.v1"); else PlayerPrefs.SetString("MantaFlight.Bindings.v1", stored);
                PlayerPrefs.Save(); InputSystem.RemoveDevice(virtualKeyboard);
                manta.settings.environmentMask = mask; manta.ResetFlight(); input.SetMenu(menu);
                manta.Paused = wasPaused; Physics.simulationMode = mode;
                File.WriteAllLines("Logs/MantaFlight/validation-camera-input.txt", results);
            }
        }
    }
}
