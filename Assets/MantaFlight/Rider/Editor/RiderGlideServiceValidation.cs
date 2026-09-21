using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace MantaFlight.Rider.Editor
{
    public static class RiderGlideServiceValidation
    {
        [MenuItem("Manta/Rider/Validate persistent glide, follow and landing (Play mode)")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play mode required");
            var r = Object.FindFirstObjectByType<RiderController>();
            using var physics = new RiderPhysicsTestScope(r);
            var report = new List<string>();
            var created = new List<GameObject>();
            bool paused = r.Paused; var view = r.view;
            void Check(bool ok, string message)
            { report.Add((ok ? "PASS " : "FAIL ") + message); if (!ok) throw new Exception(message); }
            void Step(RiderCommand command = default, bool service = false)
            { if (service) r.mount.Tick(.02f); RiderPhysicsTestScope.Step(.02f); r.Tick(command, .02f); Physics.SyncTransforms(); }
            GameObject Box(Vector3 position, Vector3 size, Quaternion rotation)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(go);
                go.transform.SetPositionAndRotation(position, rotation); go.transform.localScale = size;
                Physics.SyncTransforms(); return go;
            }
            r.mantaInput.SetMenu(false);
            try
            {
                var origin = new Vector3(-800, 1400, -800);
                r.PlaceForTest(origin, Vector3.forward * 25, RiderState.Glide);
                for (int i = 0; i < 1500; i++) Step(new RiderCommand { glide = i < 25 ? Vector2.down : Vector2.zero });
                Check(r.State == RiderState.Glide && !r.Glide.Stalled && Mathf.Abs(r.Motor.Velocity.y) < r.Motor.Velocity.magnitude * .8f,
                    "30-second glide survives pull-out and recovers to sustained flight without toggling");
                Step(new RiderCommand { toggleGlide = true });
                Check(r.State == RiderState.Falling, "Only explicit retract exits sustained glide into falling");
                r.PlaceForTest(origin, Vector3.forward * 30, RiderState.Glide);
                for (int i = 0; i < 65; i++) Step(new RiderCommand { glide = Vector2.right });
                float turn = Vector3.Angle(Vector3.forward, Vector3.ProjectOnPlane(r.Motor.Velocity, Vector3.up));
                Check(turn > 35 && Mathf.Abs(r.Glide.Bank) <= r.settings.glide.bankLimit + .1f,
                    "Bank input redirects actual velocity strongly while respecting the bank limit");
                r.mount.manta.ResetFlight(); r.mount.manta.SetExternalMotion(origin, Quaternion.identity, Vector3.forward * 30);
                r.Detach(true); Step();
                Check(r.State == RiderState.Falling && r.GlidePose == 0 && !r.CanScoop, "Jump-off stays in free fall with capture grace");
                for (int i = 0; i < 30; i++) Step();
                Check(r.State == RiderState.Falling, "Elapsed jump-off time never deploys wingsuit automatically");
                Step(new RiderCommand { toggleGlide = true });
                Check(r.State == RiderState.Deploying, "Dedicated glide input deploys after a manta jump");

                foreach (bool falling in new[] { false, true })
                {
                    r.PlaceForTest(origin, falling ? new Vector3(5,-45,25) : new Vector3(0,-12,55), falling ? RiderState.Falling : RiderState.Glide);
                    r.mount.manta.SetExternalMotion(origin + new Vector3(35,10,-90), Quaternion.identity, Vector3.zero);
                    r.mount.CallToPosition(r.transform.position, r.Motor.Velocity, true);
                    int steps = 0;
                    for (; steps < 700 && !r.Mounted; steps++) Step(default, true);
                    Check(r.Mounted && steps < 400, "Adaptive catch remounts " + (falling ? "fast falling" : "fast gliding") + " rider in " + (steps * .02f).ToString("0.00") + "s");
                }
                r.PlaceForTest(origin, Vector3.forward * 25, RiderState.Glide);
                r.mount.manta.SetExternalMotion(origin + new Vector3(20,10,-80), Quaternion.identity, Vector3.zero);
                float maxStep = 0;
                for (int i = 0; i < 300; i++)
                { var before = r.mount.transform.position; Step(default, true); maxStep = Mathf.Max(maxStep, Vector3.Distance(before, r.mount.transform.position)); }
                Check(r.mount.Mode == MantaServiceState.Following && !r.Mounted && maxStep <= r.settings.mount.callSpeed * .02f + .1f
                    && Vector3.Distance(r.transform.position, r.mount.transform.position) < r.settings.mount.followDistance * 2,
                    "Unmounted manta follows the glider smoothly without teleport or unsolicited capture");

                var ground = new Vector3(-800, 500, -800);
                Box(ground + Vector3.down * .5f, new Vector3(160,1,160), Quaternion.identity);
                r.PlaceForTest(ground + Vector3.up * .05f, Vector3.down, RiderState.Falling);
                for (int i = 0; i < 30; i++) Step();
                r.mount.manta.SetExternalMotion(ground + new Vector3(40,15,-45), Quaternion.identity, Vector3.zero);
                for (int i = 0; i < 300; i++) Step(new RiderCommand { move = Vector2.up }, true);
                Check(r.State == RiderState.Grounded && r.mount.Mode == MantaServiceState.Following
                    && Vector3.Distance(r.transform.position, r.mount.transform.position) < r.settings.mount.followDistance * 2,
                    "Companion also follows walking rider at a distance");

                var slopeCenter = ground + new Vector3(30,7,0);
                Box(slopeCenter, new Vector3(18,1,18), Quaternion.Euler(0,0,25));
                Physics.Raycast(slopeCenter + Vector3.up * 20, Vector3.down, out var slope, 40, r.settings.environment);
                Check(r.mount.RequestLanding(slope.point, slope.normal), "Reasonable slope accepts a precise landing request");
                Vector3 fixedTarget = r.mount.InterceptTarget;
                for (int i = 0; i < 700 && r.mount.Calling; i++) Step(default, true);
                for (int i = 0; i < 40; i++) Step(default, true);
                Check(r.mount.Landed && Vector3.Distance(r.mount.transform.position, fixedTarget) < r.settings.mount.landingTolerance + .1f
                    && Vector3.Angle(r.mount.transform.up, slope.normal) < 2, "Manta settles at fixed target aligned to the slope normal");
                Check(!r.mount.RequestLanding(ground, Vector3.right), "Vertical surface is refused by the mount service");

                // Exercise the actual ray, hold/release routing and indicator, not only the service API.
                var cameraObject = new GameObject("Landing aim test camera"); created.Add(cameraObject);
                r.view = cameraObject.AddComponent<Camera>(); r.view.enabled = false;
                r.PlaceForTest(ground + Vector3.up * .05f, Vector3.down, RiderState.Falling);
                for (int i = 0; i < 30; i++) Step();
                var aim = ground + Vector3.forward * 8;
                r.view.transform.position = ground + new Vector3(0,6,-5); r.view.transform.LookAt(aim);
                var previousMode = r.mount.Mode; var previousTarget = r.mount.InterceptTarget;
                Step(new RiderCommand { call = true, callHeld = true });
                Check(r.LandingTarget.Aiming && r.LandingTarget.Valid && r.mount.Mode == previousMode && r.mount.InterceptTarget == previousTarget,
                    "Holding call only previews a sphere without changing service state or destination");
                fixedTarget = r.LandingTarget.Point + r.LandingTarget.Normal * r.settings.mount.hoverHeight;
                Step(new RiderCommand { callReleased = true });
                r.view.transform.Rotate(0,70,0);
                Check(!r.LandingTarget.Aiming && r.mount.InterceptTarget == fixedTarget, "Release commits fixed target without following later camera motion");
                Box(ground + new Vector3(0,5,12), new Vector3(12,10,1), Quaternion.identity);
                r.view.transform.position = ground + new Vector3(0,3,0); r.view.transform.rotation = Quaternion.identity;
                Step(new RiderCommand { call = true, callHeld = true });
                Check(r.LandingTarget.Aiming && !r.LandingTarget.Valid && r.mount.InterceptTarget == fixedTarget, "Invalid wall preview leaves the existing service command unchanged");
                Step(new RiderCommand { callReleased = true });
                Check(!r.LandingTarget.Aiming && r.mount.InterceptTarget == fixedTarget, "Releasing invalid aim never replaces the existing destination");
                return string.Join("\n", report);
            }
            finally
            {
                r.LandingTarget.CancelPreview(); r.view = view;
                foreach (var go in created) Object.DestroyImmediate(go);
                r.mount.manta.ResetFlight(); r.mantaInput.SetMenu(paused);
                Directory.CreateDirectory("Logs/MantaFlight"); File.WriteAllLines("Logs/MantaFlight/validation-glide-service.txt", report);
            }
        }
    }
}
