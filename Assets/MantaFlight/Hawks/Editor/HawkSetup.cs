using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MantaFlight.Hawks.Editor
{
    public static class HawkSetup
    {
        const string Root = "Assets/MantaFlight/Hawks";
        const string Model = Root + "/Models/CommonHawk.fbx";
        const string ScenePath = "Assets/MantaFlight/Scenes/MantaFlight.unity";
        const string FlockName = "Ambient Hawks";

        [MenuItem("Manta/Hawks/Add flock to flight playground")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before placing hawks.");
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Animation"); AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var clips = importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = c.name.Contains("Glide") ? "Hawk_Glide" : "Hawk_Flight";
                c.loopTime = true; c.loopPose = true; c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips; importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            foreach (var source in model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct())
            {
                if (!source) continue;
                string path = Root + "/Materials/" + source.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.name = source.name;
                    mat.SetColor("_BaseColor", source.HasProperty("_Color") ? source.color : new Color(.3f, .15f, .07f));
                    mat.SetFloat("_Smoothness", .15f); mat.SetFloat("_Cull", 0);
                    AssetDatabase.CreateAsset(mat, path);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), mat);
            }
            importer.SaveAndReimport();
            var animations = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            var flight = animations.Single(c => c.name == "Hawk_Flight");
            var glide = animations.Single(c => c.name == "Hawk_Glide");
            string controllerPath = Root + "/Animation/Hawk.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            var flying = machine.AddState("Flight"); flying.motion = flight;
            var gliding = machine.AddState("Glide"); gliding.motion = glide; machine.defaultState = flying;
            var root = new GameObject("Common Hawk");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            visual.transform.SetParent(root.transform, false);
            var animator = visual.GetComponent<Animator>(); if (!animator) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>()) r.localBounds = new Bounds(Vector3.zero, Vector3.one * 4);
            root.AddComponent<HawkFlight>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/CommonHawk.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var flock = scene.GetRootGameObjects().FirstOrDefault(g => g.name == FlockName);
            if (!flock) { flock = new GameObject(FlockName); SceneManager.MoveGameObjectToScene(flock, scene); }
            // Idempotent: only replace our own ambient flock.
            foreach (Transform t in flock.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
            for (int i = 0; i < 10; i++)
            {
                var bird = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                bird.name = "Hawk " + (i + 1).ToString("00"); bird.transform.SetParent(flock.transform);
                var mover = bird.GetComponent<HawkFlight>();
                bool near = i < 6;
                mover.center = near ? new Vector3(0, 64 + (i % 3) * 5, -150) : new Vector3(0, 137 + (i % 3) * 7, 320);
                mover.radius = near ? new Vector2(24 + i * 3, 33 + i * 4) : new Vector2(32 + i * 2, 66 + i * 2);
                mover.phase = i * 2.399963f; mover.speed = 8 + (i % 4) * 1.2f;
                mover.altitudeVariation = 1.5f + i % 3; mover.clockwise = i % 3 == 0;
                mover.wingbeatSpeed = .85f + (i % 4) * .08f;
                bird.transform.localScale = Vector3.one * (1.35f + (i % 3) * .12f);
                mover.ApplyPose(0); PrefabUtility.RecordPrefabInstancePropertyModifications(mover);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bird.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate();
        }

        public static void Validate()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var flock = scene.GetRootGameObjects().Single(g => g.name == FlockName);
            var birds = flock.GetComponentsInChildren<HawkFlight>();
            if (birds.Length != 10) throw new Exception("Expected 10 hawks.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var mesh = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (!mesh || mesh.bones.Length < 7 || mesh.sharedMesh.vertexCount < 100) throw new Exception("Missing skinned hawk mesh/rig.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            if (clips.Length != 2 || clips.Any(c => !c.isLooping || c.length < 1)) throw new Exception("Expected two looping flight clips.");
            var probe = UnityEngine.Object.Instantiate(model);
            try
            {
                var skin = probe.GetComponentInChildren<SkinnedMeshRenderer>();
                var a = new Mesh(); var b = new Mesh();
                var flight = clips.Single(c => c.name == "Hawk_Flight");
                flight.SampleAnimation(probe, 0); skin.BakeMesh(a);
                flight.SampleAnimation(probe, flight.length * .5f); skin.BakeMesh(b);
                float maxDelta = a.vertices.Zip(b.vertices, (x,y) => Vector3.Distance(x,y)).Max();
                UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b);
                if (maxDelta < .15f) throw new Exception("Flight animation does not deform the wings: " + maxDelta);
                foreach (var bird in birds)
                {
                    Vector3 p = bird.transform.position; Quaternion q = bird.transform.rotation;
                    bird.ApplyPose(2); float moved = Vector3.Distance(p, bird.transform.position);
                    bird.transform.SetPositionAndRotation(p,q);
                    if (moved < 5 || !bird.GetComponentInChildren<Animator>().runtimeAnimatorController) throw new Exception("Hawk movement/controller invalid.");
                }
                Directory.CreateDirectory("Logs/Hawks");
                File.WriteAllText("Logs/Hawks/validation.txt", "PASS: 10 scene hawks; " + mesh.bones.Length + " skin bones; " + mesh.sharedMesh.vertexCount + " vertices; Flight/Glide loop clips; wing deformation " + maxDelta.ToString("F3") + "m; all flight paths move.\n");
                Debug.Log("HAWK_VALIDATION_PASS: 10 rigged, animated hawks saved in " + scene.path);
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }
    }
}
