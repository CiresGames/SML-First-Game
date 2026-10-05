using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using MantaFlight.Rider;
using Object=UnityEngine.Object;

namespace MantaFlight.Editor
{
    [InitializeOnLoad]
    public static class FlightLookPlayValidation
    {
        const string Key="Manta.FreeLook.Validation";
        const string Report="Logs/MantaFlight/validation-free-look-play.txt";
        static FlightLookPlayValidation(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Run;}
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/MantaFlight/Scenes/MantaFlight.unity");SessionState.SetBool(Key,true);
            EditorApplication.update-=Run;EditorApplication.update+=Run;EditorApplication.EnterPlaymode();
        }
        static void Check(bool ok,string message){File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);}
        static void Run()
        {
            if(!EditorApplication.isPlaying || Time.time<.2f)return;
            EditorApplication.update-=Run;
            Gamepad pad=null;Mouse mouse=null;
            var originalSettings=InputSystem.settings;
            var testSettings=Object.Instantiate(originalSettings);
            int code=0;
            try
            {
                Directory.CreateDirectory("Logs/MantaFlight");File.WriteAllText(Report,"");
                var r=Object.FindFirstObjectByType<RiderController>();var camera=Object.FindFirstObjectByType<RiderCamera>();
                var input=r.mantaInput;var manta=r.mount.manta;
                r.enabled=false;r.mount.enabled=false;manta.enabled=false;camera.enabled=false;
                input.SetMenu(false);r.ResetRider();input.ControlEnabled=true;
                InputSystem.settings=testSettings;
                testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                input.actions.Enable();r.Input.actions.Enable();
                pad=InputSystem.AddDevice<Gamepad>();mouse=InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=new Vector2(.7f,.7f)});InputSystem.Update();input.Read(.02f);
                Debug.Log("Free look input: raw="+pad.rightStick.ReadValue()+" action="+input.actions.FindAction("Flight/Look").ReadValue<Vector2>()+" delta="+input.LookDelta+" dt="+Time.deltaTime+" control="+input.ControlEnabled);
                Check(input.LookDelta.x>0 && input.LookDelta.y>0,"Right stick supplies horizontal and vertical camera look");
                Check(input.State.steering.sqrMagnitude<.00001f,"Right stick does not steer or climb the manta");
                var rotation=manta.transform.rotation;var position=manta.transform.position;
                var mounted=camera.mountedCamera;mounted.enabled=false;mounted.Snap();mounted.Tick(.02f);var startCamera=mounted.transform.position;
                for(int i=0;i<120;i++)mounted.Tick(.02f);
                Check(mounted.freeLook.Offset.x>30 && Vector3.Distance(startCamera,mounted.transform.position)>1,"Manta camera orbits in response to look input");
                Check(manta.transform.position==position && manta.transform.rotation==rotation,"Camera orbit does not change flight transform");
                InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=Vector2.right});InputSystem.Update();input.Read(.02f);
                mounted.Snap();mounted.Tick(.02f);
                var focus=manta.transform.position+Vector3.up*1.1f;
                float minRadius=float.MaxValue,maxRadius=0;
                for(int i=0;i<220;i++)
                {
                    mounted.Tick(.02f);float radius=Vector3.Distance(mounted.transform.position,focus);
                    minRadius=Mathf.Min(minRadius,radius);maxRadius=Mathf.Max(maxRadius,radius);
                    Check(Vector3.Dot(mounted.transform.forward,(focus-mounted.transform.position).normalized)>.98f,"Orbit keeps manta centered (sample "+i+")");
                }
                Check(minRadius>5 && maxRadius-minRadius<.05f,"Full orbit maintains its radius instead of cutting toward manta");
                Check(mounted.freeLook.Offset.x>70 && mounted.freeLook.Offset.x<90,"Right stick continues beyond 360 degrees");
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();input.Read(.02f);
                mounted.Snap();mounted.Tick(.02f);
                var shell=GameObject.CreatePrimitive(PrimitiveType.Sphere);shell.name="Camera self collision regression";
                shell.transform.SetParent(manta.transform,true);shell.transform.position=Vector3.Lerp(focus,mounted.transform.position,.5f);shell.transform.localScale=Vector3.one*3;
                Physics.SyncTransforms();float unobstructed=Vector3.Distance(focus,mounted.transform.position);mounted.Tick(.02f);
                Check(Mathf.Abs(Vector3.Distance(focus,mounted.transform.position)-unobstructed)<.05f,"Manta's own colliders do not pull camera inward");
                shell.transform.SetParent(null,true);Physics.SyncTransforms();mounted.Tick(.02f);
                Check(Vector3.Distance(focus,mounted.transform.position)<unobstructed-1,"Real environment obstacles still shorten the camera boom");
                Object.DestroyImmediate(shell);Physics.SyncTransforms();mounted.Tick(.02f);
                Check(Mathf.Abs(Vector3.Distance(focus,mounted.transform.position)-unobstructed)<.05f,"Camera recovers full radius when obstruction clears");
                mounted.freeLook.Reset();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.RightStick));InputSystem.Update();input.Read(.02f);
                mounted.Tick(.02f);
                Check(input.LookBackHeld && input.State.dive==0,"Held R3 selects rear view without diving");
                Check(Vector3.Dot(mounted.transform.position-manta.transform.position,manta.transform.forward)>1 && Vector3.Dot(mounted.transform.forward,manta.transform.forward)<-.7f,"Manta rear view is in front and looking back on the first tick");
                for(int i=0;i<20;i++)mounted.Tick(.02f);
                Check(Vector3.Dot(mounted.transform.position-manta.transform.position,manta.transform.forward)>1,"Rear view remains active while R3 is held");
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();input.Read(.02f);mounted.Tick(.02f);
                Check(!input.LookBackHeld && Vector3.Dot(mounted.transform.position-manta.transform.position,manta.transform.forward)<-1,"Releasing R3 immediately restores mounted forward view");
                input.SetMenu(true);var paused=mounted.freeLook.Offset;mounted.Tick(1);
                Check(mounted.freeLook.Offset==paused,"Menu freezes mounted free look");input.SetMenu(false);
                InputSystem.QueueStateEvent(pad,new GamepadState());
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(40,10)});InputSystem.Update();input.Read(.02f);
                Check(Vector2.Distance(input.LookDelta,new Vector2(40,10)*input.mouseLookSensitivity)<.001f,"Mouse look uses pixel delta without frame-time scaling");
                InputSystem.QueueStateEvent(mouse,new MouseState{delta=new Vector2(40,10),buttons=2});InputSystem.Update();input.Read(.02f);
                Check(input.LookDelta==Vector2.zero && input.State.steering.sqrMagnitude>0,"Right-button mouse steering does not also orbit camera");
                InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=Vector2.right});InputSystem.Update();
                var command=r.Input.Read(.02f);
                Check(command.look.x>0 && command.move==Vector2.zero && command.glide==Vector2.zero,"Rider right-stick look is independent of glide controls");
                r.PlaceForTest(new Vector3(-900,1000,-900),new Vector3(0,-5,30),RiderState.Deploying);
                for(int i=0;i<50;i++){r.Tick(new RiderCommand{look=new Vector2(2,.3f)},.02f);camera.Tick(.02f);}
                Check(r.State==RiderState.Glide && camera.freeLook.Offset.x>90,"Look survives Deploying-to-Glide transition");
                var before=camera.freeLook.Offset;
                for(int i=0;i<40;i++){r.Tick(default,.02f);camera.Tick(.02f);}
                Check(Vector2.Distance(before,camera.freeLook.Offset)<.01f,"Glide view holds briefly after releasing look input");
                for(int i=0;i<140;i++){r.Tick(default,.02f);camera.Tick(.02f);}
                Check(camera.freeLook.Offset.magnitude<before.magnitude*.4f,"Glide view smoothly returns toward flight direction");
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.RightStick));InputSystem.Update();
                Check(r.Input.Read(.02f).lookBack,"Rider input reads R3 as a held action");
                var savedLook=camera.freeLook.Offset;
                r.Tick(new RiderCommand{lookBack=true,look=new Vector2(10,10)},.02f);camera.Tick(.02f);
                Check(Vector3.Dot(camera.transform.position-r.transform.position,r.Motor.Velocity.normalized)>1 && Vector3.Dot(camera.transform.forward,r.Motor.Velocity.normalized)<-.8f,"Glide rear view snaps to the front on the first tick");
                Check(camera.freeLook.Offset==savedLook,"Rear view preserves previous free-look orientation");
                r.Tick(default,.02f);camera.Tick(.02f);
                Check(Vector3.Dot(camera.transform.position-r.transform.position,r.Motor.Velocity.normalized)<-1,"Releasing R3 restores glide forward view immediately");
                camera.freeLook.Reset();
                var glideOrigin=new Vector3(-900,1200,-900);
                var glideVelocity=new Vector3(0,-5,30);
                float minimumGlideRadius=float.MaxValue,maximumGlideRadius=0,minimumAlignment=1;
                // Hold physical speed and position fixed to isolate the camera's orbital motion.
                for(int i=0;i<300;i++)
                {
                    r.Tick(new RiderCommand{look=new Vector2(2,0)},.02f);
                    r.PlaceForTest(glideOrigin,glideVelocity,RiderState.Glide);
                    camera.Tick(.02f);
                    var glideFocus=r.transform.position+Vector3.up*r.settings.camera.groundHeight;
                    if(i<80)continue;
                    float radius=Vector3.Distance(camera.transform.position,glideFocus);
                    minimumGlideRadius=Mathf.Min(minimumGlideRadius,radius);maximumGlideRadius=Mathf.Max(maximumGlideRadius,radius);
                    minimumAlignment=Mathf.Min(minimumAlignment,Vector3.Dot(camera.transform.forward,(glideFocus-camera.transform.position).normalized));
                }
                Check(minimumGlideRadius>3 && maximumGlideRadius-minimumGlideRadius<.05f,"Glide full orbit maintains radius without cutting toward Rider");
                Check(minimumAlignment>.999f,"Glide orbit keeps Rider centered through full revolutions");
                Check(Mathf.Abs(camera.freeLook.Offset.x+120)<.1f,"Glide stick continues past 360 degrees");
                r.Tick(default,.02f);r.PlaceForTest(glideOrigin,glideVelocity,RiderState.Glide);camera.freeLook.Reset();
                for(int i=0;i<60;i++)camera.Tick(.02f);
                var riderFocus=r.transform.position+Vector3.up*r.settings.camera.groundHeight;
                var riderShell=GameObject.CreatePrimitive(PrimitiveType.Sphere);riderShell.name="Rider camera collision regression";
                riderShell.transform.SetParent(r.transform,true);riderShell.transform.position=Vector3.Lerp(riderFocus,camera.transform.position,.5f);riderShell.transform.localScale=Vector3.one*2;
                Physics.SyncTransforms();float glideRadius=Vector3.Distance(riderFocus,camera.transform.position);camera.Tick(.02f);
                Check(Mathf.Abs(Vector3.Distance(riderFocus,camera.transform.position)-glideRadius)<.05f,"Rider colliders do not pull glide camera inward");
                riderShell.transform.SetParent(null,true);Physics.SyncTransforms();camera.Tick(.02f);
                Check(Vector3.Distance(riderFocus,camera.transform.position)<glideRadius-1,"Glide orbit still avoids environment obstacles");
                Object.DestroyImmediate(riderShell);Physics.SyncTransforms();camera.Tick(.02f);
                Check(Mathf.Abs(Vector3.Distance(riderFocus,camera.transform.position)-glideRadius)<.05f,"Glide orbit recovers its radius after obstruction");
                // Keep air-relative flight fixed while changing world drift, including updrafts.
                // Camera pose, pullback and FOV must not respond to transported wind velocity.
                var windManager=WindManager.Instance;
                Check(windManager!=null,"Wind manager available for camera regression");
                var cameraTestOrigin=new Vector3(20000,20000,20000);
                var originalWind=windManager.backgroundWind;
                try
                {
                    windManager.backgroundWind=Vector3.zero;
                    r.PlaceForTest(cameraTestOrigin,glideVelocity,RiderState.Glide);
                    camera.freeLook.Reset();
                    for(int i=0;i<180;i++)camera.Tick(.02f);
                    var calmPosition=camera.transform.position;
                    var calmRotation=camera.transform.rotation;
                    float calmFov=camera.GetComponent<Camera>().fieldOfView;
                    foreach(var wind in new[]{new Vector3(25,0,0),new Vector3(0,18,0),new Vector3(0,0,25)})
                    {
                        windManager.backgroundWind=wind;
                        r.PlaceForTest(cameraTestOrigin,glideVelocity+wind*r.Glide.windInfluence,RiderState.Glide);
                        for(int i=0;i<120;i++)camera.Tick(.02f);
                        Check(Vector3.Distance(calmPosition,camera.transform.position)<.02f,"Wind drift does not shift glide camera orbit: "+wind);
                        Check(Quaternion.Angle(calmRotation,camera.transform.rotation)<.1f,"Wind drift does not rotate glide camera: "+wind);
                        Check(Mathf.Abs(calmFov-camera.GetComponent<Camera>().fieldOfView)<.01f,"Wind drift does not change glide FOV: "+wind);
                    }
                }
                finally {windManager.backgroundWind=originalWind;}
            }
            catch(Exception e){Debug.LogException(e);code=1;}
            finally
            {
                if(pad!=null)InputSystem.RemoveDevice(pad);if(mouse!=null)InputSystem.RemoveDevice(mouse);
                InputSystem.settings=originalSettings;Object.DestroyImmediate(testSettings);
                SessionState.SetBool(Key,false);EditorApplication.Exit(code);
            }
        }
    }
}
