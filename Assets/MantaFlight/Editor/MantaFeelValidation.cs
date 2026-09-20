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

        [MenuItem("Manta/Validate swerves and tuning export (Play mode)")]
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
                // These obstacle fixtures target the default probe range, independently of the player's tuning.
                settings.swerve = new MantaImpactSettings();
                ValidateImpacts(); ValidateExport(input);
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
        }
        static void ValidateExport(MantaInput input)
        {
            string path = null;
            try
            {
                settings.cameraDistance = 9.7f; settings.swerve.lookAheadSeconds = 1.33f; settings.swerve.cameraOffset = 1.7f;
                settings.acceleration = 31.25f;
                settings.swerve.rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
                settings.impactTurn.duration = .67f; settings.impactTurn.maximumAngleFromNormal = 43;
                input.mouseSensitivity = .071f; input.stickDeadZone = .19f;
                var exported = MantaTuningExporter.ExportNew(manta); path = AssetDatabase.GetAssetPath(exported);
                settings.acceleration = 34; settings.swerve.rotationCurve.MoveKey(1, new Keyframe(1, .8f));
                Check(exported.acceleration == 31.25f && exported.swerve.rotationCurve.Evaluate(1) == 1, "New export deep-copies values and swerve curves");
                Check(exported.acceleration == 31.25f && exported.impactTurn.duration == .67f, "Export contains old controller feel and swerve settings");
                Check(exported.cameraDistance == 9.7f && exported.swerve.lookAheadSeconds == 1.33f && exported.swerve.cameraOffset == 1.7f, "Export includes camera and predictive swerve feel");
                Check(exported.useProfileInput && exported.input.mouseSensitivity == .071f && exported.input.stickDeadZone == .19f, "Export includes live input sensitivity and dead zones");
                settings.impactTurn.duration = .91f; MantaTuningExporter.WriteToAsset(manta, exported);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<MantaFlightSettings>(path);
                Check(reloaded.impactTurn.duration == .91f && reloaded.acceleration == 34, "Overwrite is persisted to the ScriptableObject on disk");
                Check(EditorUtility.IsPersistent(manta.SourceSettings), "Original settings asset remains available as the overwrite destination");
            }
            finally { if (path != null) AssetDatabase.DeleteAsset(path); }
        }
    }
}
