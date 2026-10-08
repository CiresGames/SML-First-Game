using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaFruitTreeBuilder
    {
        const string Source = "Assets/MantaFlight/Trees/Prefabs/ReiTree_ShaderGraph.prefab";
        const string Prefabs = "Assets/MantaFlight/Resources/MantaFruitTrees";
        const string Materials = "Assets/MantaFlight/Trees/Materials/FruitTrees";
        const float Height = 32;

        [MenuItem("Manta/Progression/Build colored fruit trees")]
        public static void BuildMenu() => Debug.Log(Build());
        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Build tree assets outside Play Mode.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null) throw new InvalidOperationException("Missing source Rei tree prefab.");
            EnsureFolder(Prefabs); EnsureFolder(Materials);
            var scene = EditorSceneManager.NewPreviewScene();
            var report = new List<string>();
            try
            {
                foreach (MantaStat stat in Enum.GetValues(typeof(MantaStat)))
                {
                    var tree = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                    try
                    {
                        tree.name = stat + " Fruit Tree";
                        var renderers = tree.GetComponentsInChildren<Renderer>();
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        float factor = Height / bounds.size.y;
                        tree.transform.localScale *= factor;
                        Color color = MantaStatStyle.Color(stat);
                        bool foundLeaves = false;
                        foreach (var renderer in renderers)
                        {
                            var slots = renderer.sharedMaterials;
                            for (int i = 0; i < slots.Length; i++)
                            {
                                if (slots[i] == null || !slots[i].HasProperty("_LeafColor")) continue;
                                foundLeaves = true;
                                string path = Materials + "/" + stat + "Leaves.mat";
                                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                                if (material == null) { material = new Material(slots[i]); AssetDatabase.CreateAsset(material, path); }
                                else material.CopyPropertiesFromMaterial(slots[i]);
                                material.name = stat + " Leaves";
                                material.SetColor("_LeafColor", color);
                                material.SetColor("_LeafVariation", Color.Lerp(color, Color.white, .12f));
                                material.SetColor("_ShadowTint", new Color(.48f, .48f, .48f, 1));
                                material.SetColor("_LitTint", new Color(1.04f, 1.04f, 1.04f, 1));
                                material.SetColor("_RimTint", Color.Lerp(color, Color.white, .35f));
                                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); slots[i] = material;
                            }
                            renderer.sharedMaterials = slots;
                            // Only solid wood obstructs movement; the card-based leaf canopy remains permeable.
                            if (renderer.name.IndexOf("trunk", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                var mesh = renderer.GetComponent<MeshFilter>();
                                if (mesh && renderer.GetComponent<Collider>() == null)
                                    renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                            }
                        }
                        if (!foundLeaves) throw new InvalidOperationException("Rei tree has no recognizable leaf material.");
                        var marker = tree.AddComponent<MantaFruitTree>(); marker.stat = stat;
                        var anchor = new GameObject("Fruit Anchor").transform;
                        anchor.SetParent(tree.transform, false); anchor.localPosition = new Vector3(2.4f, 2.8f, 0);
                        marker.fruitAnchor = anchor;
                        string prefabPath = Prefabs + "/" + stat + ".prefab";
                        PrefabUtility.SaveAsPrefabAsset(tree, prefabPath);
                        report.Add(stat + ": 32 m tall, leaves #" + MantaStatStyle.Get(stat).hex);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(tree); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return string.Join("\n", report);
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
