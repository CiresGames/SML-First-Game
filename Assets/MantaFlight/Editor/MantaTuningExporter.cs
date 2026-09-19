using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaTuningExporter
    {
        [MenuItem("Manta/Tuning/Select active settings")]
        public static void SelectSettings() => Selection.activeObject = ResolveController().settings;

        [MenuItem("Manta/Tuning/Save runtime as new profile")]
        public static void SaveNewFromMenu()
        {
            var controller = ResolveController();
            var profile = ExportNew(controller);
            Selection.activeObject = profile;
            Debug.Log("MANTA: saved " + AssetDatabase.GetAssetPath(profile) + ". Assign this profile to the controller outside Play mode to reuse it.");
        }

        [MenuItem("Manta/Tuning/Write runtime to source profile")]
        public static void WriteSourceFromMenu()
        {
            var controller = ResolveController();
            var source = controller.SourceSettings;
            if (!EditorApplication.isPlaying || source == null || !EditorUtility.IsPersistent(source))
                throw new InvalidOperationException("Start Play mode with an asset assigned to MantaController.settings first.");
            WriteToAsset(controller, source);
            Selection.activeObject = source;
            Debug.Log("MANTA: runtime tuning written to " + AssetDatabase.GetAssetPath(source) + ". The asset is saved and Undo is available.");
        }

        public static MantaFlightSettings ExportNew(MantaController controller, string folder = "Assets/MantaFlight/Settings")
        {
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains(".."))
                throw new ArgumentException("Export folder must be inside Assets.");
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var snapshot = Capture(controller);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/FlightTuning.asset");
            AssetDatabase.CreateAsset(snapshot, path); AssetDatabase.SaveAssetIfDirty(snapshot);
            return snapshot;
        }

        public static void WriteToAsset(MantaController controller, MantaFlightSettings destination)
        {
            if (destination == null || !EditorUtility.IsPersistent(destination))
                throw new ArgumentException("Destination must be a saved MantaFlightSettings asset.");
            var snapshot = Capture(controller);
            string originalName = destination.name;
            try
            {
                Undo.RecordObject(destination, "Export manta runtime tuning");
                EditorUtility.CopySerialized(snapshot, destination); destination.name = originalName;
                EditorUtility.SetDirty(destination); AssetDatabase.SaveAssetIfDirty(destination);
            }
            finally { UnityEngine.Object.DestroyImmediate(snapshot); }
        }

        static MantaFlightSettings Capture(MantaController controller)
        {
            if (controller == null || controller.settings == null) throw new ArgumentException("A MantaController with settings is required.");
            var snapshot = UnityEngine.Object.Instantiate(controller.settings);
            snapshot.name = "FlightTuning"; snapshot.hideFlags = HideFlags.None;
            snapshot.input = new MantaInputSettings();
            snapshot.input.Capture(controller.GetComponent<MantaInput>()); snapshot.useProfileInput = true;
            return snapshot;
        }

        static MantaController ResolveController()
        {
            if (Selection.activeGameObject != null)
            {
                var selected = Selection.activeGameObject.GetComponentInParent<MantaController>();
                if (selected != null) return selected;
            }
            var controllers = UnityEngine.Object.FindObjectsByType<MantaController>(FindObjectsSortMode.None);
            if (controllers.Length == 1) return controllers[0];
            throw new InvalidOperationException("Select the manta whose runtime tuning should be exported.");
        }
    }
}
