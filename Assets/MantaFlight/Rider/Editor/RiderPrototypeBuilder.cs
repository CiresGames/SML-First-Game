using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
namespace MantaFlight.Rider.Editor
{
    public static class RiderPrototypeBuilder
    {
        const string Root="Assets/MantaFlight/Rider";
        [MenuItem("Manta/Rider/Install rider in current scene")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode first.");
            if(Object.FindFirstObjectByType<RiderController>()!=null)return;
            var manta=Object.FindFirstObjectByType<MantaController>();
            if(manta==null)throw new System.InvalidOperationException("Open the manta scene first.");
            Directory.CreateDirectory(Root+"/Settings");Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh();
            var config=ScriptableObject.CreateInstance<RiderSettings>();AssetDatabase.CreateAsset(config,Root+"/Settings/RiderFeel.asset");
            var actions=ScriptableObject.CreateInstance<InputActionAsset>();var map=actions.AddActionMap("Rider");
            var move=map.AddAction("Move",InputActionType.Value,expectedControlLayout:"Vector2");
            move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");
            var look=map.AddAction("Look",InputActionType.Value,expectedControlLayout:"Vector2");look.AddBinding("<Mouse>/delta");look.AddBinding("<Gamepad>/rightStick");
            Add(map,"Run","<Keyboard>/leftShift","<Gamepad>/leftStickPress");
            Add(map,"Crouch","<Keyboard>/leftCtrl","<Gamepad>/leftShoulder");
            Add(map,"Jump","<Keyboard>/space","<Gamepad>/buttonSouth");
            Add(map,"Roll","<Keyboard>/q","<Gamepad>/buttonEast");
            Add(map,"Context","<Keyboard>/f","<Gamepad>/buttonWest");
            Add(map,"Glide","<Keyboard>/g","<Gamepad>/buttonNorth");
            Add(map,"JumpOff","<Keyboard>/j","<Gamepad>/dpad/left");
            Add(map,"Drop","<Keyboard>/k","<Gamepad>/dpad/down");
            Add(map,"Call","<Keyboard>/h","<Gamepad>/dpad/up");
            Add(map,"InvertPitch","<Keyboard>/i","<Gamepad>/dpad/right");
            File.WriteAllText(Root+"/Settings/RiderControls.inputactions",actions.ToJson());Object.DestroyImmediate(actions);
            AssetDatabase.ImportAsset(Root+"/Settings/RiderControls.inputactions");actions=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root+"/Settings/RiderControls.inputactions");
            var root=new GameObject("Rider Character");Undo.RegisterCreatedObjectUndo(root,"Install rider");root.layer=2;
            var capsule=root.AddComponent<CharacterController>();capsule.radius=config.ground.radius;capsule.height=config.ground.standingHeight;capsule.center=Vector3.up*.9f;capsule.stepOffset=.3f;capsule.skinWidth=.035f;
            var motor=root.AddComponent<RiderGroundMotor>();motor.settings=config;
            var glide=root.AddComponent<RiderGlideMotor>();glide.settings=config;
            var input=root.AddComponent<RiderInput>();input.settings=config;input.actions=actions;
            var controller=root.AddComponent<RiderController>();controller.settings=config;
            var imported=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerArmature.prefab");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(imported);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            model.name="Rider Body";model.transform.SetParent(root.transform,false);
            foreach(var component in model.GetComponents<MonoBehaviour>())Object.DestroyImmediate(component);
            Object.DestroyImmediate(model.GetComponent<CharacterController>());
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
            var animator=model.GetComponent<Animator>();animator.applyRootMotion=false;model.AddComponent<RiderAnimationEvents>();
            var visuals=root.AddComponent<RiderVisuals>();visuals.rider=controller;visuals.model=model.transform;visuals.animator=animator;
            var riderAnimator=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RiderAnimationSetup.ControllerPath);
            if(riderAnimator!=null)RiderAnimationSetup.Configure(visuals,riderAnimator);
            visuals.landingClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Starter Assets/Runtime/ThirdPersonController/Character/Sfx/Player_Land.wav");
            root.AddComponent<RiderDebug>().rider=controller;
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/RiderCharacter.prefab");
            var original=manta.GetComponent<MantaVisuals>();Undo.RecordObject(original.rider.gameObject,"Hide mounted placeholder");original.rider.gameObject.SetActive(false);
            var seat=new GameObject("Rider Mount Point").transform;seat.SetParent(original.rider.parent,false);
            var adapter=manta.gameObject.AddComponent<MantaMountAdapter>();adapter.manta=manta;adapter.seat=seat;adapter.rider=controller;
            controller.mount=adapter;controller.mantaInput=manta.GetComponent<MantaInput>();controller.view=Camera.main;
            root.transform.SetPositionAndRotation(seat.position,seat.rotation);
            var camera=Object.FindFirstObjectByType<MantaCameraController>();
            var riderCamera=camera.gameObject.AddComponent<RiderCamera>();riderCamera.rider=controller;riderCamera.mountedCamera=camera;
            EditorSceneManager.MarkSceneDirty(manta.gameObject.scene);EditorSceneManager.SaveScene(manta.gameObject.scene);
            AssetDatabase.SaveAssets();Selection.activeGameObject=root;
        }
        static void Add(InputActionMap map,string name,string keyboard,string pad)
        {var action=map.AddAction(name,InputActionType.Button);action.AddBinding(keyboard);action.AddBinding(pad);}
        [MenuItem("Manta/Rider/Test on foot here (Play mode)")]
        static void TestOnFoot()
        {
            if(!EditorApplication.isPlaying)return;
            var r=Object.FindFirstObjectByType<RiderController>();
            if(r!=null && Physics.Raycast(r.mount.Seat.position+Vector3.up,Vector3.down,out var hit,2000,r.settings.environment,QueryTriggerInteraction.Ignore))
                r.PlaceForTest(hit.point+Vector3.up*.1f,Vector3.zero,RiderState.Falling);
        }
        [MenuItem("Manta/Rider/Validate mount and call (Play mode)")]
        static void TestMount()=>Debug.Log(RiderValidation.RunMount());
        [MenuItem("Manta/Rider/Validate transitions and camera (Play mode)")]
        static void TestTransitions()=>Debug.Log(RiderValidation.RunTransitions());
        [MenuItem("Manta/Rider/Select runtime feel")]
        static void SelectFeel(){var r=Object.FindFirstObjectByType<RiderController>();if(r!=null)Selection.activeObject=r.settings;}
        [MenuItem("Manta/Rider/Export runtime feel as new profile")]
        static void Export()
        {
            var r=Object.FindFirstObjectByType<RiderController>();if(r==null)return;
            var copy=Object.Instantiate(r.settings);copy.name="RiderFeel";
            AssetDatabase.CreateAsset(copy,AssetDatabase.GenerateUniqueAssetPath(Root+"/Settings/RiderFeel.asset"));AssetDatabase.SaveAssetIfDirty(copy);Selection.activeObject=copy;
        }
    }
}
