using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MantaFlight.Rider.Editor
{
    public static class RiderScarfSetup
    {
        const string PrefabPath="Assets/MantaFlight/Rider/Prefabs/RiderCharacter.prefab";
        const string Folder="Assets/MantaFlight/Rider/Models/";
        [MenuItem("Manta/Rider/Install red wind scarf")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before installing the scarf.");
            var red=Material("Scarf Crimson",new Color(.66f,.018f,.035f));
            var hem=Material("Scarf Red Hem",new Color(.29f,.008f,.016f));
            var cloth=SaveMesh(CreateRibbon(),Folder+"ScarfCloth.asset");
            var collar=SaveMesh(CreateCollar(),Folder+"ScarfCollar.asset");
            var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
            try {Configure(prefab.GetComponent<RiderVisuals>(),cloth,collar,red,hem);PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);}
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
            foreach(var visual in Object.FindObjectsByType<RiderVisuals>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                Configure(visual,cloth,collar,red,hem);EditorSceneManager.MarkSceneDirty(visual.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }
        static Material Material(string name,Color color)
        {
            string path="Assets/MantaFlight/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.2f);m.SetFloat("_Cull",0);m.doubleSidedGI=true;
            // A small ambient fabric tint keeps the unlit back face readable as a wind indicator.
            m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.18f);EditorUtility.SetDirty(m);return m;
        }
        static Mesh SaveMesh(Mesh mesh,string path)
        {
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
        }
        static void Configure(RiderVisuals visual,Mesh cloth,Mesh wrap,Material red,Material hem)
        {
            var scarf=visual.GetComponent<RiderScarf>();if(scarf==null)scarf=visual.gameObject.AddComponent<RiderScarf>();
            scarf.visuals=visual;
            scarf.fabric=Part(visual.model,"Red scarf cloth",cloth,new[]{red,hem});
            scarf.collar=Part(visual.model,"Red scarf collar",wrap,new[]{red});
            var neck=visual.animator.GetBoneTransform(HumanBodyBones.Neck);
            if(neck!=null)
            {
                scarf.collar.transform.position=neck.position;
                scarf.fabric.transform.position=neck.position-visual.model.forward*.13f;
            }
            EditorUtility.SetDirty(scarf);
            if(PrefabUtility.IsPartOfPrefabInstance(scarf))PrefabUtility.RecordPrefabInstancePropertyModifications(scarf);
        }
        static MeshFilter Part(Transform parent,string name,Mesh mesh,Material[] materials)
        {
            var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}
            var filter=t.GetComponent<MeshFilter>();if(filter==null)filter=t.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=t.GetComponent<MeshRenderer>();if(renderer==null)renderer=t.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            return filter;
        }
        static Mesh CreateRibbon() => RiderScarf.CreateFabricMesh(1.15f,.18f,.012f);
        static Mesh CreateCollar()
        {
            const int Around=48,Tube=10;
            var v=new Vector3[(Around+1)*(Tube+1)];var uv=new Vector2[v.Length];var triangles=new List<int>();
            for(int a=0;a<=Around;a++)for(int b=0;b<=Tube;b++)
            {
                float angle=a*Mathf.PI*2/Around,tube=b*Mathf.PI*2/Tube;
                float fold=.0035f*Mathf.Sin(angle*7+tube*2);
                int i=a*(Tube+1)+b;
                v[i]=new Vector3(Mathf.Cos(angle)*(.108f+Mathf.Cos(tube)*.023f+fold),.012f+Mathf.Sin(tube)*.052f,Mathf.Sin(angle)*(.098f+Mathf.Cos(tube)*.023f+fold));
                uv[i]=new Vector2((float)a/Around,(float)b/Tube);
                if(a<Around && b<Tube)triangles.AddRange(new[]{i,i+1,i+Tube+1,i+1,i+Tube+2,i+Tube+1});
            }
            var mesh=new Mesh{name="Scarf gathered neck wrap"};mesh.vertices=v;mesh.uv=uv;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static void InstallBatch()
        {
            EditorSceneManager.OpenScene("Assets/MantaFlight/Scenes/MantaFlight.unity");Install();EditorSceneManager.SaveOpenScenes();
            Debug.Log("SCARF_INSTALLED: red neck wrap and simulated ribbon on Rider prefab and scene.");
        }
    }
}
