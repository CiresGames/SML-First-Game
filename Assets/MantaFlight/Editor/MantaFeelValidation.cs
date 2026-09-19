using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaFeelValidation
    {
        static MantaController manta;
        static MantaManeuvers tricks;
        static Rigidbody body;
        static MantaFlightSettings settings;
        static readonly Vector3 Origin = new Vector3(-650, 500, -500);
        static readonly List<string> report = new List<string>();
        static readonly List<GameObject> obstacles = new List<GameObject>();

        [MenuItem("Manta/Validate loops, swerves and tuning export (Play mode)")]
        static void RunFromMenu() => Debug.Log(Run());

        static void Check(bool condition, string message)
        {
            report.Add((condition ? "PASS " : "FAIL ") + message);
            if (!condition) throw new Exception("MANTA FEEL: " + message);
        }
        static void Step(FlightInput input = default, float dt = .02f)
        { manta.Simulate(input, dt); Physics.Simulate(dt); }
        static void Advance(float seconds, FlightInput input = default)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .02f); i++) Step(input); }
        static void Place(float speed = 28, float yaw = 0, bool reset = true)
        {
            if (reset) manta.ResetFlight();
            settings.environmentMask = 0;
            var rotation = Quaternion.Euler(0, yaw, 0);
            manta.SetHeading(rotation); body.rotation = rotation;
            float response = settings.momentumResponse; settings.momentumResponse = 10000;
            Step(); settings.momentumResponse = response;
            body.position = Origin; manta.transform.SetPositionAndRotation(Origin, rotation); Physics.SyncTransforms();
            manta.SetSpeed(speed);
        }
        static void ClearObstacles()
        {
            foreach (var obstacle in obstacles) if (obstacle != null) UnityEngine.Object.DestroyImmediate(obstacle);
            obstacles.Clear(); Physics.SyncTransforms();
        }
        static GameObject Wall(Vector3 position, Quaternion rotation)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Manta validation surface";
            wall.transform.SetPositionAndRotation(position, rotation); wall.transform.localScale = new Vector3(80, 80, .3f);
            obstacles.Add(wall); Physics.SyncTransforms(); return wall;
        }
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Requires Play mode.");
            manta = UnityEngine.Object.FindFirstObjectByType<MantaController>();
            tricks = manta.GetComponent<MantaManeuvers>(); body = manta.GetComponent<Rigidbody>(); settings = manta.settings;
            var input = manta.GetComponent<MantaInput>();
            string savedSettings = JsonUtility.ToJson(settings);
            var savedInput = new MantaInputSettings(); savedInput.Capture(input);
            var simulation = Physics.simulationMode; var interpolation = body.interpolation;
            bool paused = manta.Paused, menu = input.MenuOpen;
            report.Clear();
            try
            {
                Physics.simulationMode = SimulationMode.Script; body.interpolation = RigidbodyInterpolation.None;
                manta.Paused = false; input.SetMenu(false); settings.phase = FlightPhase.Polish;
                ValidateLoops(); ValidateImpacts(); ValidateExport(input);
                return string.Join("\n", report);
            }
            finally
            {
                ClearObstacles(); JsonUtility.FromJsonOverwrite(savedSettings, settings); savedInput.Apply(input);
                Physics.simulationMode = simulation; body.interpolation = interpolation;
                manta.ResetFlight(); manta.Paused = paused; input.SetMenu(menu);
                Directory.CreateDirectory("Logs/MantaFlight"); File.WriteAllLines("Logs/MantaFlight/validation-feel.txt", report);
            }
        }
        static void ValidateLoops()
        {
            foreach (var trick in new[] { MantaTrick.LoopForward, MantaTrick.LoopBackward })
            {
                Place(); tricks.TryStart(trick, manta); float duration = tricks.Duration;
                float initialSpeed = manta.Speed, peak = initialSpeed, entryRate = 0, middleRate = 0;
                bool tangent = true; float halfwayAltitude = 0;
                for (int i = 0; i < Mathf.CeilToInt(duration / .02f); i++)
                {
                    var before = tricks.VisualRotationOffset; Step();
                    float rate = Quaternion.Angle(before, tricks.VisualRotationOffset) / .02f;
                    if (i == 0) entryRate = rate;
                    if (tricks.Progress > .4f && tricks.Progress < .6f) { middleRate = Mathf.Max(middleRate, rate); halfwayAltitude = Mathf.Abs(tricks.VisualPositionOffset.y) > Mathf.Abs(halfwayAltitude) ? tricks.VisualPositionOffset.y : halfwayAltitude; }
                    peak = Mathf.Max(peak, manta.Speed);
                    tangent &= Vector3.Dot(manta.Velocity.normalized, manta.Heading * Vector3.forward) > .999f;
                }
                Advance(.02f);
                Check(peak > initialSpeed * 1.12f, trick + " gains speed in the middle (peak " + peak.ToString("F1") + " m/s)");
                Check(middleRate > entryRate * 4, trick + " rotation eases in, accelerates, then eases out");
                Check(tangent, trick + " root continues along its stable heading");
                Check(Vector3.Dot(manta.Heading * Vector3.forward, Vector3.forward) > .999f, trick + " exits along the incoming momentum");
                Check(Mathf.Abs(manta.Speed - initialSpeed) < .2f, trick + " preserves exit speed continuously");
                Check(trick == MantaTrick.LoopForward ? halfwayAltitude < -2 : halfwayAltitude > 2, trick + " follows the correct vertical direction");
            }
            Place(); tricks.TryStart(MantaTrick.LoopBackward, manta);
            Advance(.1f, new FlightInput { brake = 1 });
            Check(tricks.IsLooping, "Loop entry honors the brief input lock");
            Advance(.2f, new FlightInput { brake = 1 });
            Check(!tricks.IsLooping, "Brake interrupts a loop after the entry lock");
            Place(); tricks.TryStart(MantaTrick.LoopBackward, manta);
            float total = tricks.Duration; Advance(total * .5f);
            Quaternion inverted = manta.Heading;
            Step(new FlightInput { brake = 1 }); Step();
            Check(Quaternion.Angle(inverted, manta.Heading) < settings.backwardLoop.releaseUprightSpeed * .04f + 1, "Canceling upside down does not snap the model upright");
            Place(); tricks.TryStart(MantaTrick.LoopForward, manta);
            Advance(tricks.Duration + .02f, new FlightInput { steering = Vector2.right });
            float deviation = Vector3.Angle(Vector3.forward, manta.Heading * Vector3.forward);
            Check(deviation > 1 && deviation < settings.forwardLoop.maximumExitDeviation + 2, "Partial steering changes the exit within the configured limit");
            var size = settings.forwardLoop.sizeMode; float radius = settings.forwardLoop.nominalRadius, maximum = settings.forwardLoop.maximumDuration;
            settings.forwardLoop.sizeMode = LoopSizeMode.NominalRadius; settings.forwardLoop.maximumDuration = 10; settings.forwardLoop.nominalRadius = 12;
            Place(); tricks.TryStart(MantaTrick.LoopForward, manta); float firstDuration = tricks.Duration;
            settings.forwardLoop.nominalRadius = 24;
            Place(); tricks.TryStart(MantaTrick.LoopForward, manta);
            Check(Mathf.Abs(tricks.Duration / firstDuration - 2) < .01f, "Radius mode scales duration consistently with entry speed");
            settings.forwardLoop.sizeMode = size; settings.forwardLoop.nominalRadius = radius; settings.forwardLoop.maximumDuration = maximum;
        }
        static void ValidateImpacts()
        {
            Place(40); Wall(Origin + Vector3.forward * 35, Quaternion.identity); settings.environmentMask = 1;
            Step();
            Check(tricks.IsImpactTurning && body.position.z < Origin.z + 2, "Swerve anticipates a wall well before contact");
            bool escaped = false;
            for (int i = 0; i < 70; i++) { Step(); if (tricks.IsImpactTurning && Mathf.Abs(manta.Velocity.x) > 10) escaped = true; }
            Check(tricks.ImpactCount == 1 && escaped, "Head-on collision triggers one moving escape instead of stopping");
            Check(Mathf.Abs(Vector3.Dot(manta.Heading * Vector3.forward, Vector3.back)) < .5f, "Swerve exits along the wall instead of reversing");
            Check(manta.Speed > settings.minimumSpeed + 10, "Impact turnaround preserves useful exit momentum");
            Check(body.position.z < Origin.z + 33.5f, "Impact response never crosses the wall");
            // Repeat during the recovery interval without resetting the maneuver state.
            Place(40, 0, false); settings.environmentMask = 1; Advance(.08f);
            Check(tricks.ImpactCount == 1, "Impact cooldown prevents an immediate chained turnaround");
            ClearObstacles();

            Place(12); Wall(Origin + Vector3.forward * 5, Quaternion.identity); settings.environmentMask = 1; Advance(.8f);
            Check(tricks.ImpactCount == 0 && body.position.z < Origin.z + 4, "Low-speed contact retains the previous collision behavior");
            ClearObstacles();
            Place(35, 70); Wall(Origin + Vector3.forward * 8, Quaternion.identity); settings.environmentMask = 1; Advance(1);
            Check(tricks.ImpactCount == 0, "Glancing impact below the angle threshold stays a slide");
            ClearObstacles();
            Place(35); Wall(Origin + Vector3.forward * 6, Quaternion.identity); settings.environmentMask = 1; settings.impactTurn.layers = 0; Advance(.5f);
            Check(tricks.ImpactCount == 0, "Impact layers can exclude a colliding surface"); settings.impactTurn.layers = 1;
            ClearObstacles();
            Place(35); Wall(Origin + Vector3.forward * 6, Quaternion.identity); settings.environmentMask = 1; settings.impactTurn.requiredTag = "Player"; Advance(.5f);
            Check(tricks.ImpactCount == 0, "Optional tag filter excludes unmatched surfaces"); settings.impactTurn.requiredTag = "";
            ClearObstacles();

            Place(35, 45);
            Wall(Origin + Vector3.forward * 9, Quaternion.identity); Wall(Origin + Vector3.right * 9, Quaternion.Euler(0, 90, 0));
            settings.environmentMask = 1; Advance(1.3f);
            Check(tricks.ImpactCount == 1 && body.position.x < Origin.x + 8 && body.position.z < Origin.z + 8, "Corner impact escapes without overlapping walls or restarting");
            ClearObstacles();
            Place(40);
            Wall(Origin + Vector3.forward * 8, Quaternion.identity); Wall(Origin - Vector3.forward * 7, Quaternion.identity);
            settings.environmentMask = 1; Advance(1);
            Check(tricks.ImpactCount == 1 && body.position.z > Origin.z - 6 && body.position.z < Origin.z + 7, "Two successive surfaces redirect the same turnaround safely");
            ClearObstacles();
            Place(35); Quaternion slope = Quaternion.Euler(40, 0, 0);
            Wall(Origin + Vector3.forward * 8, slope); settings.environmentMask = 1; Advance(1.3f);
            Check(tricks.ImpactCount == 1 && Vector3.Dot(manta.Velocity.normalized, slope * Vector3.back) > .2f, "Sloped surface produces an outward escape");
            ClearObstacles();
            Place(35); tricks.TryStart(MantaTrick.LoopBackward, manta); Advance(.25f);
            Vector3 direction = manta.Velocity.normalized;
            Wall(body.position + direction * 4, Quaternion.LookRotation(direction)); settings.environmentMask = 1; Advance(.4f);
            Check(tricks.ImpactCount == 1 && !tricks.IsLooping, "Contact during a looping replaces it with the impact turnaround");
            ClearObstacles();
        }
        static void ValidateExport(MantaInput input)
        {
            string path = null;
            try
            {
                settings.loopCamera.distance = 9.7f; settings.swerve.lookAheadSeconds = 1.33f; settings.swerve.cameraOffset = 1.7f;
                settings.acceleration = 31.25f; settings.forwardLoop.duration = 2.35f; settings.backwardLoop.middleSpeedBoost = .31f;
                settings.forwardLoop.rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
                settings.impactTurn.duration = .67f; settings.impactTurn.maximumAngleFromNormal = 43;
                input.mouseSensitivity = .071f; input.stickDeadZone = .19f;
                var exported = MantaTuningExporter.ExportNew(manta); path = AssetDatabase.GetAssetPath(exported);
                settings.forwardLoop.duration = 4; settings.forwardLoop.rotationCurve.MoveKey(1, new Keyframe(1, .8f));
                Check(exported.forwardLoop.duration == 2.35f && exported.forwardLoop.rotationCurve.Evaluate(1) == 1, "New export deep-copies loop values and curves");
                Check(exported.acceleration == 31.25f && exported.impactTurn.duration == .67f && exported.backwardLoop.middleSpeedBoost == .31f, "Export contains old controller feel and both new maneuver settings");
                Check(exported.loopCamera.distance == 9.7f && exported.swerve.lookAheadSeconds == 1.33f && exported.swerve.cameraOffset == 1.7f, "Export includes loop camera and predictive swerve feel");
                Check(exported.useProfileInput && exported.input.mouseSensitivity == .071f && exported.input.stickDeadZone == .19f, "Export includes live input sensitivity and dead zones");
                settings.impactTurn.duration = .91f; MantaTuningExporter.WriteToAsset(manta, exported);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<MantaFlightSettings>(path);
                Check(reloaded.impactTurn.duration == .91f && reloaded.forwardLoop.duration == 4, "Overwrite is persisted to the ScriptableObject on disk");
                Check(EditorUtility.IsPersistent(manta.SourceSettings), "Original settings asset remains available as the overwrite destination");
            }
            finally { if (path != null) AssetDatabase.DeleteAsset(path); }
        }
    }
}
