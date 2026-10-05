using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace MantaFlight.Rider.Editor
{
    public static class RiderOrbitValidation
    {
        public static string Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required for isolated preview validation.");
            var scene=EditorSceneManager.NewPreviewScene();
            RiderController rider=null;RiderSettings settings=null;
            var checks=new List<string>();
            void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add("PASS "+name);}
            void Set(string name,object value)=>typeof(RiderController).GetProperty(name).SetValue(rider,value);
            GameObject Create(string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
            try
            {
                rider=Create("Isolated camera target").AddComponent<RiderController>();
                settings=Object.Instantiate(AssetDatabase.LoadAssetAtPath<RiderSettings>("Assets/MantaFlight/Rider/Settings/RiderFeel.asset"));
                settings.environment=0;rider.settings=settings;
                Set("Motor",rider.GetComponent<RiderGroundMotor>());Set("Glide",rider.GetComponent<RiderGlideMotor>());Set("State",RiderState.Glide);
                rider.Motor.Velocity=new Vector3(0,-5,30);
                var go=Create("Isolated orbit camera");go.AddComponent<Camera>();
                var mounted=go.AddComponent<MantaCameraController>();var camera=go.AddComponent<RiderCamera>();
                camera.rider=rider;camera.mountedCamera=mounted;
                typeof(RiderCamera).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(camera,null);
                float min=float.MaxValue,max=0,alignment=1;
                for(int i=0;i<300;i++)
                {
                    Set("LookInput",new Vector2(2,0));camera.Tick(.02f);
                    var focus=rider.transform.position+Vector3.up*settings.camera.groundHeight;
                    float radius=Vector3.Distance(camera.transform.position,focus);min=Mathf.Min(min,radius);max=Mathf.Max(max,radius);
                    alignment=Mathf.Min(alignment,Vector3.Dot(camera.transform.forward,(focus-camera.transform.position).normalized));
                }
                Check(max-min<.001f && min>=settings.camera.glideDistance,"Full glide orbit maintains radius");
                Check(alignment>.999f,"Rider remains centered throughout the orbit");
                Check(Mathf.Abs(camera.freeLook.Offset.x+120)<.01f,"Continuous rotation beyond 360 degrees");
                var saved=camera.freeLook.Offset;Set("LookBackHeld",true);camera.Tick(.02f);
                Check(Vector3.Dot(camera.transform.forward,rider.Motor.Velocity.normalized)<-.99f,"R3 rear view takes effect in one tick");
                Check(camera.freeLook.Offset==saved,"R3 preserves the orbit offset");
                Set("LookBackHeld",false);Set("LookInput",Vector2.zero);camera.Tick(.02f);
                Check(camera.freeLook.Offset==saved,"Release returns to the stored orbit");
                camera.freeLook.Reset();Set("Motor",rider.Motor);rider.Motor.Velocity=Vector3.down*30;
                camera.Tick(.02f);
                Check(float.IsFinite(camera.transform.position.sqrMagnitude) && float.IsFinite(camera.transform.rotation.w),"Vertical flight keeps a valid camera pose");
                Set("State",RiderState.Grounded);rider.Motor.Velocity=Vector3.zero;
                for(int i=0;i<120;i++)camera.Tick(1f/60);
                foreach(int fps in new[]{24,60,144})
                {
                    var before=camera.transform.position;var offset=before-rider.transform.position;
                    Set("State",RiderState.Rolling);
                    float error=0;
                    int frames=Mathf.CeilToInt(settings.ground.rollDuration*fps);
                    for(int frame=0;frame<frames;frame++)
                    {
                        rider.transform.position+=Vector3.forward*(settings.ground.rollDistance/frames);
                        camera.Tick(1f/fps);
                        error=Mathf.Max(error,Vector3.Distance(camera.transform.position-rider.transform.position,offset));
                        if(!float.IsFinite(camera.transform.position.sqrMagnitude))throw new Exception("Invalid ground roll camera pose");
                    }
                    Check(error<.02f,"Ground roll camera follows travel immediately at "+fps+" FPS");
                    before=camera.transform.position;Set("State",RiderState.Grounded);camera.Tick(1f/fps);
                    Check(Vector3.Distance(before,camera.transform.position)<.02f,"No catch-up or snap when roll ends at "+fps+" FPS");
                }
                // Exercise the actual imported clip, not just a translated camera target.
                var body=PrefabUtility.LoadPrefabContents("Assets/MantaFlight/Rider/Prefabs/RiderCharacter.prefab");
                try
                {
                    var visual=body.GetComponent<RiderVisuals>();var animator=visual.animator;
                    animator.applyRootMotion=false;animator.Rebind();
                    var hip=animator.GetBoneTransform(HumanBodyBones.Hips);
                    float maxOffset=0;
                    for(int frame=0;frame<=60;frame++)
                    {
                        animator.Play("Roll",0,frame/60f);animator.Update(0);
                        var local=body.transform.InverseTransformPoint(hip.position);
                        maxOffset=Mathf.Max(maxOffset,new Vector2(local.x,local.z).magnitude);
                    }
                    Check(maxOffset<.4f,"Real roll clip stays with the camera's movement target: "+maxOffset.ToString("F3")+" m");
                    var last=hip.position;float maxReturnStep=0;
                    animator.CrossFadeInFixedTime("Locomotion",.15f,0,0);
                    for(int frame=0;frame<30;frame++)
                    {
                        animator.Update(1f/60);var delta=Vector3.ProjectOnPlane(hip.position-last,Vector3.up);
                        maxReturnStep=Mathf.Max(maxReturnStep,delta.magnitude);last=hip.position;
                    }
                    Check(maxReturnStep<.1f,"Roll-to-standing animation has no horizontal snap: "+maxReturnStep.ToString("F3")+" m/frame");
                }
                finally
                {
                    body.GetComponent<RiderController>().settings=null;PrefabUtility.UnloadPrefabContents(body);
                }
                return string.Join("\n",checks);
            }
            finally
            {
                if(rider!=null)rider.settings=null;
                EditorSceneManager.ClosePreviewScene(scene);
                if(settings!=null)Object.DestroyImmediate(settings);
            }
        }
    }
}
