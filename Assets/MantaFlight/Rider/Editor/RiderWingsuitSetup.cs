using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MantaFlight.Rider.Editor
{
    public static class RiderWingsuitSetup
    {
        const string ModelPath="Assets/MantaFlight/Rider/Models/RiderWingsuit.fbx";
        const string PrefabPath="Assets/MantaFlight/Rider/Prefabs/RiderCharacter.prefab";
        [MenuItem("Manta/Rider/Install Blender wingsuit")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var importer=(ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.isReadable=true;importer.importAnimation=false;importer.SaveAndReimport();
            var materials=new[]{Material("Wingsuit Petrol",new Color(.025f,.17f,.20f)),Material("Wingsuit Amber",new Color(.95f,.40f,.075f)),Material("Wingsuit Dark Trim",new Color(.012f,.025f,.035f))};
            var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
            try { Configure(prefab.GetComponent<RiderVisuals>(),materials); PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath); }
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
            foreach(var visual in Object.FindObjectsByType<RiderVisuals>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                Configure(visual,materials); EditorSceneManager.MarkSceneDirty(visual.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }
        static Material Material(string name,Color color)
        {
            string path="Assets/MantaFlight/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",.28f);material.SetFloat("_Cull",0);
            material.doubleSidedGI=true;EditorUtility.SetDirty(material);return material;
        }
        static void Configure(RiderVisuals visual,Material[] materials)
        {
            visual.fallAnimationDelay=.45f;visual.fallAnimationDownSpeed=5;
            visual.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var suit=visual.GetComponent<RiderWingsuit>(); if(suit==null)suit=visual.gameObject.AddComponent<RiderWingsuit>();
            suit.visuals=visual;
            var root=visual.model.Find("Wingsuit");
            if(root==null)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),visual.model);
                go.name="Wingsuit";root=go.transform;root.localPosition=Vector3.zero;root.localRotation=Quaternion.identity;root.localScale=Vector3.one;
            }
            var filters=root.GetComponentsInChildren<MeshFilter>(true);
            suit.leftWing=filters.First(f=>f.name=="LeftWing");suit.rightWing=filters.First(f=>f.name=="RightWing");suit.legWing=filters.First(f=>f.name=="LegWing");
            foreach(var filter in filters)
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                // FBX slots are ordered by first use, not by the Blender material list.
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.First(x=>x.name==m.name)).ToArray();
                renderer.enabled=false;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            }
            EditorUtility.SetDirty(suit);EditorUtility.SetDirty(visual);
            if(PrefabUtility.IsPartOfPrefabInstance(visual))
            { PrefabUtility.RecordPrefabInstancePropertyModifications(visual);PrefabUtility.RecordPrefabInstancePropertyModifications(suit); }
        }
        public static void InstallBatch()
        {
            EditorSceneManager.OpenScene("Assets/MantaFlight/Scenes/MantaFlight.unity");
            Install();EditorSceneManager.SaveOpenScenes();Validate();
        }
        [MenuItem("Manta/Rider/Validate wingsuit and fall timing")]
        public static void Validate()
        {
            void Check(bool condition,string message) {if(!condition)throw new Exception(message);Debug.Log("PASS "+message);}
            Check(!RiderVisuals.ShouldPlayFall(.1f,3,.45f,5,false),"Ascending take-off stays in Jump");
            Check(!RiderVisuals.ShouldPlayFall(.5f,-.1f,.45f,5,false),"Apex stays in Jump");
            Check(!RiderVisuals.ShouldPlayFall(.2f,-8,.45f,5,false),"Minimum jump readability duration");
            Check(RiderVisuals.ShouldPlayFall(.5f,-5,.45f,5,false),"Fall enters at five metres/second after delay");
            Check(RiderVisuals.ShouldPlayFall(.6f,-1,.45f,5,true),"Fall does not flicker at the threshold");
            Check(!RiderVisuals.ShouldPlayFall(0,3,.45f,5,true),"New ascent clears Fall latch");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);var suit=prefab.GetComponent<RiderWingsuit>();
            Check(suit!=null && suit.visuals!=null,"Wingsuit attached to Rider prefab");
            foreach(var filter in new[]{suit.leftWing,suit.rightWing,suit.legWing})
            {
                Check(filter!=null && filter.sharedMesh!=null && filter.sharedMesh.isReadable,"Readable Blender mesh: "+filter.name);
                Check(filter.sharedMesh.uv.All(v=>v.x>=-.001f && v.y>=-.001f && v.x+v.y<=1.001f),"Valid barycentric fabric coordinates: "+filter.name);
                Check(filter.GetComponent<Renderer>().sharedMaterials.All(m=>m!=null && m.GetFloat("_Cull")==0),"Two-sided fabric materials: "+filter.name);
            }
            Directory.CreateDirectory("Logs/MantaFlight");File.WriteAllText("Logs/MantaFlight/validation-wingsuit.txt","PASS fall timing, hysteresis, prefab wiring, three Blender meshes, UV coordinates, two-sided materials.\n");
        }
    }
}
