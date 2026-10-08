using System;
using System.Collections.Generic;
using MantaFlight.Rider;
using MantaFlight.Rider.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MantaFlight.Editor
{
    public static class MantaInteractionValidation
    {
        [MenuItem("Manta/Progression/Validate controller and ground mount (Play mode)")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play mode required.");
            var p = UnityEngine.Object.FindFirstObjectByType<MantaProgression>();
            var r = p.Mount.rider;
            var input = p.GetComponent<MantaInput>();
            var panel = p.GetComponent<MantaProgressionPanel>();
            var data = JsonUtility.ToJson(p.Data);
            string id = p.saveId;
            float cargo = p.cargoKilograms;
            var report = new List<string>();
            void Check(bool ok, string message) { if (!ok) throw new Exception(message); report.Add("PASS " + message); }
            void Step(RiderCommand command = default) { RiderPhysicsTestScope.Step(.02f); r.Tick(command, .02f); Physics.SyncTransforms(); }
            p.saveId = "interaction-validation";
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var origin = new Vector3(-700, 500, -700);
            floor.transform.position = origin + Vector3.down * .5f;
            floor.transform.localScale = new Vector3(80, 1, 80);
            Gamepad pad = null;
            using var physics = new RiderPhysicsTestScope(r);
            try
            {
                p.Data.endurance = p.MaxEndurance; p.Data.exhausted = false; p.cargoKilograms = 0;
                input.SetMenu(false);
                r.PlaceForTest(origin + Vector3.right * 8 + Vector3.up * .05f, Vector3.down, RiderState.Falling);
                p.Controller.SetExternalMotion(origin + Vector3.up * 2.2f, Quaternion.identity, Vector3.zero);
                p.Mount.RestAfterExhaustion(); Physics.SyncTransforms();
                for (int i = 0; i < 50; i++) Step();
                for (int i = 0; i < 50; i++)
                {
                    RiderPhysicsTestScope.Step(.02f);
                    r.Motor.GroundMove(new RiderCommand { move = Vector2.down }, Vector3.right, .02f, false);
                }
                Check(r.Motor.Grounded && Vector3.Distance(r.transform.position, p.Mount.Seat.position) > r.settings.mount.landedMountDistance,
                    "Real shell stops standing rider outside the old saddle radius");
                Check(p.Mount.CanMountGround(), "Landed manta can be mounted beside its collision shell");
                p.Data.exhausted = true;
                Check(!p.Mount.CanMountGround(), "Exhaustion still blocks mounting"); p.Data.exhausted = false;
                p.cargoKilograms = p.Capacity + 1;
                Check(!p.Mount.CanMountGround(), "Overload still blocks mounting"); p.cargoKilograms = 0;
                pad = InputSystem.AddDevice<Gamepad>();
                Vector3 beside = r.transform.position;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North)); InputSystem.Update(); panel.SendMessage("Update");
                Check(panel.IsOpen && r.Paused, "Y / Triangle opens Manta Info beside the manta");
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update(); panel.SendMessage("Update");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North)); InputSystem.Update(); panel.SendMessage("Update");
                Check(!panel.IsOpen && !r.Paused, "Y / Triangle closes Manta Info");
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                r.Motor.Place(origin + Vector3.right * 30, Quaternion.identity, Vector3.zero);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North)); InputSystem.Update(); panel.SendMessage("Update");
                Check(!panel.IsOpen, "Y cannot open Manta Info outside nearby reach");
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                r.PlaceForTest(beside + Vector3.up, Vector3.zero, RiderState.Falling);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North)); InputSystem.Update(); panel.SendMessage("Update");
                Check(!panel.IsOpen && r.Input.Read(.02f).toggleGlide, "Y keeps wingsuit control while airborne near the manta");
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                r.PlaceForTest(beside, Vector3.zero, RiderState.Grounded);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West)); InputSystem.Update();
                Step(r.Input.Read(.02f));
                Check(r.State == RiderState.Remounting, "X / Square starts the ground mount transition");
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                for (int i = 0; i < 25; i++) Step();
                Check(r.Mounted && p.Mount.Mode == MantaServiceState.Piloted, "Ground mount reaches the saddle and restores flight control");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North)); InputSystem.Update(); panel.SendMessage("Update");
                Check(panel.IsOpen && r.Paused, "Y / Triangle opens Manta Info and pauses riding");
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update(); panel.SendMessage("Update");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); InputSystem.Update(); panel.SendMessage("Update");
                Check(!panel.IsOpen && !r.Paused, "B / Circle closes Manta Info");
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start)); InputSystem.Update();
                Check(input.SettingsMenuOpen, "Start / Options opens settings");
                input.SetMenu(false);
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Select)); InputSystem.Update(); panel.SendMessage("Update");
                Check(!panel.IsOpen, "View / Share no longer opens Manta Info");
                return string.Join("\n", report);
            }
            finally
            {
                if (pad != null) InputSystem.RemoveDevice(pad);
                JsonUtility.FromJsonOverwrite(data, p.Data); p.cargoKilograms = cargo; p.saveId = id;
                PlayerPrefs.DeleteKey("Manta.Progression.v1.interaction-validation"); PlayerPrefs.DeleteKey("Manta.Progression.v1.interaction-validation.backup");
                input.SetMenu(false); UnityEngine.Object.DestroyImmediate(floor); p.Controller.ResetFlight();
            }
        }
    }
}
