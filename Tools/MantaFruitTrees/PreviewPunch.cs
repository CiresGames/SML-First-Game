var preview = new UnityEditor.PreviewRenderUtility();
var report = new System.Collections.Generic.List<string>();
var bakedMeshes = new System.Collections.Generic.List<UnityEngine.Mesh>();
try
{
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/MantaFlight/Rider/Prefabs/RiderCharacter.prefab");
    var clip = UnityEngine.Resources.Load<UnityEngine.AnimationClip>("RiderAnimations/TreePunch");
    float[] times = { .21f, .34f, .72f };
    for (int i = 0; i < times.Length; i++)
    {
        var rider = UnityEngine.Object.Instantiate(prefab); preview.AddSingleGO(rider);
        rider.transform.position = new UnityEngine.Vector3((i - 1) * 1.8f, 0, 0);
        rider.transform.rotation = UnityEngine.Quaternion.Euler(0, 70, 0);
        var animator = rider.GetComponentInChildren<UnityEngine.Animator>(); animator.applyRootMotion = false;
        animator.Rebind(); animator.Update(0); clip.SampleAnimation(animator.gameObject, times[i]);
        var hand = animator.GetBoneTransform(UnityEngine.HumanBodyBones.RightHand);
        report.Add(times[i] + ": right fist in rider space " + rider.transform.InverseTransformPoint(hand.position));
        foreach (var skin in rider.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>())
        {
            var baked = new UnityEngine.Mesh(); bakedMeshes.Add(baked); skin.BakeMesh(baked);
            var copy = new UnityEngine.GameObject("Baked punch pose", typeof(UnityEngine.MeshFilter), typeof(UnityEngine.MeshRenderer));
            copy.transform.SetParent(skin.transform, false); copy.GetComponent<UnityEngine.MeshFilter>().sharedMesh = baked;
            copy.GetComponent<UnityEngine.MeshRenderer>().sharedMaterials = skin.sharedMaterials; skin.enabled = false;
        }
    }
    preview.camera.transform.position = new UnityEngine.Vector3(0, 1.4f, -8);
    preview.camera.transform.LookAt(new UnityEngine.Vector3(0, .9f, 0));
    preview.camera.orthographic = true; preview.camera.orthographicSize = 1.6f;
    preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 30;
    preview.camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
    preview.camera.backgroundColor = new UnityEngine.Color(.085f, .12f, .16f, 1);
    preview.lights[0].transform.rotation = UnityEngine.Quaternion.Euler(40, -40, 0); preview.lights[0].intensity = 1.4f;
    preview.lights[1].intensity = .5f;
    preview.BeginPreview(new UnityEngine.Rect(0, 0, 1800, 900), UnityEngine.GUIStyle.none); preview.Render(true);
    var output = preview.EndPreview();
    var rt = UnityEngine.RenderTexture.GetTemporary(1800, 900, 0, UnityEngine.RenderTextureFormat.ARGB32);
    UnityEngine.Graphics.Blit(output, rt); var previous = UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active = rt;
    var image = new UnityEngine.Texture2D(1800, 900, UnityEngine.TextureFormat.RGBA32, false);
    image.ReadPixels(new UnityEngine.Rect(0, 0, 1800, 900), 0, 0); image.Apply();
    System.IO.File.WriteAllBytes("Tools/MantaFruitTrees/Punch-preview.png", UnityEngine.ImageConversion.EncodeToPNG(image));
    UnityEngine.RenderTexture.active = previous; UnityEngine.RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image);
}
finally { preview.Cleanup(); foreach (var mesh in bakedMeshes) UnityEngine.Object.DestroyImmediate(mesh); }
return string.Join("\n", report);
