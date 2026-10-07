using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;
namespace MantaFlight.Rider.Editor
{
    public static class RiderOrdersValidation
    {
        [MenuItem("Manta/Rider/Validate manta orders (Play mode)")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play mode required");
            var r = Object.FindFirstObjectByType<RiderController>();
            using var physics = new RiderPhysicsTestScope(r);
            var report = new List<string>();
            bool menu = r.mantaInput.MenuOpen; var oldView = r.view;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cameraObject = new GameObject("Order validation camera", typeof(Camera));
            var pad = InputSystem.AddDevice<Gamepad>();
            var origin = new Vector3(-1000, 500, -1000);
            void Check(bool ok, string text) { report.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) throw new Exception(text); }
            void Step() { r.mount.Tick(.02f); RiderPhysicsTestScope.Step(.02f); r.Tick(default, .02f); }
            void Choose(Vector2 direction)
            {
                r.Tick(new RiderCommand { ordersPressed = true, ordersHeld = true, orderSelection = direction }, .02f);
                r.Tick(new RiderCommand { ordersReleased = true, orderSelection = direction }, .02f);
            }
            try
            {
                r.mantaInput.SetMenu(false);
                floor.transform.position = origin - Vector3.up * .5f; floor.transform.localScale = new Vector3(180,1,180); Physics.SyncTransforms();
                r.PlaceForTest(origin + Vector3.up * .05f, Vector3.zero, RiderState.Grounded);
                r.mount.manta.SetExternalMotion(origin + new Vector3(0,12,-30), Quaternion.identity, Vector3.zero);
                r.view = cameraObject.GetComponent<Camera>(); r.view.enabled = false;
                r.view.transform.position = origin + new Vector3(0,6,-5); r.view.transform.LookAt(origin + Vector3.forward * 12);

                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder)); InputSystem.Update();
                var command = r.Input.Read(.02f);
                Check(command.ordersPressed && command.ordersHeld && !command.crouch, "LB opens orders without crouching");
                r.Tick(command, .02f); Check(r.CommandWheel.IsOpen, "Wheel opens when dismounted");
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.right }.WithButton(GamepadButton.LeftShoulder)); InputSystem.Update();
                r.Tick(r.Input.Read(.02f), .02f);
                Check(r.CommandWheel.Selected == MantaOrder.Stay && r.LookInput == Vector2.zero, "Right stick selects Stay without moving the camera");
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.right }); InputSystem.Update();
                r.Tick(r.Input.Read(.02f), .02f);
                Check(!r.CommandWheel.IsOpen && r.mount.Mode == MantaServiceState.Stay, "LB release issues the selected order");
                r.Motor.Place(origin + Vector3.right * 45, Quaternion.identity, Vector3.zero);
                for (int i=0;i<90;i++) Step();
                Check(r.mount.Mode == MantaServiceState.Stay && r.mount.Velocity.magnitude < .1f, "Stay persists when the rider walks away");
                Choose(Vector2.up); Check(r.mount.Mode == MantaServiceState.Following, "Follow resumes companion following");
                var mode = r.mount.Mode;
                Choose(Vector2.zero); Check(r.mount.Mode == mode && !r.CommandWheel.IsOpen, "Release in the dead zone cancels without an order");
                r.Tick(new RiderCommand { ordersPressed=true, ordersHeld=true, orderSelection=Vector2.right }, .02f);
                r.Tick(new RiderCommand { orderCancel=true, roll=true, ordersHeld=true }, .02f);
                Check(!r.CommandWheel.IsOpen && r.State != RiderState.Rolling && r.mount.Mode == mode, "B cancels without triggering a dodge or an order");

                r.Motor.Place(origin + Vector3.up*.05f, Quaternion.identity, Vector3.zero);
                Choose(Vector2.down);
                Check(r.CommandWheel.Targeting && r.LandingTarget.Aiming && r.LandingTarget.Valid && r.mount.Mode == mode, "Go There previews a valid point without issuing the order");
                r.Tick(new RiderCommand { orderConfirm=true, jump=true }, .02f);
                Check(!r.CommandWheel.Targeting && r.mount.Mode == MantaServiceState.Landing && !r.Airborne, "A confirms Go There without jumping");
                for(int i=0;i<800 && r.mount.Calling;i++) Step();
                Check(r.mount.Landed && r.mount.Mode == MantaServiceState.Stay, "Go There lands and waits at the chosen point");
                Choose(Vector2.left); Check(r.mount.Mode == MantaServiceState.Landing, "Come Here requests a landing near a grounded rider");
                r.CommandWheel.Cancel();
                Choose(Vector2.down); r.Tick(new RiderCommand { orderCancel=true },.02f);
                Check(!r.CommandWheel.Targeting && !r.LandingTarget.Aiming, "Cancelling Go There hides its preview");
                r.Tick(new RiderCommand { ordersPressed=true, ordersHeld=true },.02f);
                r.Tick(default,.02f); Check(!r.CommandWheel.IsOpen, "Lost held input cancels the wheel");
                r.mount.manta.ResetFlight();
                r.Tick(new RiderCommand { ordersPressed=true, ordersHeld=true },.02f);
                Check(r.Mounted && !r.CommandWheel.IsOpen, "Mounted rider cannot open the orders wheel");
                Check(RiderCommandWheel.SelectDirection(new Vector2(.05f,.05f)) == MantaOrder.None, "Stick noise stays inside the dead zone");
                return string.Join("\n", report);
            }
            finally
            {
                InputSystem.RemoveDevice(pad); r.CommandWheel.Cancel(); r.view=oldView;
                Object.DestroyImmediate(floor); Object.DestroyImmediate(cameraObject);
                r.mount.manta.ResetFlight(); r.mantaInput.SetMenu(menu);
                Directory.CreateDirectory("Logs/MantaFlight"); File.WriteAllLines("Logs/MantaFlight/validation-orders.txt", report);
            }
        }
    }
}
