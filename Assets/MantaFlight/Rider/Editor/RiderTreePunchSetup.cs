using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MantaFlight.Rider.Editor
{
    public static class RiderTreePunchSetup
    {
        public const string ClipPath = "Assets/MantaFlight/Resources/RiderAnimations/TreePunch.anim";
        const string IdlePath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Stand--Idle.anim.fbx";
        [MenuItem("Manta/Rider/Build tree punch animation")]
        public static void BuildMenu() => Debug.Log(Build());
        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Build outside Play Mode.");
            const string folder = "Assets/MantaFlight/Resources/RiderAnimations";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/MantaFlight/Resources", "RiderAnimations");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MantaFlight/Rider/Prefabs/RiderCharacter.prefab");
            var idle = AssetDatabase.LoadAllAssetsAtPath(IdlePath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
            clip.ClearCurves(); clip.name = "TreePunch"; clip.frameRate = 60;
            var scene = EditorSceneManager.NewPreviewScene();
            var character = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            try
            {
                var animator = character.GetComponentInChildren<Animator>(); animator.applyRootMotion = false;
                animator.Rebind(); animator.Update(0);
                using var handler = new HumanPoseHandler(animator.avatar, animator.transform);
                var curves = Enumerable.Range(0, HumanTrait.MuscleCount).Select(_ => new AnimationCurve()).ToArray();
                float[] times = { 0, .08f, .21f, .34f, .40f, .50f, .72f, RiderTreeInteraction.PunchDuration };
                for (int frame = 0; frame < times.Length; frame++)
                {
                    idle.SampleAnimation(animator.gameObject, 0);
                    if (frame > 0 && frame < times.Length - 1)
                    {
                        var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                        float twist = frame == 2 ? -18 : frame == 3 || frame == 4 ? 16 : frame == 5 ? 5 : 0;
                        chest.rotation = Quaternion.AngleAxis(twist, character.transform.up) * chest.rotation;
                        var upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                        float scale = animator.humanScale;
                        Vector3 forward = character.transform.forward, right = character.transform.right, up = character.transform.up;
                        Vector3 offset = frame == 2 ? forward * -.20f + right * .12f - up * .12f
                            : frame == 3 || frame == 4 ? forward * .56f + right * -.04f - up * .09f
                            : forward * .17f + right * .06f - up * .17f;
                        AimArm(animator, false, upper.position + offset * scale, -up + right * .5f);
                        var left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                        AimArm(animator, true, left.position + (forward * .22f + right * .16f - up * .16f) * scale, -up - right);
                    }
                    var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                    for (int muscle = 0; muscle < curves.Length; muscle++)
                    {
                        float value = pose.muscles[muscle];
                        string name = HumanTrait.MuscleName[muscle];
                        if (frame > 0 && frame < times.Length - 1 && name.EndsWith("Stretched") && (name.StartsWith("Right ") || name.StartsWith("Left ")))
                            value = -.85f;
                        curves[muscle].AddKey(new Keyframe(times[frame], value));
                    }
                }
                for (int i = 0; i < curves.Length; i++)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), MuscleProperty(HumanTrait.MuscleName[i])), curves[i]);
                // Preserve the idle body root; all travel comes from the rider capsule.
                foreach (var binding in AnimationUtility.GetCurveBindings(idle))
                    if (binding.type == typeof(Animator) && (binding.propertyName.StartsWith("RootT.") || binding.propertyName.StartsWith("RootQ.")))
                    {
                        float value = AnimationUtility.GetEditorCurve(idle, binding).Evaluate(0);
                        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, RiderTreeInteraction.PunchDuration, value));
                    }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false; settings.loopBlend = false; settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true; settings.keepOriginalOrientation = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RiderAnimationSetup.ControllerPath);
                var machine = controller.layers[0].stateMachine;
                var state = machine.states.FirstOrDefault(s => s.state.name == "TreePunch").state;
                if (state == null) state = machine.AddState("TreePunch", new Vector3(750, 240));
                state.motion = clip; state.speedParameter = "ActionSpeed"; state.speedParameterActive = true;
                EditorUtility.SetDirty(state); EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
                if (!clip.humanMotion) throw new InvalidOperationException("Punch clip must contain humanoid muscle curves.");
                return "TreePunch: 0.9 s humanoid animation; impact at 0.34 s; wind-up, strike, recoil and recovery.";
            }
            finally { UnityEngine.Object.DestroyImmediate(character); EditorSceneManager.ClosePreviewScene(scene); }
        }
        static string MuscleProperty(string muscle)
        {
            var words = muscle.Split(' ');
            if (words.Length >= 3 && (words[0] == "Left" || words[0] == "Right")
                && new[] { "Thumb", "Index", "Middle", "Ring", "Little" }.Contains(words[1]))
                return words[0] + "Hand." + words[1] + "." + string.Join(" ", words.Skip(2));
            return muscle;
        }
        static void AimArm(Animator animator, bool left, Vector3 target, Vector3 bend)
        {
            var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, hand.position);
            Vector3 direction = (target - upper.position).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(target, upper.position), Mathf.Abs(a - b) + .005f, a + b - .005f);
            float x = (a * a - b * b + distance * distance) / (2 * distance);
            float y = Mathf.Sqrt(Mathf.Max(0, a * a - x * x));
            Vector3 elbow = upper.position + direction * x + Vector3.ProjectOnPlane(bend, direction).normalized * y;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            Vector3 wrist = upper.position + direction * distance;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, wrist - lower.position) * lower.rotation;
        }
    }
}
