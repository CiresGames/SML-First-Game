using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MantaFlight.Editor
{
    public static class MantaLightingSetup
    {
        [MenuItem("Manta/Environment/Set up day night and god rays")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=MantaPrototypeBuilder.ScenePath) throw new System.InvalidOperationException("Open MantaFlight first.");
            var pipeline=UniversalRenderPipeline.asset;
            if(!pipeline) throw new System.InvalidOperationException("URP is required.");
            var cycle=Object.FindFirstObjectByType<MantaDayNightCycle>();
            if(!cycle)
            {
                var go=new GameObject("Sky • day night and atmosphere"); Undo.RegisterCreatedObjectUndo(go,"Create sky lighting");
                cycle=go.AddComponent<MantaDayNightCycle>();
            }
            Undo.RecordObject(cycle,"Configure day night cycle");
            if(!cycle.sun) cycle.sun=RenderSettings.sun;
            if(!cycle.sun) throw new System.InvalidOperationException("Assign the scene sun before setup.");
            if(!cycle.moon)
            {
                var moon=new GameObject("Moon • blue hour fill"); moon.transform.SetParent(cycle.transform);
                Undo.RegisterCreatedObjectUndo(moon,"Create moonlight"); cycle.moon=moon.AddComponent<Light>();
                cycle.moon.type=LightType.Directional;
            }
            cycle.sun.shadows=LightShadows.Soft; cycle.moon.shadows=LightShadows.Soft;
            if(!cycle.skyFill)
            {
                var fill=new GameObject("Sky bounce • soft ambient fill"); fill.transform.SetParent(cycle.transform);
                Undo.RegisterCreatedObjectUndo(fill,"Create atmospheric fill"); cycle.skyFill=fill.AddComponent<Light>();
                cycle.skyFill.type=LightType.Directional; cycle.skyFill.shadows=LightShadows.None;
            }
            cycle.sun.shadowStrength=.9f; cycle.moon.shadowStrength=.65f;
            cycle.sun.shadowBias=.05f; cycle.sun.shadowNormalBias=.3f;
            cycle.clouds=Object.FindFirstObjectByType<MantaCloudscape>();
            cycle.skyMaterial=Material("Assets/MantaFlight/Materials/Day night sky.mat","Manta/Day Night Sky");
            if(!cycle.grading) cycle.grading=cycle.GetComponent<Volume>() ?? cycle.gameObject.AddComponent<Volume>();
            const string path="Assets/MantaFlight/Settings/Cinematic sky grading.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,path);
                var tone=profile.Add<Tonemapping>(true); tone.mode.value=TonemappingMode.ACES;
                var bloom=profile.Add<Bloom>(true); bloom.threshold.value=1; bloom.intensity.value=.22f;
                bloom.scatter.value=.65f; bloom.highQualityFiltering.value=true;
                var color=profile.Add<ColorAdjustments>(true); color.postExposure.value=.1f; color.contrast.value=10; color.saturation.value=3;
                var vignette=profile.Add<Vignette>(true); vignette.intensity.value=.12f; vignette.smoothness.value=.55f;
                foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
            }
            cycle.grading.isGlobal=true; cycle.grading.priority=10; cycle.grading.sharedProfile=profile;
            var camera=Camera.main;
            Undo.RecordObject(camera,"Enable cinematic lighting"); camera.allowHDR=true;
            var data=camera.GetUniversalAdditionalCameraData(); Undo.RecordObject(data,"Enable post processing");
            data.renderPostProcessing=true; data.requiresDepthTexture=true; data.volumeLayerMask|=1<<cycle.gameObject.layer;
            var serialized=new SerializedObject(pipeline);
            int rendererIndex=serialized.FindProperty("m_DefaultRendererIndex").intValue;
            var renderer=(ScriptableRendererData)serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(rendererIndex).objectReferenceValue;
            MantaGodRayFeature feature=null;
            foreach(var existing in renderer.rendererFeatures) if(existing is MantaGodRayFeature found) feature=found;
            if(!feature)
            {
                feature=ScriptableObject.CreateInstance<MantaGodRayFeature>(); feature.name="Manta • sun shafts";
                AssetDatabase.AddObjectToAsset(feature,renderer); renderer.rendererFeatures.Add(feature);
            }
            feature.material=Material("Assets/MantaFlight/Materials/Sun shafts.mat","Hidden/Manta/God Rays");
            feature.SetActive(true); feature.Create(); renderer.SetDirty();
            Undo.RecordObject(pipeline,"Improve flight shadow range"); pipeline.supportsHDR=true;
            pipeline.shadowDistance=Mathf.Max(pipeline.shadowDistance,350);
            EditorUtility.SetDirty(pipeline); EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(feature);
            cycle.ApplyLighting();
            EditorUtility.SetDirty(cycle.skyMaterial); EditorUtility.SetDirty(profile); EditorUtility.SetDirty(cycle);
            foreach(var c in profile.components) EditorUtility.SetDirty(c);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject=cycle.gameObject;
        }
        static Material Material(string path,string shaderName)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material) return material;
            var shader=Shader.Find(shaderName);
            if(!shader) throw new System.InvalidOperationException("Missing shader: "+shaderName);
            material=new Material(shader); AssetDatabase.CreateAsset(material,path); return material;
        }
        public static void Preview(float hour)
        {
            var cycle=Object.FindFirstObjectByType<MantaDayNightCycle>();
            if(!cycle) throw new System.InvalidOperationException("Set up day/night first.");
            Undo.RecordObject(cycle,"Preview time of day"); cycle.SetTime(hour);
            Selection.activeGameObject=cycle.gameObject;
        }
        [MenuItem("Manta/Environment/Time of day/Dawn")] static void Dawn()=>Preview(6.5f);
        [MenuItem("Manta/Environment/Time of day/Noon")] static void Noon()=>Preview(12);
        [MenuItem("Manta/Environment/Time of day/Sunset")] static void Sunset()=>Preview(17);
        [MenuItem("Manta/Environment/Time of day/Night")] static void Night()=>Preview(0);
    }
}
