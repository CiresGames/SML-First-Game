using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaCloudDistributionSetup
    {
        [MenuItem("Manta/Environment/Use procedural cloud distribution")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=MantaPrototypeBuilder.ScenePath) throw new System.InvalidOperationException("Open MantaFlight first.");
            var clouds=Object.FindFirstObjectByType<MantaCloudscape>();
            if(!clouds) throw new System.InvalidOperationException("Add the cloudscape first.");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/MantaFlight/Materials/Cinematic cloud volume.mat");
            if(!material) throw new System.InvalidOperationException("Cloud volume material is missing.");
            Configure(clouds,material);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        public static void Configure(MantaCloudscape clouds,Material material)
        {
            Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Procedural cloud distribution");
            var distribution=clouds.GetComponent<MantaCloudDistribution>();
            if(!distribution) distribution=Undo.AddComponent<MantaCloudDistribution>(clouds.gameObject);
            Undo.RecordObject(distribution,"Configure cloud generator");
            distribution.volumeMaterial=material;
            if(!distribution.volumeMesh)
            {
                var temporary=GameObject.CreatePrimitive(PrimitiveType.Cube);
                distribution.volumeMesh=temporary.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(temporary);
            }
            // Migrate only the original authored banks. Other user-authored children are preserved.
            var oldNames=new[] {"01 • western cathedral","02 • sunlit eastern crest","03 • far horizon billows",
                "04 • high crossing veil","05 • southern drift","06 • western horizon","07 • eastern horizon"};
            for(int i=clouds.transform.childCount-1;i>=0;i--)
            {
                var child=clouds.transform.GetChild(i);
                var renderer=child.GetComponent<MeshRenderer>();
                if(System.Array.IndexOf(oldNames,child.name)>=0 && renderer && renderer.sharedMaterial==material)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
            Undo.RecordObject(clouds.gameObject,"Name procedural cloudscape");
            clouds.gameObject.name="Cloudscape • procedural weather";
            distribution.Regenerate(); EditorUtility.SetDirty(distribution);
            Selection.activeGameObject=clouds.gameObject;
            Debug.Log("MANTA CLOUDS: "+distribution.GenerationStatus);
        }

        [MenuItem("Manta/Environment/Cloud layout/Regenerate current seed")]
        static void Regenerate()
        {
            var distribution=Object.FindFirstObjectByType<MantaCloudDistribution>();
            if(distribution) { distribution.Regenerate(); Selection.activeGameObject=distribution.gameObject; }
        }

        [MenuItem("Manta/Environment/Cloud layout/Try another seed")]
        static void Reseed()
        {
            var distribution=Object.FindFirstObjectByType<MantaCloudDistribution>();
            if(!distribution) return;
            Undo.RecordObject(distribution,"Change cloud distribution seed");
            distribution.seed=unchecked(distribution.seed*1664525+1013904223);
            distribution.Regenerate(); EditorUtility.SetDirty(distribution);
            Selection.activeGameObject=distribution.gameObject;
        }
    }
}
