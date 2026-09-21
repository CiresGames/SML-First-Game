using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MantaFlight.Rider;

namespace MantaFlight.Sharks.Editor
{
    public static class SharkSetup
    {
        const string Root="Assets/Wildlife/WhiteShark";
        const string Model=Root+"/Models/GreatWhite.fbx";
        const string ScenePath="Assets/MantaFlight/Scenes/MantaFlight.unity";
        [MenuItem("Manta/Sharks/Populate flight course")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            foreach(var folder in new[]{"Materials","Animation","Prefabs"}) Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();
            var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType=ModelImporterAnimationType.Generic; importer.importAnimation=true;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
            var clips=importer.defaultClipAnimations;
            foreach(var clip in clips)
            {
                clip.name=clip.name.Contains("Bite") ? "Shark_Bite" : "Shark_Swim";
                clip.loopTime=clip.name=="Shark_Swim"; clip.loopPose=clip.loopTime;
                clip.lockRootRotation=true; clip.lockRootHeightY=true; clip.lockRootPositionXZ=true;
            }
            importer.clipAnimations=clips; importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            foreach(var source in model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct())
            {
                if(!source) continue;
                string path=Root+"/Materials/"+source.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material)
                {
                    material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name=source.name;
                    material.SetColor("_BaseColor",source.color); material.SetFloat("_Smoothness",.35f);
                    material.SetFloat("_Cull",0); AssetDatabase.CreateAsset(material,path);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source.name),material);
            }
            importer.SaveAndReimport();
            var animations=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Root+"/Animation/Shark.controller");
            if(!controller) controller=AnimatorController.CreateAnimatorControllerAtPath(Root+"/Animation/Shark.controller");
            var machine=controller.layers[0].stateMachine;
            foreach(var s in machine.states) machine.RemoveState(s.state);
            var swim=machine.AddState("Swim"); swim.motion=animations.Single(c=>c.name=="Shark_Swim"); machine.defaultState=swim;
            machine.AddState("Bite").motion=animations.Single(c=>c.name=="Shark_Bite");
            var root=new GameObject("Great White Shark");
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model)); visual.transform.SetParent(root.transform,false);
            var animator=visual.GetComponent<Animator>(); if(!animator) animator=visual.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller; animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.localBounds=new Bounds(Vector3.zero,Vector3.one*9);
            root.AddComponent<FlyingShark>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/GreatWhite.prefab"); UnityEngine.Object.DestroyImmediate(root);
            var scene=SceneManager.GetSceneByPath(ScenePath); if(!scene.isLoaded) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var rider=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RiderController>(true)).Single();
            var health=rider.GetComponent<SharkPlayerHealth>(); if(!health) health=Undo.AddComponent<SharkPlayerHealth>(rider.gameObject);
            var group=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Flying Great Whites");
            if(!group) { group=new GameObject("Flying Great Whites"); SceneManager.MoveGameObjectToScene(group,scene); Undo.RegisterCreatedObjectUndo(group,"Add sharks"); }
            foreach(Transform t in group.transform.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(t.gameObject);
            for(int i=0;i<6;i++)
            {
                var shark=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene); Undo.RegisterCreatedObjectUndo(shark,"Add shark");
                shark.name="Great White "+(i+1).ToString("00"); shark.transform.SetParent(group.transform);
                var flight=shark.GetComponent<FlyingShark>(); flight.target=health;
                flight.patrolCenter= i<3 ? new Vector3(0,60+i*9,-75+i*40) : new Vector3((i-4)*35,108+(i-3)*12,170+(i-3)*65);
                flight.patrolRadius=24+i*3; flight.phase=i*2.399963f; flight.patrolSpeed=9+i*.6f; flight.PlaceOnPatrol();
                PrefabUtility.RecordPrefabInstancePropertyModifications(flight); PrefabUtility.RecordPrefabInstancePropertyModifications(shark.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); Validate();
        }
        [MenuItem("Manta/Sharks/Validate sharks")]
        public static void Validate()
        {
            var scene=SceneManager.GetSceneByPath(ScenePath);
            var sharks=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FlyingShark>()).ToArray();
            if(sharks.Length!=6 || sharks.Any(s=>!s.target)) throw new Exception("Expected six sharks with player targets.");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var clips=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            var swim=clips.Single(c=>c.name=="Shark_Swim");
            if(!swim.isLooping || clips.Length!=2) throw new Exception("Missing swim/bite clips.");
            var probe=UnityEngine.Object.Instantiate(model); var a=new Mesh(); var b=new Mesh();
            try
            {
                var skin=probe.GetComponentInChildren<SkinnedMeshRenderer>();
                if(skin.bones.Length<8) throw new Exception("Missing shark rig.");
                swim.SampleAnimation(probe,0); skin.BakeMesh(a); swim.SampleAnimation(probe,swim.length*.5f); skin.BakeMesh(b);
                float movement=a.vertices.Zip(b.vertices,(x,y)=>Vector3.Distance(x,y)).Max();
                if(movement<.1f) throw new Exception("Swim does not deform the mesh.");
                Directory.CreateDirectory("Logs/Sharks");
                File.WriteAllText("Logs/Sharks/validation.txt",$"PASS: six targeted scene sharks; {skin.bones.Length} bones; {skin.sharedMesh.vertexCount} vertices; swim/bite clips; swim deformation {movement:F3}m.\n");
                Debug.Log("SHARK_VALIDATION_PASS");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }
    }
}
