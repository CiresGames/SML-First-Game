using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MantaFlight.Rider.Editor
{
    [InitializeOnLoad]
    public static class RiderWingsuitPlayValidation
    {
        const string Key="RiderWingsuitPlayValidation.Pending";
        static int frames;
        static RiderController rider;
        static RiderWingsuit suit;
        static Vector3[] before;
        static readonly Vector3 Origin=new Vector3(-900,1000,-900);
        static RiderWingsuitPlayValidation()
        {
            if(SessionState.GetBool(Key,false)) Hook();
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/MantaFlight/Scenes/MantaFlight.unity");
            SessionState.SetBool(Key,true);Hook();EditorApplication.EnterPlaymode();
        }
        static void Hook()
        {
            EditorApplication.wantsToQuit-=PreventQuit;EditorApplication.wantsToQuit+=PreventQuit;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static bool PreventQuit()=>!SessionState.GetBool(Key,false);
        static void Check(bool value,string text)
        {
            File.AppendAllText("Logs/MantaFlight/validation-wingsuit-play.txt",(value?"PASS ":"FAIL ")+text+"\n");
            if(!value)throw new Exception(text);
        }
        static int lastFrame=-1;
        static void Tick()
        {
            if(!EditorApplication.isPlaying || EditorApplication.isPaused || Time.frameCount==lastFrame)return;
            lastFrame=Time.frameCount;
            try
            {
                if(rider==null)
                {
                    rider=Object.FindFirstObjectByType<RiderController>();if(rider==null || rider.Motor==null)return;
                    Time.captureDeltaTime=.02f;
                    suit=rider.GetComponent<RiderWingsuit>();
                    Directory.CreateDirectory("Logs/MantaFlight");File.WriteAllText("Logs/MantaFlight/validation-wingsuit-play.txt","");
                    rider.mantaInput.SetMenu(false);rider.enabled=false;rider.mount.enabled=false;rider.mount.manta.enabled=false;
                    rider.PlaceForTest(Origin,new Vector3(0,3,12),RiderState.Falling);
                    frames=0;
                }
                frames++;
                // Real frames evaluate Animator and LateUpdate; fixed dt makes the flight path repeatable.
                rider.Tick(default,.02f);
                if(frames==8) Check(!suit.visuals.animator.GetCurrentAnimatorStateInfo(0).IsName("Fall"),"Take-off does not immediately play Fall");
                if(frames==45)
                {
                    Check(suit.Deployment>.95f,"Wingsuit deploys during jump/fall");
                    Check(suit.visuals.animator.GetCurrentAnimatorStateInfo(0).IsName("Fall"),"Fall animation follows the delayed descending threshold");
                    rider.PlaceForTest(Origin,new Vector3(0,-5,30),RiderState.Glide);
                }
                if(frames==100)
                {
                    Check(rider.GlidePose>.99f && suit.visuals.animator.GetCurrentAnimatorStateInfo(0).IsName("Glide"),"Glide pose and animation engaged");
                    before=suit.leftWing.sharedMesh.vertices;
                    Capture("ArtSource/Rider/RiderGlide-preview.png");
                }
                if(frames==110)
                {
                    var now=suit.leftWing.sharedMesh.vertices;float difference=0;
                    for(int i=0;i<now.Length;i++)difference+=Vector3.Distance(now[i],before[i]);
                    Check(difference>.01f,"Fabric and body animate during sustained glide");
                    foreach(var filter in new[]{suit.leftWing,suit.rightWing,suit.legWing})
                    {
                        Check(filter.GetComponent<Renderer>().enabled,"Visible deployed panel: "+filter.name);
                        var bounds=filter.GetComponent<Renderer>().bounds;
                        Check(bounds.size.sqrMagnitude<100 && bounds.size.sqrMagnitude>.001f,"Finite fitted membrane world bounds: "+filter.name);
                    }
                    rider.ResetRider();
                }
                if(frames==140)
                {
                    Check(suit.Deployment<.01f && !suit.leftWing.GetComponent<Renderer>().enabled,"Wingsuit retracts on remount");
                    Finish(0);
                }
            }
            catch(Exception e) {Debug.LogException(e);Finish(1);}
        }
        static void Capture(string path)
        {
            var go=new GameObject("Wingsuit validation camera");var camera=go.AddComponent<Camera>();
            Vector3 center=suit.visuals.animator.GetBoneTransform(HumanBodyBones.Hips).position;
            camera.transform.position=center+rider.transform.TransformDirection(new Vector3(2,2,-3));
            camera.transform.LookAt(center);camera.orthographic=true;camera.orthographicSize=1.35f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.085f,.12f);
            bool fog=RenderSettings.fog;RenderSettings.fog=false;
            var fill=new GameObject("Wingsuit validation light");var light=fill.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;
            fill.transform.rotation=camera.transform.rotation;
            var texture=new RenderTexture(1100,900,24);camera.targetTexture=texture;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=texture;
            var pixels=new Texture2D(1100,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1100,900),0,0);pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;
            Object.DestroyImmediate(pixels);Object.DestroyImmediate(texture);Object.DestroyImmediate(go);
            Object.DestroyImmediate(fill);RenderSettings.fog=fog;
        }
        static void Finish(int code)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
            EditorApplication.Exit(code);
        }
    }
}
