var mat=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/MantaFlight/Trees/Shaders/ReiLeaves_Green.mat");
var model=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Tree 1/Rei_treeLeavesGN.fbx");
var preview=new UnityEditor.PreviewRenderUtility();
try {
    var tree=UnityEngine.Object.Instantiate(model);
    tree.name="ReiTree_ShaderGraph";
    preview.AddSingleGO(tree);
    foreach(var r in tree.GetComponentsInChildren<UnityEngine.Renderer>(true)) {
        if(r.name.ToLowerInvariant().Contains("leaves")) r.sharedMaterial=mat;
        else r.sharedMaterial=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Tree 1/trunk.mat");
    }
    System.IO.Directory.CreateDirectory("Assets/MantaFlight/Trees/Prefabs");
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(tree,"Assets/MantaFlight/Trees/Prefabs/ReiTree_ShaderGraph.prefab");
    preview.camera.transform.position=new UnityEngine.Vector3(10,7,-15);
    preview.camera.transform.LookAt(new UnityEngine.Vector3(0,3.5f,0));
    preview.camera.orthographic=true;
    preview.camera.orthographicSize=4.4f;
    preview.camera.nearClipPlane=.1f;
    preview.camera.farClipPlane=80;
    preview.camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
    preview.camera.backgroundColor=new UnityEngine.Color(.085f,.12f,.16f,1);
    preview.lights[0].transform.rotation=UnityEngine.Quaternion.Euler(45,-40,0);
    preview.lights[0].intensity=1.2f;
    preview.lights[0].shadows=UnityEngine.LightShadows.Soft;
    preview.lights[1].intensity=0;
    preview.BeginPreview(new UnityEngine.Rect(0,0,1024,1024),UnityEngine.GUIStyle.none);
    preview.Render(true);
    var output=preview.EndPreview();
    var rt=UnityEngine.RenderTexture.GetTemporary(1024,1024,0,UnityEngine.RenderTextureFormat.ARGB32);
    UnityEngine.Graphics.Blit(output,rt);
    var previous=UnityEngine.RenderTexture.active;
    UnityEngine.RenderTexture.active=rt;
    var image=new UnityEngine.Texture2D(1024,1024,UnityEngine.TextureFormat.RGBA32,false);
    image.ReadPixels(new UnityEngine.Rect(0,0,1024,1024),0,0); image.Apply();
    System.IO.File.WriteAllBytes("Tools/ReiLeafShader/ReiTree_Preview.png",image.EncodeToPNG());
    UnityEngine.RenderTexture.active=previous;
    UnityEngine.RenderTexture.ReleaseTemporary(rt);
    UnityEngine.Object.DestroyImmediate(image);
    return UnityEditor.ShaderUtil.GetShaderMessages(mat.shader).Select(m=>new{m.message,m.severity}).ToArray();
} finally {preview.Cleanup();}
