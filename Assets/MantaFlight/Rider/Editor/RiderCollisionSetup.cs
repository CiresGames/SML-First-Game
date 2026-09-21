using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace MantaFlight.Rider.Editor
{
    public static class RiderCollisionSetup
    {
        [MenuItem("Manta/Rider/Install manta collision shell")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var manta = Object.FindFirstObjectByType<MantaController>();
            var body = manta.GetComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.Interpolate;
            var combines = new System.Collections.Generic.List<CombineInstance>();
            var baked = new System.Collections.Generic.List<Mesh>();
            foreach (var skin in manta.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skin.enabled) continue;
                var mesh = new Mesh(); skin.BakeMesh(mesh, true); baked.Add(mesh);
                combines.Add(new CombineInstance { mesh = mesh, transform = manta.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix });
            }
            foreach (var filter in manta.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                combines.Add(new CombineInstance { mesh = filter.sharedMesh, transform = manta.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            var hull = new Mesh { name = "Manta collision shell", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            hull.CombineMeshes(combines.ToArray(), true, true);
            const string path = "Assets/MantaFlight/Rider/Settings/MantaCollisionShell.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(hull, path); saved = hull; }
            else { EditorUtility.CopySerialized(hull, saved); Object.DestroyImmediate(hull); EditorUtility.SetDirty(saved); }
            foreach (var mesh in baked) Object.DestroyImmediate(mesh);
            var shell = manta.transform.Find("Manta collision shell");
            if (shell == null) { shell = new GameObject("Manta collision shell").transform; shell.SetParent(manta.transform, false); }
            shell.gameObject.layer = manta.gameObject.layer;
            var collider = shell.GetComponent<MeshCollider>();
            if (collider == null) collider = shell.gameObject.AddComponent<MeshCollider>();
            // Static local shell under the kinematic root: visual wing motion never writes collider poses.
            collider.sharedMesh = saved; collider.convex = false; collider.isTrigger = false;
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(manta.gameObject.scene);
            EditorSceneManager.SaveScene(manta.gameObject.scene);
        }
    }
}
