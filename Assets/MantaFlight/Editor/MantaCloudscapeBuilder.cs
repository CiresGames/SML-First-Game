using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MantaFlight.Editor
{
    public static class MantaCloudscapeBuilder
    {
        [MenuItem("Manta/Environment/Add cinematic procedural clouds")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != MantaPrototypeBuilder.ScenePath)
                throw new System.InvalidOperationException("Open MantaFlight before adding clouds.");
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<MantaCloudscape>()) { Selection.activeGameObject = root; return; }
            var shape = AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/Plugins/ProceduralClouds/Resources/Flight Demo_ShapeNoise.asset");
            var detail = AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/Plugins/ProceduralClouds/Resources/Flight Demo_DetailNoise.asset");
            var shader = Shader.Find("Manta/Procedural Cloud Volume");
            if (!shape || !detail || !shader) throw new System.InvalidOperationException("Cloud shader or package textures missing.");
            const string materialPath = "Assets/MantaFlight/Materials/Cinematic cloud volume.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(shader) { name = "Cinematic cloud volume" };
                material.SetTexture("_ShapeNoise",shape); material.SetTexture("_DetailNoise",detail);
                AssetDatabase.CreateAsset(material,materialPath);
            }
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add cinematic cloudscape");
            var sky = new GameObject("Cloudscape • sunlit procession");
            Undo.RegisterCreatedObjectUndo(sky,"Create cloudscape");
            var cloudscape = sky.AddComponent<MantaCloudscape>();
            cloudscape.shapeNoise = shape; cloudscape.detailNoise = detail;
            MantaCloudDistributionSetup.Configure(cloudscape,material);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Distant cloud") { Undo.RecordObject(t.gameObject,"Replace placeholder clouds"); t.gameObject.SetActive(false); }
                    var camera = t.GetComponent<Camera>();
                    if (camera)
                    {
                        var data = camera.GetUniversalAdditionalCameraData();
                        Undo.RecordObject(data,"Enable cloud depth occlusion"); data.requiresDepthTexture = true;
                        Undo.RecordObject(camera,"Extend cloud horizon"); camera.farClipPlane = Mathf.Max(camera.farClipPlane,5000);
                    }
                }
            cloudscape.Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = sky;
            Debug.Log("MANTA CLOUDSCAPE: seeded procedural cloud distribution added; package noise textures connected.");
        }
    }
}
