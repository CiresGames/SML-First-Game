var previousScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;
UnityEditor.ShaderUtil.allowAsyncCompilation=false;
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.RenderTexture target=null;
try {
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
    var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/MantaFlight/Trees/Prefabs/ReiTree_ShaderGraph.prefab");
    var tree=UnityEngine.Object.Instantiate(prefab);
    foreach(var t in tree.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.layer=31;
    var sun=new UnityEngine.GameObject("Preview sunlight").AddComponent<UnityEngine.Light>();
    sun.type=UnityEngine.LightType.Directional; sun.intensity=1.5f; sun.color=new UnityEngine.Color(1,.96f,.85f);sun.shadows=UnityEngine.LightShadows.Soft;sun.cullingMask=1<<31;
    sun.transform.rotation=UnityEngine.Quaternion.Euler(45,-45,0);
    UnityEngine.RenderSettings.sun=sun;UnityEngine.RenderSettings.fog=false;
    UnityEngine.RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;UnityEngine.RenderSettings.ambientLight=new UnityEngine.Color(.35f,.4f,.45f);
    var camera=new UnityEngine.GameObject("Preview camera").AddComponent<UnityEngine.Camera>();
    camera.enabled=false;camera.cullingMask=1<<31;
    camera.transform.position=new UnityEngine.Vector3(10,6,-15);camera.transform.LookAt(new UnityEngine.Vector3(0,3.5f,0));
    camera.orthographic=true;camera.orthographicSize=4.2f;camera.nearClipPlane=.1f;camera.farClipPlane=80;
    camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.085f,.12f,.16f,1);
    target=new UnityEngine.RenderTexture(1024,1024,24,UnityEngine.RenderTextureFormat.ARGB32,UnityEngine.RenderTextureReadWrite.sRGB);
    camera.targetTexture=target;
    camera.Render();
    var previous=UnityEngine.RenderTexture.active;UnityEngine.RenderTexture.active=target;
    var image=new UnityEngine.Texture2D(1024,1024,UnityEngine.TextureFormat.RGBA32,false);
    image.ReadPixels(new UnityEngine.Rect(0,0,1024,1024),0,0);image.Apply();
    System.IO.File.WriteAllBytes("Tools/ReiLeafShader/ReiTree_Preview.png",image.EncodeToPNG());
    UnityEngine.RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);
    var shader=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>("Assets/MantaFlight/Trees/Shaders/ReiLeaves.shadergraph");
    return new{messages=UnityEditor.ShaderUtil.GetShaderMessages(shader).Select(m=>new{m.message,m.severity}).ToArray(),renderers=tree.GetComponentsInChildren<UnityEngine.Renderer>().Select(r=>new{r.name,bounds=r.bounds.ToString(),r.isVisible}).ToArray()};
} finally {
    UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
    if(target) UnityEngine.Object.DestroyImmediate(target);
}
