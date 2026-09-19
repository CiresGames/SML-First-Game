using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaLoopCameraValidation
    {
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Requires Play mode.");
            var manta = UnityEngine.Object.FindFirstObjectByType<MantaController>();
            var camera = UnityEngine.Object.FindFirstObjectByType<MantaCameraController>();
            var lens = camera.GetComponent<Camera>();
            var input = manta.GetComponent<MantaInput>();
            var tricks = manta.GetComponent<MantaManeuvers>();
            var body = manta.GetComponent<Rigidbody>();
            var mode = Physics.simulationMode;
            var mask = manta.settings.environmentMask;
            var interpolation = body.interpolation;
            bool paused = manta.Paused, menu = input.MenuOpen;
            var report = new List<string>();
            void Check(bool pass, string message)
            {
                report.Add((pass ? "PASS " : "FAIL ") + message);
                if (!pass) throw new Exception(message);
            }
            void Step()
            {
                manta.Simulate(default, .02f); Physics.Simulate(.02f); manta.GetComponent<MantaVisuals>().Tick(.02f); camera.Tick(.02f);
            }
            try
            {
                Physics.simulationMode = SimulationMode.Script; body.interpolation = RigidbodyInterpolation.None;
                manta.Paused = false; input.SetMenu(false); manta.settings.environmentMask = 0;
                foreach (var trick in new[] { MantaTrick.LoopForward, MantaTrick.LoopBackward })
                foreach (float speed in new[] { 12f, 28f, 90f })
                {
                    manta.ResetFlight(); body.position = new Vector3(0, 180, -210); manta.transform.position = body.position; Physics.SyncTransforms();
                    manta.ScaleSpeed(speed / manta.Speed); camera.Snap(); camera.Tick(.02f);
                    Check(tricks.TryStart(trick, manta), trick + " starts at " + speed + " m/s");
                    Vector3 holdPosition = Vector3.zero;
                    Quaternion holdRotation = Quaternion.identity;
                    float maximumDrift = 0, maximumRotation = 0, minimumDistance = float.MaxValue;
                    bool visible = true;
                    int frames = Mathf.RoundToInt(tricks.Duration / .02f);
                    for (int i = 0; i < frames - 1; i++)
                    {
                        Step();
                        float progress = (i + 1f) / frames;
                        if (progress > .2f)
                        {
                            Vector3 viewport = lens.WorldToViewportPoint(body.position + manta.Heading * tricks.VisualPositionOffset);
                            visible &= viewport.z > 0 && viewport.x > .02f && viewport.x < .98f && viewport.y > .02f && viewport.y < .98f;
                            minimumDistance = Mathf.Min(minimumDistance, Vector3.Distance(body.position, camera.transform.position));
                        }
                        if (i == frames / 2) { holdPosition = camera.transform.position - body.position; holdRotation = camera.transform.rotation; }
                        if (i > frames / 2)
                        {
                            maximumDrift = Mathf.Max(maximumDrift, Vector3.Distance(holdPosition, camera.transform.position - body.position));
                            maximumRotation = Mathf.Max(maximumRotation, Quaternion.Angle(holdRotation, camera.transform.rotation));
                        }
                        if (speed == 28 && (i == frames / 4 || i == frames / 2 || i == frames * 3 / 4))
                        {
                            manta.GetComponent<MantaVisuals>().Tick(.02f);
                            Capture(lens, "loop-" + trick + "-" + i + ".png");
                        }
                    }
                    Check(visible, trick + " stays framed at " + speed + " m/s");
                    Check(maximumDrift < .25f && maximumRotation < 1,
                        trick + " camera remains steady relative to root: " + maximumDrift.ToString("F3") + " m / " + maximumRotation.ToString("F2") + " degrees");
                    Check(minimumDistance < manta.settings.cameraDistance && minimumDistance > manta.settings.loopCamera.distance - 1, trick + " uses close root framing (minimum " + minimumDistance.ToString("F1") + " m)");
                    Vector3 before = camera.transform.position - body.position;
                    Step();
                    Check(Vector3.Distance(before, camera.transform.position - body.position) < .5f, trick + " exit has no position snap");
                    for (int i = 0; i < 100; i++) Step();
                    float chaseDistance = Vector3.Distance(body.position, camera.transform.position);
                    float chaseLimit = manta.settings.cameraDistance + manta.settings.speedCameraDistance
                        + manta.Speed * manta.settings.cameraLag + manta.settings.cameraHeight + 2;
                    Check(chaseDistance < chaseLimit, trick + " returns to chase framing (" + chaseDistance.ToString("F1") + " m)");
                }
                tricks.TryStart(MantaTrick.LoopBackward, manta);
                for (int i = 0; i < 30; i++) Step();
                tricks.Cancel(); Step();
                Check(!float.IsNaN(camera.transform.position.x), "Interrupted loop releases the camera safely");
                manta.ResetFlight(); camera.Tick(.02f);
                Check(Vector3.Distance(body.position, camera.transform.position) < 30, "Reset clears the held loop shot");
                return string.Join("\n", report);
            }
            finally
            {
                manta.settings.environmentMask = mask; Physics.simulationMode = mode; body.interpolation = interpolation;
                manta.ResetFlight(); manta.Paused = paused; input.SetMenu(menu);
                Directory.CreateDirectory("Logs/MantaFlight"); File.WriteAllLines("Logs/MantaFlight/validation-loop-camera.txt", report);
            }
        }
        static void Capture(Camera camera, string name)
        {
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var texture = new RenderTexture(1280, 720, 24);
            var png = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                png.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); png.Apply();
                Directory.CreateDirectory("Logs/MantaFlight"); File.WriteAllBytes("Logs/MantaFlight/" + name, png.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(png); texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
