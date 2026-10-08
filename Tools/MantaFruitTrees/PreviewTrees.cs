var preview = new UnityEditor.PreviewRenderUtility();
var report = new System.Collections.Generic.List<string>();
try
{
    int index = 0;
    foreach (MantaFlight.MantaStat stat in System.Enum.GetValues(typeof(MantaFlight.MantaStat)))
    {
        var asset = UnityEngine.Resources.Load<UnityEngine.GameObject>(MantaFlight.MantaFruitTree.ResourcePath(stat));
        if (UnityEditor.PrefabUtility.GetPrefabAssetType(asset) != UnityEditor.PrefabAssetType.Variant) throw new System.Exception(stat + " is not a prefab variant");
        var tree = UnityEngine.Object.Instantiate(asset);
        preview.AddSingleGO(tree);
        tree.transform.position = new UnityEngine.Vector3((index++ - 2) * 36, 0, 0);
        var renderers = tree.GetComponentsInChildren<UnityEngine.Renderer>();
        var bounds = renderers[0].bounds;
        bool leaves = false;
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
            foreach (var material in renderer.sharedMaterials)
                if (material.HasProperty("_LeafColor"))
                {
                    leaves = true;
                    if (material.GetColor("_LeafColor") != MantaFlight.MantaStatStyle.Color(stat)) throw new System.Exception(stat + " color mismatch");
                }
        }
        if (!leaves || UnityEngine.Mathf.Abs(bounds.size.y - 32) > .01f) throw new System.Exception(stat + " bounds/leaves mismatch " + bounds.size);
        if (tree.GetComponentInChildren<UnityEngine.MeshCollider>() == null || tree.GetComponent<MantaFlight.MantaFruitTree>().fruitAnchor == null) throw new System.Exception(stat + " missing collider or anchor");
        report.Add(stat + ": variant, 32 m, matching leaves, trunk collision and fruit anchor OK");
    }
    preview.camera.transform.position = new UnityEngine.Vector3(0, 40, -160);
    preview.camera.transform.LookAt(new UnityEngine.Vector3(0, 15, 0));
    preview.camera.orthographic = true;
    preview.camera.orthographicSize = 30;
    preview.camera.nearClipPlane = .1f;
    preview.camera.farClipPlane = 300;
    preview.camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
    preview.camera.backgroundColor = new UnityEngine.Color(.085f, .12f, .16f, 1);
    preview.lights[0].transform.rotation = UnityEngine.Quaternion.Euler(45, -40, 0);
    preview.lights[0].intensity = 1.2f;
    preview.lights[1].intensity = .3f;
    preview.BeginPreview(new UnityEngine.Rect(0, 0, 2000, 650), UnityEngine.GUIStyle.none);
    preview.Render(true);
    var output = preview.EndPreview();
    var rt = UnityEngine.RenderTexture.GetTemporary(2000, 650, 0, UnityEngine.RenderTextureFormat.ARGB32);
    UnityEngine.Graphics.Blit(output, rt);
    var previous = UnityEngine.RenderTexture.active;
    UnityEngine.RenderTexture.active = rt;
    var image = new UnityEngine.Texture2D(2000, 650, UnityEngine.TextureFormat.RGBA32, false);
    image.ReadPixels(new UnityEngine.Rect(0, 0, 2000, 650), 0, 0); image.Apply();
    System.IO.File.WriteAllBytes("Tools/MantaFruitTrees/FruitTrees-preview.png", UnityEngine.ImageConversion.EncodeToPNG(image));
    UnityEngine.RenderTexture.active = previous;
    UnityEngine.RenderTexture.ReleaseTemporary(rt);
    UnityEngine.Object.DestroyImmediate(image);
}
finally { preview.Cleanup(); }
return string.Join("\n", report);
