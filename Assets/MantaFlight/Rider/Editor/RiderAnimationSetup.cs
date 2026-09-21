using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MantaFlight.Rider.Editor
{
    public static class RiderAnimationSetup
    {
        public const string ControllerPath = "Assets/MantaFlight/Rider/Settings/RiderAnimator.controller";
        const string Starter = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/";
        static readonly string[] Files = { "Male Sitting Pose", "Crouched Walking", "Falling", "Gliding", "Falling To Landing", "Falling To Roll" };
        static readonly string[] States = { "Mounted", "Crouch", "Fall", "Glide", "Land", "Roll" };

        [MenuItem("Manta/Rider/Connect rider animations")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before configuring animations.");
            for (int i = 0; i < Files.Length; i++)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Animations/" + Files[i] + ".fbx");
                if (importer == null) throw new InvalidOperationException("Missing animation: " + Files[i]);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.name = Files[i];
                    clip.loopTime = i < 4;
                    clip.loopPose = i < 4;
                    clip.lockRootRotation = true;
                    clip.lockRootPositionXZ = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionXZ = true;
                    clip.keepOriginalPositionY = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath);
                var avatar = model.GetComponent<Animator>().avatar;
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                    throw new InvalidOperationException("Invalid humanoid avatar: " + Files[i]);
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            // Update only our named states, retaining the asset identity on subsequent runs.
            if (!controller.parameters.Any(p => p.name == "Speed")) controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            if (!controller.parameters.Any(p => p.name == "ActionSpeed")) controller.AddParameter("ActionSpeed", AnimatorControllerParameterType.Float);
            for (int i = 0; i < Files.Length; i++)
            {
                var state = State(machine, States[i], i);
                state.motion = Clip("Assets/Animations/" + Files[i] + ".fbx");
                state.speedParameter = "ActionSpeed";
                state.speedParameterActive = true;
            }
            var locomotion = State(machine, "Locomotion", 6);
            var tree = locomotion.motion as BlendTree;
            if (tree == null)
            {
                tree = new BlendTree { name = "Rider Idle Walk Run" };
                AssetDatabase.AddObjectToAsset(tree, controller);
            }
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.children = new[] {
                new ChildMotion { motion = Clip(Starter + "Stand--Idle.anim.fbx"), threshold = 0, timeScale = 1 },
                new ChildMotion { motion = Clip(Starter + "Locomotion--Walk_N.anim.fbx"), threshold = 2, timeScale = 1 },
                new ChildMotion { motion = Clip(Starter + "Locomotion--Run_N.anim.fbx"), threshold = 6, timeScale = 1 }
            };
            locomotion.motion = tree;
            State(machine, "Jump", 7).motion = AssetDatabase.LoadAllAssetsAtPath(Starter + "Jump--Jump.anim.fbx")
                .OfType<AnimationClip>().First(c => c.name == "JumpStart");
            machine.defaultState = machine.states.First(s => s.state.name == "Mounted").state;
            EditorUtility.SetDirty(controller);

            const string prefabPath = "Assets/MantaFlight/Rider/Prefabs/RiderCharacter.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Configure(prefab.GetComponent<RiderVisuals>(), controller);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (var visual in UnityEngine.Object.FindObjectsByType<RiderVisuals>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObjects(new UnityEngine.Object[] { visual, visual.animator }, "Connect Rider animations");
                Configure(visual, controller);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.animator);
                EditorSceneManager.MarkSceneDirty(visual.gameObject.scene);
                EditorSceneManager.SaveScene(visual.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Rider animations connected: Mounted, Crouch, Fall, Glide, Land, Roll, Locomotion, Jump.");
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, int index)
        {
            var state = machine.states.FirstOrDefault(s => s.state.name == name).state;
            return state != null ? state : machine.AddState(name, new Vector3(250 * (index % 3), 80 * (index / 3)));
        }
        static AnimationClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) throw new InvalidOperationException("No animation clip at " + path);
            return clip;
        }
        public static void Configure(RiderVisuals visual, RuntimeAnimatorController controller)
        {
            visual.animator.runtimeAnimatorController = controller;
            visual.animator.applyRootMotion = false;
            visual.landingAnimation = Clip("Assets/Animations/Falling To Landing.fbx");
            visual.rollingAnimation = Clip("Assets/Animations/Falling To Roll.fbx");
            EditorUtility.SetDirty(visual);
            EditorUtility.SetDirty(visual.animator);
        }
    }
}
