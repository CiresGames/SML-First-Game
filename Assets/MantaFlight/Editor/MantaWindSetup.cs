using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaWindSetup
    {
        [MenuItem("Manta/Environment/Set up wind zones")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != MantaPrototypeBuilder.ScenePath) throw new InvalidOperationException("Open MantaFlight first.");
            var manager = UnityEngine.Object.FindFirstObjectByType<WindManager>();
            if (manager) { Selection.activeGameObject = manager.gameObject; return; }
            var root = new GameObject("Wind zones"); Undo.RegisterCreatedObjectUndo(root, "Install wind zones");
            manager = root.AddComponent<WindManager>();
            var weather = root.AddComponent<WindWeatherAdapter>();
            weather.manager = manager; weather.clouds = UnityEngine.Object.FindFirstObjectByType<MantaCloudscape>();
            var camera = Camera.main;
            var rider = UnityEngine.Object.FindFirstObjectByType<Rider.RiderController>();
            var manta = UnityEngine.Object.FindFirstObjectByType<MantaController>();
            Vector3 origin = rider ? rider.transform.position : manta ? manta.transform.position : Vector3.zero;
            const string path = "Assets/MantaFlight/Materials/Wind streak.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Manta/Wind streak");
                if (!shader) throw new InvalidOperationException("Wind shader has not imported.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            CreateBox(root.transform, "Prevailing wind", origin + new Vector3(0, 300, 350),
                new Vector3(2400, 1400, 2600), new Vector3(1, .03f, .35f), 8, 160, material, camera);
            var lift = CreateBox(root.transform, "Rising current", origin + new Vector3(0, 230, 500),
                new Vector3(550, 900, 1100), new Vector3(.2f, 1, .3f), 22, 100, material, camera);
            lift.weight = 3;
            var cross = CreateBox(root.transform, "Crosswind", origin + new Vector3(900, 350, 500),
                new Vector3(1100, 1400, 1800), new Vector3(-.3f, .08f, 1), 16, 140, material, camera);
            cross.weight = 2;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
        }

        static WindZone3D CreateBox(Transform parent, string name, Vector3 center, Vector3 size,
            Vector3 direction, float strength, float falloff, Material material, Camera camera)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = center;
            var collider = go.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = size;
            var zone = go.AddComponent<WindZone3D>(); zone.volume = collider; zone.direction = direction.normalized;
            zone.strength = strength; zone.falloff = falloff; zone.noiseSeed = center.x * .013f + 19;
            var visual = new GameObject("Local wind particles"); visual.transform.SetParent(go.transform, false);
            var particles = visual.AddComponent<WindZoneParticles>();
            particles.zone = zone; particles.material = material; particles.viewCamera = camera;
            particles.Configure();
            return zone;
        }

        // Entry point for the installed Unity Editor in batch mode; no CLI or package install required.
        public static void InstallAndValidateBatch()
        {
            try
            {
                Debug.Log(MantaWindValidation.Run());
                EditorSceneManager.OpenScene(MantaPrototypeBuilder.ScenePath);
                Install();
                var shader = Shader.Find("Manta/Wind streak");
                if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Wind shader compilation failed.");
                Debug.Log("WIND_SETUP_AND_VALIDATION_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
